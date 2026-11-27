using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CalcInsulationCommand : IExternalCommand
    {
        private const double MinWidthM = 0.1;

        public static readonly HashSet<EWallTypes> NotOuterWalls = new HashSet<EWallTypes>
        {
            EWallTypes.AR_Vitrage, EWallTypes.AR_VentFasad, EWallTypes.AR_Uteplitel
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                RevitAPI.Initialize(commandData);
                UIDocument uidoc = RevitAPI.UiDocument;
                if (uidoc == null) throw new InvalidOperationException("Нет активного документа Revit.");
                List<ECWall> walls = ExecuteLogic(uidoc.Document);
                List<ElementId> ids = walls.Where(w => w != null && w.ElementId != null)
                    .Select(w => w.ElementId).ToList();
                if (ids.Count > 0)
                {
                    uidoc.Selection.SetElementIds(ids);
                    uidoc.ShowElements(ids);
                }
                return Result.Succeeded;
            }
            catch (Exception error)
            {
                message = "Расчёт охлаждаемой длины стен не выполнен: " + error.Message;
                return Result.Failed;
            }
        }

        public static List<ECWall> ExecuteLogic(Document doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            // The architects hide conflicting walls on this view. Ignore any old selection.
            List<ECWall> wallsOnView = CalcWalls.GetWallsOnActiveView(doc);
            if (wallsOnView == null || wallsOnView.Count == 0)
                throw new InvalidOperationException("На активном виде нет видимых стен.");

            List<ECWall> walls = wallsOnView.Where(w => w != null && w.ECLevel != null &&
                w.ECInsulation != null && !w.ECInsulation.InWallFromNAmeMaterial &&
                !NotOuterWalls.Contains(w.EWallTypes) && w.WidthM > MinWidthM).ToList();
            if (walls.Count == 0)
                throw new InvalidOperationException("На активном виде нет стен после фильтрации типов.");

            IntersectWalls.CalcIntersects(doc, walls);
            var groups = walls.GroupBy(w => w.ECLevel.LevelElevation).OrderBy(g => g.Key);
            var answerWalls = new List<List<IExportStringDoubleData>>();
            var answerInsulation = new List<List<IExportStringDoubleData>>();
            var allWalls = new List<ECWall>();

            foreach (var group in groups)
            {
                List<ECWall> ordered = ECWallPerimeterChainBuilder.FillLengthInsideM(group.ToList(), doc);
                allWalls.AddRange(ordered);
                answerWalls.Add(ordered.Cast<IExportStringDoubleData>().ToList());
                answerInsulation.Add(ordered.Select(w => w.ECInsulation)
                    .Cast<IExportStringDoubleData>().ToList());
            }

            ExcelGroupExporter.Exporter(new List<ExportElementOnSheetExcell>
            {
                new ExportElementOnSheetExcell
                {
                    SheetName = "Стены", exportStringDoubleDatas = answerWalls
                },
                new ExportElementOnSheetExcell
                {
                    SheetName = "ТеплоИзол", exportStringDoubleDatas = answerInsulation
                }
            }, "ТеплоизолСтен");
            return allWalls;
        }
    }
}