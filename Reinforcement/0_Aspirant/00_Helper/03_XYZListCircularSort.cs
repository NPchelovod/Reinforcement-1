using Autodesk.Revit.DB;
using Reinforcement.Aspirant;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Reinforcement.Aspirant
{
    public static class XYZListCircularSort
    {
        private const double TolMm = 0.1;

        /// <summary>
        /// Сортирует точки по кругу вокруг центра (по умолчанию — площадной центроид).
        /// Результат — новый список; исходный не изменяется.
        /// Для выпуклых контуров возвращает порядок обхода («цепочку»).
        /// </summary>
        /// <param name="points">Точки для сортировки (в одной системе единиц).</param>
        /// <param name="center">Центр сортировки. Если null — площадной центроид.</param>
        /// <param name="clockwise">true — по часовой (если смотреть против нормали),
        /// false — против часовой (по умолчанию).</param>
        /// <param name="startPoint">Точка, с которой начать обход (например, чтобы
        /// порядок был детерминированным). Если null — начнём с минимального угла.</param>
        /// <param name="tol">Допуск дедупликации на входе. Если ≤ 0 — не чистим.</param>
        public static List<XYZ> SortCircular(
            this List<XYZ> points,
            XYZ center = null,
            bool clockwise = false,
            XYZ startPoint = null,
            double tol = TolMm)
        {
            if (points == null) return null;
            if (points.Count < 2) return new List<XYZ>(points);

            // 1. Чистка от дублей (опционально)
            List<XYZ> pts = tol > 0 ? points.Dedupe(tol) : new List<XYZ>(points);
            if (pts.Count < 2) return pts;

            // 2. Центр
            XYZ c = center ?? PlateSupportSelector.PolygonCentroid(pts);

            // 3. Нормаль по Ньюэллу
            XYZ n = NewellNormal(pts);
            if (n.GetLength() < 1e-12)
            {
                // Точки коллинеарны или совпадают — сортировка по расстоянию от центра
                return pts.OrderBy(p => p.DistanceTo(c)).ToList();
            }
            n = n.Normalize();

            // 4. Локальные оси в плоскости
            XYZ u = Math.Abs(n.X) < 0.9 ? XYZ.BasisX : XYZ.BasisY;
            u = (u - n * u.DotProduct(n)).Normalize();
            XYZ v = n.CrossProduct(u); // (u, v, n) — правая тройка

            // 5. Углы
            var withAngles = new List<(XYZ pt, double angle)>(pts.Count);
            foreach (var p in pts)
            {
                XYZ d = p - c;
                double angle = Math.Atan2(d.DotProduct(v), d.DotProduct(u));
                withAngles.Add((p, angle));
            }

            // 6. Направление обхода
            var sorted = clockwise
                ? withAngles.OrderByDescending(t => t.angle).ToList()
                : withAngles.OrderBy(t => t.angle).ToList();

            var result = sorted.Select(t => t.pt).ToList();

            // 7. Поворот к стартовой точке
            if (startPoint != null && result.Count > 1)
                result = RotateToStart(result, startPoint, tol);

            return result;
        }

        /// <summary>
        /// Нормаль по методу Ньюэлла для плоского многоугольника.
        /// Возвращает вектор, пропорциональный площади (не нормированный).
        /// Для коллинеарных точек возвращает XYZ.Zero.
        /// </summary>
        private static XYZ NewellNormal(List<XYZ> pts)
        {
            XYZ n = XYZ.Zero;
            int m = pts.Count;
            for (int i = 0; i < m; i++)
            {
                XYZ a = pts[i], b = pts[(i + 1) % m];
                n += new XYZ(
                    (a.Y - b.Y) * (a.Z + b.Z),
                    (a.Z - b.Z) * (a.X + b.X),
                    (a.X - b.X) * (a.Y + b.Y));
            }
            return n;
        }

        /// <summary>
        /// Циклически сдвигает список так, чтобы он начинался с точки,
        /// ближайшей к startPoint (в пределах tol). Если такой нет — возвращает
        /// исходный порядок.
        /// </summary>
        private static List<XYZ> RotateToStart(List<XYZ> pts, XYZ startPoint, double tol)
        {
            int bestIdx = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                double d = pts[i].DistanceTo(startPoint);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            if (bestIdx <= 0 || bestDist > tol * 10) return pts; // -1 или уже первый

            var rotated = new List<XYZ>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                rotated.Add(pts[(bestIdx + i) % pts.Count]);
            return rotated;
        }
    }
}