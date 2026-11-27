using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    public static class ECWallPerimeterChainBuilder
    {
        public const double NodeToleranceMm = 1.0;

        private static double NodeToleranceInternal =>
            UnitUtils.ConvertToInternalUnits(NodeToleranceMm, UnitTypeId.Millimeters);

        // ============================================================
        //   ПУБЛИЧНЫЙ API
        // ============================================================

        public static void FillLengthInsideM(List<ECWall> walls, Document doc)
        {
            if (walls == null || walls.Count == 0) return;

            var center = ComputeHouseCenter(walls);
            var innerFaces = walls.Select(w => GetInnerFace(w, center)).ToList();
            var contour = BuildBestContour(walls, innerFaces, center);
            if (contour.Count < walls.Count)
            {
                foreach (var w in walls)
                {
                    double total = 0;
                    foreach (var seg in contour)
                        total += ClipSegmentToWall(seg.Start, seg.End, w);
                    if (!IsPartOfContour(w, center, contour))
                        total += ComputePerpendicularContribution(w, contour, center);
                    w.ECInsulation.LengthInsideM =
                        UnitUtils.ConvertFromInternalUnits(total, UnitTypeId.Meters);
                }
                FillFallback(walls, contour, center);   // ← добавить
            }
            foreach (var w in walls)
            {
                double total = 0;
                foreach (var seg in contour)
                    total += ClipSegmentToWall(seg.Start, seg.End, w);

                if (!IsPartOfContour(w, center, contour))
                    total += ComputePerpendicularContribution(w, contour, center);

                // ClipSegmentToWall возвращает футы -> переводим в метры
                w.ECInsulation.LengthInsideM =
                    UnitUtils.ConvertFromInternalUnits(total, UnitTypeId.Meters);
            }
        }
        /// <summary>
        /// Вклад торцевой стены, проходящей СКВОЗЬ контур (не примыкающей к нему).
        /// T-стык (перегородка с внутренней стороны) НЕ должен сюда попадать:
        /// у такой стены нет части, выходящей наружу за контур.
        /// </summary>
        private static double ComputePerpendicularContribution(
            ECWall w, List<(XYZ Start, XYZ End)> contour, XYZ center)
        {
            var curve = GetCurve(w);
            if (curve == null) return 0;

            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            var axisVec = p1 - p0;
            if (axisVec.GetLength() < 1e-9) return 0;
            var axisDir = axisVec.Normalize();
            var perp = new XYZ(-axisDir.Y, axisDir.X, 0);

            double widthInt = GetWallWidthInternal(w);
            double hw = widthInt / 2.0;
            double L = curve.Length;
            double tol = NodeToleranceInternal;
            // «значимый вылет» наружу — иначе это T-стык / примыкание
            double crossThreshold = widthInt * 0.7;

            // 4 угла прямоугольника стены
            var corners = new[] {
        p0 + perp * hw, p0 - perp * hw,
        p1 + perp * hw, p1 - perp * hw
    };

            foreach (var seg in contour)
            {
                var segVec = seg.End - seg.Start;
                if (segVec.GetLength() < tol) continue;
                var segDir = segVec.Normalize();
                var segNormal = new XYZ(-segDir.Y, segDir.X, 0);
                var segMid = (seg.Start + seg.End) * 0.5;

                // Сегмент должен быть смежным со стеной (конец сегмента рядом с footprint)
                bool adjacent = false;
                foreach (var P in new[] { seg.Start, seg.End })
                {
                    var r = P - p0;
                    double x = r.DotProduct(axisDir);
                    double y = r.DotProduct(perp);
                    if (x >= -tol && x <= L + tol && Math.Abs(y) <= hw + tol)
                    { adjacent = true; break; }
                }
                if (!adjacent) continue;

                // «Наружу» = сторона сегмента, противоположная центру дома
                double centerSide = (center - segMid).DotProduct(segNormal);
                if (Math.Abs(centerSide) < 1e-9) continue;
                double outsideSign = -Math.Sign(centerSide);

                // Максимальный вылет стены за контур в «наружную» сторону
                double maxOutside = double.NegativeInfinity;
                foreach (var c in corners)
                {
                    double side = (c - seg.Start).DotProduct(segNormal) * outsideSign;
                    if (side > maxOutside) maxOutside = side;
                }

                // Значимый вылет -> стена реально пересекает контур (кейс ____|_____)
                if (maxOutside > crossThreshold)
                    return widthInt;
            }
            return 0;
        }
        public static List<(XYZ Start, XYZ End)> BuildInnerContour(List<ECWall> walls)
        {
            if (walls == null || walls.Count == 0) return new List<(XYZ, XYZ)>();
            var center = ComputeHouseCenter(walls);
            var innerFaces = walls.Select(w => GetInnerFace(w, center)).ToList();
            return BuildBestContour(walls, innerFaces, center);
        }

        public static List<ECWall> SortClockwise(List<ECWall> walls)
        {
            if (walls == null || walls.Count == 0) return new List<ECWall>();
            var center = ComputeHouseCenter(walls);
            return walls
                .OrderByDescending(w =>
                {
                    var c = GetCurve(w);
                    if (c == null) return 0.0;
                    var m = c.Evaluate(0.5, true);
                    return Math.Atan2(m.Y - center.Y, m.X - center.X);
                })
                .ToList();
        }

        // ============================================================
        //   ЦЕНТР / ВНУТРЕННЯЯ ГРАНЬ
        // ============================================================

        private static XYZ ComputeHouseCenter(List<ECWall> walls)
        {
            double totalW = 0, sx = 0, sy = 0;
            foreach (var w in walls)
            {
                var c = GetCurve(w);
                if (c == null) continue;
                var m = c.Evaluate(0.5, true);
                double len = c.Length;
                sx += m.X * len;
                sy += m.Y * len;
                totalW += len;
            }
            return totalW < 1e-9 ? XYZ.Zero : new XYZ(sx / totalW, sy / totalW, 0);
        }

        private static (XYZ Start, XYZ End) GetInnerFace(ECWall w, XYZ center)
        {
            var c = GetCurve(w);
            if (c == null) return (XYZ.Zero, XYZ.Zero);

            var p0 = c.GetEndPoint(0);
            var p1 = c.GetEndPoint(1);
            var m = c.Evaluate(0.5, true);
            var dir = (p1 - p0).Normalize();
            var nrm = new XYZ(-dir.Y, dir.X, 0);
            double hw = GetWallWidthInternal(w) / 2.0;

            double side = (m + nrm * hw).DistanceTo(center)
                        < (m - nrm * hw).DistanceTo(center) ? +1 : -1;
            var offset = nrm * (hw * side);
            return (p0 + offset, p1 + offset);   // ← БЕЗ extension
        }

        // ============================================================
        //   СБОРКА КОНТУРА
        // ============================================================

        private static List<(XYZ Start, XYZ End)> BuildBestContour(
    List<ECWall> walls, List<(XYZ, XYZ)> innerFaces, XYZ center)
        {
            int n = walls.Count;
            var best = new List<(XYZ, XYZ)>();
            if (n == 0) return best;

            var starts = Enumerable.Range(0, n)
                .OrderByDescending(i =>
                {
                    var c = GetCurve(walls[i]);
                    return c == null ? 0 : c.Evaluate(0.5, true).DistanceTo(center);
                })
                .ToList();

            foreach (int startIdx in starts)
            {
                var chain = WalkContour(startIdx, walls, innerFaces);
                if (chain.Count > best.Count) best = chain;
                if (best.Count == n) break;
            }
            return best;
        }
        private static List<(XYZ Start, XYZ End)> WalkContour(
    int startIdx, List<ECWall> walls, List<(XYZ Start, XYZ End)> innerFaces)
        {
            int n = walls.Count;
            var best = new List<(XYZ, XYZ)>();

            foreach (bool flipStart in new[] { false, true })
            {
                var used = new bool[n];
                used[startIdx] = true;
                var chain = new List<(XYZ, XYZ)>();
                var ifc0 = innerFaces[startIdx];
                var first = flipStart ? (ifc0.End, ifc0.Start) : ifc0;
                chain.Add(first);
                var startPoint = first.Item1;
                var currentEnd = first.Item2;
                var lastDir = (first.Item2 - first.Item1).Normalize();

                var result = WalkDFS(chain, used, currentEnd, startIdx, lastDir,
                                     startPoint, walls, innerFaces, n);
                if (result.Chain.Count > best.Count) best = result.Chain;
            }
            return best;
        }

        private static (List<(XYZ, XYZ)> Chain, bool Closed) WalkDFS(
            List<(XYZ, XYZ)> chain, bool[] used,
            XYZ currentEnd, int lastIdx, XYZ lastDir,
            XYZ startPoint,
            List<ECWall> walls, List<(XYZ Start, XYZ End)> innerFaces, int n)
        {
            // Замыкание на стартовую точку — цикл завершён
            double closeTol = UnitUtils.ConvertToInternalUnits(80, UnitTypeId.Millimeters);
            bool closed = chain.Count > 2 && currentEnd.DistanceTo(startPoint) < closeTol;
            if (closed) return (new List<(XYZ, XYZ)>(chain), true);

            // Кандидаты — только в пределах допуска стыка
            var candidates = new List<(int Idx, bool Flip, double Dist, double Align)>();
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                var ifc = innerFaces[i];
                double tol = CalcJoinTolerance(walls[lastIdx], walls[i]);

                double d1 = currentEnd.DistanceTo(ifc.Start);
                if (d1 < tol)
                {
                    var dir = (ifc.End - ifc.Start).Normalize();
                    double align = dir.DotProduct(lastDir);           // 1 = прямо, -1 = назад
                    candidates.Add((i, false, d1, align));
                }
                double d2 = currentEnd.DistanceTo(ifc.End);
                if (d2 < tol)
                {
                    var dir = (ifc.Start - ifc.End).Normalize();
                    double align = dir.DotProduct(lastDir);
                    candidates.Add((i, true, d2, align));
                }
            }
            if (candidates.Count == 0)
                return (new List<(XYZ, XYZ)>(chain), false);

            // Сортировка: сначала максимальный align (продолжение прямо),
            // при равенстве — минимальная дистанция.
            candidates.Sort((a, b) =>
            {
                int cmp = b.Align.CompareTo(a.Align);
                return cmp != 0 ? cmp : a.Dist.CompareTo(b.Dist);
            });

            var bestChain = new List<(XYZ, XYZ)>(chain);
            bool bestClosed = false;

            // Ограничение на ветвление, чтобы не уйти в экспоненту
            int tried = 0;
            foreach (var c in candidates)
            {
                if (tried >= 3) break;   // пробуем топ-3 кандидатов
                tried++;

                var ifc = innerFaces[c.Idx];
                var newSeg = c.Flip ? (ifc.End, ifc.Start) : ifc;
                var newEnd = c.Flip ? ifc.Start : ifc.End;
                var newDir = (newEnd - newSeg.Item1).Normalize();

                used[c.Idx] = true;
                chain.Add(newSeg);

                var (subChain, subClosed) = WalkDFS(
                    chain, used, newEnd, c.Idx, newDir, startPoint,
                    walls, innerFaces, n);

                chain.RemoveAt(chain.Count - 1);
                used[c.Idx] = false;

                if (subClosed && (!bestClosed || subChain.Count > bestChain.Count))
                { bestChain = subChain; bestClosed = true; }
                else if (!bestClosed && subChain.Count > bestChain.Count)
                { bestChain = subChain; }
            }
            return (bestChain, bestClosed);
        }

        private static void FillFallback(
    List<ECWall> walls,
    List<(XYZ Start, XYZ End)> contour,
    XYZ center)
        {
            double coveredTol = UnitUtils.ConvertToInternalUnits(20, UnitTypeId.Millimeters);

            foreach (var w in walls)
            {
                // Пропускаем те, чей LengthInsideM уже > 0 (по контуру или перпендикулярный вклад)
                if (w.ECInsulation.LengthInsideM > 1e-6) continue;

                // Стена не граничная? Проверяем, видна ли её внутренняя грань из центра
                var curve = GetCurve(w);
                if (curve == null) continue;

                var ifc = GetInnerFace(w, center);
                var ifMid = (ifc.Start + ifc.End) * 0.5;

                // Луч от центра к ifMid
                var toIf = ifMid - center;
                double toIfLen = toIf.GetLength();
                if (toIfLen < 1e-9) continue;
                var dir = toIf / toIfLen;

                bool blocked = false;
                foreach (var other in walls)
                {
                    if (other == w) continue;
                    if (RayHitsWall(center, dir, toIfLen - coveredTol, other))
                    { blocked = true; break; }
                }
                if (blocked) continue;

                // Не перекрыта — считаем её вклад
                // Длина inner face w (за вычетом углов — приближённо, без стыков)
                double innerLen = ifc.Start.DistanceTo(ifc.End);
                // Если на одном из концов есть стык с другой стеной — вычесть
                // (упрощённо: вычитаем hw того, с кем стыкуется)
                double deduct = 0;
                foreach (var other in walls)
                {
                    if (other == w) continue;
                    var ofc = GetInnerFace(other, center);
                    double tol = CalcJoinTolerance(w, other);
                    if (ifc.Start.DistanceTo(ofc.Start) < tol ||
                        ifc.Start.DistanceTo(ofc.End) < tol)
                        deduct += GetWallWidthInternal(other) / 2.0;
                    if (ifc.End.DistanceTo(ofc.Start) < tol ||
                        ifc.End.DistanceTo(ofc.End) < tol)
                        deduct += GetWallWidthInternal(other) / 2.0;
                }

                double contrib = Math.Max(0, innerLen - deduct);
                w.ECInsulation.LengthInsideM +=
                    UnitUtils.ConvertFromInternalUnits(contrib, UnitTypeId.Meters);
            }
        }

        private static bool RayHitsWall(XYZ origin, XYZ dir, double maxDist, ECWall w)
        {
            var curve = GetCurve(w);
            if (curve == null) return false;

            var a = curve.GetEndPoint(0);
            var b = curve.GetEndPoint(1);
            double L = a.DistanceTo(b);
            if (L < 1e-9) return false;

            var wDir = (b - a) / L;
            var wPerp = new XYZ(-wDir.Y, wDir.X, 0);
            double hw = GetWallWidthInternal(w) / 2.0;

            var rel = origin - a;
            double ox = rel.DotProduct(wDir);
            double oy = rel.DotProduct(wPerp);
            double dx = dir.DotProduct(wDir);
            double dy = dir.DotProduct(wPerp);

            // Луч от origin в направлении dir, длина maxDist
            // В локальных координатах стены: (ox,oy) + t*(dx,dy), t ∈ [0, maxDist]
            // Проверяем пересечение с [0,L]×[-hw,hw]
            if (!LiangBarsky(ox, oy, ox + dx * maxDist, oy + dy * maxDist,
                             0, L, -hw, hw, out double t0, out double t1))
                return false;

            // Если t1 >= 1 — пересечение только в конце луча (в самой точке innerMid)
            // Это не блокировка. Блокировка — если t1 < 1 (луч упирается в стену раньше).
            return t1 < 0.999;
        }
        //private static List<(XYZ Start, XYZ End)> WalkContour(
        //    int startIdx,
        //    List<ECWall> walls,
        //    List<(XYZ Start, XYZ End)> innerFaces)
        //{
        //    int n = walls.Count;
        //    var best = new List<(XYZ, XYZ)>();

        //    foreach (bool flipStart in new[] { false, true })
        //    {
        //        var used = new bool[n];
        //        used[startIdx] = true;
        //        var chain = new List<(XYZ, XYZ)>();
        //        var ifc0 = innerFaces[startIdx];
        //        chain.Add(flipStart ? (ifc0.End, ifc0.Start) : ifc0);
        //        var currentEnd = chain[0].Item2;
        //        int lastIdx = startIdx;                             // FIX: запоминаем последнюю стену

        //        bool progress = true;
        //        while (progress)
        //        {
        //            progress = false;
        //            for (int i = 0; i < n; i++)
        //            {
        //                if (used[i]) continue;
        //                var ifc = innerFaces[i];
        //                double tol = CalcJoinTolerance(walls[lastIdx], walls[i]); // FIX

        //                if (currentEnd.DistanceTo(ifc.Start) < tol)
        //                {
        //                    chain.Add(ifc);
        //                    currentEnd = ifc.End;
        //                    used[i] = true;
        //                    lastIdx = i;
        //                    progress = true;
        //                    break;
        //                }
        //                if (currentEnd.DistanceTo(ifc.End) < tol)
        //                {
        //                    chain.Add((ifc.End, ifc.Start));
        //                    currentEnd = ifc.Start;
        //                    used[i] = true;
        //                    lastIdx = i;
        //                    progress = true;
        //                    break;
        //                }
        //            }
        //        }
        //        if (chain.Count > best.Count) best = chain;
        //    }
        //    return best;
        //}

        private static double CalcJoinTolerance(ECWall a, ECWall b)
        {
            // Требуется покрыть:
            //   L-угол (равные ширины)       → gap = 0
            //   L-угол (ширины 200/300)      → gap ≈ |hwA - hwB| = 50мм
            //   коллинеарная склейка равных  → gap = w = 200мм
            // Берём с запасом: max(w_a, w_b) + 20мм.
            double wMax = Math.Max(GetWallWidthM(a), GetWallWidthM(b));
            double tolMm = wMax * 1000.0 + 20.0;
            return UnitUtils.ConvertToInternalUnits(tolMm, UnitTypeId.Millimeters);
        }

        // ============================================================
        //   ПЕРЕСЕЧЕНИЕ: КОНТУР ∩ ПРЯМОУГОЛЬНИК СТЕНЫ
        // ============================================================

        private static double ClipSegmentToWall(XYZ segStart, XYZ segEnd, ECWall w)
        {
            var curve = GetCurve(w);
            if (curve == null) return 0;

            var axisStart = curve.GetEndPoint(0);
            var axisEnd = curve.GetEndPoint(1);
            double L = axisStart.DistanceTo(axisEnd);
            if (L < 1e-9) return 0;

            var dir = (axisEnd - axisStart) / L;
            var perp = new XYZ(-dir.Y, dir.X, 0);
            double hw = GetWallWidthInternal(w) / 2.0;

            // Эпсилон геометрической погрешности double (~0.3 мкм в футах).
            // НЕ путать с допуском стыка (NodeToleranceInternal = 1мм).
            const double tol = 1e-6;

            var segVec = segEnd - segStart;
            double segLen = segVec.GetLength();
            if (segLen < 1e-9) return 0;
            var segDirN = segVec / segLen;
            double cosAngle = Math.Abs(segDirN.DotProduct(dir));
            if (cosAngle < 0.7) return 0;

            var r1 = segStart - axisStart;
            var r2 = segEnd - axisStart;
            double x1 = r1.DotProduct(dir);
            double y1 = r1.DotProduct(perp);
            double x2 = r2.DotProduct(dir);
            double y2 = r2.DotProduct(perp);

            if (!LiangBarsky(x1, y1, x2, y2,
                             -tol, L + tol, -hw - tol, hw + tol,
                             out double t0, out double t1))
                return 0;

            double dx = x2 - x1, dy = y2 - y1;
            double fullLen = Math.Sqrt(dx * dx + dy * dy);
            return fullLen * (t1 - t0);
        }

        private static bool LiangBarsky(
    double x1, double y1, double x2, double y2,
    double xmin, double xmax, double ymin, double ymax,
    out double t0, out double t1)
        {
            const double eps = 1e-9;         // для проверки q[i] < 0
            t0 = 0; t1 = 1;
            double dx = x2 - x1, dy = y2 - y1;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { x1 - xmin, xmax - x1, y1 - ymin, ymax - y1 };

            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-12)
                {
                    if (q[i] < -eps) return false;
                }
                else
                {
                    double r = q[i] / p[i];
                    if (p[i] < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
                    else { if (r < t0) return false; if (r < t1) t1 = r; }
                }
            }
            return true;
        }

        // ============================================================
        //   ТОРЦЕВЫЕ СТЕНЫ (T-ОБРАЗНЫЕ СТЫКИ)
        // ============================================================

        /// <summary>
        /// Стена является частью контура, если середина её внутренней грани
        /// лежит на одном из контурных отрезков.
        /// </summary>
        private static bool IsPartOfContour(
            ECWall w,
            XYZ center,
            List<(XYZ Start, XYZ End)> contour)
        {
            var (ifStart, ifEnd) = GetInnerFace(w, center);
            var ifMid = (ifStart + ifEnd) * 0.5;
            double tol = NodeToleranceInternal;

            foreach (var seg in contour)
            {
                var segVec = seg.End - seg.Start;
                double segLen2 = segVec.DotProduct(segVec);
                if (segLen2 < 1e-12) continue;

                double t = (ifMid - seg.Start).DotProduct(segVec) / segLen2;
                t = Math.Max(0.0, Math.Min(1.0, t));
                var proj = seg.Start + segVec * t;
                if (proj.DistanceTo(ifMid) < tol) return true;
            }
            return false;
        }

        /// <summary>
        /// Для торцевой стены (не части контура), через торец которой проходит
        /// контур, — вклад в длину охлаждения равен толщине стены.
        /// </summary>
        private static double ComputePerpendicularContribution(
            ECWall w,
            List<(XYZ Start, XYZ End)> contour)
        {
            var curve = GetCurve(w);
            if (curve == null) return 0;

            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            var axisDir = (p1 - p0).Normalize();
            var perp = new XYZ(-axisDir.Y, axisDir.X, 0);
            double L = curve.Length;
            double hw = GetWallWidthInternal(w) / 2.0;
            double tol = NodeToleranceInternal;

            foreach (var seg in contour)
            {
                foreach (var P in new[] { seg.Start, seg.End })
                {
                    var r = P - p0;
                    double x = r.DotProduct(axisDir);
                    double y = r.DotProduct(perp);

                    // Точка контура попадает в прямоугольник стены
                    // (торец "внутри" стены, с учётом допуска)
                    if (x >= -tol && x <= L + tol && Math.Abs(y) <= hw + tol)
                        return GetWallWidthInternal(w);
                }
            }
            return 0;
        }

        // ============================================================
        //   ХЕЛПЕРЫ
        // ============================================================

        private static Curve GetCurve(ECWall w)
        {
            if (w == null) return null;
            if (w.Curve != null) return w.Curve;
            var wall = w.Wall ?? w.Element as Wall;
            return (wall?.Location as LocationCurve)?.Curve;
        }

        /// <summary>Ширина стены в метрах — для расчётов и допусков.</summary>
        private static double GetWallWidthM(ECWall w)
        {
            if (w == null) return 0;
            var wall = w.Wall ?? w.Element as Wall;
            if (wall != null)
                return UnitUtils.ConvertFromInternalUnits(wall.Width, UnitTypeId.Meters);
            return w.WidthM;
        }

        /// <summary>Ширина стены во внутренних единицах Revit (футы) — для геометрии.</summary>
        private static double GetWallWidthInternal(ECWall w)
        {
            return UnitUtils.ConvertToInternalUnits(GetWallWidthM(w), UnitTypeId.Meters);
        }
    }
}