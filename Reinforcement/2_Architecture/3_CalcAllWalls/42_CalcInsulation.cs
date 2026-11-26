using Autodesk.Revit.DB;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YourNamespace
{
    /// <summary>
    /// Построение цепочек стен по обходу контура (по часовой стрелке).
    /// </summary>
    public static class ECWallPerimeterChainBuilder
    {
        /// <summary>Допуск склейки узлов, мм.</summary>
        public const double NodeToleranceMm = 1.0;

        /// <summary>Минимальная площадь замкнутой цепочки, чтобы считать её «значащей», м².</summary>
        public const double MinChainAreaM2 = 0.01;

        // ---------- Модели результата ----------
        public class ChainSegment
        {
            public ECWall Wall;
            /// <summary>+1 = по кривой (start→end), -1 = против (end→start).</summary>
            public int Direction;
            public override string ToString() => $"{Wall?.Name} ({(Direction > 0 ? "→" : "←")})";
        }

        public class Chain
        {
            public List<ChainSegment> Segments = new List<ChainSegment>();
            public bool IsClosed;
            public double AreaM2;       // модуль площади (для открытой — по замыканию)
            public bool IsClockwise;
            public bool IsOutermost;

            public List<ECWall> Walls => Segments.Select(s => s.Wall).ToList();
            public override string ToString()
                => $"Chain: {Segments.Count} walls, closed={IsClosed}, CW={IsClockwise}, S={AreaM2:F2}м²";
        }

        // ---------- Внутренние модели ----------
        private class HalfEdge
        {
            public ECWall Wall;
            public XYZ Position;
            public XYZ DirIntoWall;   // единичный вектор «от узла внутрь стены»
            public bool IsStart;      // true = узел на начале кривой, false = на конце
            public HalfEdge Paired;
        }

        private class Node
        {
            public XYZ Position;
            public List<HalfEdge> HalfEdges = new List<HalfEdge>();
        }

        // ---------- Публичный API ----------
        /// <summary>
        /// Строит все цепочки, сортирует по площади убыв.
        /// Первая (IsOutermost=true) — наиболее вероятный наружный контур,
        /// ориентирована по часовой стрелке (если удалось).
        /// </summary>
        public static List<Chain> Build(List<ECWall> walls)
        {
            var result = new List<Chain>();
            if (walls == null || walls.Count == 0) return result;

            double tolFt = UnitUtils.ConvertToInternalUnits(NodeToleranceMm, UnitTypeId.Millimeters);

            // 1. Полуребра
            var heByWall = new Dictionary<ECWall, (HalfEdge start, HalfEdge end)>();
            var allHe = new List<HalfEdge>();

            foreach (var w in walls)
            {
                var curve = GetCurve(w);
                if (curve == null) continue;

                var p0 = curve.GetEndPoint(0);
                var p1 = curve.GetEndPoint(1);
                double len = p0.DistanceTo(p1);
                if (len < 1e-9) continue;

                var h0 = new HalfEdge
                {
                    Wall = w,
                    Position = p0,
                    IsStart = true,
                    DirIntoWall = (p1 - p0) / len
                };
                var h1 = new HalfEdge
                {
                    Wall = w,
                    Position = p1,
                    IsStart = false,
                    DirIntoWall = (p0 - p1) / len
                };
                heByWall[w] = (h0, h1);
                allHe.Add(h0); allHe.Add(h1);
            }

            if (allHe.Count == 0) return result;

            // 2. Узлы
            var nodes = new List<Node>();
            foreach (var he in allHe)
            {
                Node node = null;
                foreach (var n in nodes)
                {
                    if (n.Position.DistanceTo(he.Position) <= tolFt) { node = n; break; }
                }
                if (node == null) { node = new Node { Position = he.Position }; nodes.Add(node); }
                node.HalfEdges.Add(he);
            }

            // 3. Спаривание в узлах
            foreach (var n in nodes) PairAtNode(n);

            // 4. Обход цепочек
            var visitedWalls = new HashSet<ECWall>();
            foreach (var startWall in heByWall.Keys)
            {
                if (visitedWalls.Contains(startWall)) continue;
                var chain = WalkChain(startWall, heByWall, visitedWalls);
                if (chain != null && chain.Segments.Count > 0) result.Add(chain);
            }

            // 5. Площади и ориентация
            foreach (var c in result) ComputeAreaAndOrientation(c);

            // 6. Сортировка + отбрасывание пустых
            result = result.Where(c => c.Segments.Count > 0).ToList();
            result.Sort((a, b) => b.AreaM2.CompareTo(a.AreaM2));

            if (result.Count > 0) result[0].IsOutermost = true;
            return result;
        }

        /// <summary>
        /// Наиболее вероятный наружный контур, ориентированный по часовой.
        /// Возвращает null, если стен нет.
        /// </summary>
        public static Chain BuildOuterClockwise(List<ECWall> walls)
        {
            var all = Build(walls);
            if (all.Count == 0) return null;
            var outer = all[0];
            if (!outer.IsClockwise) ReverseChain(outer);
            return outer;
        }

        /// <summary>
        /// Только стены наружного контура (по максимальной площади).
        /// </summary>
        public static HashSet<ECWall> GetOuterWalls(List<ECWall> walls)
        {
            var outer = BuildOuterClockwise(walls);
            if (outer == null) return new HashSet<ECWall>();
            return new HashSet<ECWall>(outer.Walls);
        }

        // ---------- Спаривание ----------
        private static void PairAtNode(Node node)
        {
            var hes = node.HalfEdges;
            int n = hes.Count;
            if (n < 2) return;

            if (n == 2) { hes[0].Paired = hes[1]; hes[1].Paired = hes[0]; return; }

            // сортируем пары по возрастанию dot (сначала самые «противоположные»)
            var pairs = new List<(int i, int j, double dot)>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    pairs.Add((i, j, hes[i].DirIntoWall.DotProduct(hes[j].DirIntoWall)));
            pairs.Sort((a, b) => a.dot.CompareTo(b.dot));

            var used = new bool[n];
            foreach (var p in pairs)
            {
                if (used[p.i] || used[p.j]) continue;
                if (p.dot > 0.0) continue;               // не склеиваем «одинаково направленные»
                hes[p.i].Paired = hes[p.j];
                hes[p.j].Paired = hes[p.i];
                used[p.i] = used[p.j] = true;
            }
        }

        // ---------- Обход ----------
        private static Chain WalkChain(
            ECWall startWall,
            Dictionary<ECWall, (HalfEdge start, HalfEdge end)> he,
            HashSet<ECWall> visitedWalls)
        {
            var chain = new Chain();
            var seenDir = new HashSet<(ECWall, int)>();

            // --- Вперёд ---
            var forward = new List<ChainSegment>();
            bool closed = false;
            var cur = new ChainSegment { Wall = startWall, Direction = +1 };

            while (cur != null)
            {
                if (seenDir.Contains((cur.Wall, cur.Direction))) { closed = true; break; }
                seenDir.Add((cur.Wall, cur.Direction));
                visitedWalls.Add(cur.Wall);
                forward.Add(cur);

                var exitHe = cur.Direction > 0 ? he[cur.Wall].end : he[cur.Wall].start;
                if (exitHe.Paired == null) { cur = null; break; }

                var p = exitHe.Paired;
                int d = p.IsStart ? +1 : -1;

                if (p.Wall == startWall && d == +1) { closed = true; break; }
                cur = new ChainSegment { Wall = p.Wall, Direction = d };
            }

            if (closed)
            {
                chain.Segments = forward;
                chain.IsClosed = true;
                return chain;
            }

            // --- Назад ---
            var backward = new List<ChainSegment>();
            var startEntryHe = he[startWall].start;
            if (startEntryHe.Paired != null)
            {
                var p = startEntryHe.Paired;
                int d = p.IsStart ? -1 : +1;
                var prev = new ChainSegment { Wall = p.Wall, Direction = d };

                while (prev != null)
                {
                    if (seenDir.Contains((prev.Wall, prev.Direction))) break;
                    seenDir.Add((prev.Wall, prev.Direction));
                    if (visitedWalls.Contains(prev.Wall)) break;
                    visitedWalls.Add(prev.Wall);
                    backward.Add(prev);

                    var entryHe = prev.Direction > 0 ? he[prev.Wall].start : he[prev.Wall].end;
                    if (entryHe.Paired == null) break;

                    var pp = entryHe.Paired;
                    int dd = pp.IsStart ? -1 : +1;
                    prev = new ChainSegment { Wall = pp.Wall, Direction = dd };
                }
            }
            backward.Reverse();

            var combined = new List<ChainSegment>();
            combined.AddRange(backward);
            combined.AddRange(forward);
            chain.Segments = combined;
            chain.IsClosed = false;
            return chain;
        }

        // ---------- Геометрия / площадь ----------
        private static Curve GetCurve(ECWall w)
        {
            if (w == null) return null;
            if (w.Curve != null) return w.Curve;
            var wall = w.Wall ?? w.Element as Wall;
            return (wall?.Location as LocationCurve)?.Curve;
        }

        private static void ComputeAreaAndOrientation(Chain c)
        {
            var pts = BuildPolygon(c);
            if (pts.Count < 3) { c.AreaM2 = 0; c.IsClockwise = false; return; }

            double s = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                s += a.X * b.Y - b.X * a.Y;
            }
            s *= 0.5;

            c.AreaM2 = UnitUtils.ConvertFromInternalUnits(Math.Abs(s), UnitTypeId.SquareMeters);
            c.IsClockwise = s < 0;      // в проекции сверху (X вправо, Y вверх) отрицательная площадь = по часовой
        }

        private static List<XYZ> BuildPolygon(Chain c)
        {
            var pts = new List<XYZ>();
            foreach (var seg in c.Segments)
            {
                var curve = GetCurve(seg.Wall);
                if (curve == null) continue;

                if (curve is Line)
                {
                    pts.Add(seg.Direction > 0 ? curve.GetEndPoint(0) : curve.GetEndPoint(1));
                }
                else
                {
                    const int N = 8;
                    for (int i = 0; i < N; i++)
                    {
                        double t = (double)i / N;
                        if (seg.Direction < 0) t = 1 - t;
                        pts.Add(curve.Evaluate(t, false));
                    }
                }
            }

            if (!c.IsClosed && c.Segments.Count > 0)
            {
                var lastSeg = c.Segments[c.Segments.Count - 1];
                var curve = GetCurve(lastSeg.Wall);
                if (curve != null)
                {
                    var last = lastSeg.Direction > 0 ? curve.GetEndPoint(1) : curve.GetEndPoint(0);
                    if (pts.Count == 0 || !pts[pts.Count - 1].IsAlmostEqualTo(last))
                        pts.Add(last);
                }
            }
            return pts;
        }

        private static void ReverseChain(Chain c)
        {
            c.Segments.Reverse();
            foreach (var s in c.Segments) s.Direction = -s.Direction;
            c.IsClockwise = !c.IsClockwise;
        }
    }
}