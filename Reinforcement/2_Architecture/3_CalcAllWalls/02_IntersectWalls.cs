using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public static class IntersectWalls
    {
        const double ToleranceMm = 10.0; // допуск "касания"
        public static void CalcIntersects(Document doc, List<ECWall> eCWalls)
        {
            // Группируем по отметке уровня
            var groups = eCWalls
                .Where(x => x != null && x.Curve != null && x.ECLevel != null)
                .GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var wallsOnLevel = group.ToList();

                // безопасный словарь — на случай дубликатов ElementId
                var wallIdsOnLevel = wallsOnLevel
                    .GroupBy(x => x.ElementId)
                    .ToDictionary(g => g.Key, g => g.First());
                //смотрим пересечения только с нашими стенами

                // дальше работаешь со стенами одного уровня

                // --- 1) Соединения через JoinGeometryUtils ---
                foreach (var wall in wallsOnLevel)
                {
                    ICollection<ElementId> joinedIds = JoinGeometryUtils.GetJoinedElements(doc, wall.Element);
                    foreach (ElementId id in joinedIds)
                    {
                        if (!wallIdsOnLevel.TryGetValue(id, out var otherWall)) continue;
                        wall.RelationsWalls.Add(otherWall);
                        otherWall.RelationsWalls.Add(wall);
                    }
                }
                //не всегда стены соединены, так что надо еще вручную находить пересечения
                // --- 2) Геометрический поиск касаний (с учётом толщины) ---
                for (int i = 0; i < wallsOnLevel.Count; i++)
                {
                    var w1 = wallsOnLevel[i];
                    for (int j = i + 1; j < wallsOnLevel.Count; j++)
                    {
                        var w2 = wallsOnLevel[j];

                        // уже связаны через JoinGeometryUtils — пропускаем
                        if (w1.RelationsWalls.Contains(w2)) continue;

                        if (AreWallsTouching(w1, w2, ToleranceMm))
                        {
                            w1.RelationsWalls.Add(w2);
                            w2.RelationsWalls.Add(w1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает точку пересечения двух стен.
        /// 1) Если отрезки Curve пересекаются — их точку пересечения (для коллинеарных — середину перекрытия).
        /// 2) Иначе — точку пересечения бесконечных лучей (прямых) этих отрезков.
        /// null — если прямые параллельны или скрещиваются в 3D.
        /// </summary>
        /// <summary>
        /// Возвращает точку пересечения двух стен.
        /// 1) Если отрезки Curve пересекаются — их точку пересечения (для коллинеарных — середину перекрытия).
        /// 2) Иначе — точку пересечения бесконечных лучей (прямых) этих отрезков.
        /// null — если прямые параллельны или скрещиваются в 3D.
        /// </summary>
        public static XYZ GetWallsIntersectionPoint(ECWall wall1, ECWall wall2)
        {
            if (wall1?.Curve == null || wall2?.Curve == null) return null;

            Curve c1 = wall1.Curve;
            Curve c2 = wall2.Curve;

            // --- 1) Реальное пересечение или перекрытие отрезков ---
            IntersectionResultArray results;
            SetComparisonResult comparison = c1.Intersect(c2, out results);

            if (comparison == SetComparisonResult.Overlap && results != null && results.Size > 0)
            {
                // Если пересечение в одной точке
                if (results.Size == 1)
                {
                    return results.get_Item(0).XYZPoint;
                }

                // Если коллинеарное перекрытие (две точки и более) — берем середину
                XYZ startPoint = results.get_Item(0).XYZPoint;
                XYZ endPoint = results.get_Item(results.Size - 1).XYZPoint;
                return (startPoint + endPoint) * 0.5;
            }

            // --- 2) Пересечение бесконечных лучей (прямых) ---
            // Сюда попадаем, если отрезки не пересекаются (Disjoint) или параллельны (Subset/Superset)
            return IntersectRays(
                c1.GetEndPoint(0), c1.GetEndPoint(1),
                c2.GetEndPoint(0), c2.GetEndPoint(1));
        }

        /// <summary>
        /// Проверяет, что хотя бы один торец стены wall1 примыкает к стене wall2.
        /// «Примыкает» = расстояние от точки торца до ОСИ wall2 (в плане) 
        /// не превышает halfWidth(wall2) + tolerance.
        /// </summary>
        public static bool IsEndOfWall1TouchingWall2(
            ECWall wall1, ECWall wall2, out XYZ closestPointOnWall1, double toleranceMm = 10)
        {
            closestPointOnWall1 = null;
            if (wall1?.Curve == null || wall2?.Curve == null) return false;
            if (ReferenceEquals(wall1, wall2)) return false;
            if (wall1.ElementId == wall2.ElementId) return false;

            XYZ a1 = wall1.Curve.GetEndPoint(0);
            XYZ b1 = wall1.Curve.GetEndPoint(1);

            double halfWidthFt2 = RevitAPI.ToFoot(wall2.WidthM) / 2.0;
            double tolFt = RevitAPI.ToFoot(toleranceMm) / 1000.0;
            // если ToFoot(мм)→футы принимает мм напрямую — замените на RevitAPI.ToFoot(toleranceMm)

            double threshold = halfWidthFt2 + tolFt;

            double dA = PointToCurveDistanceXY(a1, wall2.Curve);
            double dB = PointToCurveDistanceXY(b1, wall2.Curve);

            // Берём тот торец, который ближе к wall2
            if (dA <= dB)
            {
                if (dA <= threshold)
                {
                    closestPointOnWall1 = a1;
                    return true;
                }
            }
            else
            {
                if (dB <= threshold)
                {
                    closestPointOnWall1 = b1;
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Расстояние от точки до кривой в плоскости XY (Z игнорируется).
        /// Для Line — точная формула; для остальных — сэмплирование.
        /// </summary>
        private static double PointToCurveDistanceXY(XYZ pt, Curve curve, int samples = 64)
        {
            if (curve is Line line)
                return PointToSegmentDistanceXY(pt, line.GetEndPoint(0), line.GetEndPoint(1));

            // Arc, Nurbs, CylindricalHelix — сэмплируем
            double minDist = double.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                XYZ c = curve.Evaluate((double)i / samples, true);
                double dx = pt.X - c.X;
                double dy = pt.Y - c.Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }
        /// <summary>
        /// Классическое расстояние точка→отрезок в плоскости XY.
        /// </summary>
        private static double PointToSegmentDistanceXY(XYZ pt, XYZ a, XYZ b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;

            if (len2 < 1e-12) // отрезок вырожденный
            {
                double ddx0 = pt.X - a.X, ddy0 = pt.Y - a.Y;
                return Math.Sqrt(ddx0 * ddx0 + ddy0 * ddy0);
            }

            // Параметр проекции, клампим к [0,1]
            double t = ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / len2;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            double projX = a.X + t * dx;
            double projY = a.Y + t * dy;
            double ddx = pt.X - projX;
            double ddy = pt.Y - projY;
            return Math.Sqrt(ddx * ddx + ddy * ddy);
        }
        /// <summary>
        /// Пересечение двух бесконечных прямых, заданных отрезками (p1,q1) и (p2,q2).
        /// Возвращает null, если прямые параллельны или скрещиваются.
        /// </summary>
        private static XYZ IntersectRays(XYZ p1, XYZ q1, XYZ p2, XYZ q2)
        {
            const double EPS_PARALLEL = 1e-9;
            const double EPS_SKEW_FT = 1e-3; // 0.3 мм — прямые в одной плоскости

            XYZ d1 = q1 - p1;
            XYZ d2 = q2 - p2;

            // Нулевые отрезки — вырожденный случай
            if (d1.GetLength() < EPS_PARALLEL || d2.GetLength() < EPS_PARALLEL)
                return null;

            // Нормализуем — повышает численную устойчивость
            d1 = d1.Normalize();
            d2 = d2.Normalize();

            XYZ r = p2 - p1;

            double a = d1.DotProduct(d1); // = 1
            double b = d1.DotProduct(d2);
            double c = d2.DotProduct(d2); // = 1
            double d = d1.DotProduct(r);
            double e = d2.DotProduct(r);

            double denom = a * c - b * b;
            if (Math.Abs(denom) < EPS_PARALLEL)
                return null; // прямые параллельны

            // Параметры ближайших точек на двух прямых
            double s = (b * e - c * d) / denom;
            double t = (a * e - b * d) / denom;

            XYZ ptOnL1 = p1 + d1 * s;
            XYZ ptOnL2 = p2 + d2 * t;

            // Прямые скрещиваются (разные высоты, не пересекаются в 3D)
            if (ptOnL1.DistanceTo(ptOnL2) > EPS_SKEW_FT)
                return null;

            // Точки практически совпали — возвращаем середину
            return (ptOnL1 + ptOnL2) * 0.5;
        }
        //Правило: две стены «касаются», если distance(centerline1, centerline2)  ≤  (thickness1 + thickness2)/2 + tolerance
        /// <summary>
        /// Проверяет, касаются ли стены с учётом их толщин.
        /// </summary>
        private static bool AreWallsTouching(ECWall w1, ECWall w2, double toleranceMm)
        {
            if (w1?.Curve == null || w2?.Curve == null) return false;

            // Порог: сумма полутолщин + допуск
            double maxDistM = (w1.WidthM + w2.WidthM) / 2.0 + toleranceMm / 1000.0;

            // Быстрая отбраковка по bounding box — расширяем каждый на maxDist
            if (!BoundingBoxesOverlapWithTolerance(w1, w2, maxDistM)) return false;

            // Точная проверка: расстояние между кривыми
            double distFt = MinDistanceBetweenCurves(w1.Curve, w2.Curve);
            double distM = UnitUtils.ConvertFromInternalUnits(distFt, UnitTypeId.Meters);

            return distM <= maxDistM;
        }

        /// <summary>
        /// Быстрая грубая отбраковка: AABB стен + расширение допуском.
        /// Для диагональных стен даёт ложные срабатывания, но они отсекаются точной проверкой.
        /// </summary>
        private static bool BoundingBoxesOverlapWithTolerance(ECWall w1, ECWall w2, double tolM)
        {
            var bb1 = w1.Element?.get_BoundingBox(null);
            var bb2 = w2.Element?.get_BoundingBox(null);
            if (bb1 == null || bb2 == null) return true; // не рискуем отбраковать

            double t = UnitUtils.ConvertToInternalUnits(tolM, UnitTypeId.Meters);

            return bb1.Min.X - t <= bb2.Max.X && bb1.Max.X + t >= bb2.Min.X &&
                   bb1.Min.Y - t <= bb2.Max.Y && bb1.Max.Y + t >= bb2.Min.Y &&
                   bb1.Min.Z - t <= bb2.Max.Z && bb1.Max.Z + t >= bb2.Min.Z;
        }

        /// <summary>
        /// Минимальное расстояние между двумя кривыми.
        /// Для пары линий — точный алгоритм "segment-segment distance".
        /// Для дуг/сложных — сэмплирование + Curve.Project.
        /// </summary>
        private static double MinDistanceBetweenCurves(Curve c1, Curve c2)
        {
            if (c1 is Line l1 && c2 is Line l2)
            {
                return SegmentSegmentDistance(
                    l1.GetEndPoint(0), l1.GetEndPoint(1),
                    l2.GetEndPoint(0), l2.GetEndPoint(1));
            }

            // Общий случай: сэмплируем c1 и проецируем точки на c2
            const int samples = 32;
            double minDist = double.MaxValue;

            for (int i = 0; i <= samples; i++)
            {
                double t = (double)i / samples;
                XYZ p = c1.Evaluate(t, true);

                IntersectionResult proj = c2.Project(p);
                double d;
                if (proj != null)
                {
                    d = proj.Distance;
                }
                else
                {
                    // Проекция вне границ c2 — берём ближайший конец
                    d = Math.Min(p.DistanceTo(c2.GetEndPoint(0)),
                                 p.DistanceTo(c2.GetEndPoint(1)));
                }
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>
        /// Минимальное расстояние между двумя 3D-отрезками.
        /// Классический алгоритм из Ericson, "Real-Time Collision Detection".
        /// </summary>
        private static double SegmentSegmentDistance(XYZ p1, XYZ q1, XYZ p2, XYZ q2)
        {
            const double EPS = 1e-9;

            XYZ d1 = q1 - p1;
            XYZ d2 = q2 - p2;
            XYZ r = p1 - p2;

            double a = d1.DotProduct(d1);
            double e = d2.DotProduct(d2);
            double f = d2.DotProduct(r);

            double s, t;

            if (a <= EPS && e <= EPS)
                return p1.DistanceTo(p2);     // обе точки

            if (a <= EPS)
            {
                s = 0;
                t = Clamp(f / e, 0, 1);
            }
            else
            {
                double c = d1.DotProduct(r);
                if (e <= EPS)
                {
                    t = 0;
                    s = Clamp(-c / a, 0, 1);
                }
                else
                {
                    double b = d1.DotProduct(d2);
                    double denom = a * e - b * b;

                    s = Math.Abs(denom) > EPS
                        ? Clamp((b * f - c * e) / denom, 0, 1)
                        : 0;

                    t = (b * s + f) / e;

                    if (t < 0) { t = 0; s = Clamp(-c / a, 0, 1); }
                    else if (t > 1) { t = 1; s = Clamp((b - c) / a, 0, 1); }
                }
            }

            XYZ closest1 = p1 + d1 * s;
            XYZ closest2 = p2 + d2 * t;
            return closest1.DistanceTo(closest2);
        }

        private static double Clamp(double v, double lo, double hi)
            => v < lo ? lo : (v > hi ? hi : v);

    }
}
