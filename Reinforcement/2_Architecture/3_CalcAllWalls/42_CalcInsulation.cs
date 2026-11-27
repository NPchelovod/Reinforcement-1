using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    public static class ECWallPerimeterChainBuilder
    {
        /// <summary>
        /// Builds the closed cooling boundary for the supplied level and writes metres to
        /// ECInsulation.LengthInsideM. The active view is filtered by the caller.
        /// </summary>
        public static List<ECWall> FillLengthInsideM(List<ECWall> walls, Document doc)
        {
            if (walls == null || walls.Count == 0) return new List<ECWall>();
            CoolingContourGeometry.Result geometry = Calculate(walls);
            if (geometry.LoopCount == 0)
                throw new InvalidOperationException(
                    "Не найден замкнутый контур стен на уровне " +
                    (walls[0].ECLevel == null ? "?" : walls[0].ECLevel.LevelElevation.ToString()) +
                    ". Проверьте видимость и стыки стен на активном виде.");

            for (int i = 0; i < walls.Count; i++)
            {
                ECWall wall = walls[i];
                double lengthFeet;
                geometry.Lengths.TryGetValue(i, out lengthFeet);
                wall.ECInsulation.LengthInsideM =
                    UnitUtils.ConvertFromInternalUnits(lengthFeet, UnitTypeId.Meters);
                wall.ECInsulation.Number = 0;
                wall.ECInsulation._exportElements = null;
            }
            for (int i = 0; i < geometry.WallOrder.Count; i++)
                walls[geometry.WallOrder[i]].ECInsulation.Number = i + 1;
            return InContourOrder(walls, geometry.WallOrder);
        }

        public static List<ECWall> SortClockwise(List<ECWall> walls)
        {
            if (walls == null || walls.Count == 0) return new List<ECWall>();
            return InContourOrder(walls, Calculate(walls).WallOrder);
        }

        public static List<(XYZ Start, XYZ End)> BuildInnerContour(List<ECWall> walls)
        {
            var answer = new List<(XYZ Start, XYZ End)>();
            if (walls == null || walls.Count == 0) return answer;
            CoolingContourGeometry.Result geometry = Calculate(walls);
            Curve first = GetCurve(walls.FirstOrDefault(w => GetCurve(w) != null));
            double z = first == null ? 0 : first.GetEndPoint(0).Z;
            foreach (CoolingContourGeometry.ContourPart part in geometry.Contour)
                answer.Add((new XYZ(part.Start.X, part.Start.Y, z),
                            new XYZ(part.End.X, part.End.Y, z)));
            return answer;
        }

        private static List<ECWall> InContourOrder(List<ECWall> walls, List<int> indices)
        {
            var answer = new List<ECWall>(walls.Count);
            var used = new HashSet<int>();
            foreach (int index in indices)
                if (index >= 0 && index < walls.Count && used.Add(index))
                    answer.Add(walls[index]);
            for (int i = 0; i < walls.Count; i++)
                if (used.Add(i)) answer.Add(walls[i]);
            return answer;
        }

        private static CoolingContourGeometry.Result Calculate(List<ECWall> walls)
        {
            var axes = new List<CoolingContourGeometry.WallAxis>();
            for (int i = 0; i < walls.Count; i++)
            {
                ECWall wall = walls[i];
                Curve curve = GetCurve(wall);
                if (curve == null) continue;
                double width = GetWidthInternal(wall);
                if (width <= 0) continue;

                IList<XYZ> points = curve.Tessellate();
                if (points == null || points.Count < 2) continue;
                for (int j = 1; j < points.Count; j++)
                {
                    XYZ a = points[j - 1], b = points[j];
                    if (a.DistanceTo(b) < 1e-6) continue;
                    axes.Add(new CoolingContourGeometry.WallAxis(i,
                        new CoolingContourGeometry.Point(a.X, a.Y),
                        new CoolingContourGeometry.Point(b.X, b.Y), width));
                }
            }
            return CoolingContourGeometry.Calculate(axes);
        }

        private static Curve GetCurve(ECWall wall)
        {
            if (wall == null) return null;
            if (wall.Curve != null) return wall.Curve;
            Wall element = wall.Wall ?? wall.Element as Wall;
            return (element == null ? null : element.Location as LocationCurve)?.Curve;
        }

        private static double GetWidthInternal(ECWall wall)
        {
            Wall element = wall.Wall ?? wall.Element as Wall;
            if (element != null) return element.Width;
            return UnitUtils.ConvertToInternalUnits(wall.WidthM, UnitTypeId.Meters);
        }
    }
}