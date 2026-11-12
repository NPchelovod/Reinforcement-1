using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Wpf = System.Windows;   // алиас, чтобы не путать с Autodesk.Revit.DB.Window

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class RoundElementPositions : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp?.ActiveUIDocument;
            if (uidoc == null) { message = "Нет активного документа."; return Result.Failed; }
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds;
            try { selectedIds = uidoc.Selection.GetElementIds(); }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }

            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("Округление позиций", "Сначала выберите элементы.");
                return Result.Cancelled;
            }

            int stepMm;
            bool roundZ;
            if (!ShowSettingsDialog(out stepMm, out roundZ))
                return Result.Cancelled;

            // Шаг округления в футах (внутренние единицы Revit)
            double stepFeet = UnitUtils.ConvertToInternalUnits(stepMm, UnitTypeId.Millimeters);

            int moved = 0, skippedPinned = 0, skippedGroup = 0, skippedNoGeom = 0;
            var errors = new List<string>();

            using (var tx = new Transaction(doc, "Округление позиций элементов"))
            {
                tx.Start();

                foreach (var id in selectedIds)
                {
                    Element el;
                    try { el = doc.GetElement(id); }
                    catch { continue; }
                    if (el == null) continue;

                    if (el.Pinned) { skippedPinned++; continue; }

                    // Член группы — пропускаем: иначе сдвинется вся группа
                    if (el.GroupId != ElementId.InvalidElementId)
                    {
                        skippedGroup++;
                        continue;
                    }

                    XYZ center = GetElementCenter(el);
                    if (center == null) { skippedNoGeom++; continue; }

                    double dx = RoundToStep(center.X, stepFeet) - center.X;
                    double dy = RoundToStep(center.Y, stepFeet) - center.Y;
                    double dz = roundZ
                        ? RoundToStep(center.Z, stepFeet) - center.Z
                        : 0.0;

                    // Порог: < 1e-6 фута (~0.0003 мм) — считаем «уже на месте»
                    if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6 && Math.Abs(dz) < 1e-6)
                        continue;

                    try
                    {
                        ElementTransformUtils.MoveElement(doc, el.Id, new XYZ(dx, dy, dz));
                        moved++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"ID {el.Id}: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            // ---- Отчёт ----
            var sb = new StringBuilder();
            sb.AppendLine($"Шаг округления: {stepMm} мм");
            sb.AppendLine($"Округление Z: {(roundZ ? "да" : "нет")}");
            sb.AppendLine();
            sb.AppendLine($"Перемещено: {moved}");
            if (skippedPinned > 0) sb.AppendLine($"Пропущено (закреплено): {skippedPinned}");
            if (skippedGroup > 0) sb.AppendLine($"Пропущено (в группе): {skippedGroup}");
            if (skippedNoGeom > 0) sb.AppendLine($"Пропущено (нет геометрии): {skippedNoGeom}");
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Ошибок при перемещении: {errors.Count}");
                foreach (var e in errors.Take(10)) sb.AppendLine("  " + e);
                if (errors.Count > 10) sb.AppendLine("  ...");
            }
            TaskDialog.Show("Округление позиций", sb.ToString());

            return Result.Succeeded;
        }

        /// <summary>
        /// Центр bounding box в координатах модели. Универсально работает
        /// для стен, перекрытий, колонн и т.д. Fallback на LocationPoint /
        /// середину LocationCurve — для элементов без видимой геометрии.
        /// </summary>
        private static XYZ GetElementCenter(Element el)
        {
            BoundingBoxXYZ bb = null;
            try { bb = el.get_BoundingBox(null); } catch { }
            if (bb != null && bb.Min != null && bb.Max != null)
                return (bb.Min + bb.Max) * 0.5;

            if (el.Location is LocationPoint lp && lp.Point != null)
                return lp.Point;

            if (el.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Evaluate(0.5, true);

            return null;
        }

        private static double RoundToStep(double value, double step)
        {
            if (step <= 0) return value;
            // AwayFromZero — «обычное» округление, ожидаемое пользователем
            // (1.5 → 2, −1.5 → −2). Default Math.Round использует ToEven.
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        // ---- Простой WPF-диалог ----
        private static bool ShowSettingsDialog(out int stepMm, out bool roundZ)
        {
            stepMm = 1;
            roundZ = false;

            var window = new Wpf.Window
            {
                Title = "Округление позиций элементов",
                Width = 360,
                Height = 230,
                WindowStartupLocation = Wpf.WindowStartupLocation.CenterScreen,
                ResizeMode = Wpf.ResizeMode.NoResize
            };

            var sp = new Wpf.Controls.StackPanel { Margin = new Wpf.Thickness(15) };

            sp.Children.Add(new Wpf.Controls.TextBlock
            {
                Text = "Округлить координаты центра элементов до кратного (мм):",
                TextWrapping = Wpf.TextWrapping.Wrap,
                Margin = new Wpf.Thickness(0, 0, 0, 8)
            });

            var tb = new Wpf.Controls.TextBox { Text = "1", Margin = new Wpf.Thickness(0, 0, 0, 12) };
            sp.Children.Add(tb);

            var cbZ = new Wpf.Controls.CheckBox
            {
                Content = "Округлять также Z (высоту)",
                IsChecked = false,
                Margin = new Wpf.Thickness(0, 0, 0, 15)
            };
            sp.Children.Add(cbZ);

            var buttons = new Wpf.Controls.StackPanel
            {
                Orientation = Wpf.Controls.Orientation.Horizontal,
                HorizontalAlignment = Wpf.HorizontalAlignment.Right
            };
            var okBtn = new Wpf.Controls.Button
            {
                Content = "OK",
                Width = 80,
                IsDefault = true,
                Margin = new Wpf.Thickness(0, 0, 6, 0)
            };
            var cancelBtn = new Wpf.Controls.Button { Content = "Отмена", Width = 80, IsCancel = true };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            sp.Children.Add(buttons);

            window.Content = sp;

            int resultStep = -1;
            bool resultZ = false;

            okBtn.Click += (s, e) =>
            {
                if (!int.TryParse(tb.Text?.Trim(), out int v) || v < 1 || v > 10)
                {
                    Wpf.MessageBox.Show("Введите целое число от 1 до 10.", "Ошибка",
                        Wpf.MessageBoxButton.OK, Wpf.MessageBoxImage.Warning);
                    return;
                }
                resultStep = v;
                resultZ = cbZ.IsChecked == true;
                window.DialogResult = true;
            };

            bool? r = window.ShowDialog();
            if (r == true)
            {
                stepMm = resultStep;
                roundZ = resultZ;
                return true;
            }
            return false;
        }
    }
}