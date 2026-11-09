using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    // ---------------------------------------------------------------------
    //  ПАКЕТНОЕ ВОССТАНОВЛЕНИЕ ВСЕХ ВИДОВ НА АКТИВНОМ ЛИСТЕ
    //  Берётся самый свежий снимок каждого вида из памяти, вопросы
    //  пользователю не задаются, в конце — короткий отчёт.
    // ---------------------------------------------------------------------
    [Transaction(TransactionMode.Manual)]
    public class MemoryViewCorrectSheet : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;

            // 1) Команда только для активного листа
            ViewSheet sheet = doc.ActiveView as ViewSheet;
            if (sheet == null)
            {
                TaskDialog.Show("Восстановление видов на листе",
                    "Команда должна запускаться из активного листа.\n" +
                    "Откройте лист и повторите.");
                return Result.Succeeded;
            }

            // 2) Все виды, размещённые на листе
            var placedViewIds = sheet.GetAllPlacedViews();
            if (placedViewIds == null || placedViewIds.Count == 0)
            {
                TaskDialog.Show("Восстановление видов на листе",
                    $"На листе «{sheet.Name}» нет размещённых видов.");
                return Result.Succeeded;
            }

            int restored = 0;
            int skippedNoChange = 0;
            int skippedNoMemory = 0;
            int skippedUnsupported = 0;
            int totalHidden = 0;
            int totalUnhidden = 0;
            var failed = new List<string>();

            // 3) Одна общая транзакция — скрываем/показываем пакетно
            using (Transaction t = new Transaction(doc, "Восстановление видов на листе"))
            {
                t.Start();

                foreach (ElementId viewId in placedViewIds)
                {
                    View view = doc.GetElement(viewId) as View;
                    if (view == null) continue;

                    // Спецификации/листы — не поддерживают hide/unhide
                    if (view is ViewSchedule || view is ViewSheet)
                    {
                        skippedUnsupported++;
                        continue;
                    }

                    int vid = (int)view.Id.Value;
                    ElementMemory em = MemoryViewSave.GetElementMemory(vid);
                    if (em.Snapshots == null || em.Snapshots.Count == 0)
                    {
                        skippedNoMemory++;
                        continue;
                    }

                    // Самый свежий снимок (на случай ручной правки Date)
                    MemorySnapshot snapshot = em.Snapshots
                        .OrderByDescending(s => s.Date)
                        .First();

                    if (snapshot.ElementIds == null)
                        snapshot.ElementIds = new HashSet<int>();
                    HashSet<int> keepIds = snapshot.ElementIds;

                    try
                    {
                        var allElements = new FilteredElementCollector(doc, view.Id)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        // Скрыть «лишние»
                        int hiddenHere = 0;
                        foreach (var el in allElements)
                        {
                            int idCur = (int)el.Id.Value;
                            if (keepIds.Contains(idCur)) continue;
                            if (el.IsHidden(view)) continue;

                            try
                            {
                                view.HideElements(new List<ElementId> { el.Id });
                                hiddenHere++;
                            }
                            catch { /* элемент не скрывается на этом виде */ }
                        }

                        // Вернуть видимость тем, кто был в снимке
                        int unhiddenHere = 0;
                        foreach (int idInt in keepIds)
                        {
                            ElementId eid = new ElementId(idInt);
                            Element el = doc.GetElement(eid);
                            if (el == null) continue; // был удалён

                            try
                            {
                                if (el.IsHidden(view))
                                {
                                    view.UnhideElements(new List<ElementId> { eid });
                                    unhiddenHere++;
                                }
                            }
                            catch { /* скрыт категорией/фильтром/РГ */ }
                        }

                        if (hiddenHere == 0 && unhiddenHere == 0)
                            skippedNoChange++;
                        else
                            restored++;

                        totalHidden += hiddenHere;
                        totalUnhidden += unhiddenHere;
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{view.Name}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            // 4) Короткий отчёт
            string report = $"Лист: {sheet.Name}\n";
            report += $"Видов на листе: {placedViewIds.Count}\n";
            report += $"Восстановлено видов: {restored}";
            if (skippedNoChange > 0)
                report += $", без изменений: {skippedNoChange}";
            if (skippedNoMemory > 0)
                report += $", без памяти: {skippedNoMemory}";
            if (skippedUnsupported > 0)
                report += $", неподходящий тип: {skippedUnsupported}";
            report += ".\n";

            if (totalHidden > 0 || totalUnhidden > 0)
                report += $"Скрыто элементов: {totalHidden}, показано: {totalUnhidden}.\n";

            if (failed.Count > 0)
                report += $"Ошибки ({failed.Count}):\n" + string.Join("\n", failed);

            TaskDialog.Show("Восстановление видов на листе", report);

            return Result.Succeeded;
        }
    }
}
