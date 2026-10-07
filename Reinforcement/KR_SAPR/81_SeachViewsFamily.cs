using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    /// <summary>
    /// Поиск вида(-ов), где используется указанное семейство.
    /// Если задан типоразмер — фильтруем ещё и по нему.
    /// Отчёт: Лист → Вид → Семейство [Типоразмер] (кол-во) + Группы.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class SeachViewsFamily : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            UIDocument uidoc = RevitAPI.UiDocument;
            // -------------------------------------------------------------------
            // 1. Пытаемся взять семейство/тип из выделения, если оно есть
            // -------------------------------------------------------------------
            var input = TryGetFromSelection(uidoc, doc, out string selInfo);

            // Если из выделения ничего не извлеклось — показываем окно
            if (input == null)
            {
                input = FamilySearchInput.Show();
                if (input == null) return Result.Cancelled;
            }

            string familyQuery = SafeTrim(input.FamilyName);
            string typeQuery = SafeTrim(input.TypeName);

            if (string.IsNullOrEmpty(familyQuery))
            {
                TaskDialog.Show("Поиск семейства", "Не задано имя семейства.");
                return Result.Cancelled;
            }
            bool hasTypeFilter = !string.IsNullOrEmpty(typeQuery);

            // -------------------------------------------------------------------
            // 2. Отбираем FamilySymbol'ы, подходящие под фильтр
            // -------------------------------------------------------------------
            var targetSymbolIds = new HashSet<ElementId>();
            var matchedTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (FamilySymbol fs in new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>())
            {
                string famName = SafeTrim(fs.FamilyName);
                if (!string.Equals(famName, familyQuery, StringComparison.OrdinalIgnoreCase))
                    continue;

                string typeName = SafeTrim(fs.Name);
                if (hasTypeFilter &&
                    !string.Equals(typeName, typeQuery, StringComparison.OrdinalIgnoreCase))
                    continue;

                targetSymbolIds.Add(fs.Id);
                matchedTypeNames.Add(typeName);
            }

            if (targetSymbolIds.Count == 0)
            {
                string msg = hasTypeFilter
                    ? $"Не найдено типоразмер «{typeQuery}» у семейства «{familyQuery}»."
                    : $"Семейство «{familyQuery}» в проекте не найдено.";
                TaskDialog.Show("Поиск семейства", msg);
                return Result.Succeeded;
            }

            // -------------------------------------------------------------------
            // 3. Собираем все экземпляры проекта и фильтруем по типу
            // -------------------------------------------------------------------
            var allInstances = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToList();

            // -------------------------------------------------------------------
            // 4. Предварительно: viewId → (имя листа, номер листа) через Viewport
            // -------------------------------------------------------------------
            var viewToSheet = new Dictionary<ElementId, (string Name, string Number)>();
            foreach (Viewport vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)))
            {
                ElementId sheetId = vp.SheetId;
                ElementId viewId = vp.ViewId;
                if (sheetId == ElementId.InvalidElementId || viewId == ElementId.InvalidElementId)
                    continue;
                if (!(doc.GetElement(sheetId) is ViewSheet sheet)) continue;
                if (!viewToSheet.ContainsKey(viewId))
                    viewToSheet[viewId] = (sheet.Name, sheet.SheetNumber);
            }

            // -------------------------------------------------------------------
            // 5. Раскладываем по структурам
            // -------------------------------------------------------------------
            var sheetData = new Dictionary<string, SheetBucket>(StringComparer.OrdinalIgnoreCase);
            var noSheetData = new Dictionary<string, ViewBucket>(StringComparer.OrdinalIgnoreCase);

            int totalInstances = 0;
            int skippedNoView = 0;

            foreach (var el in allInstances)
            {
                ElementId tid = el.GetTypeId();
                if (tid == ElementId.InvalidElementId) continue;
                if (!targetSymbolIds.Contains(tid)) continue;

                if (!(doc.GetElement(tid) is FamilySymbol sym)) continue;

                string familyName = SafeTrim(sym.FamilyName);
                string typeName = SafeTrim(sym.Name);

                // --- вид и лист ---
                string viewName = null;
                string sheetName = null;
                string sheetNumber = null;

                ElementId vid = el.OwnerViewId;
                if (vid == ElementId.InvalidElementId && el.GroupId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(el.GroupId) is Group grp)
                        vid = grp.OwnerViewId;
                }

                if (vid != ElementId.InvalidElementId)
                {
                    Element viewElem = doc.GetElement(vid);
                    if (viewElem is ViewSheet sheetDirect)
                    {
                        sheetName = sheetDirect.Name;
                        sheetNumber = sheetDirect.SheetNumber;
                        viewName = "<на листе>";
                    }
                    else if (viewElem is View v)
                    {
                        viewName = v.Name;
                        if (viewToSheet.TryGetValue(vid, out var s))
                        {
                            sheetName = s.Name;
                            sheetNumber = s.Number;
                        }
                    }
                }

                if (string.IsNullOrEmpty(viewName))
                {
                    skippedNoView++;
                    viewName = "<вид не определён>";
                }

                string groupName = null;
                if (el.GroupId != ElementId.InvalidElementId)
                    groupName = GetGroupName(doc, el);

                // --- раскладываем ---
                if (!string.IsNullOrEmpty(sheetName))
                {
                    string sheetKey = sheetNumber + "|" + sheetName;
                    if (!sheetData.TryGetValue(sheetKey, out var bucket))
                    {
                        bucket = new SheetBucket { Name = sheetName, Number = sheetNumber };
                        sheetData[sheetKey] = bucket;
                    }
                    if (!bucket.Views.TryGetValue(viewName, out var vb))
                    {
                        vb = new ViewBucket();
                        bucket.Views[viewName] = vb;
                    }
                    vb.IncrementFamilyType(familyName, typeName);
                    if (!string.IsNullOrEmpty(groupName))
                        vb.IncrementGroup(groupName);

                    bucket.TotalInstances++;
                }
                else
                {
                    if (!noSheetData.TryGetValue(viewName, out var vb))
                    {
                        vb = new ViewBucket();
                        noSheetData[viewName] = vb;
                    }
                    vb.IncrementFamilyType(familyName, typeName);
                    if (!string.IsNullOrEmpty(groupName))
                        vb.IncrementGroup(groupName);
                }

                totalInstances++;
            }

            // -------------------------------------------------------------------
            // 6. Отчёт
            // -------------------------------------------------------------------
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(selInfo))
                sb.AppendLine(selInfo);
            sb.AppendLine($"Семейство: «{familyQuery}»"
                          + (hasTypeFilter ? $"  |  типоразмер: «{typeQuery}»" : ""));
            sb.AppendLine($"Семейство: «{familyQuery}»"
                          + (hasTypeFilter ? $"  |  типоразмер: «{typeQuery}»" : ""));
            sb.AppendLine($"Найдено экземпляров: {totalInstances}");
            sb.AppendLine($"Листов с семейством: {sheetData.Count}");
            sb.AppendLine($"Видов без листа: {noSheetData.Count}");
            sb.AppendLine();

            foreach (var bucket in sheetData.Values
                .OrderBy(s => s.Number, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"=== Лист «{bucket.Name}» №{bucket.Number} (экз.: {bucket.TotalInstances}) ===");
                foreach (var viewKv in bucket.Views.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    AppendViewLine(sb, viewKv.Key, viewKv.Value);
                sb.AppendLine();
            }

            if (noSheetData.Count > 0)
            {
                sb.AppendLine("=== Виды без листа ===");
                foreach (var kv in noSheetData.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    AppendViewLine(sb, kv.Key, kv.Value);
            }

            if (skippedNoView > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Экземпляров без определённого вида: {skippedNoView}");
            }

            if (hasTypeFilter && matchedTypeNames.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine($"Внимание: у семейства несколько символов с именем «{typeQuery}» "
                            + $"({matchedTypeNames.Count}). Учтены все.");
            }

            TaskDialog td = new TaskDialog("Поиск семейства на видах");
            td.MainInstruction = hasTypeFilter
                ? $"«{familyQuery}» / «{typeQuery}» — экземпляров: {totalInstances}"
                : $"«{familyQuery}» — экземпляров: {totalInstances}";
            td.MainContent = sb.ToString();
            td.Show();

            return Result.Succeeded;
        }

        // -------------------------------------------------------------------
        // Вспомогательные
        // -------------------------------------------------------------------
        /// <summary>
        /// Пытается вытащить семейство/тип из текущего выделения.
        /// Возвращает null, если выделения нет или оно не подходит.
        /// </summary>
        private static FamilySearchInput.Result TryGetFromSelection(
            UIDocument uidoc, Document doc, out string info)
        {
            info = null;

            var ids = uidoc.Selection.GetElementIds();
            if (ids == null || ids.Count == 0) return null;

            // Ищем первый элемент, у которого есть FamilySymbol
            foreach (var id in ids)
            {
                Element el = doc.GetElement(id);
                if (el == null) continue;

                FamilySymbol sym = null;

                // 1) Сам элемент — тип (FamilySymbol)
                if (el is FamilySymbol fsDirect)
                    sym = fsDirect;
                // 2) Сам элемент — экземпляр (FamilyInstance или любой с типом-символом)
                else
                {
                    ElementId tid = el.GetTypeId();
                    if (tid != ElementId.InvalidElementId)
                        sym = doc.GetElement(tid) as FamilySymbol;
                }

                if (sym == null || sym.Family == null) continue;

                string famName = SafeTrim(sym.FamilyName);
                if (string.IsNullOrEmpty(famName)) continue;

                string typeName = SafeTrim(sym.Name);

                info = $"Из выделения: «{famName}» [{typeName}]";
                return new FamilySearchInput.Result
                {
                    FamilyName = famName,
                    TypeName = typeName
                };
            }

            return null;
        }
        private static void AppendViewLine(StringBuilder sb, string viewName, ViewBucket vb)
        {
            var famParts = new List<string>();

            foreach (var fk in vb.FamilyTypes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string fam = fk.Key;
                var types = fk.Value;
                int famTotal = types.Values.Sum();

                if (types.Count == 1)
                {
                    var t = types.First();
                    famParts.Add($"{fam} [{t.Key}] ({t.Value})");
                }
                else
                {
                    string typesStr = string.Join(", ",
                        types.OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                             .Select(t => $"{t.Key} ({t.Value})"));
                    famParts.Add($"{fam} ({famTotal}): {typesStr}");
                }
            }

            var line = new StringBuilder();
            line.Append($"  Вид «{viewName}» — {string.Join("; ", famParts)}");

            if (vb.Groups.Count > 0)
            {
                var grpList = vb.Groups
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Value > 1 ? $"{g.Key} ({g.Value})" : g.Key);
                line.Append($" | Группы: {string.Join(", ", grpList)}");
            }

            sb.AppendLine(line.ToString());
        }

        private sealed class ViewBucket
        {
            // family → (type → count)
            public Dictionary<string, Dictionary<string, int>> FamilyTypes =
                new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, int> Groups =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public void IncrementFamilyType(string family, string type)
            {
                if (!FamilyTypes.TryGetValue(family, out var types))
                {
                    types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    FamilyTypes[family] = types;
                }
                types.TryGetValue(type, out int c);
                types[type] = c + 1;
            }

            public void IncrementGroup(string name)
            {
                Groups.TryGetValue(name, out int c);
                Groups[name] = c + 1;
            }
        }

        private sealed class SheetBucket
        {
            public string Name;
            public string Number;
            public Dictionary<string, ViewBucket> Views =
                new Dictionary<string, ViewBucket>(StringComparer.OrdinalIgnoreCase);
            public int TotalInstances;
        }

        // -------------------------------------------------------------------
        // Служебные — как в ListDuplicateFamiliesByView
        // -------------------------------------------------------------------

        /// <summary>Имя группы, в которой находится элемент. null — если нет.</summary>
        private static string GetGroupName(Document doc, Element elem)
        {
            try
            {
                ElementId gid = elem.GroupId;
                if (gid == null || gid == ElementId.InvalidElementId) return null;

                if (doc.GetElement(gid) is Group grp)
                {
                    if (grp.GroupType != null && !string.IsNullOrEmpty(grp.GroupType.Name))
                        return grp.GroupType.Name;
                    if (!string.IsNullOrEmpty(grp.Name))
                        return grp.Name;
                }
                return $"<группа Id {gid.IntegerValue}>";
            }
            catch { }
            return null;
        }

        private static readonly char[] TrimChars = new[]
        {
            ' ', '\t', '\r', '\n', '\f', '\v',
            '\u00A0', '\uFEFF',
            '\u200B', '\u200C', '\u200D', '\u2060'
        };

        private static string SafeTrim(string s)
            => string.IsNullOrEmpty(s) ? (s ?? "") : s.Trim(TrimChars);
    }

    // -------------------------------------------------------------------
    // Диалог ввода: семейство + (опционально) типоразмер
    // -------------------------------------------------------------------
    internal static class FamilySearchInput
    {
        public sealed class Result
        {
            public string FamilyName;
            public string TypeName;
        }

        public static Result Show()
        {
            Result result = null;

            var window = new Window
            {
                Title = "Поиск семейства на видах",
                Width = 460,
                Height = 210,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 4; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Семейство
            var lblFam = new TextBlock
            {
                Text = "Семейство:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };
            var txtFam = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Controls.Grid.SetRow(lblFam, 0); System.Windows.Controls.Grid.SetColumn(lblFam, 0);
            System.Windows.Controls.Grid.SetRow(txtFam, 0); System.Windows.Controls.Grid.SetColumn(txtFam, 1);
            grid.Children.Add(lblFam);
            grid.Children.Add(txtFam);

            // Типоразмер
            var lblType = new TextBlock
            {
                Text = "Типоразмер:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };
            var txtType = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Controls.Grid.SetRow(lblType, 1); System.Windows.Controls.Grid.SetColumn(lblType, 0);
            System.Windows.Controls.Grid.SetRow(txtType, 1); System.Windows.Controls.Grid.SetColumn(txtType, 1);
            grid.Children.Add(lblType);
            grid.Children.Add(txtType);

            // Подсказка
            var hint = new TextBlock
            {
                Text = "Типоразмер можно оставить пустым — будут показаны все типы семейства.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 4)
            };
            System.Windows.Controls.Grid.SetRow(hint, 2); System.Windows.Controls.Grid.SetColumn(hint, 0); System.Windows.Controls.Grid.SetColumnSpan(hint, 2);
            grid.Children.Add(hint);

            // Кнопки
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var ok = new Button { Content = "Найти", Width = 100, Margin = new Thickness(4), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Width = 80, Margin = new Thickness(4), IsCancel = true };

            ok.Click += (s, e) =>
            {
                result = new Result
                {
                    FamilyName = txtFam.Text,
                    TypeName = txtType.Text
                };
                window.DialogResult = true;
                window.Close();
            };
            cancel.Click += (s, e) =>
            {
                window.DialogResult = false;
                window.Close();
            };

            panel.Children.Add(ok);
            panel.Children.Add(cancel);
            System.Windows.Controls.Grid.SetRow(panel, 3); System.Windows.Controls.Grid.SetColumn(panel, 0); System.Windows.Controls.Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);

            window.Content = grid;
            window.ShowDialog();

            return result;
        }
    }
}