using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
namespace Reinforcement.Aspirant
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autodesk.Revit.DB;

    public static class XYZListExtensions
    {
        private const double FtToMm = 304.8;
        /// <summary>
        /// Переводит точку из футов (внутренние единицы Revit) в миллиметры.
        /// Возвращает новый XYZ; исходный не изменяется (XYZ неизменяемый).
        /// Если передан null — возвращает null.
        /// </summary>
        public static XYZ ToMm(this XYZ pointFt)
        {
            if (pointFt == null) return null;
            return new XYZ(
                pointFt.X * FtToMm,
                pointFt.Y * FtToMm,
                pointFt.Z * FtToMm);
        }

        // ─────────────────────────────────────────────────────────────
        // 2. Одна точка: мм → футы
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Переводит точку из миллиметров в футы (внутренние единицы Revit).
        /// Возвращает новый XYZ; исходный не изменяется.
        /// Если передан null — возвращает null.
        /// </summary>
        public static XYZ ToFoot(this XYZ pointMm)
        {
            if (pointMm == null) return null;
            return new XYZ(
                pointMm.X / FtToMm,
                pointMm.Y / FtToMm,
                pointMm.Z / FtToMm);
        }

        // ─────────────────────────────────────────────────────────────
        // 3. (Опционально) Точечные скаляры — если нужно по одной координате
        // ─────────────────────────────────────────────────────────────

        /// <summary>Футы → мм для одного числа (длина).</summary>
        public static double ToMm(this double valueFt) => valueFt * FtToMm;

        /// <summary>Мм → футы для одного числа (длина).</summary>
        public static double ToFoot(this double valueMm) => valueMm / FtToMm;
        // ─────────────────────────────────────────────────────────────
        // 1. Перевод списка точек: футы → мм
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает новый список точек в миллиметрах (вход — в футах).
        /// Исходный список не изменяется.
        /// </summary>
        public static List<XYZ> ToMm(this List<XYZ> pointsFt)
        {
            if (pointsFt == null) return null;
            var result = new List<XYZ>(pointsFt.Count);
            foreach (var p in pointsFt)
            {
                if (p == null) { result.Add(null); continue; }
                result.Add(new XYZ(p.X * FtToMm, p.Y * FtToMm, p.Z * FtToMm));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // 2. Перевод списка точек: мм → футы
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает новый список точек в футах (вход — в миллиметрах).
        /// Исходный список не изменяется.
        /// </summary>
        public static List<XYZ> ToFoot(this List<XYZ> pointsMm)
        {
            if (pointsMm == null) return null;
            var result = new List<XYZ>(pointsMm.Count);
            foreach (var p in pointsMm)
            {
                if (p == null) { result.Add(null); continue; }
                result.Add(new XYZ(p.X / FtToMm, p.Y / FtToMm, p.Z / FtToMm));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // 3. Периметр (длина замкнутого контура)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Длина замкнутого контура: сумма расстояний между соседними точками
        /// плюс расстояние от последней к первой.
        /// Возвращает значение в тех же единицах, что и координаты точек.
        /// </summary>
        public static double GetPerimeter(this List<XYZ> points, bool closed = true)
        {
            if (points == null || points.Count < 2) return 0.0;

            double sum = 0.0;
            for (int i = 1; i < points.Count; i++)
                sum += points[i - 1].DistanceTo(points[i]);

            if (closed)
                sum += points[points.Count - 1].DistanceTo(points[0]);

            return sum;
        }

        // ─────────────────────────────────────────────────────────────
        // 4. Площадь плоского многоугольника (3D, метод Ньюэлла)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Площадь плоского многоугольника в 3D. Контур может быть наклонным —
        /// метод Ньюэлла корректно считает площадь проекции на нормаль.
        /// Возвращает значение в квадратных единицах координат (мм² или фут²).
        /// </summary>
        public static double GetArea(this List<XYZ> points)
        {
            if (points == null || points.Count < 3) return 0.0;

            // Метод Ньюэлла: сумма векторных произведений
            XYZ normal = XYZ.Zero;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                XYZ current = points[i];
                XYZ next = points[(i + 1) % n];
                normal += new XYZ(
                    (current.Y - next.Y) * (current.Z + next.Z),
                    (current.Z - next.Z) * (current.X + next.X),
                    (current.X - next.X) * (current.Y + next.Y));
            }

            // Площадь = половина длины вектора нормали
            return normal.GetLength() / 2.0;
        }

        // ─────────────────────────────────────────────────────────────
        // 5. Площадь в мм² (если точки заданы в футах)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Площадь многоугольника с точками в футах, результат — в мм².
        /// </summary>
        public static double GetAreaMm2(this List<XYZ> pointsFt)
        {
            const double Ft2ToMm2 = FtToMm * FtToMm;
            return pointsFt.GetArea() * Ft2ToMm2;
        }

        /// <summary>
        /// Периметр контура с точками в футах, результат — в мм.
        /// </summary>
        public static double GetPerimeterMm(this List<XYZ> pointsFt)
        {
            return pointsFt.GetPerimeter() * FtToMm;
        }



        /// <summary>
        /// Находит в A точку, максимально удалённую от множества B
        /// по критерию «минимум расстояний до B» (maximin).
        /// Это точка A, наиболее изолированная от B — её ближайший сосед в B
        /// находится дальше, чем у любой другой точки A.
        /// Возвращает null, если один из списков пуст.
        /// </summary>
        /// <param name="bestDistance">Минимальное расстояние до B для найденной точки.
        /// -1, если точку найти не удалось.</param>
        public static XYZ FindFarthestFromSet(
            this List<XYZ> pointsA,
            List<XYZ> pointsB,
            out double bestDistance)
        {
            bestDistance = -1.0;
            if (pointsA == null || pointsA.Count == 0) return null;
            if (pointsB == null || pointsB.Count == 0) return null;

            XYZ best = null;
            foreach (var a in pointsA)
            {
                if (a == null) continue;

                double minDist = double.MaxValue;
                bool any = false;
                foreach (var b in pointsB)
                {
                    if (b == null) continue;
                    any = true;
                    double d = a.DistanceTo(b);
                    if (d < minDist) minDist = d;
                }
                if (!any) continue;

                if (minDist > bestDistance)
                {
                    bestDistance = minDist;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Вариант «по сумме расстояний»: находит в A точку, у которой
        /// суммарное расстояние до всех точек B максимально.
        /// Полезно, когда важна не близость к ближайшему соседу, а общая удалённость.
        /// </summary>
        public static XYZ FindFarthestFromSetBySum(
            this List<XYZ> pointsA,
            List<XYZ> pointsB,
            out double bestSum)
        {
            bestSum = -1.0;
            if (pointsA == null || pointsA.Count == 0) return null;
            if (pointsB == null || pointsB.Count == 0) return null;

            XYZ best = null;
            foreach (var a in pointsA)
            {
                if (a == null) continue;

                double sum = 0.0;
                bool any = false;
                foreach (var b in pointsB)
                {
                    if (b == null) continue;
                    any = true;
                    sum += a.DistanceTo(b);
                }
                if (!any) continue;

                if (sum > bestSum)
                {
                    bestSum = sum;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Вариант «по среднему расстоянию» — нормирует сумму на количество точек B,
        /// чтобы результат не зависел от размера B.
        /// </summary>
        public static XYZ FindFarthestFromSetByAverage(
            this List<XYZ> pointsA,
            List<XYZ> pointsB,
            out double bestAverage)
        {
            bestAverage = -1.0;
            if (pointsA == null || pointsA.Count == 0) return null;
            if (pointsB == null || pointsB.Count == 0) return null;

            XYZ best = null;
            foreach (var a in pointsA)
            {
                if (a == null) continue;

                double sum = 0.0;
                int cnt = 0;
                foreach (var b in pointsB)
                {
                    if (b == null) continue;
                    sum += a.DistanceTo(b);
                    cnt++;
                }
                if (cnt == 0) continue;

                double avg = sum / cnt;
                if (avg > bestAverage)
                {
                    bestAverage = avg;
                    best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Удаляет из списка точки, совпадающие с уже добавленными в пределах допуска.
        /// Возвращает новый список; исходный не изменяется.
        /// Порядок оставшихся точек сохраняется.
        /// </summary>
        /// <param name="points">Исходный список.</param>
        /// <param name="tol">Допуск совпадения (в единицах координат точек, например мм).</param>
        public static List<XYZ> Dedupe(this List<XYZ> points, double tol = 0.1)
        {
            if (points == null) return null;
            if (points.Count == 0) return new List<XYZ>();
            if (tol < 0) tol = 0;

            var result = new List<XYZ>(points.Count);
            double tol2 = tol * tol;

            foreach (var p in points)
            {
                if (p == null) continue;

                bool duplicate = false;
                foreach (var q in result)
                {
                    XYZ d = p - q;
                    if (d.DotProduct(d) <= tol2) // квадрат расстояния ≤ tol²
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) result.Add(p);
            }
            return result;
        }

        /// <summary>
        /// Вариант «in-place»: модифицирует переданный список, удаляя дубликаты.
        /// Полезно, чтобы не плодить копии для больших контуров.
        /// </summary>
        public static void DedupeInPlace(this List<XYZ> points, double tol = 0.1)
        {
            if (points == null || points.Count < 2) return;
            if (tol < 0) tol = 0;

            double tol2 = tol * tol;
            var kept = new List<XYZ>(points.Count);

            foreach (var p in points)
            {
                if (p == null) continue;

                bool duplicate = false;
                foreach (var q in kept)
                {
                    XYZ d = p - q;
                    if (d.DotProduct(d) <= tol2) { duplicate = true; break; }
                }
                if (!duplicate) kept.Add(p);
            }

            points.Clear();
            points.AddRange(kept);
        }
    }
}
