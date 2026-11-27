using Autodesk.Revit.DB;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    /// <summary>
    /// Построение цепочек стен по обходу контура (по часовой стрелке).
    /// </summary>
    public static class ECWallPerimeterChainBuilder
    {
        /// <summary>Допуск склейки узлов, мм.</summary>
        public const double NodeToleranceMm = 5.0;

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
            public XYZ MiddlePoint;   // ← НОВОЕ: середина средней стены цепочки
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
            // 2. Узлы (склеиваем концы с учётом возможного зазора из-за не-join стен)
            var nodes = new List<Node>();
            foreach (var he in allHe)
            {
                Node node = null;
                double bestDist = double.MaxValue;

                foreach (var n in nodes)
                {
                    double dist = n.Position.DistanceTo(he.Position);
                    double tol = tolFt;

                    foreach (var member in n.HalfEdges)
                    {
                        double wA = GetWallWidth(member.Wall);
                        double wB = GetWallWidth(he.Wall);
                        double pairTol = UnitUtils.ConvertToInternalUnits(
                            Math.Max(wA, wB) * 500.0, UnitTypeId.Millimeters); // полширины, мм
                        if (pairTol > tol) tol = pairTol;
                    }

                    if (dist <= tol && dist < bestDist) { node = n; bestDist = dist; }
                }

                if (node == null) { node = new Node { Position = he.Position }; nodes.Add(node); }
                node.HalfEdges.Add(he);
            }
            //// 2. Узлы
            //var nodes = new List<Node>();
            //foreach (var he in allHe)
            //{
            //    Node node = null;
            //    foreach (var n in nodes)
            //    {
            //        if (n.Position.DistanceTo(he.Position) <= tolFt) { node = n; break; }
            //    }
            //    if (node == null) { node = new Node { Position = he.Position }; nodes.Add(node); }
            //    node.HalfEdges.Add(he);
            //}

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
            // Средняя точка
            foreach (var c in result) ComputeChainMiddlePoint(c);   // ← новая строка
            // 6. Сортировка + отбрасывание пустых
            result = result.Where(c => c.Segments.Count > 0).ToList();
            result.Sort((a, b) => b.AreaM2.CompareTo(a.AreaM2));

            if (result.Count > 0) result[0].IsOutermost = true;



            return result;
        }
        private static void ComputeChainMiddlePoint(Chain c)
        {
            c.MiddlePoint = XYZ.Zero;
            if (c.Segments.Count == 0) return;

            int mid = c.Segments.Count / 2;
            var curve = GetCurve(c.Segments[mid].Wall);
            if (curve == null) return;

            c.MiddlePoint = curve.Evaluate(0.5, true);   // середина средней стены
        }
        /// <summary>
        /// Разворачивает цепочку на месте (меняет порядок Segments и знаки Direction).
        /// </summary>
        public static void ReverseChainInPlace(Chain c)
        {
            if (c == null) return;
            c.Segments.Reverse();
            foreach (var s in c.Segments) s.Direction = -s.Direction;
            c.IsClockwise = !c.IsClockwise;
        }
        /// <summary>
        /// Взвешенный по периметру центр всех цепочек (центр дома).
        /// </summary>
        public static XYZ ComputeGlobalCenter(IEnumerable<Chain> chains)
        {
            double totalW = 0, sx = 0, sy = 0;
            foreach (var c in chains)
            {
                double w = 0;
                foreach (var s in c.Segments)
                {
                    var cv = GetCurve(s.Wall);
                    if (cv != null) w += cv.Length;
                }
                if (w <= 0) w = 1;
                sx += c.MiddlePoint.X * w;
                sy += c.MiddlePoint.Y * w;
                totalW += w;
            }
            if (totalW <= 1e-9) return XYZ.Zero;
            return new XYZ(sx / totalW, sy / totalW, 0);
        }
        /// <summary>
        /// 1) Сортирует цепочки по часовой стрелке вокруг общего центра дома
        ///    (по углу MiddlePoint через atan2, CW = убывание угла).
        /// 2) Каждую цепочку ориентирует так, чтобы её ход (start→end)
        ///    совпадал с направлением по часовой стрелке.
        ///
        /// После этого простая конкатенация c.Walls даёт непрерывный обход периметра.
        /// </summary>
        public static List<Chain> SortAndOrientChainsClockwise(List<Chain> chains)
        {
            if (chains == null || chains.Count == 0) return new List<Chain>();

            var sorted = new List<Chain>(chains);
            if (sorted.Count == 1) return sorted;

            var center = ComputeGlobalCenter(sorted);

            // CW = убывание стандартного угла atan2 (ось Y вверх)
            sorted.Sort((a, b) =>
            {
                double angA = Math.Atan2(a.MiddlePoint.Y - center.Y, a.MiddlePoint.X - center.X);
                double angB = Math.Atan2(b.MiddlePoint.Y - center.Y, b.MiddlePoint.X - center.X);
                return angB.CompareTo(angA);
            });

            foreach (var c in sorted) OrientChainClockwise(c, center);
            return sorted;
        }
        /// <summary>
        /// Разворачивает цепочку так, чтобы её направление совпало
        /// с CW-тангенсом к радиусу center→MiddlePoint.
        /// </summary>
        private static void OrientChainClockwise(Chain c, XYZ center)
        {
            if (c.Segments.Count == 0) return;

            XYZ radial = new XYZ(c.MiddlePoint.X - center.X,
                                 c.MiddlePoint.Y - center.Y, 0);
            double rlen = radial.GetLength();
            if (rlen < 1e-9) return;

            // CW-тангенс в плоскости XY: поворот радиуса на -90° = (r.Y, -r.X)/|r|
            XYZ cwTangent = new XYZ(radial.Y / rlen, -radial.X / rlen, 0);

            var cStart = GetSegStart(c.Segments[0]);
            var cEnd = GetSegEnd(c.Segments[c.Segments.Count - 1]);
            if (cStart == null || cEnd == null) return;

            XYZ dir = new XYZ(cEnd.X - cStart.X, cEnd.Y - cStart.Y, 0);
            double dlen = dir.GetLength();
            if (dlen < 1e-9) return;
            dir = new XYZ(dir.X / dlen, dir.Y / dlen, 0);

            if (dir.DotProduct(cwTangent) < 0)
                ReverseChainInPlace(c);
        }

        private static XYZ GetSegStart(ChainSegment s)
        {
            var cv = GetCurve(s.Wall);
            if (cv == null) return null;
            return s.Direction > 0 ? cv.GetEndPoint(0) : cv.GetEndPoint(1);
        }

        private static XYZ GetSegEnd(ChainSegment s)
        {
            var cv = GetCurve(s.Wall);
            if (cv == null) return null;
            return s.Direction > 0 ? cv.GetEndPoint(1) : cv.GetEndPoint(0);
        }

        /// <summary>
        /// Полный пайплайн: Build → SortAndOrient → плоский список стен по периметру CW.
        /// </summary>
        public static List<ECWall> BuildContinuousPerimeter(List<ECWall> walls)
        {
            var chains = Build(walls);
            if (chains.Count == 0) return new List<ECWall>();

            var sorted = SortAndOrientChainsClockwise(chains);

            var result = new List<ECWall>();
            foreach (var c in sorted) result.AddRange(c.Walls);
            return result;
        }
        /// <summary>
        /// Возвращает Build(walls), но каждая цепочка принудительно ориентирована по часовой.
        /// Сортировка по площади сохраняется.
        /// </summary>
        public static List<Chain> BuildAllClockwise(List<ECWall> walls)
        {
            var chains = Build(walls);
            foreach (var c in chains)
                if (!c.IsClockwise) ReverseChainInPlace(c);
            return chains;
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

        /// <summary>
        /// Заполняет ECWall.LengthInsideM — длину по внутренней грани.
        ///
        /// Логика по паре соседних стен (a, b) в порядке обхода по часовой:
        ///
        ///  A) Стены JOIN в Revit (mitered):
        ///     • правый поворот (выпуклый угол здания)  → обе: LengthM − ширина_соседа/2
        ///     • левый  поворот (угол "остриём внутрь") → без изменений
        ///
        ///  B) Стены НЕ joined (одна примыкает к другой):
        ///     • определяем длинную/короткую по правилу:
        ///       проекция конца короткой на ось длинной лежит на w_long/2 от конца длинной
        ///     • длинная  — без изменений
        ///     • короткая:
        ///         правый поворот (выпуклый) → LengthM − w_длинной/2
        ///         левый  поворот (угол внутрь) → LengthM + w_короткой (торец виден)
        ///
        ///  C) Не удалось определить — LengthM без изменений.
        /// </summary>
        public static void FillLengthInsideM(
            List<ECWall> sortedWalls,
            Document doc,
            double baseToleranceMm = NodeToleranceMm)
        {
            if (sortedWalls == null || sortedWalls.Count == 0) return;

            // 1. Сброс: по умолчанию внутренняя длина = осевой длине
            foreach (var w in sortedWalls)
                if (w != null) w.ECInsulation.LengthInsideM = w.LengthM;

            if (sortedWalls.Count < 2) return;

            double tolFt = UnitUtils.ConvertToInternalUnits(baseToleranceMm, UnitTypeId.Millimeters);
            int n = sortedWalls.Count;

            // 2. Обходим все пары (i, i+1 mod n) — так же обрабатывается замыкающая пара
            for (int i = 0; i < n; i++)
            {
                var a = sortedWalls[i];
                var b = sortedWalls[(i + 1) % n];
                if (a?.Wall == null || b?.Wall == null) continue;

                // Поворот в узле (знак crossZ)
                if (!TryGetJunctionCross(a, b, tolFt, out double crossZ)) continue;
                if (Math.Abs(crossZ) < 1e-6) continue;   // коллинеарно — пропуск

                double wa = GetWallWidth(a);
                double wb = GetWallWidth(b);
                bool joined = AreJoined(doc, a.Wall, b.Wall);

                if (joined)
                {
                    if (crossZ < 0.0)
                    {
                        // Выпуклый угол здания
                        a.ECInsulation.LengthInsideM = a.LengthM - wb / 2.0;
                        b.ECInsulation.LengthInsideM = b.LengthM - wa / 2.0;
                    }
                    // crossZ > 0 (угол внутрь) — без изменений
                }
                else
                {
                    var ls = DetectLongShort(a, b, baseToleranceMm);

                    if (ls == LongShort.AIsLong)
                    {
                        // b — короткая, примыкает к a
                        if (crossZ < 0.0)
                            b.ECInsulation.LengthInsideM = b.LengthM - wa / 2.0;     // выпуклый: спрятана в длинной на w_длинной/2
                        else
                            b.ECInsulation.LengthInsideM = b.LengthM + wb;           // внутрь: торец короткой виден
                    }
                    else if (ls == LongShort.BIsLong)
                    {
                        // a — короткая, примыкает к b
                        if (crossZ < 0.0)
                            a.ECInsulation.LengthInsideM = a.LengthM - wb / 2.0;
                        else
                            a.ECInsulation.LengthInsideM = a.LengthM + wa;
                    }
                    // ls == Unknown — без изменений
                }
            }
        }
        private enum LongShort { Unknown, AIsLong, BIsLong }

        private static bool AreJoined(Document doc, Wall w1, Wall w2)
        {
            if (doc == null || w1 == null || w2 == null) return false;
            try { return JoinGeometryUtils.AreElementsJoined(doc, w1, w2); }
            catch { return false; }
        }
        /// <summary>
        /// Определяет длинную/короткую стену в не-join L-угле:
        /// проекция конца короткой на ось длинной лежит на w_long/2 от конца длинной.
        /// </summary>
        private static LongShort DetectLongShort(ECWall a, ECWall b, double baseToleranceMm)
        {
            var ca = GetCurve(a);
            var cb = GetCurve(b);
            if (ca == null || cb == null) return LongShort.Unknown;

            double wa = GetWallWidth(a);
            double wb = GetWallWidth(b);

            // Допуск проверки: четверть ширины + база (щедро, чтобы не промахнуться)
            double detectTolMm = Math.Max(wa, wb) * 250.0 + baseToleranceMm;
            double detectTolFt = UnitUtils.ConvertToInternalUnits(detectTolMm, UnitTypeId.Millimeters);

            var aNear = EndClosestToOther(ca, cb);
            var bNear = EndClosestToOther(cb, ca);
            if (aNear == null || bNear == null) return LongShort.Unknown;

            // Проекция a-конца на ось b: расстояние до b-конца ≈ wb/2 ⇒ b длинная
            var bProj = ProjectOntoLine(aNear, cb);
            bool bIsLong = bProj != null
                && Math.Abs(bNear.DistanceTo(bProj) - wb / 2.0) <= detectTolFt;

            // Проекция b-конца на ось a: расстояние до a-конца ≈ wa/2 ⇒ a длинная
            var aProj = ProjectOntoLine(bNear, ca);
            bool aIsLong = aProj != null
                && Math.Abs(aNear.DistanceTo(aProj) - wa / 2.0) <= detectTolFt;

            if (aIsLong && !bIsLong) return LongShort.AIsLong;
            if (bIsLong && !aIsLong) return LongShort.BIsLong;
            return LongShort.Unknown;
        }
        /// <summary>Знак поворота при движении a→b в узле (crossZ).</summary>
        private static bool TryGetJunctionCross(ECWall a, ECWall b, double tolFt, out double crossZ)
        {
            crossZ = 0;
            var ca = GetCurve(a);
            var cb = GetCurve(b);
            if (ca == null || cb == null) return false;

            var a0 = ca.GetEndPoint(0); var a1 = ca.GetEndPoint(1);
            var b0 = cb.GetEndPoint(0); var b1 = cb.GetEndPoint(1);

            double d00 = a0.DistanceTo(b0), d01 = a0.DistanceTo(b1);
            double d10 = a1.DistanceTo(b0), d11 = a1.DistanceTo(b1);
            double min = Math.Min(Math.Min(d00, d01), Math.Min(d10, d11));

            // Для не-join допускаем зазор до полуширины более толстой стены
            double effTol = Math.Max(tolFt,
                UnitUtils.ConvertToInternalUnits(
                    Math.Max(GetWallWidth(a), GetWallWidth(b)) * 500.0,
                    UnitTypeId.Millimeters));
            if (min > effTol) return false;

            XYZ pA, qA, pB, qB;
            if (min == d00) { pA = a0; qA = a1; pB = b0; qB = b1; }
            else if (min == d01) { pA = a0; qA = a1; pB = b1; qB = b0; }
            else if (min == d10) { pA = a1; qA = a0; pB = b0; qB = b1; }
            else { pA = a1; qA = a0; pB = b1; qB = b0; }

            var dAin = new XYZ(pA.X - qA.X, pA.Y - qA.Y, 0);
            var dBout = new XYZ(qB.X - pB.X, qB.Y - pB.Y, 0);

            double la = dAin.GetLength(), lb = dBout.GetLength();
            if (la < 1e-9 || lb < 1e-9) return false;

            dAin /= la;
            dBout /= lb;

            crossZ = dAin.X * dBout.Y - dAin.Y * dBout.X;
            return true;
        }

        private static XYZ EndClosestToOther(Curve self, Curve other)
        {
            var s0 = self.GetEndPoint(0); var s1 = self.GetEndPoint(1);
            var o0 = other.GetEndPoint(0); var o1 = other.GetEndPoint(1);
            double d00 = s0.DistanceTo(o0), d01 = s0.DistanceTo(o1);
            double d10 = s1.DistanceTo(o0), d11 = s1.DistanceTo(o1);
            double m = Math.Min(Math.Min(d00, d01), Math.Min(d10, d11));
            return (m == d00 || m == d01) ? s0 : s1;
        }

        private static XYZ ProjectOntoLine(XYZ pt, Curve line)
        {
            var p0 = line.GetEndPoint(0);
            var p1 = line.GetEndPoint(1);
            var dir = p1 - p0;
            double len = dir.GetLength();
            if (len < 1e-9) return null;
            dir /= len;
            return p0 + dir * (pt - p0).DotProduct(dir);
        }

        private static double GetWallWidth(ECWall w) => w?.WidthM ?? 0.0;
       

        /// <summary>
        /// Находит общий узел стен A и B (в пределах tolFt).
        /// Возвращает знак поворота при движении A→B:
        ///   crossZ &lt; 0 — правый поворот (выпуклый угол);
        ///   crossZ &gt; 0 — левый  поворот (входящий угол);
        ///   crossZ ≈ 0 — коллинеарно.
        /// </summary>
        private static bool TryGetJunctionInfo(ECWall a, ECWall b, double tolFt, out double crossZ)
        {
            crossZ = 0;
            var ca = GetCurve(a);
            var cb = GetCurve(b);
            if (ca == null || cb == null) return false;

            var a0 = ca.GetEndPoint(0); var a1 = ca.GetEndPoint(1);
            var b0 = cb.GetEndPoint(0); var b1 = cb.GetEndPoint(1);

            double d00 = a0.DistanceTo(b0);
            double d01 = a0.DistanceTo(b1);
            double d10 = a1.DistanceTo(b0);
            double d11 = a1.DistanceTo(b1);

            double min = Math.Min(Math.Min(d00, d01), Math.Min(d10, d11));
            if (min > tolFt) return false;

            XYZ pA, qA, pB, qB;
            if (min == d00) { pA = a0; qA = a1; pB = b0; qB = b1; }
            else if (min == d01) { pA = a0; qA = a1; pB = b1; qB = b0; }
            else if (min == d10) { pA = a1; qA = a0; pB = b0; qB = b1; }
            else { pA = a1; qA = a0; pB = b1; qB = b0; }

            // Вектор A "в узел" и B "из узла" (строго в плоскости XY)
            XYZ dA_in = new XYZ(pA.X - qA.X, pA.Y - qA.Y, 0);
            XYZ dB_out = new XYZ(qB.X - pB.X, qB.Y - pB.Y, 0);

            double lenA = dA_in.GetLength();
            double lenB = dB_out.GetLength();
            if (lenA < 1e-9 || lenB < 1e-9) return false;

            dA_in /= lenA;
            dB_out /= lenB;

            crossZ = dA_in.X * dB_out.Y - dA_in.Y * dB_out.X;
            return true;
        }
    }
}