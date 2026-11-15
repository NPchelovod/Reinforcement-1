using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Reinforcement.Aspirant
{
    /// <summary>
    /// Результат подбора сбалансированных опорных точек плиты.
    /// </summary>
    public class SupportSelectionResult
    {
        /// <summary>Выбранные опорные точки (мм).</summary>
        public List<XYZ> Supports { get; set; } = new List<XYZ>();

        /// <summary>Центр масс (площадной центроид) контура плиты (мм).</summary>
        public XYZ PlateCentroid { get; set; } = XYZ.Zero;

        /// <summary>Отклонение центроида опор от центра масс плиты (мм).</summary>
        public double CentroidError { get; set; }

        /// <summary>Минимальное попарное расстояние между опорами (мм).</summary>
        public double MinPairDistance { get; set; }

        /// <summary>Итоговый балл: больше — лучше.</summary>
        public double Score { get; set; }

        /// <summary>true, если центр масс лежит внутри выпуклой оболочки опор (для N ≥ 3).</summary>
        public bool IsStaticallyValid { get; set; }

        /// <summary>true, если сработал жадный fallback вместо полного перебора.</summary>
        public bool UsedGreedyFallback { get; set; }


        //public static void primer()
        //{
        //    var floors = GetAllFloorsContoursInView(doc, view);
        //    var columns = GetAllColumns(doc, view);

        //    foreach (var flr in floors)
        //    {
        //        // Уже выбрали 2 опоры (например, по колоннам на плите)
        //        List<XYZ> fixedSupports = columns
        //            .Where(c => ColumnIntersectsFloor(c, flr))
        //            .Select(c => c.StartMm)   // или точку пересечения с плитой
        //            .Take(2)
        //            .ToList();

        //        // Нужно 4 опоры итого — 2 фиксированные + 2 подобрать из контура
        //        var r = PlateSupportSelector.FindBalancedSupportsWithFixed(
        //            flr.OuterContourMm, fixedSupports, N: 4, centroidWeight: 2.0);

        //        System.Diagnostics.Debug.WriteLine(
        //            $"Плита [{flr.FloorId}]: фикс. {fixedSupports.Count}, всего {r.Supports.Count}, " +
        //            $"валидна={r.IsStaticallyValid}, ошибка центроида {r.CentroidError:F1} мм, " +
        //            $"мин. дистанция {r.MinPairDistance:F1} мм, жадный={r.UsedGreedyFallback}");

        //        for (int i = 0; i < r.Supports.Count; i++)
        //        {
        //            string kind = i < fixedSupports.Count ? "fix" : "new";
        //            var p = r.Supports[i];
        //            System.Diagnostics.Debug.WriteLine(
        //                $"   [{kind}] ({p.X:F1}, {p.Y:F1}, {p.Z:F1}) мм");
        //        }
        //    }
           
        //    foreach (var flr in floors)
        //    {
        //        var r = PlateSupportSelector.FindBalancedSupports(flr.OuterContourMm, N: 3);
        //        System.Diagnostics.Debug.WriteLine(
        //            $"Плита [{flr.FloorId}] \"{flr.FloorName}\": " +
        //            $"опор {r.Supports.Count}, CoM внутри оболочки: {r.IsStaticallyValid}, " +
        //            $"ошибка центроида {r.CentroidError:F1} мм, " +
        //            $"мин. дистанция {r.MinPairDistance:F1} мм, score {r.Score:F3}");
        //        foreach (var p in r.Supports)
        //            System.Diagnostics.Debug.WriteLine($"   ({p.X:F1}, {p.Y:F1}, {p.Z:F1}) мм");
        //    }
        //}
    }

    /// <summary>
    /// Подбор сбалансированных опорных точек на контуре плиты.
    /// Критерий: центроид опор совпадает с центром масс плиты (максимально),
    /// минимальное попарное расстояние велико, центр масс внутри оболочки.
    /// </summary>
    public static class PlateSupportSelector
    {
        /// <summary>Допуск сравнения точек (мм).</summary>
        private const double TolMm = 0.1;

        /// <summary>Порог числа сочетаний, выше которого включается жадный fallback.</summary>
        private const long ComboThreshold = 2_000_000;

        /// <summary>
        /// Подбирает N сбалансированных опорных точек из контура плиты.
        /// </summary>
        /// <param name="contourMm">Внешний контур плиты (мм), без дублей.</param>
        /// <param name="N">Требуемое число опор.</param>
        /// <param name="centroidWeight">Вес штрафа за отклонение центроида от центра масс.
        /// Больше — сильнее приоритет точного центроида. По умолчанию 2.0.</param>
        public static SupportSelectionResult FindBalancedSupports(
            List<XYZ> contourMm, int N, double centroidWeight = 2.0)
        {
            var result = new SupportSelectionResult();
            if (contourMm == null || contourMm.Count == 0 || N <= 0) return result;

            var pts = contourMm.Dedupe(TolMm);
            result.PlateCentroid = PolygonCentroid(pts);

            if (N >= pts.Count)
            {
                result.Supports = new List<XYZ>(pts);
                result.IsStaticallyValid = true;
                return result;
            }

            double diag = BoundingBoxDiagonal(pts);
            if (diag < 1e-6) diag = 1.0;

            if (Binomial(pts.Count, N) > ComboThreshold)
            {
                result.UsedGreedyFallback = true;
                return GreedySelect(pts, N, result.PlateCentroid, diag, centroidWeight);
            }

            double bestScore = double.NegativeInfinity;
            int[] bestCombo = null;
            double bestCe = 0, bestMpd = 0;
            bool bestValid = false;

            foreach (var combo in Combinations(pts.Count, N))
            {
                var subset = new List<XYZ>(N);
                for (int i = 0; i < N; i++) subset.Add(pts[combo[i]]);

                if (!EvaluateSubset(subset, result.PlateCentroid, diag, N,
                                    centroidWeight, out double ce, out double mpd,
                                    out double score, out bool valid))
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCombo = (int[])combo.Clone();
                    bestCe = ce; bestMpd = mpd; bestValid = valid;
                }
            }

            if (bestCombo == null) return result;

            result.Supports = bestCombo.Select(i => pts[i]).ToList();
            result.CentroidError = bestCe;
            result.MinPairDistance = bestMpd;
            result.Score = bestScore;
            result.IsStaticallyValid = bestValid;
            return result;
        }

        /// <summary>
        /// Подбирает N опор с учётом заранее выбранных (фиксированных) точек.
        /// Фиксированные точки всегда входят в результат; из оставшихся кандидатов
        /// контура выбираются (N − fixedPoints.Count) точек так, чтобы итоговый
        /// набор был максимально сбалансирован. Удобно, когда часть опор
        /// уже задана (например, по колоннам), а часть нужно подобрать.
        /// </summary>
        /// <param name="contourMm">Внешний контур плиты (мм).</param>
        /// <param name="fixedPointsMm">Уже выбранные опорные точки (мм), могут быть
        /// с контура или вне его. Если пусто — эквивалентно FindBalancedSupports.</param>
        /// <param name="N">Итоговое число опор (включая фиксированные).</param>
        /// <param name="centroidWeight">Вес штрафа за отклонение центроида.</param>
        public static SupportSelectionResult FindBalancedSupportsWithFixed(
            List<XYZ> contourMm,
            List<XYZ> fixedPointsMm,
            int N,
            double centroidWeight = 2.0)
        {
            var result = new SupportSelectionResult();
            if (contourMm == null || contourMm.Count == 0 || N <= 0) return result;

            var fixedPts = fixedPointsMm?.Where(p => p != null).ToList() ?? new List<XYZ>();

            // Без фиксированных — обычный сценарий
            if (fixedPts.Count == 0)
                return FindBalancedSupports(contourMm, N, centroidWeight);

            var allPts = contourMm.Dedupe(TolMm);
            result.PlateCentroid = PolygonCentroid(allPts);

            // Фиксированных больше или столько же, сколько нужно — возвращаем первые N
            if (fixedPts.Count >= N)
            {
                result.Supports = fixedPts.Take(N).ToList();
                result.IsStaticallyValid = N >= 3;
                return result;
            }

            // Исключаем из кандидатов точки, совпадающие с фиксированными
            var candidates = new List<XYZ>();
            foreach (var p in allPts)
            {
                bool isFixed = false;
                foreach (var fp in fixedPts)
                    if (fp.DistanceTo(p) <= TolMm) { isFixed = true; break; }
                if (!isFixed) candidates.Add(p);
            }

            int need = N - fixedPts.Count;
            if (need > candidates.Count)
            {
                // Не хватает точек — возвращаем всё, что есть
                result.Supports = new List<XYZ>(fixedPts);
                result.Supports.AddRange(candidates);
                result.IsStaticallyValid = N >= 3;
                return result;
            }

            double diag = BoundingBoxDiagonal(allPts);
            if (diag < 1e-6) diag = 1.0;

            if (Binomial(candidates.Count, need) > ComboThreshold)
            {
                result.UsedGreedyFallback = true;
                return GreedyWithFixed(candidates, fixedPts, result.PlateCentroid,
                                       diag, N, centroidWeight);
            }

            double bestScore = double.NegativeInfinity;
            int[] bestCombo = null;
            double bestCe = 0, bestMpd = 0;
            bool bestValid = false;

            foreach (var combo in Combinations(candidates.Count, need))
            {
                var subset = new List<XYZ>(N);
                subset.AddRange(fixedPts);
                for (int i = 0; i < need; i++) subset.Add(candidates[combo[i]]);

                if (!EvaluateSubset(subset, result.PlateCentroid, diag, N,
                                    centroidWeight, out double ce, out double mpd,
                                    out double score, out bool valid))
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCombo = (int[])combo.Clone();
                    bestCe = ce; bestMpd = mpd; bestValid = valid;
                }
            }

            // Ни одна комбинация не дала валидный результат — берём «как есть»
            if (bestCombo == null)
            {
                result.Supports = new List<XYZ>(fixedPts);
                for (int i = 0; i < need; i++) result.Supports.Add(candidates[i]);
                return result;
            }

            result.Supports = new List<XYZ>(N);
            result.Supports.AddRange(fixedPts);
            foreach (var i in bestCombo) result.Supports.Add(candidates[i]);
            result.CentroidError = bestCe;
            result.MinPairDistance = bestMpd;
            result.Score = bestScore;
            result.IsStaticallyValid = bestValid;
            return result;
        }

        /// <summary>
        /// Оценивает подмножество опор: считает ошибку центроида, минимальное
        /// попарное расстояние и итоговый балл. Возвращает false, если подмножество
        /// не проходит проверку статической устойчивости (N ≥ 3).
        /// </summary>
        /// <param name="subset">Набор точек (обычно ровно N, при жадном выборе — меньше).</param>
        /// <param name="cPlate">Центр масс плиты.</param>
        /// <param name="diag">Диагональ bounding box плиты — для нормировки.</param>
        /// <param name="N">Фактическое число точек в subset (используется для центроида).</param>
        /// <param name="centroidWeight">Вес штрафа за отклонение центроида.</param>
        private static bool EvaluateSubset(
            List<XYZ> subset, XYZ cPlate, double diag, int N,
            double centroidWeight,
            out double centroidError, out double minPairDist,
            out double score, out bool valid)
        {
            centroidError = 0; minPairDist = 0; score = 0; valid = false;

            // Центроид подмножества
            XYZ cS = XYZ.Zero;
            foreach (var p in subset) cS += p;
            cS = cS / (double)N;
            centroidError = cS.DistanceTo(cPlate);

            // Минимальное попарное расстояние
            minPairDist = double.MaxValue;
            for (int i = 0; i < N; i++)
                for (int j = i + 1; j < N; j++)
                {
                    double d = subset[i].DistanceTo(subset[j]);
                    if (d < minPairDist) minPairDist = d;
                }
            if (N == 1) minPairDist = diag;

            // Проверка статической устойчивости (N ≥ 3)
            if (N >= 3)
            {
                if (!IsPointInPolygon3D(cPlate, subset, TolMm)) return false;
            }
            valid = N >= 3;

            double ceNorm = centroidError / diag;
            double mpdNorm = minPairDist / diag;
            score = mpdNorm - centroidWeight * ceNorm;
            return true;
        }

        /// <summary>
        /// Площадной центроид (центр масс) плоского многоугольника в 3D.
        /// Для контуров &lt; 3 точек — среднее арифметическое вершин.
        /// Корректно работает для наклонных плоскостей.
        /// </summary>
        public static XYZ PolygonCentroid(List<XYZ> points)
        {
            if (points == null || points.Count == 0) return XYZ.Zero;
            if (points.Count < 3)
            {
                XYZ s = XYZ.Zero;
                foreach (var p in points) s += p;
                return s / (double)points.Count;
            }

            // Нормаль по Ньюэллу
            XYZ n = XYZ.Zero;
            int m = points.Count;
            for (int i = 0; i < m; i++)
            {
                XYZ a = points[i], b = points[(i + 1) % m];
                n += new XYZ(
                    (a.Y - b.Y) * (a.Z + b.Z),
                    (a.Z - b.Z) * (a.X + b.X),
                    (a.X - b.X) * (a.Y + b.Y));
            }
            if (n.GetLength() < 1e-12)
            {
                XYZ s = XYZ.Zero;
                foreach (var p in points) s += p;
                return s / (double)points.Count;
            }
            n = n.Normalize();

            // Локальные оси в плоскости
            XYZ u = Math.Abs(n.X) < 0.9 ? XYZ.BasisX : XYZ.BasisY;
            u = (u - n * u.DotProduct(n)).Normalize();
            XYZ v = n.CrossProduct(u);

            XYZ origin = points[0];
            double A2 = 0, cx = 0, cy = 0;
            for (int i = 0; i < m; i++)
            {
                XYZ a = points[i] - origin, b = points[(i + 1) % m] - origin;
                double au = a.DotProduct(u), av = a.DotProduct(v);
                double bu = b.DotProduct(u), bv = b.DotProduct(v);
                double cross = au * bv - bu * av;
                A2 += cross;
                cx += (au + bu) * cross;
                cy += (av + bv) * cross;
            }
            if (Math.Abs(A2) < 1e-12)
            {
                XYZ s = XYZ.Zero;
                foreach (var p in points) s += p;
                return s / (double)points.Count;
            }
            return origin + u * (cx / (3 * A2)) + v * (cy / (3 * A2));
        }

        /// <summary>
        /// Проверяет, лежит ли точка внутри плоского многоугольника в 3D
        /// (ray casting в проекции на плоскость).
        /// </summary>
        private static bool IsPointInPolygon3D(XYZ p, List<XYZ> poly, double tol)
        {
            int m = poly.Count;
            if (m < 3) return false;

            // Ищем три неколлинеарные точки для построения плоскости
            XYZ p0 = poly[0];
            int i1 = -1;
            for (int i = 1; i < m; i++)
                if (poly[i].DistanceTo(p0) > tol) { i1 = i; break; }
            if (i1 < 0) return false;

            int i2 = -1;
            for (int i = i1 + 1; i < m; i++)
            {
                XYZ v1 = poly[i1] - p0, v2 = poly[i] - p0;
                if (v1.CrossProduct(v2).GetLength() > tol) { i2 = i; break; }
            }
            if (i2 < 0) return false;

            Plane plane = Plane.CreateByThreePoints(p0, poly[i1], poly[i2]);
            XYZ u = plane.XVec, v = plane.YVec, o = plane.Origin;

            Func<XYZ, UV> to2D = q =>
            {
                XYZ d = q - o;
                return new UV(d.DotProduct(u), d.DotProduct(v));
            };

            UV pt = to2D(p);
            bool inside = false;
            for (int i = 0, j = m - 1; i < m; j = i++)
            {
                UV a = to2D(poly[i]), b = to2D(poly[j]);
                if ((a.V > pt.V) != (b.V > pt.V))
                {
                    double xCross = a.U + (pt.V - a.V) * (b.U - a.U) / (b.V - a.V);
                    if (pt.U < xCross) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Жадный подбор (без фиксированных): перебирает «повороты»
        /// равномерной угловой выборки вокруг центра масс.
        /// </summary>
        private static SupportSelectionResult GreedySelect(
            List<XYZ> pts, int N, XYZ cPlate, double diag, double w)
        {
            var result = new SupportSelectionResult { PlateCentroid = cPlate, UsedGreedyFallback = true };

            // Плоскость по первым трём неколлинеарным
            XYZ p0 = pts[0];
            int i1 = -1;
            for (int i = 1; i < pts.Count; i++)
                if (pts[i].DistanceTo(p0) > TolMm) { i1 = i; break; }
            if (i1 < 0) { result.Supports = pts.Take(N).ToList(); return result; }

            int i2 = -1;
            for (int i = i1 + 1; i < pts.Count; i++)
                if ((pts[i1] - p0).CrossProduct(pts[i] - p0).GetLength() > TolMm) { i2 = i; break; }
            if (i2 < 0) { result.Supports = pts.Take(N).ToList(); return result; }

            Plane plane = Plane.CreateByThreePoints(p0, pts[i1], pts[i2]);
            XYZ u = plane.XVec, v = plane.YVec;

            var withAngles = pts.Select((p, idx) =>
            {
                XYZ d = p - cPlate;
                return (idx, angle: Math.Atan2(d.DotProduct(v), d.DotProduct(u)));
            }).OrderBy(t => t.angle).ToList();

            int M = withAngles.Count;
            double bestScore = double.NegativeInfinity;
            List<XYZ> bestSet = null;
            double bestCe = 0, bestMpd = 0;

            for (int r = 0; r < M; r++)
            {
                var idxs = new List<int>();
                for (int k = 0; k < N; k++)
                {
                    int idx = (r + (int)Math.Round((double)k * M / N)) % M;
                    if (!idxs.Contains(idx)) idxs.Add(idx);
                }
                if (idxs.Count < N) continue;

                var subset = idxs.Select(i => withAngles[i].idx).Select(i => pts[i]).ToList();
                if (!EvaluateSubset(subset, cPlate, diag, N, w,
                                    out double ce, out double mpd, out double sc, out bool valid))
                    continue;
                if (sc > bestScore) { bestScore = sc; bestSet = subset; bestCe = ce; bestMpd = mpd; }
            }

            if (bestSet != null)
            {
                result.Supports = bestSet;
                result.CentroidError = bestCe;
                result.MinPairDistance = bestMpd;
                result.Score = bestScore;
                result.IsStaticallyValid = N >= 3;
            }
            return result;
        }

        /// <summary>
        /// Жадный подбор с фиксированными точками: на каждой итерации добавляет
        /// кандидата, дающего лучший score с уже выбранным набором.
        /// </summary>
        private static SupportSelectionResult GreedyWithFixed(
            List<XYZ> candidates, List<XYZ> fixedPts, XYZ cPlate, double diag,
            int N, double w)
        {
            var result = new SupportSelectionResult { PlateCentroid = cPlate, UsedGreedyFallback = true };

            var chosen = new List<XYZ>(fixedPts);
            var remaining = new List<XYZ>(candidates);

            while (chosen.Count < N && remaining.Count > 0)
            {
                double bestScore = double.NegativeInfinity;
                XYZ bestPt = null;

                foreach (var candidate in remaining)
                {
                    var trial = new List<XYZ>(chosen) { candidate };
                    // N = trial.Count — EvaluateSubset корректно посчитает центроид
                    if (!EvaluateSubset(trial, cPlate, diag, trial.Count, w,
                                        out _, out _, out double sc, out _))
                        continue;
                    if (sc > bestScore) { bestScore = sc; bestPt = candidate; }
                }

                if (bestPt == null) break;
                chosen.Add(bestPt);
                remaining.Remove(bestPt);
            }

            result.Supports = chosen;
            if (chosen.Count >= 3 &&
                EvaluateSubset(chosen, cPlate, diag, chosen.Count, w,
                               out double ce, out double mpd, out double sc2, out bool valid))
            {
                result.CentroidError = ce;
                result.MinPairDistance = mpd;
                result.Score = sc2;
                result.IsStaticallyValid = valid;
            }
            return result;
        }

        /// <summary>Диагональ bounding box списка точек.</summary>
        private static double BoundingBoxDiagonal(List<XYZ> pts)
        {
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            double minZ = pts.Min(p => p.Z), maxZ = pts.Max(p => p.Z);
            return Math.Sqrt(
                (maxX - minX) * (maxX - minX) +
                (maxY - minY) * (maxY - minY) +
                (maxZ - minZ) * (maxZ - minZ));
        }

        /// <summary>Биномиальный коэффициент C(n, k).</summary>
        private static long Binomial(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Math.Min(k, n - k);
            long r = 1;
            for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
            return r;
        }

        /// <summary>Перебор всех сочетаний из n по k (в порядке возрастания индексов).</summary>
        private static IEnumerable<int[]> Combinations(int n, int k)
        {
            if (k > n) yield break;
            var c = new int[k];
            for (int i = 0; i < k; i++) c[i] = i;
            while (true)
            {
                yield return (int[])c.Clone();
                int idx = k - 1;
                while (idx >= 0 && c[idx] == n - k + idx) idx--;
                if (idx < 0) yield break;
                c[idx]++;
                for (int i = idx + 1; i < k; i++) c[i] = c[i - 1] + 1;
            }
        }
    }
}