using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class HighOtm : IExternalCommand
    {
        private const double Mm = 1.0 / 304.8;
        private const double HeightTolerance = 1.0 * Mm;
        private const double MatchTolerance = 1.0 * Mm;
        private const double MergeDistance = 200.0 * Mm;
        private const double MinPipeLength = 500.0 * Mm;

        private sealed class PipeInfo
        {
            public Pipe Pipe;
            public XYZ Start;
            public XYZ End;
            public Reference AxisReference;
        }

        private sealed class Candidate
        {
            public PipeInfo PipeInfo;
            public XYZ Joint;
            public XYZ Point;
            public XYZ EdgePoint;
            public Reference EdgeReference;
        }

        private sealed class PipeSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) { return element is Pipe; }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            View3D view3D = view as View3D;
            if (!(view is ViewPlan) && !(view is ViewSection) &&
                (view3D == null || view3D.IsPerspective))
            {
                TaskDialog.Show("Отметки оси труб",
                    "Откройте план, разрез, фасад или ортогональный 3D-вид.");
                return Result.Cancelled;
            }

            List<ElementId> ids = uiDoc.Selection.GetElementIds()
                .Where(id => doc.GetElement(id) is Pipe).ToList();
            if (ids.Count == 0)
            {
                try
                {
                    ids = uiDoc.Selection.PickObjects(ObjectType.Element,
                        new PipeSelectionFilter(), "Выберите трубы для отметок оси")
                        .Select(r => r.ElementId).Distinct().ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }
            if (ids.Count == 0) return Result.Cancelled;

            // Если выделена ровно одна труба — работаем с ней независимо от длины.
            bool singlePipe = ids.Count == 1;

            SpotDimensionType spotType = new FilteredElementCollector(doc)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.SpotElevation)
                .OrderByDescending(t => string.Equals(t.Name,
                    "ADSK_Стрелка_Проектная_Вверх", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (spotType == null)
            {
                message = "В проекте нет типоразмера высотной отметки.";
                return Result.Failed;
            }

            List<string> errors = new List<string>();
            List<PipeInfo> pipes = new List<PipeInfo>();
            int skippedShort = 0;
            foreach (ElementId id in ids)
            {
                Pipe pipe = doc.GetElement(id) as Pipe;
                LocationCurve location = pipe == null ? null : pipe.Location as LocationCurve;
                if (location == null || !(location.Curve is Line))
                {
                    errors.Add("Труба " + id.Value + ": нет прямой оси.");
                    continue;
                }

                // Отсекаем короткие трубы, если пользователь выделил более одной.
                if (!singlePipe)
                {
                    double length = GetPipeLength(pipe, location);
                    if (length < MinPipeLength)
                    {
                        skippedShort++;
                        continue;
                    }
                }

                XYZ start = location.Curve.GetEndPoint(0);
                XYZ end = location.Curve.GetEndPoint(1);
                Reference axis = FindAxisReference(pipe, view, start, end);
                pipes.Add(new PipeInfo
                {
                    Pipe = pipe,
                    Start = start,
                    End = end,
                    AxisReference = axis
                });
            }

            if (pipes.Count == 0)
            {
                string msg = "Нет подходящих труб для простановки отметок.";
                if (skippedShort > 0)
                    msg += "\nПропущено коротких труб (< 500 мм): " + skippedShort + ".";
                TaskDialog.Show("Отметки оси труб", msg);
                return Result.Cancelled;
            }

            List<Candidate> candidates = MakeCandidates(pipes);
            List<Candidate> unique = new List<Candidate>();
            foreach (Candidate item in candidates)
            {
                if (!unique.Any(other =>
                    Math.Abs(other.Joint.Z - item.Joint.Z) < HeightTolerance &&
                    other.Joint.DistanceTo(item.Joint) < MergeDistance))
                    unique.Add(item);
            }

            int created = 0;
            int axisFallbacks = 0;
            List<ElementId> createdSpotIds = new List<ElementId>();
            using (Transaction transaction = new Transaction(doc, "Отметки оси труб"))
            {
                transaction.Start();
                foreach (Candidate item in unique)
                {
                    string edgeError = null;
                    string axisError = null;
                    SpotDimension spot = null;
                    bool ok = item.EdgeReference != null && TryCreateSpot(
                        doc, view, spotType, item.EdgeReference, item.EdgePoint,
                        out spot, out edgeError);
                    if (!ok && item.PipeInfo.AxisReference != null)
                    {
                        ok = TryCreateSpot(doc, view, spotType,
                            item.PipeInfo.AxisReference, item.Point,
                            out spot, out axisError);
                        if (ok) axisFallbacks++;
                    }
                    if (ok)
                    {
                        created++;
                        if (spot != null) createdSpotIds.Add(spot.Id);
                    }
                    else
                    {
                        errors.Add("Труба " + item.PipeInfo.Pipe.Id.Value +
                            ": кромка: " + (edgeError ?? "ссылка не найдена") +
                            "; ось: " + (axisError ?? "ссылка не найдена"));
                    }
                }
                transaction.Commit();
            }

            // Выделяем созданные отметки.
            if (createdSpotIds.Count > 0)
            {
                try { uiDoc.Selection.SetElementIds(createdSpotIds); }
                catch (Exception) { }
            }

            string report = "Создано отметок оси: " + created + ".";
            if (axisFallbacks > 0)
                report += "\nИз них по ссылке на ось: " + axisFallbacks + ".";
            if (skippedShort > 0)
                report += "\nПропущено коротких труб (< 500 мм): " + skippedShort + ".";
            if (errors.Count > 0)
                report += "\nОшибок/пропусков: " + errors.Count + ".\n\n" +
                    string.Join("\n", errors.Take(8).ToArray());
            TaskDialog.Show("Отметки оси труб", report);
            return Result.Succeeded;
        }

        private static double GetPipeLength(Pipe pipe, LocationCurve location)
        {
            Parameter p = pipe.LookupParameter("Длина");
            if (p == null || !p.HasValue)
                p = pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (p != null && p.HasValue)
            {
                if (p.StorageType == StorageType.Double) return p.AsDouble();
                if (p.StorageType == StorageType.Integer) return p.AsInteger();
            }
            try { return location.Curve.Length; }
            catch (Exception) { return 0.0; }
        }

        private static bool TryCreateSpot(Document doc, View view,
            SpotDimensionType spotType, Reference reference, XYZ point,
            out SpotDimension createdSpot, out string error)
        {
            createdSpot = null;
            error = null;
            using (SubTransaction attempt = new SubTransaction(doc))
            {
                attempt.Start();
                try
                {
                    XYZ bend = point + view.UpDirection * (180.0 * Mm)
                        + view.RightDirection * (120.0 * Mm);
                    XYZ leaderEnd = bend + view.RightDirection * (280.0 * Mm);
                    SpotDimension spot = doc.Create.NewSpotElevation(
                        view, reference, point, bend, leaderEnd, point, true);
                    if (spot == null)
                        throw new InvalidOperationException("NewSpotElevation вернул null.");
                    spot.SpotDimensionType = spotType;
                    attempt.Commit();
                    createdSpot = spot;
                    return true;
                }
                catch (Exception ex)
                {
                    attempt.RollBack();
                    error = ex.GetType().Name + " — " + ex.Message;
                    return false;
                }
            }
        }

        private static Reference FindAxisReference(Pipe pipe, View view, XYZ start, XYZ end)
        {
            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                View = view
            };
            Reference found = null;
            try { found = FindAxisInGeometry(pipe.get_Geometry(options), start, end); }
            catch (Exception) { }
            if (found != null) return found;

            options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            try { return FindAxisInGeometry(pipe.get_Geometry(options), start, end); }
            catch (Exception) { return null; }
        }

        private static Reference FindAxisInGeometry(
            GeometryElement geometry, XYZ start, XYZ end)
        {
            if (geometry == null) return null;
            foreach (GeometryObject obj in geometry)
            {
                Line line = obj as Line;
                if (line == null || line.Reference == null) continue;
                XYZ a = line.GetEndPoint(0);
                XYZ b = line.GetEndPoint(1);
                bool forward = a.DistanceTo(start) < MatchTolerance &&
                    b.DistanceTo(end) < MatchTolerance;
                bool reverse = a.DistanceTo(end) < MatchTolerance &&
                    b.DistanceTo(start) < MatchTolerance;
                if (forward || reverse) return line.Reference;
            }
            return null;
        }

        private static List<Candidate> MakeCandidates(List<PipeInfo> pipes)
        {
            List<Candidate> result = new List<Candidate>();
            List<PipeInfo> horizontal = new List<PipeInfo>();
            foreach (PipeInfo pipe in pipes)
            {
                if (Math.Abs(pipe.Start.Z - pipe.End.Z) > HeightTolerance)
                {
                    result.Add(AtEnd(pipe, true));
                    result.Add(AtEnd(pipe, false));
                }
                else horizontal.Add(pipe);
            }

            List<List<PipeInfo>> levels = new List<List<PipeInfo>>();
            foreach (PipeInfo pipe in horizontal)
            {
                List<PipeInfo> level = levels.FirstOrDefault(group =>
                    Math.Abs(group[0].Start.Z - pipe.Start.Z) < HeightTolerance);
                if (level == null)
                {
                    level = new List<PipeInfo>();
                    levels.Add(level);
                }
                level.Add(pipe);
            }
            foreach (List<PipeInfo> level in levels)
            {
                XYZ centroid = new XYZ(level.Average(p => (p.Start.X + p.End.X) / 2),
                    level.Average(p => (p.Start.Y + p.End.Y) / 2),
                    level.Average(p => (p.Start.Z + p.End.Z) / 2));
                PipeInfo extremePipe = level.OrderByDescending(p =>
                    Math.Max(p.Start.DistanceTo(centroid), p.End.DistanceTo(centroid)))
                    .First();
                result.Add(AtEnd(extremePipe,
                    extremePipe.Start.DistanceTo(centroid) >=
                    extremePipe.End.DistanceTo(centroid)));
            }
            return result;
        }

        private static Candidate AtEnd(PipeInfo pipe, bool start)
        {
            XYZ joint = start ? pipe.Start : pipe.End;
            XYZ other = start ? pipe.End : pipe.Start;
            double length = joint.DistanceTo(other);
            if (length < 0.01 * Mm)
                return new Candidate { PipeInfo = pipe, Joint = joint, Point = joint };
            XYZ point = length > 2.0 * Mm
                ? joint + (other - joint).Normalize() * Mm
                : joint + (other - joint) * 0.5;
            XYZ edgePoint;
            Reference edge = FindEdgeAtAxisHeight(pipe.Pipe, joint, other, out edgePoint);
            return new Candidate
            {
                PipeInfo = pipe,
                Joint = joint,
                Point = point,
                EdgePoint = edgePoint,
                EdgeReference = edge
            };
        }

        private static Reference FindEdgeAtAxisHeight(
            Pipe pipe, XYZ joint, XYZ other, out XYZ edgePoint)
        {
            edgePoint = null;
            Parameter diameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
            if (diameter == null || !diameter.HasValue || diameter.AsDouble() <= 0)
                return null;

            XYZ axis = (other - joint).Normalize();
            XYZ radial = axis.CrossProduct(XYZ.BasisZ);
            if (radial.GetLength() < 0.01) radial = XYZ.BasisX;
            else radial = radial.Normalize();
            XYZ target = joint + radial * (diameter.AsDouble() / 2.0);

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };
            GeometryElement geometry;
            try { geometry = pipe.get_Geometry(options); }
            catch (Exception) { return null; }
            if (geometry == null) return null;

            Reference best = null;
            double bestDistance = 2.0 * Mm;
            foreach (GeometryObject obj in geometry)
            {
                Solid solid = obj as Solid;
                if (solid == null || solid.Volume <= 0) continue;
                foreach (Edge edge in solid.Edges)
                {
                    if (edge.Reference == null) continue;
                    try
                    {
                        IntersectionResult projection = edge.AsCurve().Project(target);
                        if (projection == null) continue;
                        XYZ p = projection.XYZPoint;
                        double distance = p.DistanceTo(target);
                        if (Math.Abs(p.Z - joint.Z) < HeightTolerance &&
                            distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = edge.Reference;
                            edgePoint = p;
                        }
                    }
                    catch (Exception) { }
                }
            }
            return best;
        }
    }
}