using System;
using System.Collections.Generic;
using System.Linq;

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
            public bool FacadeHint;
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
            public readonly List<int> UnmatchedFacadeWalls = new List<int>();
            public readonly List<string> Diagnostics = new List<string>();
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
            return Calculate(axes, null);
        }

        // slabRings contains the outer loop of each physical slab. A common edge of
        // adjacent slabs is removed before it can attract an internal structural wall.
        public static Result Calculate(IList<WallAxis> axes, IList<List<Point>> slabRings)
        {
            if (axes == null) throw new ArgumentNullException("axes");
            var valid = axes.Where(w => w != null && w.Width > 0 && Distance(w.Start, w.End) > Snap).ToList();
            var hints = valid.Where(w => w.FacadeHint).ToList();
            var result = new Result();
            if (valid.Count == 0) return result;
            if (slabRings != null && slabRings.Count != 0)
            {
                List<List<Point>> outlines = SlabOutlines(slabRings);
                result = Measure(outlines, valid, true);
                bool slabEnclosesWalls = hints.Count == 0
                    ? CoversExtremes(outlines, valid, true)
                    : HintCoverage(result, hints) >= .65 && CoversExtremes(outlines, hints);
                if (result.LoopCount > 0 && slabEnclosesWalls)
                    return ReportMissingFacades(result, hints);
            }
            // An interior structural cell can be the only closed cell in an open
            // section. Validate a wall-derived loop against all explicit facade runs.
            List<List<Point>> guide = AxisLoops(valid);
            result = Measure(guide, valid, false);
            var anchors = hints.Count >= 2 ? hints : valid;
            if (result.LoopCount == 0 || (hints.Count >= 2 && HintCoverage(result, anchors) < .7) ||
                !CoversExtremes(guide, anchors))
            {
                var repaired = CloseOpenRuns(anchors);
                guide = AxisLoops(repaired);
                result = Measure(guide, valid, false);
                if (result.LoopCount == 0 || !CoversExtremes(guide, anchors) ||
                    hints.Count >= 2 && HintCoverage(result, hints) < .65)
                {
                    var hull = ConvexHull(anchors.SelectMany(w => new[] { w.Start, w.End }));
                    result = Measure(new List<List<Point>> { hull }, valid, false);
                    result.Diagnostics.Add("Внешняя граница приближена выпуклой оболочкой стен. Проверьте уступы фасада.");
                }
                else result.Diagnostics.Add("Разрывы фасадных цепочек замкнуты по геометрии концов стен. Проверьте достроенные участки.");
            }
            if (slabRings != null && slabRings.Count > 0)
                result.Diagnostics.Add("Граница плит недостаточно согласуется с фасадными стенами этого уровня. Использован обход стен.");
            return ReportMissingFacades(result, hints);
        }

        private static Result ReportMissingFacades(Result result, List<WallAxis> hints)
        {
            foreach (int wallIndex in hints.Select(w => w.WallIndex).Distinct())
            {
                double measured;
                if (!result.Lengths.TryGetValue(wallIndex, out measured) || measured <= Snap)
                    result.UnmatchedFacadeWalls.Add(wallIndex);
            }
            if (result.UnmatchedFacadeWalls.Count > 0)
                result.Diagnostics.Add("Фасадных стен без измеренной длины: " + result.UnmatchedFacadeWalls.Count + ".");
            return result;
        }

        private static double HintCoverage(Result result, List<WallAxis> hints)
        {
            var lengths = hints.GroupBy(w => w.WallIndex).ToDictionary(g => g.Key, g => g.Sum(w => Distance(w.Start, w.End)));
            double covered = 0;
            foreach (var pair in lengths)
            {
                double measured;
                if (result.Lengths.TryGetValue(pair.Key, out measured))
                    covered += Math.Min(pair.Value, measured);
            }
            return covered / Math.Max(Snap, lengths.Values.Sum());
        }

        private static bool CoversExtremes(List<List<Point>> rings, List<WallAxis> axes, bool allowOneSpur = false)
        {
            if (rings.Count == 0) return false;
            var hull = ConvexHull(axes.SelectMany(a => new[] { a.Start, a.End }));
            // A short wall passing through the facade can extend past its support axis.
            int supported = hull.Count(p => rings.Any(r => InsidePolygon(p, r) ||
                Enumerable.Range(0, r.Count).Any(i =>
                    PointSegmentDistance(p, r[i], r[(i + 1) % r.Count]) <= 0.5 / 0.3048)));
            return supported == hull.Count || (allowOneSpur && supported >= 3 && supported == hull.Count - 1);
        }

        private static List<Point> ConvexHull(IEnumerable<Point> points)
        {
            var sorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var lower = new List<Point>();
            var upper = new List<Point>();
            foreach (Point p in sorted) ExtendHull(lower, p);
            sorted.Reverse();
            foreach (Point p in sorted) ExtendHull(upper, p);
            if (lower.Count > 0) lower.RemoveAt(lower.Count - 1);
            if (upper.Count > 0) upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            lower.Reverse();
            return lower;
        }

        private static void ExtendHull(List<Point> chain, Point p)
        {
            if (chain.Count > 0 && Distance(p, chain[chain.Count - 1]) <= Snap) return;
            while (chain.Count >= 2 && Cross(chain[chain.Count - 1] - chain[chain.Count - 2],
                p - chain[chain.Count - 1]) <= 1e-9)
                chain.RemoveAt(chain.Count - 1);
            chain.Add(p);
        }

        private static double PointSegmentDistance(Point p, Point a, Point b)
        {
            if (Distance(a, b) < Snap) return Distance(p, a);
            double t = Math.Max(0, Math.Min(1, ProjectParameter(p, a, b)));
            return Distance(p, a + (b - a) * t);
        }

        private static bool InsidePolygon(Point p, IList<Point> ring)
        {
            bool inside = false;
            for (int i = 0; i < ring.Count; i++)
            {
                Point a = ring[i], b = ring[(i + 1) % ring.Count];
                if ((a.Y > p.Y) != (b.Y > p.Y) &&
                    p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }

        private class FreeEnd
        {
            public Point P, Direction;
            public int Axis;
        }

        private static List<WallAxis> CloseOpenRuns(List<WallAxis> anchors)
        {
            var result = new List<WallAxis>(anchors);
            var ends = new List<FreeEnd>();
            for (int i = 0; i < anchors.Count; i++)
            {
                WallAxis w = anchors[i];
                foreach (Point p in new[] { w.Start, w.End })
                {
                    bool joined = false;
                    for (int j = 0; j < anchors.Count && !joined; j++)
                    {
                        if (j == i) continue;
                        WallAxis other = anchors[j];
                        joined = PointSegmentDistance(p, other.Start, other.End) <= other.Width / 2 + Contact;
                    }
                    if (!joined) ends.Add(new FreeEnd
                    {
                        P = p,
                        Axis = i,
                        Direction = Unit(p - (Distance(p, w.Start) < Snap ? w.End : w.Start))
                    });
                }
            }
            var pairs = new List<Tuple<double, int, int>>();
            for (int i = 0; i < ends.Count; i++)
                for (int j = i + 1; j < ends.Count; j++)
                {
                    FreeEnd a = ends[i], b = ends[j];
                    if (a.Axis == b.Axis) continue;
                    Point direction = Unit(b.P - a.P);
                    double alignment = Dot(a.Direction, direction) - Dot(b.Direction, direction);
                    pairs.Add(Tuple.Create(Distance(a.P, b.P) * (1.5 - alignment / 2), i, j));
                }
            var used = new HashSet<int>();
            int id = -1;
            foreach (var pair in pairs.OrderBy(v => v.Item1))
            {
                if (used.Contains(pair.Item2) || used.Contains(pair.Item3)) continue;
                FreeEnd a = ends[pair.Item2], b = ends[pair.Item3];
                if (result.Any(w => IntersectsInterior(a.P, b.P, w.Start, w.End))) continue;
                result.Add(new WallAxis(id--, a.P, b.P, Snap));
                used.Add(pair.Item2);
                used.Add(pair.Item3);
            }
            return result;
        }

        private static bool IntersectsInterior(Point a, Point b, Point c, Point d)
        {
            Point p;
            if (!LineIntersection(a, b, c, d, out p)) return false;
            double ab = ProjectParameter(p, a, b), cd = ProjectParameter(p, c, d);
            return ab > 1e-6 && ab < 1 - 1e-6 && cd > 1e-6 && cd < 1 - 1e-6;
        }

        private sealed class SlabEdge
        {
            public int Ring;
            public Point A, B;
        }

        private static double SignedArea(IList<Point> ring)
        {
            double twice = 0;
            for (int i = 1; i + 1 < ring.Count; i++)
                twice += Cross(ring[i] - ring[0], ring[i + 1] - ring[0]);
            return twice / 2;
        }

        // Split slab boundaries at every meeting point. An interval belongs to the
        // exterior only when samples on opposite sides disagree on union membership.
        private static List<List<Point>> SlabOutlines(IList<List<Point>> input)
        {
            var rings = input.Where(r => r != null && r.Count >= 3 && Math.Abs(SignedArea(r)) > Snap * Snap).ToList();
            var segments = new List<SlabEdge>();
            for (int k = 0; k < rings.Count; k++)
                for (int i = 0; i < rings[k].Count; i++)
                {
                    Point a = rings[k][i], b = rings[k][(i + 1) % rings[k].Count];
                    if (Distance(a, b) > Snap)
                        segments.Add(new SlabEdge { Ring = k, A = a, B = b });
                }
            var nodes = new List<Point>();
            var edges = new List<Edge>();
            var edgeKeys = new HashSet<Tuple<int, int>>();
            foreach (SlabEdge e in segments)
            {
                var cuts = new List<double> { 0, 1 };
                foreach (SlabEdge f in segments)
                {
                    if (e.Ring == f.Ring ||
                        Math.Max(e.A.X, e.B.X) + Snap < Math.Min(f.A.X, f.B.X) ||
                        Math.Max(f.A.X, f.B.X) + Snap < Math.Min(e.A.X, e.B.X) ||
                        Math.Max(e.A.Y, e.B.Y) + Snap < Math.Min(f.A.Y, f.B.Y) ||
                        Math.Max(f.A.Y, f.B.Y) + Snap < Math.Min(e.A.Y, e.B.Y)) continue;
                    Point p;
                    if (LineIntersection(e.A, e.B, f.A, f.B, out p))
                    {
                        if (PointSegmentDistance(p, f.A, f.B) <= Snap)
                            cuts.Add(Clamp(ProjectParameter(p, e.A, e.B), 0, 1));
                    }
                    else if (Math.Abs(Cross(Unit(e.B - e.A), f.A - e.A)) < Snap)
                    {
                        cuts.Add(Clamp(ProjectParameter(f.A, e.A, e.B), 0, 1));
                        cuts.Add(Clamp(ProjectParameter(f.B, e.A, e.B), 0, 1));
                    }
                }
                cuts.Sort();
                Point right = RightNormal(e.A, e.B) * (Snap / 3);
                for (int i = 1; i < cuts.Count; i++)
                {
                    Point a = e.A + (e.B - e.A) * cuts[i - 1], b = e.A + (e.B - e.A) * cuts[i];
                    if (Distance(a, b) <= Snap) continue;
                    Point mid = (a + b) * .5;
                    bool rightInside = rings.Any(r => InsidePolygon(mid + right, r));
                    bool leftInside = rings.Any(r => InsidePolygon(mid - right, r));
                    if (rightInside == leftInside) continue;
                    int start = FindOrAdd(nodes, rightInside ? a : b), end = FindOrAdd(nodes, rightInside ? b : a);
                    if (start != end && edgeKeys.Add(Tuple.Create(start, end)))
                        edges.Add(new Edge(start, end, -1));
                }
            }
            var from = edges.GroupBy(e => e.A).ToDictionary(g => g.Key, g => g.ToList());
            var visited = new HashSet<Edge>();
            var outer = new List<List<Point>>();
            foreach (Edge first in edges)
            {
                if (visited.Contains(first)) continue;
                var ring = new List<Point>();
                Edge edge = first;
                while (visited.Add(edge))
                {
                    ring.Add(nodes[edge.A]);
                    if (edge.B == first.A)
                    {
                        if (SignedArea(ring) < -Snap * Snap) outer.Add(ring);
                        break;
                    }
                    List<Edge> next;
                    if (!from.TryGetValue(edge.B, out next)) break;
                    double reverse = Angle(nodes[edge.B], nodes[edge.A]);
                    edge = next.Where(n => !visited.Contains(n)).OrderBy(n =>
                        (Angle(nodes[n.A], nodes[n.B]) - reverse + 2 * Math.PI) % (2 * Math.PI)).FirstOrDefault();
                    if (edge == null) break;
                }
            }
            return outer;
        }

        private class ProjectedWall
        {
            public WallAxis Wall;
            public double Start, End, Distance;
            public bool CanOwn;
        }

        private sealed class FaceSpan
        {
            public Point Start, End;
            public WallAxis Wall;
            public bool CanCross;
        }

        private static double Clamp(double x, double min, double max)
        {
            return Math.Max(min, Math.Min(max, x));
        }

        private static List<Point> SimplifyRing(List<Point> input)
        {
            var ring = new List<Point>(input);
            bool changed = true;
            while (changed && ring.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    Point a = ring[(i + ring.Count - 1) % ring.Count], b = ring[i], c = ring[(i + 1) % ring.Count];
                    if (Distance(a, b) <= Snap || Distance(b, c) <= Snap ||
                        Distance(a, c) > Snap && PointSegmentDistance(b, a, c) < Snap / 2)
                    {
                        ring.RemoveAt(i);
                        changed = true;
                        break;
                    }
                }
            }
            if (SignedArea(ring) > 0) ring.Reverse();
            return ring;
        }

        private static double HalfDepth(Point p, Point inward, IList<Point> ring)
        {
            double nearest = double.PositiveInfinity;
            for (int i = 0; i < ring.Count; i++)
            {
                Point a = ring[i], b = ring[(i + 1) % ring.Count];
                Point q;
                if (!LineIntersection(p, p + inward, a, b, out q)) continue;
                double depth = Dot(q - p, inward);
                if (depth > Snap && PointSegmentDistance(q, a, b) < Snap) nearest = Math.Min(nearest, depth);
            }
            return double.IsPositiveInfinity(nearest) ? 0 : nearest / 2;
        }

        private static HashSet<int> FacadeRuns(List<WallAxis> walls)
        {
            var hints = walls.Where(w => w.FacadeHint).ToList();
            var ids = new HashSet<int>(hints.Select(h => h.WallIndex));
            foreach (WallAxis w in walls)
            {
                if (ids.Contains(w.WallIndex)) continue;
                Point dir = Unit(w.End - w.Start);
                foreach (WallAxis h in hints)
                {
                    if (Math.Abs(Dot(dir, Unit(h.End - h.Start))) < .995 ||
                        Math.Abs(Cross(dir, h.Start - w.Start)) > Math.Min(h.Width, w.Width) / 2 + Contact) continue;
                    if (PointSegmentDistance(w.Start, h.Start, h.End) <= h.Width / 2 + Contact ||
                        PointSegmentDistance(w.End, h.Start, h.End) <= h.Width / 2 + Contact)
                    { ids.Add(w.WallIndex); break; }
                }
            }
            return ids;
        }

        private static List<FaceSpan> ProjectEdge(Point a, Point b, List<WallAxis> walls, bool slab, List<Point> ring, HashSet<int> facadeLine)
        {
            Point tangent = Unit(b - a);
            Point inward = RightNormal(a, b);
            double length = Distance(a, b);
            var possible = new List<ProjectedWall>();
            var ends = new List<double> { 0, length };
            foreach (WallAxis wall in walls)
            {
                if (Math.Abs(Dot(tangent, Unit(wall.End - wall.Start))) < .99) continue;
                double s = Dot(wall.Start - a, tangent), e = Dot(wall.End - a, tangent);
                double lo = Math.Max(0, Math.Min(s, e)), hi = Math.Min(length, Math.Max(s, e));
                if (hi - lo <= Snap) continue;
                double d1 = Dot(wall.Start - a, inward), d2 = Dot(wall.End - a, inward);
                double mid = (lo + hi) / 2;
                double depth = d1 + (d2 - d1) * ((mid - s) / (e - s));
                double distance = Math.Abs(depth);
                double band = wall.Width / 2 + (slab ? .15 / .3048 : Contact);
                bool canOwn = distance <= band;
                if (!canOwn)
                {
                    if (!slab || depth <= 0) continue;
                    // Search from the slab edge across its cantilever. Stop before the
                    // opposite half of the footprint and at any nearer parallel wall.
                    Point sample = a + tangent * mid;
                    if (depth > HalfDepth(sample, inward, ring)) continue;
                    canOwn = facadeLine.Contains(wall.WallIndex);
                }
                double cornerTolerance = wall.Width / 2 + Contact;
                // Endpoints close to a ring corner can be cut at the neighbour's face.
                if (lo < cornerTolerance) lo = 0;
                if (length - hi < cornerTolerance) hi = length;
                possible.Add(new ProjectedWall { Wall = wall, Start = lo, End = hi, Distance = distance, CanOwn = canOwn });
                ends.Add(lo);
                ends.Add(hi);
            }
            ends.Sort();
            var spans = new List<FaceSpan>();
            for (int i = 1; i < ends.Count; i++)
            {
                double lo = ends[i - 1], hi = ends[i];
                if (hi - lo < 1e-9) continue;
                double mid = (lo + hi) / 2;
                var owner = possible.Where(w => w.Start <= mid && mid <= w.End)
                    .OrderBy(w => w.Distance - (w.Wall.FacadeHint ? Contact : 0))
                    .ThenBy(w => w.Wall.WallIndex).FirstOrDefault();
                Point p = a + tangent * lo, q = a + tangent * hi;
                WallAxis wall = owner == null || !owner.CanOwn ? null : owner.Wall;
                if (wall != null)
                {
                    Point direction = Unit(wall.End - wall.Start);
                    if (Dot(direction, tangent) < 0) direction = direction * -1;
                    Point normal = new Point(direction.Y, -direction.X);
                    Point face = wall.Start + normal * (wall.Width / 2);
                    p = p + inward * (Dot(face - p, normal) / Dot(inward, normal));
                    q = q + inward * (Dot(face - q, normal) / Dot(inward, normal));
                }
                FaceSpan previous = spans.Count == 0 ? null : spans[spans.Count - 1];
                if (previous != null && ReferenceEquals(previous.Wall, wall)) previous.End = q;
                else spans.Add(new FaceSpan { Start = p, End = q, Wall = wall, CanCross = wall != null });
            }
            for (int i = 1; i + 1 < spans.Count; i++)
            {
                FaceSpan gap = spans[i], left = spans[i - 1], right = spans[i + 1];
                if (gap.Wall != null || left.Wall == null || right.Wall == null) continue;
                gap.Start = left.End;
                gap.End = right.Start;
                // Short interruptions between real side faces can expose a wall end.
                gap.CanCross = Distance(gap.Start, gap.End) <=
                    Math.Max(left.Wall.Width, right.Wall.Width) * 3 + Contact;
            }
            return spans;
        }

        private static Result Measure(List<List<Point>> outlines, List<WallAxis> walls, bool slab)
        {
            var result = new Result();
            foreach (WallAxis wall in walls) result.Lengths[wall.WallIndex] = 0;
            var seenCrossings = new Dictionary<int, List<Point>>();
            var facadeLine = FacadeRuns(walls);
            foreach (List<Point> outline in outlines)
            {
                var ring = SimplifyRing(outline);
                if (ring.Count < 3 || Math.Abs(SignedArea(ring)) < Snap * Snap) continue;
                var spans = new List<FaceSpan>();
                for (int i = 0; i < ring.Count; i++)
                    spans.AddRange(ProjectEdge(ring[i], ring[(i + 1) % ring.Count], walls, slab, ring, facadeLine));
                if (!spans.Any(s => s.Wall != null)) continue;
                if (slab) spans = FitReturns(spans, walls);
                // Move endpoints onto the common intersection of the inner faces.
                // Parallel faces of differing depth are joined by a transverse virtual segment.
                for (int i = 0; i < spans.Count; i++)
                {
                    var left = spans[i];
                    var right = spans[(i + 1) % spans.Count];
                    if (Distance(left.End, right.Start) < 1e-9) continue;
                    Point p;
                    if (LineIntersection(left.Start, left.End, right.Start, right.End, out p) &&
                        Distance(p, left.End) <= Distance(left.End, right.Start) * 4 + Contact &&
                        Distance(p, right.Start) <= Distance(left.End, right.Start) * 4 + Contact)
                    { left.End = p; right.Start = p; }
                }
                if (slab) spans = OuterWalk(spans);
                result.LoopCount++;
                for (int i = 0; i < spans.Count; i++)
                {
                    FaceSpan s = spans[i];
                    int owner = s.Wall == null ? -1 : s.Wall.WallIndex;
                    if (Distance(s.Start, s.End) > Snap)
                    {
                        result.Contour.Add(new ContourPart(s.Start, s.End, owner));
                        if (s.Wall != null)
                        {
                            Point dir = Unit(s.Wall.End - s.Wall.Start);
                            double extent = Distance(s.Wall.Start, s.Wall.End);
                            double alongStart = Clamp(Dot(s.Start - s.Wall.Start, dir), 0, extent);
                            double alongEnd = Clamp(Dot(s.End - s.Wall.Start, dir), 0, extent);
                            // The contour may extend across the neighbouring wall's end.
                            // Only the projection within this actual wall belongs to it.
                            double length = Math.Abs(alongEnd - alongStart);
                            result.Lengths[owner] += length;
                            if (!result.WallOrder.Contains(owner)) result.WallOrder.Add(owner);
                        }
                        if (s.CanCross) AddEndWidths(s, walls, result, seenCrossings);
                    }
                    Point next = spans[(i + 1) % spans.Count].Start;
                    if (Distance(s.End, next) > 1e-9) result.Contour.Add(new ContourPart(s.End, next, -1));
                }
            }
            return result;
        }

        private static void AddEndWidths(FaceSpan s, List<WallAxis> walls, Result result,
            Dictionary<int, List<Point>> seen)
        {
            foreach (WallAxis w in walls)
            {
                if (s.Wall != null && s.Wall.WallIndex == w.WallIndex) continue;
                // Nearly parallel overlaps are side projections, not transverse end faces.
                if (Math.Abs(Dot(Unit(s.End - s.Start), Unit(w.End - w.Start))) > .95) continue;
                Point crossing;
                if (!StrictCrossing(w.Start, w.End, s.Start, s.End, out crossing) &&
                    !ExposedEndContact(w, s.Start, s.End, out crossing)) continue;
                List<Point> points;
                if (!seen.TryGetValue(w.WallIndex, out points)) seen[w.WallIndex] = points = new List<Point>();
                if (points.Any(p => Distance(p, crossing) <= Contact)) continue;
                points.Add(crossing);
                result.Lengths[w.WallIndex] += w.Width;
            }
        }

        // A slab balcony can extend beyond the warm envelope. Two parallel brick
        // runs then project at different depths. Use an actual return wall between
        // these depths and join its side face to the two facade faces.
        private static List<FaceSpan> FitReturns(List<FaceSpan> spans, List<WallAxis> walls)
        {
            var real = Enumerable.Range(0, spans.Count).Where(i => spans[i].Wall != null).ToList();
            if (real.Count < 2) return spans;
            var fitted = new List<FaceSpan>();
            for (int k = 0; k < real.Count; k++)
            {
                int i = real[k], j = real[(k + 1) % real.Count];
                var prev = spans[i];
                var next = spans[j];
                fitted.Add(prev);
                Point dir = Unit(prev.End - prev.Start), nextDir = Unit(next.End - next.Start);
                Point delta = next.Start - prev.End;
                double across = Cross(dir, delta);
                if (Dot(dir, nextDir) > .995 && Math.Abs(across) > Math.Min(prev.Wall.Width, next.Wall.Width))
                {
                    var transverse = RightNormal(prev.Start, prev.End);
                    if (Dot(transverse, delta) < 0) transverse = transverse * -1;
                    double span = Math.Abs(across);
                    WallAxis best = null;
                    double bestScore = double.PositiveInfinity;
                    foreach (WallAxis wall in walls)
                    {
                        if (wall.WallIndex == prev.Wall.WallIndex || wall.WallIndex == next.Wall.WallIndex) continue;
                        Point along = Unit(wall.End - wall.Start);
                        if (Math.Abs(Dot(along, transverse)) < .99) continue;
                        double pa = Dot(wall.Start - prev.End, transverse), pb = Dot(wall.End - prev.End, transverse);
                        // The actual transverse wall has to reach both facade lines.
                        double joinReach = Math.Max(wall.Width, Math.Max(prev.Wall.Width, next.Wall.Width)) / 2 + Contact;
                        if (Math.Min(pa, pb) > joinReach || Math.Max(pa, pb) < span - joinReach) continue;
                        double d1 = Math.Abs(Dot(wall.Start - prev.End, dir));
                        double d2 = Math.Abs(Dot(wall.Start - next.Start, dir));
                        if (Math.Min(d1, d2) > wall.Width + Math.Max(prev.Wall.Width, next.Wall.Width) + Contact) continue;
                        double score = Math.Min(d1, d2);
                        if (score < bestScore) { best = wall; bestScore = score; }
                    }
                    if (best != null)
                    {
                        Point v = Unit(best.End - best.Start);
                        if (Dot(v, transverse) < 0) v = v * -1;
                        Point face = best.Start + new Point(v.Y, -v.X) * (best.Width / 2);
                        Point p, q;
                        if (LineIntersection(prev.Start, prev.End, face, face + v, out p) &&
                           LineIntersection(next.Start, next.End, face, face + v, out q))
                        {
                            if (Distance(prev.End, p) <= best.Width + Contact) prev.End = p;
                            else fitted.Add(new FaceSpan { Start = prev.End, End = p, CanCross = Distance(prev.End, p) < best.Width * 2 + Contact });
                            fitted.Add(new FaceSpan { Start = p, End = q, Wall = best, CanCross = true });
                            if (Distance(next.Start, q) <= best.Width + Contact) next.Start = q;
                            else fitted.Add(new FaceSpan { Start = q, End = next.Start, CanCross = Distance(next.Start, q) < best.Width * 2 + Contact });
                            continue;
                        }
                    }
                }
                for (int pos = (i + 1) % spans.Count; pos != j; pos = (pos + 1) % spans.Count)
                    fitted.Add(spans[pos]);
            }
            return fitted;
        }

        private static FaceSpan Slice(FaceSpan span, Point start, Point end)
        {
            return new FaceSpan { Start = start, End = end, Wall = span.Wall, CanCross = span.CanCross };
        }

        // Offsetting a slab with a projecting ledge may fold an edge back across
        // the facade that was just fitted. Keep the larger enclosure at a crossing.
        private static List<FaceSpan> OuterWalk(List<FaceSpan> spans)
        {
            var path = new List<FaceSpan>();
            for (int i = 0; i < spans.Count; i++)
            {
                var s = spans[i]; Point next = spans[(i + 1) % spans.Count].Start;
                if (Distance(s.Start, s.End) > Snap / 10) path.Add(s);
                if (Distance(s.End, next) > Snap / 10) path.Add(new FaceSpan { Start = s.End, End = next });
            }
            bool cut;
            do
            {
                cut = false;
                for (int i = 0; i < path.Count && !cut; i++)
                    for (int j = i + 2; j < path.Count && !cut; j++)
                    {
                        if (i == 0 && j == path.Count - 1) continue;
                        var a = path[i]; var b = path[j];
                        Point p;
                        if (!LineIntersection(a.Start, a.End, b.Start, b.End, out p)) continue;
                        if (PointSegmentDistance(p, a.Start, a.End) > Snap / 10 ||
                           PointSegmentDistance(p, b.Start, b.End) > Snap / 10) continue;
                        var first = new List<FaceSpan> { Slice(a, p, a.End) };
                        first.AddRange(path.GetRange(i + 1, j - i - 1));
                        first.Add(Slice(b, b.Start, p));
                        var second = new List<FaceSpan> { Slice(b, p, b.End) };
                        second.AddRange(path.Skip(j + 1));
                        second.AddRange(path.Take(i));
                        second.Add(Slice(a, a.Start, p));
                        double areaA = Math.Abs(SignedArea(first.Select(s => s.Start).ToList()));
                        double areaB = Math.Abs(SignedArea(second.Select(s => s.Start).ToList()));
                        var candidate = (areaA > areaB ? first : second).Where(s => Distance(s.Start, s.End) > Snap / 10).ToList();
                        if (candidate.Count >= path.Count) continue;
                        path = candidate;
                        cut = true;
                    }
            } while (cut);
            return path;
        }

        private static List<List<Point>> AxisLoops(List<WallAxis> valid)
        {
            var rings = new List<List<Point>>();
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
                rings.Add(loop.HalfEdges.Select(id => nodes[half[id].From]).ToList());
            }
            return rings;
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

        private static bool ExposedEndContact(WallAxis wall, Point a, Point b, out Point crossing)
        {
            crossing = new Point();
            Point inward = RightNormal(a, b);
            double start = Dot(wall.Start - a, inward), end = Dot(wall.End - a, inward);
            if (Math.Abs(start) <= Snap && end < -Snap) crossing = wall.Start;
            else if (Math.Abs(end) <= Snap && start < -Snap) crossing = wall.End;
            else return false;
            // The segment runs along an exposed end cap. An interior wall that
            // merely stops on the warm side of the same line has zero contribution.
            Point tangent = Unit(b - a);
            double t = Dot(crossing - a, tangent);
            return t >= -Snap && t <= Distance(a, b) + Snap;
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