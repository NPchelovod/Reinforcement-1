using System;
using System.Collections.Generic;

namespace Reinforcement
{
    // Coordinates and lengths are in Revit internal units (feet).
    public static class CoolingContourGeometry
    {
        public struct Point
        {
            public double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
            public static Point operator +(Point a, Point b) { return new Point(a.X + b.X, a.Y + b.Y); }
            public static Point operator -(Point a, Point b) { return new Point(a.X - b.X, a.Y - b.Y); }
            public static Point operator *(Point a, double k) { return new Point(a.X * k, a.Y * k); }
        }

        public sealed class WallAxis
        {
            public int WallIndex;
            public Point Start, End;
            public double Width;
            public WallAxis(int wallIndex, Point start, Point end, double width)
            { WallIndex = wallIndex; Start = start; End = end; Width = width; }
        }

        public sealed class ContourPart
        {
            public Point Start, End;
            public int WallIndex;
            public ContourPart(Point start, Point end, int wallIndex)
            { Start = start; End = end; WallIndex = wallIndex; }
        }

        public sealed class Result
        {
            public readonly List<ContourPart> Contour = new List<ContourPart>();
            public readonly Dictionary<int, double> Lengths = new Dictionary<int, double>();
            public readonly List<int> WallOrder = new List<int>();
            public int LoopCount;
        }

        private sealed class Mark
        {
            public double T;
            public Point Position;
            public Mark(double t, Point position) { T = t; Position = position; }
        }
        private sealed class Edge
        {
            public int A, B, WallIndex;
            public Edge(int a, int b, int wallIndex) { A = a; B = b; WallIndex = wallIndex; }
        }
        private sealed class HalfEdge
        {
            public int From, To, EdgeIndex, Reverse;
            public double Angle;
        }
        private sealed class Loop
        {
            public readonly List<int> HalfEdges = new List<int>();
            public double Area;
        }

        private const double Snap = 0.00328084; // 1 mm
        private const double Contact = 0.0328084; // 10 mm

