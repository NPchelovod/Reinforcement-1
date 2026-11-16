using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement.Aspirant
{
    /// <summary>
    /// Дотягивает ближайший конец заранее выделенной адаптивной балки
    /// к ближайшему концу второй балки, выбранной пользователем.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SnapBeamEndToBeam : IExternalCommand
    {
        private const double FtToMm = 304.8;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            // 1. Первая балка — уже выделена
            var selIds = uidoc.Selection.GetElementIds();
            if (selIds == null || selIds.Count != 1)
            {
                TaskDialog.Show("Дотянуть балку",
                    "Выделите ОДНУ адаптивную балку в проекте, затем запустите команду.");
                return Result.Cancelled;
            }

            var beam1 = doc.GetElement(selIds.First()) as FamilyInstance;
            if (beam1 == null || !IsAdaptive(beam1))
            {
                TaskDialog.Show("Дотянуть балку",
                    "Выделенный элемент не является адаптивной балкой.");
                return Result.Cancelled;
            }

            // 2. Выбор второй балки
            Reference pickedRef;
            try
            {
                pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new AdaptiveBeamFilter(beam1.Id),
                    "Выберите вторую балку");
            }
            catch (OperationCanceledException) { return Result.Cancelled; }

            if (pickedRef == null) return Result.Cancelled;

            var beam2 = doc.GetElement(pickedRef.ElementId) as FamilyInstance;
            if (beam2 == null)
            {
                TaskDialog.Show("Дотянуть балку",
                    "Второй элемент не является FamilyInstance.");
                return Result.Cancelled;
            }

            // 3. Placement-точки обеих балок
            var rp1 = GetPlacementPoints(doc, beam1);
            var rp2 = GetPlacementPoints(doc, beam2);

            if (rp1.Count < 2 || rp2.Count < 2)
            {
                TaskDialog.Show("Дотянуть балку",
                    $"У первой балки точек: {rp1.Count}, у второй: {rp2.Count}. " +
                    $"Для команды нужно минимум 2 у каждой.");
                return Result.Cancelled;
            }

            XYZ p1a = rp1[0].Position, p1b = rp1[1].Position;
            XYZ p2a = rp2[0].Position, p2b = rp2[1].Position;

            if (p1a == null || p1b == null || p2a == null || p2b == null)
            {
                TaskDialog.Show("Дотянуть балку", "Одна из точек не имеет координат.");
                return Result.Cancelled;
            }

            // 4. Поиск ближайшей пары концов (4 комбинации)
            var candidates = new[]
            {
                new { i1 = 0, i2 = 0, d = p1a.DistanceTo(p2a) },
                new { i1 = 0, i2 = 1, d = p1a.DistanceTo(p2b) },
                new { i1 = 1, i2 = 0, d = p1b.DistanceTo(p2a) },
                new { i1 = 1, i2 = 1, d = p1b.DistanceTo(p2b) },
            };
            var best = candidates.OrderBy(c => c.d).First();
            double distBeforeMm = best.d * FtToMm;

            ReferencePoint toMove = rp1[best.i1];
            XYZ targetPos = (best.i2 == 0) ? p2a : p2b;
            string endName = (best.i1 == 0) ? "начало" : "конец";
            string targetName = (best.i2 == 0) ? "начало" : "конец";

            if (distBeforeMm < 0.1)
            {
                TaskDialog.Show("Дотянуть балку",
                    $"Концы уже совпадают (расстояние {distBeforeMm:F2} мм). " +
                    $"Перемещение не требуется.");
                return Result.Succeeded;
            }

            // 5. Перемещение
            try
            {
                using (Transaction t = new Transaction(doc, "Дотянуть конец балки"))
                {
                    t.Start();

                    // Если закреплена — снимаем закрепление, иначе Position не изменится
                    if (toMove.Pinned)
                        toMove.Pinned = false;

                    toMove.Position = targetPos;

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ErrorAnalyse.Error(ex, "SnapBeamEndToBeam");
                return Result.Failed;
            }

            // 6. Отчёт
            TaskDialog.Show("Дотянуть балку",
                $"Готово.\n\n" +
                $"Перемещён {endName} первой балки → {targetName} второй.\n" +
                $"Было расстояние: {distBeforeMm:F1} мм.\n" +
                $"Элементы: [{beam1.Id}] → [{beam2.Id}]");
            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────
        //  Вспомогательные методы
        // ─────────────────────────────────────────────────────────

        private static List<ReferencePoint> GetPlacementPoints(Document doc, FamilyInstance fi)
        {
            var list = new List<ReferencePoint>();
            try
            {
                var ids = AdaptiveComponentInstanceUtils
                    .GetInstancePlacementPointElementRefIds(fi);
                if (ids != null)
                    foreach (var id in ids)
                        if (doc.GetElement(id) is ReferencePoint rp)
                            list.Add(rp);
            }
            catch { }
            return list;
        }

        private static bool IsAdaptive(FamilyInstance fi)
        {
            try { return AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(fi); }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────
        //  Фильтр выбора второй балки
        // ─────────────────────────────────────────────────────────

        private class AdaptiveBeamFilter : ISelectionFilter
        {
            private readonly ElementId _excludeId;

            public AdaptiveBeamFilter(ElementId excludeId) { _excludeId = excludeId; }

            public bool AllowElement(Element elem)
            {
                if (elem == null) return false;
                if (elem.Id == _excludeId) return false;   // нельзя ту же самую балку
                if (!(elem is FamilyInstance fi)) return false;
                try
                {
                    return AdaptiveComponentInstanceUtils
                        .IsAdaptiveComponentInstance(fi);
                }
                catch { return false; }
            }

            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}