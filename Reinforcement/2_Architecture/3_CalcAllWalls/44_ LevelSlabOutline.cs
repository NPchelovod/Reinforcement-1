using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Point2 = Reinforcement.CoolingContourGeometry.Point;

namespace Reinforcement
{
    internal static class LevelSlabOutline
    {
        // Host floors are a geometric reference. A floor need not be shown on the AR wall view.
        internal static List<List<Point2>> Read(Document doc, IList<ECWall> walls)
        {
            var rings = new List<List<Point2>>();
            if (doc == null || walls.Count == 0) return rings;
            var levels = new HashSet<long>(walls.Where(w => w.ECLevel?.Level != null)
                .Select(w => w.ECLevel.Level.Id.Value));
            var options = new HashSet<long>(walls.Where(w => w.Element?.DesignOption != null)
                .Select(w => w.Element.DesignOption.Id.Value));
            var phase = doc.ActiveView?.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
            var structural = new List<List<Point2>>();
            using (var floors = new FilteredElementCollector(doc).OfClass(typeof(Floor)))
            {
                foreach (Floor floor in floors)
                {
                    if (!levels.Contains(floor.LevelId.Value)) continue;
                    var option = floor.DesignOption;
                    if (option != null && !options.Contains(option.Id.Value) && !option.IsPrimary) continue;
                    if (phase != null && phase != ElementId.InvalidElementId && floor.HasPhases())
                    {
                        var status = floor.GetPhaseStatus(phase);
                        if (status == ElementOnPhaseStatus.Past || status == ElementOnPhaseStatus.Future ||
                            status == ElementOnPhaseStatus.Demolished) continue;
                    }
                    var isStructural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    var destination = isStructural?.AsInteger() == 1 ? structural : rings;
                    foreach (Reference faceRef in HostObjectUtils.GetTopFaces(floor))
                    {
                        var face = floor.GetGeometryObjectFromReference(faceRef) as Face;
                        if (face == null) continue;
                        // A single top face has one outer loop and may have openings.
                        // The openings are not references for the building's outer facade.
                        var outer = face.GetEdgesAsCurveLoops().Select(Flatten)
                            .Where(loop => loop.Count >= 3).OrderByDescending(Area).FirstOrDefault();
                        if (outer != null && Area(outer) > 1e-6) destination.Add(outer);
                    }
                }
            }
            return structural.Count > 0 ? structural : rings;
        }

        private static List<Point2> Flatten(CurveLoop loop)
        {
            var ring = new List<Point2>();
            foreach (Curve edge in loop)
            {
                IList<XYZ> points = edge.Tessellate();
                for (int i = 0; i + 1 < points.Count; i++)
                    ring.Add(new Point2(points[i].X, points[i].Y));
            }
            return ring;
        }

        private static double Area(List<Point2> ring)
        {
            double twiceArea = 0;
            Point2 origin = ring[0];
            for (int i = 1; i < ring.Count - 1; i++)
                twiceArea += (ring[i].X - origin.X) * (ring[i + 1].Y - origin.Y)
                           - (ring[i].Y - origin.Y) * (ring[i + 1].X - origin.X);
            return Math.Abs(twiceArea) * 0.5;
        }
    }
}
