using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    internal static class GetColumnIntersectsFloor
    {
        /// <summary>
        /// Проверяет, пересекает ли колонна плиту, и возвращает точку пересечения оси
        /// колонны с плоскостью плиты (в мм).
        /// </summary>
        /// <param name="intersectionMm">
        /// Точка пересечения в мм, если метод вернул true; иначе XYZ.Zero.
        /// Точка лежит на оси колонны и в плоскости плиты, внутри её внешнего контура
        /// и вне отверстий.
        /// </param>
        public static bool ColumnIntersectsFloor(
            ColumnContourData column,
            FloorContourData floor,
            out XYZ intersectionMm,
            double tolMm = 0.5)
        {
            intersectionMm = XYZ.Zero;

            if (column == null || floor == null) return false;
            if (floor.OuterContourMm == null || floor.OuterContourMm.Count < 3) return false;

            // 1. Плоскость плиты по контуру
            if (!TryFitPlane(floor.OuterContourMm, out Plane plane, tolMm))
                return false;

            // 2. Пересечение отрезка [StartMm, EndMm] с плоскостью
            XYZ n = plane.Normal;
            double dStart = (column.StartMm - plane.Origin).DotProduct(n);
            double dEnd = (column.EndMm - plane.Origin).DotProduct(n);

            // Отрезок целиком по одну сторону плоскости — пересечения нет
            if (dStart * dEnd > tolMm * tolMm)
                return false;

            XYZ intersection;
            if (Math.Abs(dStart - dEnd) < 1e-9)
            {
                // Колонна параллельна плоскости — считаем только если лежит на ней
                if (Math.Abs(dStart) > tolMm) return false;
                intersection = new XYZ(
                    (column.StartMm.X + column.EndMm.X) / 2.0,
                    (column.StartMm.Y + column.EndMm.Y) / 2.0,
                    (column.StartMm.Z + column.EndMm.Z) / 2.0);
            }
            else
            {
                double t = dStart / (dStart - dEnd);
                intersection = column.StartMm + (column.EndMm - column.StartMm) * t;
            }

            // 3. Точка должна быть внутри внешнего контура…
            if (!IsPointInPolygon2D(intersection, floor.OuterContourMm, plane, tolMm))
                return false;

            // 4. …и не попадать ни в одно отверстие
            if (floor.InnerLoopsMm != null)
            {
                foreach (var hole in floor.InnerLoopsMm)
                {
                    if (hole != null && hole.Count >= 3 &&
                        IsPointInPolygon2D(intersection, hole, plane, tolMm))
                        return false;
                }
            }

            intersectionMm = intersection;
            return true;
        }
        public static bool ColumnIntersectsFloor(
    ColumnContourData column,
    FloorContourData floor,
    double tolMm = 0.5)
        {
            if (column == null || floor == null) return false;
            if (floor.OuterContourMm == null || floor.OuterContourMm.Count < 3) return false;

            // 1. Плоскость плиты по контуру
            if (!TryFitPlane(floor.OuterContourMm, out Plane plane, tolMm))
                return false;

            // 2. Пересечение отрезка [StartMm, EndMm] с плоскостью
            XYZ n = plane.Normal;
            double dStart = (column.StartMm - plane.Origin).DotProduct(n);
            double dEnd = (column.EndMm - plane.Origin).DotProduct(n);

            // Отрезок целиком по одну сторону плоскости — пересечения нет
            if (dStart * dEnd > tolMm * tolMm)
                return false;

            XYZ intersection;
            if (Math.Abs(dStart - dEnd) < 1e-9)
            {
                // Колонна параллельна плоскости — считаем только если лежит на ней
                if (Math.Abs(dStart) > tolMm) return false;
                intersection = new XYZ(
                    (column.StartMm.X + column.EndMm.X) / 2.0,
                    (column.StartMm.Y + column.EndMm.Y) / 2.0,
                    (column.StartMm.Z + column.EndMm.Z) / 2.0);
            }
            else
            {
                double t = dStart / (dStart - dEnd);
                intersection = column.StartMm + (column.EndMm - column.StartMm) * t;
            }

            // 3. Точка должна быть внутри внешнего контура…
            if (!IsPointInPolygon2D(intersection, floor.OuterContourMm, plane, tolMm))
                return false;

            // 4. …и не попадать ни в одно отверстие
            if (floor.InnerLoopsMm != null)
            {
                foreach (var hole in floor.InnerLoopsMm)
                {
                    if (hole != null && hole.Count >= 3 &&
                        IsPointInPolygon2D(intersection, hole, plane, tolMm))
                        return false;
                }
            }

            return true;
        }
        private static bool TryFitPlane(List<XYZ> points, out Plane plane, double tol)
        {
            plane = null;
            XYZ p0 = points[0];

            int i1 = -1;
            for (int i = 1; i < points.Count; i++)
                if (points[i].DistanceTo(p0) > tol) { i1 = i; break; }
            if (i1 < 0) return false;

            int i2 = -1;
            for (int i = i1 + 1; i < points.Count; i++)
            {
                XYZ v1 = points[i1] - p0;
                XYZ v2 = points[i] - p0;
                if (v1.CrossProduct(v2).GetLength() > tol) { i2 = i; break; }
            }
            if (i2 < 0) return false;

            plane = Plane.CreateByThreePoints(p0, points[i1], points[i2]);
            return true;
        }

        private static bool IsPointInPolygon2D(XYZ pt3D, List<XYZ> poly3D, Plane plane, double tol)
        {
            XYZ u = plane.XVec;
            XYZ v = plane.YVec;
            XYZ o = plane.Origin;

            Func<XYZ, UV> to2D = p =>
            {
                XYZ d = p - o;
                return new UV(d.DotProduct(u), d.DotProduct(v));
            };

            UV pt = to2D(pt3D);
            var poly = poly3D.Select(to2D).ToList();

            // Ray casting
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                UV a = poly[i], b = poly[j];
                if ((a.V > pt.V) != (b.V > pt.V))
                {
                    double xCross = a.U + (pt.V - a.V) * (b.U - a.U) / (b.V - a.V);
                    if (pt.U < xCross)
                        inside = !inside;
                }
            }
            if (inside) return true;

            // Точка на границе (в пределах допуска) — считаем пересечением
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (DistancePointToSegment2D(pt, poly[j], poly[i]) <= tol)
                    return true;

            return false;
        }

        private static double DistancePointToSegment2D(UV p, UV a, UV b)
        {
            double abU = b.U - a.U, abV = b.V - a.V;
            double len2 = abU * abU + abV * abV;
            double t = len2 < 1e-18 ? 0.0
                : ((p.U - a.U) * abU + (p.V - a.V) * abV) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            double cu = a.U + abU * t, cv = a.V + abV * t;
            double du = p.U - cu, dv = p.V - cv;
            return Math.Sqrt(du * du + dv * dv);
        }
    }
}
