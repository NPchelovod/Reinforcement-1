using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    public static class ECWallPerimeterChainBuilder
    {
        public static List<ECWall> FillLengthInsideM(List<ECWall> walls, Document doc)
        {
            return FillLengthInsideM(walls, doc, null);
        }

        internal static List<ECWall> FillLengthInsideM(List<ECWall> walls, Document doc,
            IList<string> diagnostics)
        {
            if (walls == null || walls.Count == 0) return new List<ECWall>();
            var geometry = Calculate(walls, doc);
            string level = walls[0].ECLevel?.Name ?? "?";
            if (diagnostics != null)
            {
                foreach (string detail in geometry.Diagnostics) diagnostics.Add(level + " — " + detail);
                if (geometry.UnmatchedFacadeWalls.Count > 0)
                    diagnostics.Add(level + " — проверьте ID " + string.Join(", ",
                        geometry.UnmatchedFacadeWalls.Select(i => walls[i].Id)));
            }
            if (geometry.LoopCount == 0)
                throw new InvalidOperationException("Не удалось восстановить границу этажа «" + level +
                    "». " + string.Join(" ", geometry.Diagnostics));

            for (int i = 0; i < walls.Count; i++)
            {
                var info = walls[i].ECInsulation;
                double feet;
                geometry.Lengths.TryGetValue(i, out feet);
                info.LengthInsideM = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
                info.Number = 0;
                info._exportElements = null;
            }
            var participating = InContourOrder(walls, geometry.WallOrder)
                .Where(w => w.ECInsulation.LengthInsideM > 1e-6).ToList();
            for (int i = 0; i < participating.Count; i++)
                participating[i].ECInsulation.Number = i + 1;
            return participating;
        }

        public static List<ECWall> SortClockwise(List<ECWall> walls)
        {
            if (walls == null || walls.Count == 0) return new List<ECWall>();
            return InContourOrder(walls, Calculate(walls, GetDocument(walls)).WallOrder);
        }

        public static List<(XYZ Start, XYZ End)> BuildInnerContour(List<ECWall> walls)
        {
            var parts = new List<(XYZ, XYZ)>();
            if (walls == null || walls.Count == 0) return parts;
            var result = Calculate(walls, GetDocument(walls));
            double z = walls[0].ECLevel?.LevelElevation ?? 0;
            foreach (var part in result.Contour)
                parts.Add((new XYZ(part.Start.X, part.Start.Y, z), new XYZ(part.End.X, part.End.Y, z)));
            return parts;
        }

        private static Document GetDocument(List<ECWall> walls)
        {
            return walls.Select(w => w.Element?.Document ?? w.Wall?.Document).FirstOrDefault(d => d != null);
        }

        private static List<ECWall> InContourOrder(List<ECWall> walls, IEnumerable<int> order)
        {
            return order.Concat(Enumerable.Range(0, walls.Count)).Where(i => i >= 0 && i < walls.Count)
                .Distinct().Select(i => walls[i]).ToList();
        }

        private static CoolingContourGeometry.Result Calculate(List<ECWall> walls, Document doc)
        {
            var axes = new List<CoolingContourGeometry.WallAxis>();
            for (int i = 0; i < walls.Count; i++)
            {
                var wall = walls[i];
                var element = wall.Wall ?? wall.Element as Wall;
                var curve = wall.Curve ?? (element?.Location as LocationCurve)?.Curve;
                if (curve == null) continue;
                double width = element?.Width ?? UnitUtils.ConvertToInternalUnits(wall.WidthM, UnitTypeId.Meters);
                IList<XYZ> vertices = curve.Tessellate();
                for (int j = 1; j < vertices.Count; j++)
                {
                    XYZ a = vertices[j - 1], b = vertices[j];
                    axes.Add(new CoolingContourGeometry.WallAxis(i,
                        new CoolingContourGeometry.Point(a.X, a.Y),
                        new CoolingContourGeometry.Point(b.X, b.Y), width)
                    { FacadeHint = wall.ECInsulation.OutWallFromNAmeMaterial });
                }
            }
            return CoolingContourGeometry.Calculate(axes, LevelSlabOutline.Read(doc, walls));
        }
    }
}
