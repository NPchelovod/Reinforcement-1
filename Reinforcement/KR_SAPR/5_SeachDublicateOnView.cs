using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Globalization;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class SeachDublicateOnView : IExternalCommand
    {
       
        private const int MaxGroupsToPrint = 100;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uiDoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            if (uiDoc == null || doc == null) { message = "Нет активного документа."; return Result.Failed; }

            Autodesk.Revit.DB.View activeView = uiDoc.ActiveView;
            if (activeView == null) { message = "Нет активного вида."; return Result.Failed; }
            // ---- запрос погрешности у пользователя ----
            double? tolMm = AskToleranceMm(12.0);
            if (tolMm == null)
            {
                message = "Операция отменена пользователем.";
                return Result.Cancelled;
            }
            double tol = tolMm.Value / 304.8;   // мм -> футы
            double inv = 1.0 / tol;             // футы -> мм (для ключа и отчёта)

            // ---- 1. Предварительно соберём ID, на которые ссылаются размеры ----
            var dimensionedIds = new HashSet<ElementId>();
            try
            {
                var dims = new FilteredElementCollector(doc)
                    .OfClass(typeof(Dimension))
                    .WhereElementIsNotElementType();
                foreach (Dimension dim in dims)
                {
                    try
                    {
                        foreach (Reference r in dim.References)
                        {
                            if (r != null && r.ElementId != ElementId.InvalidElementId)
                                dimensionedIds.Add(r.ElementId);
                        }
                    }
                    catch { /* пропускаем проблемные размеры */ }
                }
            }
            catch { }

            // ---- 2. Группировка: ключ теперь включает категорию ----
            // (стена и перекрытие в одной точке — РАЗНЫЕ группы)
            var groups = new Dictionary<(long X, long Y, long Z, ElementId Cat), List<ElementId>>();

            var collector = new FilteredElementCollector(doc, activeView.Id)
                .WhereElementIsNotElementType();

            int checkedCount = 0;

            foreach (Element el in collector)
            {
                if (el?.Category == null) continue;
                if (el.Category.CategoryType != CategoryType.Model) continue;

                XYZ center = GetElementCenter(el, activeView);
                if (center == null) continue;

                long kx = (long)Math.Round(center.X * inv, MidpointRounding.AwayFromZero);
                long ky = (long)Math.Round(center.Y * inv, MidpointRounding.AwayFromZero);
                long kz = (long)Math.Round(center.Z * inv, MidpointRounding.AwayFromZero);

                var key = (kx, ky, kz, el.Category.Id);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<ElementId>();
                    groups[key] = list;
                }
                list.Add(el.Id);
                checkedCount++;
            }

            var duplicates = groups
                .Where(g => g.Value.Count > 1)
                .OrderByDescending(g => g.Value.Count)
                .ToList();

            if (duplicates.Count == 0)
            {
                TaskDialog.Show("Поиск дубликатов",
                    $"Проверено элементов: {checkedCount}\nДубликатов не найдено.");
                return Result.Succeeded;
            }

            // ---- 3. Выбираем «хранителя» в каждой группе, остальное — в выделение ----
            var toSelect = new List<ElementId>();     // пойдут в Selection.SetElementIds
            var keepers = new HashSet<ElementId>();  // для отчёта

            var sb = new StringBuilder();
            sb.AppendLine($"Проверено элементов: {checkedCount}");
            sb.AppendLine($"Групп дубликатов:   {duplicates.Count}");
            sb.AppendLine($"Погрешность: {tolMm.Value:0.###} мм");
            sb.AppendLine();

            int idx = 1;
            int truncated = 0;

            foreach (var group in duplicates)
            {
                // Хранитель = элемент с НАИМЕНЬШИМ числом привязок
                // (размеры + зависимые элементы). Если все равны — первый в списке.
                // Если хотите наоборот (хранить «наиболее привязанный») — 
                // замените OrderBy на OrderByDescending.
                ElementId keeper = group.Value
                    .OrderBy(id => GetAttachmentScore(doc, id, dimensionedIds))
                    .First();

                keepers.Add(keeper);

                foreach (var id in group.Value)
                    if (id != keeper)
                        toSelect.Add(id);

                // ---- отчёт ----
                if (idx <= MaxGroupsToPrint)
                {
                    XYZ center = new XYZ(
                        group.Key.X / inv,
                        group.Key.Y / inv,
                        group.Key.Z / inv);

                    string catName = doc.GetElement(group.Value[0])?.Category?.Name ?? "?";

                    sb.AppendLine($"Группа {idx} [{catName}] ({group.Value.Count} шт.) — " +
                                  $"X={center.X * 304.8:F1}  " +
                                  $"Y={center.Y * 304.8:F1}  " +
                                  $"Z={center.Z * 304.8:F1} мм");

                    foreach (var id in group.Value)
                    {
                        var e = doc.GetElement(id);
                        string typeName = "";
                        try
                        {
                            ElementId typeId = e.GetTypeId();
                            if (typeId != ElementId.InvalidElementId)
                                typeName = doc.GetElement(typeId)?.Name ?? "";
                        }
                        catch { }

                        string mark = (id == keeper) ? "  [ОСТАВИТЬ]" : "  [удалить]";
                        sb.AppendLine($"    ID {id}{mark}   {typeName}");
                    }
                    sb.AppendLine();
                }
                else
                {
                    truncated++;
                }
                idx++;
            }

            if (truncated > 0)
                sb.AppendLine($"... (ещё {truncated} групп, отчёт обрезан)");

            uiDoc.Selection.SetElementIds(toSelect);

            TaskDialog dlg = new TaskDialog("Дубликаты на виде")
            {
                MainInstruction =
                    $"Групп: {duplicates.Count}, " +
                    $"выделено под удаление: {toSelect.Count}, " +
                    $"оставлено: {keepers.Count}",
                MainContent = sb.ToString(),
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            dlg.Show();

            return Result.Succeeded;
        }

        /// <summary>
        /// Чем больше «привязок» у элемента — тем больше score.
        /// Размер, ссылающийся на элемент, весит 1000 (сильный признак).
        /// Плюс количество зависимых элементов (hosted).
        /// </summary>
        private static int GetAttachmentScore(Document doc, ElementId id, HashSet<ElementId> dimensionedIds)
        {
            int score = 0;
            if (dimensionedIds.Contains(id)) score += 1000;
            try
            {
                Element el = doc.GetElement(id);
                if (el != null)
                {
                    var deps = el.GetDependentElements(null);
                    if (deps != null) score += deps.Count;
                }
            }
            catch { }
            return score;
        }

        /// <summary>Без изменений.</summary>
        private static XYZ GetElementCenter(Element el, Autodesk.Revit.DB.View view)
        {
            try
            {
                Location loc = el.Location;
                if (loc is LocationPoint lp) return lp.Point;
                if (loc is LocationCurve lc && lc.Curve != null)
                    return lc.Curve.Evaluate(0.5, true);

                BoundingBoxXYZ bb = el.get_BoundingBox(view);
                if (bb != null) return (bb.Min + bb.Max) * 0.5;
            }
            catch { }
            return null;
        }
        private static double? AskToleranceMm(double defaultMm)
        {
            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = "Погрешность поиска дубликатов";
                form.Width = 340;
                form.Height = 170;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var lbl = new Label
                {
                    Text = "Погрешность совпадения центров, мм:",
                    Left = 12,
                    Top = 15,
                    Width = 300
                };
                var txt = new System.Windows.Forms.TextBox
                {
                    Left = 12,
                    Top = 42,
                    Width = 300,
                    Text = defaultMm.ToString("0.###", CultureInfo.InvariantCulture)
                };
                var btnOk = new Button
                {
                    Text = "ОК",
                    Left = 150,
                    Top = 80,
                    Width = 75,
                    DialogResult = DialogResult.OK
                };
                var btnCancel = new Button
                {
                    Text = "Отмена",
                    Left = 237,
                    Top = 80,
                    Width = 75,
                    DialogResult = DialogResult.Cancel
                };

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                // Фокус в поле + выделить текст, чтобы сразу можно было печатать
                form.Shown += (s, e) => { txt.Focus(); txt.SelectAll(); };

                if (form.ShowDialog() != DialogResult.OK) return null;

                string ss = (txt.Text ?? "").Trim().Replace(',', '.');
                if (double.TryParse(ss,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double mm) && mm > 0)
                {
                    return mm;
                }
                return null;
            }
        }
    }
}