        public static Result Calculate(IList<WallAxis> axes)
        {
            if (axes == null) throw new ArgumentNullException("axes");
            var result = new Result();
            var valid = new List<WallAxis>();
            var widths = new Dictionary<int, double>();
            foreach (WallAxis axis in axes)
            {
                if (axis == null || axis.Width <= 0 || Distance(axis.Start, axis.End) <= Snap) continue;
                valid.Add(axis);
                if (!result.Lengths.ContainsKey(axis.WallIndex)) result.Lengths.Add(axis.WallIndex, 0);
                if (!widths.ContainsKey(axis.WallIndex)) widths.Add(axis.WallIndex, axis.Width);
            }
            if (valid.Count == 0) return result;
            var marks = new List<List<Mark>>();
            foreach (WallAxis axis in valid)
                marks.Add(new List<Mark> { new Mark(0, axis.Start), new Mark(1, axis.End) });

            // Extend an axis only as far as the face of its neighbour permits.
            for (int i = 0; i < valid.Count; i++)
                for (int j = i + 1; j < valid.Count; j++)
                {
                    WallAxis a = valid[i], b = valid[j];
                    Point da = a.End - a.Start, db = b.End - b.Start;
                    double denominator = Cross(da, db);
                    if (Math.Abs(denominator) > 1e-10)
                    {
                        Point separation = b.Start - a.Start;
                        double ta = Cross(separation, db) / denominator;
                        double tb = Cross(separation, da) / denominator;
                        double allowanceA = (b.Width / 2 + Contact) / Length(da);
                        double allowanceB = (a.Width / 2 + Contact) / Length(db);
                        if (ta >= -allowanceA && ta <= 1 + allowanceA &&
                            tb >= -allowanceB && tb <= 1 + allowanceB)
                        {
                            Point junction = a.Start + da * ta;
                            marks[i].Add(new Mark(ta, junction));
                            marks[j].Add(new Mark(tb, junction));
                        }
                    }
                    else if (Math.Abs(Cross(b.Start - a.Start, da)) / Length(da) <= Snap)
                    {
                        Point[] pa = { a.Start, a.End }, pb = { b.Start, b.End };
                        for (int ia = 0; ia < 2; ia++)
                            for (int ib = 0; ib < 2; ib++)
                            {
                                if (Distance(pa[ia], pb[ib]) <= Math.Max(Contact, Math.Min(a.Width, b.Width) / 2))
                                {
                                    Point junction = (pa[ia] + pb[ib]) * 0.5;
                                    marks[i].Add(new Mark(ProjectParameter(junction, a.Start, a.End), junction));
                                    marks[j].Add(new Mark(ProjectParameter(junction, b.Start, b.End), junction));
                                }
                            }
                    }
                }

            var nodes = new List<Point>();
            var edges = new List<Edge>();
            for (int i = 0; i < valid.Count; i++)
            {
                marks[i].Sort((a, b) => a.T.CompareTo(b.T));
                int previous = -1;
                foreach (Mark mark in marks[i])
                {
                    int node = FindOrAdd(nodes, mark.Position);
                    if (previous >= 0 && previous != node)
                        edges.Add(new Edge(previous, node, valid[i].WallIndex));
                    previous = node;
                }
            }

            // Dead-end walls are bridges and do not redirect the envelope.
            bool[] bridge = FindBridges(nodes.Count, edges);
            var half = new List<HalfEdge>();
            var outgoing = new List<List<int>>();
            for (int i = 0; i < nodes.Count; i++) outgoing.Add(new List<int>());
            for (int i = 0; i < edges.Count; i++)
            {
                if (bridge[i]) continue;
                Edge edge = edges[i];
                int first = half.Count;
                half.Add(new HalfEdge
                {
                    From = edge.A,
                    To = edge.B,
                    EdgeIndex = i,
                    Reverse = first + 1,
                    Angle = Angle(nodes[edge.A], nodes[edge.B])
                });
                half.Add(new HalfEdge
                {
                    From = edge.B,
                    To = edge.A,
                    EdgeIndex = i,
                    Reverse = first,
                    Angle = Angle(nodes[edge.B], nodes[edge.A])
                });
                outgoing[edge.A].Add(first);
                outgoing[edge.B].Add(first + 1);
            }
            foreach (List<int> fan in outgoing)
                fan.Sort((a, b) => half[a].Angle.CompareTo(half[b].Angle));

            bool[] visited = new bool[half.Count];
            var outerLoops = new List<Loop>();
            for (int start = 0; start < half.Count; start++)
            {
                if (visited[start]) continue;
                var loop = new Loop();
                int current = start;
                while (!visited[current])
                {
                    visited[current] = true;
                    loop.HalfEdges.Add(current);
                    HalfEdge h = half[current];
                    List<int> fan = outgoing[h.To];
                    int reversePosition = fan.IndexOf(h.Reverse);
                    if (reversePosition < 0) throw new InvalidOperationException("Broken contour graph.");
                    current = fan[(reversePosition - 1 + fan.Count) % fan.Count];
                }
                if (current != start || loop.HalfEdges.Count < 3) continue;
                foreach (int id in loop.HalfEdges)
                {
                    HalfEdge h = half[id];
                    loop.Area += Cross(nodes[h.From], nodes[h.To]) / 2;
                }
                if (loop.Area < -(Snap * Snap)) outerLoops.Add(loop);
            }

            // Disconnected rings nested inside an envelope are interior walls.
            for (int i = 0; i < outerLoops.Count; i++)
            {
                Loop loop = outerLoops[i];
                Point sample = nodes[half[loop.HalfEdges[0]].From];
                bool enclosed = false;
                for (int j = 0; j < outerLoops.Count; j++)
                    if (i != j && Math.Abs(outerLoops[j].Area) > Math.Abs(loop.Area) &&
                        IsInside(sample, outerLoops[j], half, nodes)) { enclosed = true; break; }
                if (enclosed) continue;
                AddInnerContour(loop, half, edges, nodes, widths, result);
                result.LoopCount++;
            }

            // A wall crossing the contour contributes its transverse width once per crossing.
            var crossingPoints = new Dictionary<int, List<Point>>();
            foreach (WallAxis axis in valid)
                foreach (ContourPart part in result.Contour)
                {
                    if (axis.WallIndex == part.WallIndex) continue;
                    Point crossing;
                    if (!StrictCrossing(axis.Start, axis.End, part.Start, part.End, out crossing)) continue;
                    List<Point> seen;
                    if (!crossingPoints.TryGetValue(axis.WallIndex, out seen))
                    {
                        seen = new List<Point>();
                        crossingPoints.Add(axis.WallIndex, seen);
                    }
                    bool duplicate = false;
                    foreach (Point other in seen)
                        if (Distance(other, crossing) <= Contact) { duplicate = true; break; }
                    if (!duplicate)
                    {
                        seen.Add(crossing);
                        result.Lengths[axis.WallIndex] += axis.Width;
                    }
                }
            return result;
        }

