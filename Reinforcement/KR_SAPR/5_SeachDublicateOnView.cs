using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class SeachDublicateOnView : IExternalCommand
    {
        // 1 мм в футах (внутренние единицы Revit)
        private const double MmToFeet = 10.0 / 304.8;

        // Ограничение на вывод в TaskDialog, чтобы не упереться в лимиты UI
        private const int MaxGroupsToPrint = 100;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uiDoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            if (uiDoc == null || doc == null)
            {
                message = "Нет активного документа.";
                return Result.Failed;
            }

            View activeView = uiDoc.ActiveView;
            if (activeView == null)
            {
                message = "Нет активного вида.";
                return Result.Failed;
            }

            // Точность 1 мм. Ключ = целое число миллиметров.
            double tol = MmToFeet;         // 1 мм в футах
            double inv = 1.0 / tol;        // множитель для перевода футов в мм

            // Ключ: округлённые до мм координаты центра
            var groups = new Dictionary<(long X, long Y, long Z), List<ElementId>>();

            var collector = new FilteredElementCollector(doc, activeView.Id)
                .WhereElementIsNotElementType();

            int checkedCount = 0;

            foreach (Element el in collector)
            {
                if (el?.Category == null) continue;
                // Только модельные категории (без аннотаций, осей, размеров)
                if (el.Category.CategoryType != CategoryType.Model) continue;

                XYZ center = GetElementCenter(el, activeView);
                if (center == null) continue;

                long kx = (long)Math.Round(center.X * inv, MidpointRounding.AwayFromZero);
                long ky = (long)Math.Round(center.Y * inv, MidpointRounding.AwayFromZero);
                long kz = (long)Math.Round(center.Z * inv, MidpointRounding.AwayFromZero);

                var key = (kx, ky, kz);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<ElementId>();
                    groups[key] = list;
                }
                list.Add(el.Id);
                checkedCount++;
            }

            // Оставляем только группы с более чем одним элементом
            var duplicates = groups
                .Where(g => g.Value.Count > 1)
                .OrderByDescending(g => g.Value.Count)
                .ToList();

            if (duplicates.Count == 0)
            {
                TaskDialog.Show("Поиск дубликатов",
                    $"Проверено элементов: {checkedCount}\n" +
                    "Дубликатов не найдено.");
                return Result.Succeeded;
            }

            // Собираем все ID и выделяем их на активном виде
            var allIds = new List<ElementId>();
            foreach (var g in duplicates)
                allIds.AddRange(g.Value);

            uiDoc.Selection.SetElementIds(allIds);

            // ==== Формируем отчёт ====
            var sb = new StringBuilder();
            sb.AppendLine($"Проверено элементов: {checkedCount}");
            sb.AppendLine($"Групп дубликатов:   {duplicates.Count}");
            sb.AppendLine($"Элементов в дубликатах: {allIds.Count}");
            sb.AppendLine();

            int idx = 1;
            foreach (var group in duplicates)
            {
                if (idx > MaxGroupsToPrint)
                {
                    sb.AppendLine($"... (ещё {duplicates.Count - MaxGroupsToPrint + 1} групп, отчёт обрезан)");
                    break;
                }

                // Восстанавливаем «центр» в футах для отображения в мм
                XYZ center = new XYZ(
                    group.Key.X / inv,
                    group.Key.Y / inv,
                    group.Key.Z / inv);

                sb.AppendLine($"Группа {idx++} ({group.Value.Count} шт.) — " +
                              $"X={center.X * 304.8:F1}  " +
                              $"Y={center.Y * 304.8:F1}  " +
                              $"Z={center.Z * 304.8:F1} мм");

                foreach (var id in group.Value)
                {
                    var e = doc.GetElement(id);
                    string catName = e?.Category?.Name ?? "?";
                    string typeName = "";

                    try
                    {
                        ElementId typeId = e.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            var typeEl = doc.GetElement(typeId);
                            typeName = typeEl?.Name ?? "";
                        }
                    }
                    catch { /* пропускаем */ }

                    sb.AppendLine($"    ID {id}   [{catName}] {typeName}");
                }
                sb.AppendLine();
            }

            TaskDialog dlg = new TaskDialog("Дубликаты на виде")
            {
                MainInstruction = $"Найдено групп: {duplicates.Count}, элементов: {allIds.Count}",
                MainContent = sb.ToString(),
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            dlg.Show();

            return Result.Succeeded;
        }

        /// <summary>
        /// Возвращает «центральную точку» элемента в футах.
        /// Для стен и кривых — середина LocationCurve.
        /// Для семейств — LocationPoint.
        /// Fallback — центр BoundingBox на виде.
        /// </summary>
        private static XYZ GetElementCenter(Element el, View view)
        {
            try
            {
                Location loc = el.Location;

                if (loc is LocationPoint lp)
                    return lp.Point;

                if (loc is LocationCurve lc && lc.Curve != null)
                {
                    // Середина кривой (для стен, балок, труб и т.п.)
                    return lc.Curve.Evaluate(0.5, true);
                }

                BoundingBoxXYZ bb = el.get_BoundingBox(view);
                if (bb != null)
                    return (bb.Min + bb.Max) * 0.5;
            }
            catch
            {
                // некоторые элементы могут бросать исключение при обращении к Location
            }
            return null;
        }
    }
}