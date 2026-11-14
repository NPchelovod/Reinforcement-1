using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Только WPF-пространства — в этом файле WinForms не нужен.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//Ваши промежуточные точки разбиения — это Shape Handle Points 

//GetInstancePlacementPointElementRefIds — только размещаемые точки (их у вас две).

//GetInstancePointElementRefIds — все точки: размещаемые плюс Shape Handle Points.

//GetInstanceShapeHandlePointElementRefIds — только Shape Handle Points (промежуточные точки разбиения).


namespace Reinforcement
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowAdaptiveBeamPoints : IExternalCommand
    {
        private const double FtToMm = 304.8;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            var uidoc = commandData?.Application?.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            var selIds = uidoc.Selection.GetElementIds();
            if (selIds == null || selIds.Count == 0)
            {
                TaskDialog.Show("Точки балок",
                    "Выделите балки (адаптивные компоненты) и повторите.");
                return Result.Cancelled;
            }

            var sb = new StringBuilder(4096);
            int adaptive = 0, nonAdaptive = 0, skipped = 0;

            foreach (var id in selIds)
            {
                Element el;
                try { el = doc.GetElement(id); }
                catch { skipped++; continue; }
                if (el == null) { skipped++; continue; }

                var fi = el as FamilyInstance;
                if (fi == null) { skipped++; continue; }

                IList<ElementId> pts = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(fi))
                        pts = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(fi);
                }
                catch (Exception ex) { AppErrors.LogError(ex); }

                if (pts == null || pts.Count == 0)
                {
                    nonAdaptive++;
                    sb.AppendLine($"● [{id.IntegerValue}] \"{el.Name}\" — не адаптивный компонент");
                    sb.AppendLine();
                    continue;
                }
                // После получения pts (размещаемых точек)
                IList<ElementId> shapePointIds = null;
                try
                {
                    if (AdaptiveComponentInstanceUtils.IsAdaptiveComponentInstance(fi))
                    {
                        // Получаем промежуточные точки (Shape Handle Points)
                        shapePointIds = AdaptiveComponentInstanceUtils.GetInstanceShapeHandlePointElementRefIds(fi);
                    }
                }
                catch (Exception ex) { AppErrors.LogError(ex); }

                adaptive++;
                string family = fi.Symbol?.Family?.Name ?? "?";
                string symbol = fi.Name ?? "?";
                sb.AppendLine($"● [{id.IntegerValue}] {family} / {symbol}");
                sb.AppendLine($"  Адаптивных точек: {pts.Count}");
                sb.AppendLine($"  {"#",-3} {"X, мм",12} {"Y, мм",12} {"Z, мм",12}");

                for (int i = 0; i < pts.Count; i++)
                {
                    var rp = doc.GetElement(pts[i]) as ReferencePoint;
                    if (rp == null)
                    {
                        sb.AppendLine($"  {i + 1,-3} элемент {pts[i].IntegerValue} не является ReferencePoint");
                        continue;
                    }

                    XYZ p = rp.Position ?? XYZ.Zero;
                    sb.AppendLine(
                        $"  {i + 1,-3} {p.X * FtToMm,12:F1} {p.Y * FtToMm,12:F1} {p.Z * FtToMm,12:F1}");
                }
                sb.AppendLine();

                if (shapePointIds != null && shapePointIds.Count > 0)
                {
                    sb.AppendLine($"  Промежуточных точек (Shape Handle): {shapePointIds.Count}");
                    for (int i = 0; i < shapePointIds.Count; i++)
                    {
                        var rp = doc.GetElement(shapePointIds[i]) as ReferencePoint;
                        if (rp == null) continue;
                        XYZ p = rp.Position ?? XYZ.Zero;
                        sb.AppendLine($"    {i + 1,-3} {p.X * FtToMm,12:F1} {p.Y * FtToMm,12:F1} {p.Z * FtToMm,12:F1}");
                    }
                    sb.AppendLine();
                }
            }

            if (sb.Length == 0)
            {
                TaskDialog.Show("Точки балок",
                    "Среди выделенных нет FamilyInstance — ни адаптивных, ни обычных.");
                return Result.Cancelled;
            }

            string header =
                $"Адаптивных: {adaptive}    Не адаптивных: {nonAdaptive}    " +
                $"Пропущено (не FamilyInstance): {skipped}\n" +
                new string('─', 72) + "\n";

            ShowPointsWindow("Адаптивные точки балок", header + sb);
            return Result.Succeeded;
        }

        private static void ShowPointsWindow(string title, string content)
        {
            var win = new Window
            {
                Title = title,
                Width = 720,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var tb = new System.Windows.Controls.TextBox
            {
                Text = content,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(8),
                Padding = new Thickness(6)
            };

            win.Content = tb;
            win.ShowDialog();
        }
    }
}