        private static void AddInnerContour(Loop loop, List<HalfEdge> half,
            List<Edge> edges, List<Point> nodes, Dictionary<int, double> widths, Result result)
        {
            int count = loop.HalfEdges.Count;
            var corners = new Point[count];
            for (int i = 0; i < count; i++)
            {
                HalfEdge before = half[loop.HalfEdges[(i - 1 + count) % count]];
                HalfEdge after = half[loop.HalfEdges[i]];
                Point vertex = nodes[after.From];
                Point first = RightNormal(nodes[before.From], nodes[before.To]) *
                    (widths[edges[before.EdgeIndex].WallIndex] / 2);
                Point second = RightNormal(nodes[after.From], nodes[after.To]) *
                    (widths[edges[after.EdgeIndex].WallIndex] / 2);
                Point intersection;
                if (LineIntersection(nodes[before.From] + first, vertex + first,
                    vertex + second, nodes[after.To] + second, out intersection) &&
                    Distance(intersection, vertex) <= 8 * Math.Max(Length(first), Length(second)) + Contact)
                    corners[i] = intersection;
                else corners[i] = vertex + (first + second) * 0.5;
            }
            for (int i = 0; i < count; i++)
            {
                HalfEdge h = half[loop.HalfEdges[i]];
                int wall = edges[h.EdgeIndex].WallIndex;
                Point from = corners[i], to = corners[(i + 1) % count];
                result.Contour.Add(new ContourPart(from, to, wall));
                Point direction = Unit(nodes[h.To] - nodes[h.From]);
                result.Lengths[wall] += Math.Abs(Dot(to - from, direction));
                if (!result.WallOrder.Contains(wall)) result.WallOrder.Add(wall);
            }
        }

        private static bool[] FindBridges(int nodeCount, List<Edge> edges)
        {
            var adjacency = new List<List<int>>();
            for (int i = 0; i < nodeCount; i++) adjacency.Add(new List<int>());
            for (int i = 0; i < edges.Count; i++)
            {
                adjacency[edges[i].A].Add(i);
                adjacency[edges[i].B].Add(i);
            }
            var bridge = new bool[edges.Count];
            var discovery = new int[nodeCount];
            var low = new int[nodeCount];
            int clock = 0;
            for (int i = 0; i < nodeCount; i++)
                if (discovery[i] == 0) VisitBridge(i, -1, edges, adjacency, discovery, low, bridge, ref clock);
            return bridge;
        }

        private static void VisitBridge(int at, int parentEdge, List<Edge> edges,
            List<List<int>> adjacency, int[] discovery, int[] low, bool[] bridge, ref int clock)
        {
            discovery[at] = low[at] = ++clock;
            foreach (int edgeId in adjacency[at])
            {
                if (edgeId == parentEdge) continue;
                Edge edge = edges[edgeId];
                int next = edge.A == at ? edge.B : edge.A;
                if (discovery[next] == 0)
                {
                    VisitBridge(next, edgeId, edges, adjacency, discovery, low, bridge, ref clock);
                    low[at] = Math.Min(low[at], low[next]);
                    if (low[next] > discovery[at]) bridge[edgeId] = true;
                }
                else low[at] = Math.Min(low[at], discovery[next]);
            }
        }

        private static bool IsInside(Point p, Loop loop, List<HalfEdge> half, List<Point> nodes)
        {
            bool inside = false;
            foreach (int id in loop.HalfEdges)
            {
                Point a = nodes[half[id].From], b = nodes[half[id].To];
                if ((a.Y > p.Y) != (b.Y > p.Y) &&
                    p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        private static bool StrictCrossing(Point a, Point b, Point c, Point d, out Point point)
        {
            point = new Point();
            Point cd = d - c;
            double span = Length(cd);
            if (span <= Snap) return false;
            double sideA = Cross(cd, a - c) / span;
            double sideB = Cross(cd, b - c) / span;
            if (!((sideA > Snap && sideB < -Snap) || (sideA < -Snap && sideB > Snap))) return false;
            double denominator = Cross(b - a, cd);
            if (Math.Abs(denominator) < 1e-10) return false;
            double ta = Cross(c - a, cd) / denominator;
            double tc = Cross(c - a, b - a) / denominator;
            if (ta < -1e-8 || ta > 1 + 1e-8 || tc < -1e-8 || tc > 1 + 1e-8) return false;
            point = a + (b - a) * ta;
            return true;
        }

        private static bool LineIntersection(Point a, Point b, Point c, Point d, out Point point)
        {
            point = new Point();
            double denominator = Cross(b - a, d - c);
            if (Math.Abs(denominator) < 1e-10) return false;
            point = a + (b - a) * (Cross(c - a, d - c) / denominator);
            return true;
        }

        private static int FindOrAdd(List<Point> nodes, Point point)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (Distance(nodes[i], point) <= Snap) return i;
            nodes.Add(point);
            return nodes.Count - 1;
        }

        private static double ProjectParameter(Point p, Point a, Point b)
        {
            Point ab = b - a;
            return Dot(p - a, ab) / Dot(ab, ab);
        }
        private static Point RightNormal(Point a, Point b)
        {
            Point v = Unit(b - a);
            return new Point(v.Y, -v.X);
        }
        private static Point Unit(Point p) { return p * (1 / Length(p)); }
        private static double Angle(Point a, Point b) { return Math.Atan2(b.Y - a.Y, b.X - a.X); }
        private static double Dot(Point a, Point b) { return a.X * b.X + a.Y * b.Y; }
        private static double Cross(Point a, Point b) { return a.X * b.Y - a.Y * b.X; }
        private static double Length(Point a) { return Math.Sqrt(Dot(a, a)); }
        private static double Distance(Point a, Point b) { return Length(a - b); }
    }
}