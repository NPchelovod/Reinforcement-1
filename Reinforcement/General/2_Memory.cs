using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static Reinforcement.SnapshotChooser;

namespace Reinforcement
{
    public class MemorySnapshot
    {
        public DateTime Date { get; set; }
        public string UserName { get; set; }
        public string NameDocument { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public HashSet<int> ElementIds { get; set; } = new HashSet<int>();

        public override string ToString()
        {
            string core = $"{Date:yyyy-MM-dd HH:mm}  |  {UserName,-20}  |  элементов: {ElementIds.Count}";
            return string.IsNullOrWhiteSpace(Label)
                ? core
                : $"[{Label}]  " + core;
        }
    }

    public class ElementMemory
    {
        //если нет директории - сохраняет во временную
        public static string PrimaryPath =
            @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy\MemoryViews";

        public static string FolderName = "MemoryViews";

        public int ViewId { get; set; }
        public string NameSheet { get; set; }
        public string NameDocument { get; set; }
        public DateTime DateLastSave { get; set; }

        public List<MemorySnapshot> Snapshots { get; set; } = new List<MemorySnapshot>();

        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        public static string GetPath()
        {
            if (Directory.Exists(PrimaryPath))
                return PrimaryPath;

            string path = App_Folders.TargetPluginDir;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return null;

            path = Path.Combine(path, FolderName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string FilePath(int viewId)
        {
            string dir = GetPath();
            if (string.IsNullOrEmpty(dir))
                return null;

            return Path.Combine(dir, viewId + ".json");
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class MemoryViewSave : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            if (view == null) return Result.Succeeded;

            int viewIdValue = (int)view.Id.Value;
            if (viewIdValue <= 0) return Result.Succeeded;

            var allElements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            if (allElements.Count == 0) return Result.Succeeded;

            var currentIds = new HashSet<int>();
            foreach (var el in allElements)
            {
                if (el != null) currentIds.Add((int)el.Id.Value);
            }

            ElementMemory em = GetElementMemory(viewIdValue);

            var last = em.Snapshots.LastOrDefault();
            if (last != null && last.ElementIds.SetEquals(currentIds))
            {
                TaskDialog.Show("Сохранение вида",
                    $"Состояние вида «{view.Name}» не изменилось с последнего снимка.\n" +
                    $"Снимок от {last.Date:yyyy-MM-dd HH:mm} ({last.UserName}), элементов: {last.ElementIds.Count}.\n" +
                    "Новая запись не создавалась.");
                return Result.Succeeded;
            }

            SnapshotLabelResult labelResult = SnapshotLabelDialog.Ask(
                view.Name, doc.Title, currentIds.Count);

            if (labelResult.IsCancelled)
                return Result.Cancelled;

            string label = labelResult.Label;

            em.ViewId = viewIdValue;
            em.NameSheet = view.Name;
            em.NameDocument = doc.Title;
            em.DateLastSave = DateTime.Now;
            em.Snapshots.Add(new MemorySnapshot
            {
                Date = DateTime.Now,
                UserName = Environment.UserName,
                NameDocument = doc.Title,
                Label = label,
                ElementIds = currentIds
            });

            string filePath = ElementMemory.FilePath(viewIdValue);
            if (string.IsNullOrEmpty(filePath))
            {
                message = "Не удалось определить путь для файла памяти вида.";
                return Result.Failed;
            }

            try
            {
                string json = JsonSerializer.Serialize(em, ElementMemory.Options);
                string tmp = filePath + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(filePath))
                    File.Replace(tmp, filePath, destinationBackupFileName: null,
                                 ignoreMetadataErrors: true);
                else
                    File.Move(tmp, filePath);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Сохранение вида",
                    $"Не удалось сохранить состояние вида:\n{ex.Message}");
                return Result.Failed;
            }

            string pathInfo = filePath;
            TaskDialog dialog = new TaskDialog("Сохранение вида")
            {
                MainInstruction = "Состояние вида сохранено",
                MainContent =
                    $"Вид: {view.Name}\n" +
                    $"Документ: {doc.Title}\n" +
                    (string.IsNullOrWhiteSpace(label) ? "" : $"Название: {label}\n") +
                    $"Элементов сохранено: {currentIds.Count}\n" +
                    $"Снимок № {em.Snapshots.Count} от {DateTime.Now:yyyy-MM-dd HH:mm}\n",
                CommonButtons = TaskDialogCommonButtons.Ok,
                ExpandedContent = em.Snapshots.Count == 1
                    ? "Это первый снимок для данного вида."
                    : $"Всего снимков для вида: {em.Snapshots.Count}.\n" +
                      $"Предыдущий: {em.Snapshots[em.Snapshots.Count - 2].Date:yyyy-MM-dd HH:mm} " +
                      $"({em.Snapshots[em.Snapshots.Count - 2].UserName})."
            };
            dialog.Show();

            return Result.Succeeded;
        }

        public static ElementMemory GetElementMemory(int viewId)
        {
            string target = ElementMemory.FilePath(viewId);
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
                return new ElementMemory { ViewId = viewId };

            try
            {
                string json = File.ReadAllText(target);
                if (string.IsNullOrWhiteSpace(json))
                    return new ElementMemory { ViewId = viewId };

                var data = JsonSerializer.Deserialize<ElementMemory>(json, ElementMemory.Options);
                if (data == null) return new ElementMemory { ViewId = viewId };
                if (data.Snapshots == null) data.Snapshots = new List<MemorySnapshot>();
                return data;
            }
            catch
            {
                return new ElementMemory { ViewId = viewId };
            }
        }
    }

    // ---------------------------------------------------------------------
    //  ВОССТАНОВЛЕНИЕ СОСТОЯНИЯ ВИДА (скрытие «лишних» элементов)
    // ---------------------------------------------------------------------
    [Transaction(TransactionMode.Manual)]
    public class MemoryViewCorrect : IExternalCommand
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
            View view = doc.ActiveView;
            if (view == null) return Result.Succeeded;

            int viewIdValue = (int)view.Id.Value;
            if (viewIdValue <= 0) return Result.Succeeded;

            if (view is ViewSheet || view is ViewSchedule)
            {
                TaskDialog.Show("Восстановление вида",
                    "На листах и спецификациях скрытие элементов недоступно.");
                return Result.Succeeded;
            }

            ElementMemory em = MemoryViewSave.GetElementMemory(viewIdValue);
            if (em.Snapshots == null || em.Snapshots.Count == 0)
            {
                TaskDialog.Show("Восстановление вида",
                    "Для этого вида ещё не сохранялось состояние.");
                return Result.Succeeded;
            }

            var selectedIdsInt = uidoc.Selection.GetElementIds()
                .Select(i => (int)i.Value).ToHashSet();

            var allElements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            MemorySnapshot snapshot;
            if (em.Snapshots.Count == 1)
            {
                snapshot = em.Snapshots[0];
            }
            else
            {
                snapshot = SnapshotChooser.Choose(em, viewIdValue);
                if (snapshot == null) return Result.Cancelled;
            }

            if (snapshot.ElementIds == null)
                snapshot.ElementIds = new HashSet<int>();
            HashSet<int> keepIds = snapshot.ElementIds;

            // ---- NEW: предложить добавить выделенные элементы в память снимка ----
            int addedToMemory = 0;
            var selectedNotInSnapshot = selectedIdsInt
                .Where(id => !keepIds.Contains(id))
                .ToList();

            if (selectedNotInSnapshot.Count > 0)
            {
                TaskDialog addDialog = new TaskDialog("Добавление в память вида")
                {
                    MainInstruction = "Добавить выделенные элементы в память снимка?",
                    MainContent =
                        $"В текущем выделении {selectedNotInSnapshot.Count} элем., " +
                        "которых нет в выбранном снимке.\n\n" +
                        $"Да — добавить их в снимок от " +
                        $"{snapshot.Date:yyyy-MM-dd HH:mm} ({snapshot.UserName}).\n" +
                        "Нет — оставить снимок без изменений.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };

                if (addDialog.Show() == TaskDialogResult.Yes)
                {
                    foreach (int id in selectedNotInSnapshot)
                        keepIds.Add(id);

                    SnapshotChooser.SaveMemory(em, viewIdValue);
                    addedToMemory = selectedNotInSnapshot.Count;
                }
            }
            // ---- /NEW ----

            // ---- 1. Что скрыть ----
            List<ElementId> toHide = new List<ElementId>();
            foreach (var el in allElements)
            {
                int idCur = (int)el.Id.Value;
                if (keepIds.Contains(idCur)) continue;
                if (selectedIdsInt.Contains(idCur)) continue;
                if (el.IsHidden(view)) continue;
                toHide.Add(el.Id);
            }

            // ---- 2. Что показать ----
            List<ElementId> toUnhide = new List<ElementId>();
            foreach (int idInt in keepIds)
            {
                if (selectedIdsInt.Contains(idInt)) continue;

                ElementId eid = new ElementId(idInt);
                Element el = doc.GetElement(eid);
                if (el == null) continue;

                try
                {
                    if (el.IsHidden(view))
                        toUnhide.Add(eid);
                }
                catch
                {
                }
            }

            if (toHide.Count == 0 && toUnhide.Count == 0)
            {
                string nothingReport = "";
                if (addedToMemory > 0)
                    nothingReport += $"Добавлено в память снимка: {addedToMemory}.\n";
                nothingReport += "Все элементы соответствуют сохранённому состоянию.";

                TaskDialog.Show("Восстановление вида", nothingReport);
                return Result.Succeeded;
            }

            // ---- 3. Спрашиваем про скрытые из снимка ----
            bool doUnhide = false;
            if (toUnhide.Count > 0)
            {
                TaskDialog askDialog = new TaskDialog("Восстановление вида")
                {
                    MainInstruction =
                        $"Восстановить {toUnhide.Count} скрытых элементов?",
                    MainContent =
                        "В сохранённом снимке эти элементы были видимы, " +
                        "а сейчас они скрыты.\n\n" +
                        $"Да — сделать видимыми {toUnhide.Count} элем.\n" +
                        "Нет — оставить их скрытыми.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };
                doUnhide = askDialog.Show() == TaskDialogResult.Yes;
            }

            if (toHide.Count == 0 && !doUnhide)
            {
                string skipReport = "";
                if (addedToMemory > 0)
                    skipReport += $"Добавлено в память снимка: {addedToMemory}.\n";
                skipReport +=
                    $"Скрывать нечего.\n" +
                    $"Скрытых элементов из снимка: {toUnhide.Count} — по вашему выбору оставлены скрытыми.\n" +
                    $"Снимок: {snapshot.Date:yyyy-MM-dd HH:mm} ({snapshot.UserName}).";

                TaskDialog.Show("Восстановление вида", skipReport);
                return Result.Succeeded;
            }

            int hiddenCount = 0;
            int unhiddenCount = 0;

            using (Transaction t = new Transaction(doc, "Восстановление вида по памяти"))
            {
                t.Start();
                try
                {
                    foreach (var id in toHide)
                    {
                        try { view.HideElements(new List<ElementId> { id }); hiddenCount++; }
                        catch { }
                    }

                    if (doUnhide && toUnhide.Count > 0)
                    {
                        foreach (var id in toUnhide)
                        {
                            try { view.UnhideElements(new List<ElementId> { id }); unhiddenCount++; }
                            catch { }
                        }
                    }

                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    message = ex.Message;
                    return Result.Failed;
                }
            }

            // ---- 4. Отчёт ----
            string report = "";
            if (addedToMemory > 0)
                report += $"Добавлено в память снимка: {addedToMemory}.\n";
            if (hiddenCount > 0)
                report += $"Скрыто элементов: {hiddenCount}.\n";
            if (unhiddenCount > 0)
                report += $"Восстановлено видимость: {unhiddenCount}.\n";

            if (doUnhide && toUnhide.Count > 0 && unhiddenCount < toUnhide.Count)
            {
                report +=
                    $"Не удалось показать: {toUnhide.Count - unhiddenCount} " +
                    "(скрыты категорией, фильтром или рабочей группой).\n";
            }

            report += $"Снимок: {snapshot.Date:yyyy-MM-dd HH:mm} ({snapshot.UserName}).";
            if (!string.IsNullOrWhiteSpace(snapshot.NameDocument))
                report += $"\nДокумент: {snapshot.NameDocument}";

            TaskDialog.Show("Восстановление вида", report);

            return Result.Succeeded;
        }
    }

    // ---------------------------------------------------------------------
    //  Диалог выбора снимка
    // ---------------------------------------------------------------------
    internal static class SnapshotChooser
    {
        public static MemorySnapshot Choose(ElementMemory memory, int viewId)
        {
            MemorySnapshot result = null;
            var snapshots = memory.Snapshots ?? new List<MemorySnapshot>();

            var window = new Window
            {
                Title = "Выбор сохранённого состояния вида",
                Width = 620,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var hint = new TextBlock
            {
                Text = "Выделите строку и нажмите Delete, чтобы удалить снимок из файла памяти вида.",
                Margin = new Thickness(8, 8, 8, 4),
                Foreground = System.Windows.Media.Brushes.DimGray,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            };
            System.Windows.Controls.Grid.SetRow(hint, 0);
            grid.Children.Add(hint);

            var listBox = new ListBox { Margin = new Thickness(8, 0, 8, 0) };
            System.Windows.Controls.Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6)
            };
            System.Windows.Controls.Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            var ok = new Button
            {
                Content = "Восстановить",
                Width = 120,
                Margin = new Thickness(4),
                IsDefault = true
            };
            var cancel = new Button
            {
                Content = "Отмена",
                Width = 90,
                Margin = new Thickness(4),
                IsCancel = true
            };
            panel.Children.Add(ok);
            panel.Children.Add(cancel);

            void RefreshList()
            {
                listBox.Items.Clear();
                foreach (var s in snapshots)
                {
                    listBox.Items.Add(new ListBoxItem
                    {
                        Content = s.ToString(),
                        ToolTip =
                            $"Дата: {s.Date:yyyy-MM-dd HH:mm}\n" +
                            $"Автор: {s.UserName}\n" +
                            $"Документ: {(string.IsNullOrWhiteSpace(s.NameDocument) ? "<не сохранён>" : s.NameDocument)}\n" +
                            $"Элементов: {s.ElementIds.Count}" +
                            (string.IsNullOrWhiteSpace(s.Label) ? "" : $"\nНазвание: {s.Label}")
                    });
                }
                if (snapshots.Count > 0)
                    listBox.SelectedIndex = snapshots.Count - 1;
                ok.IsEnabled = snapshots.Count > 0;
            }
            RefreshList();

            window.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Delete) return;
                int idx = listBox.SelectedIndex;
                if (idx < 0 || idx >= snapshots.Count) return;

                var toRemove = snapshots[idx];
                var confirm = MessageBox.Show(window,
                    $"Удалить снимок от {toRemove.Date:yyyy-MM-dd HH:mm} ({toRemove.UserName})?\n\n" +
                    "Изменение будет немедленно записано в файл памяти вида. Действие необратимо.",
                    "Удаление снимка",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                e.Handled = true;
                if (confirm != MessageBoxResult.Yes) return;

                snapshots.RemoveAt(idx);
                memory.Snapshots = snapshots;
                SaveMemory(memory, viewId);
                RefreshList();

                if (snapshots.Count == 0)
                {
                    MessageBox.Show(window,
                        "Все снимки для этого вида удалены. Файл памяти вида стёрт.",
                        "Удаление снимков",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    window.DialogResult = false;
                    window.Close();
                }
            };

            ok.Click += (s, e) =>
            {
                if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex < snapshots.Count)
                    result = snapshots[listBox.SelectedIndex];
                window.DialogResult = true;
                window.Close();
            };
            cancel.Click += (s, e) =>
            {
                window.DialogResult = false;
                window.Close();
            };

            window.Content = grid;
            window.ShowDialog();

            return result;
        }

        /// <summary>
        /// Перезаписывает JSON-файл памяти вида. Если снимков не осталось — удаляет файл.
        /// Сделан public, чтобы MemoryViewCorrect мог сохранять изменения после
        /// добавления в память выделенных элементов.
        /// </summary>
        public static void SaveMemory(ElementMemory memory, int viewId)
        {
            string path = ElementMemory.FilePath(viewId);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (memory.Snapshots == null || memory.Snapshots.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                string json = JsonSerializer.Serialize(memory, ElementMemory.Options);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                    File.Replace(tmp, path, destinationBackupFileName: null,
                                 ignoreMetadataErrors: true);
                else
                    File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось сохранить изменения в файл памяти вида:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ---------------------------------------------------------------------
    //  Диалог ввода названия снимка
    // ---------------------------------------------------------------------
    internal sealed class SnapshotLabelResult
    {
        public bool IsCancelled { get; private set; }
        public string Label { get; private set; }

        public static SnapshotLabelResult Cancel()
            => new SnapshotLabelResult { IsCancelled = true };

        public static SnapshotLabelResult Ok(string label)
            => new SnapshotLabelResult { Label = label };
    }

    internal static class SnapshotLabelDialog
    {
        public static SnapshotLabelResult Ask(
            string viewName, string docTitle, int elementCount)
        {
            SnapshotLabelResult result = SnapshotLabelResult.Cancel();

            var window = new Window
            {
                Title = "Название снимка",
                Width = 480,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock
            {
                Margin = new Thickness(10, 10, 10, 4),
                TextWrapping = TextWrapping.Wrap,
                Text =
                    $"Вид: {viewName}\n" +
                    $"Документ: {docTitle}\n" +
                    $"Элементов: {elementCount}"
            };
            System.Windows.Controls.Grid.SetRow(info, 0);
            grid.Children.Add(info);

            var prompt = new TextBlock
            {
                Text = "Название / примечание (необязательно):",
                Margin = new Thickness(10, 6, 10, 2)
            };
            System.Windows.Controls.Grid.SetRow(prompt, 1);
            grid.Children.Add(prompt);

            var textBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(10, 0, 10, 8),
                VerticalAlignment = VerticalAlignment.Top,
                AcceptsReturn = false,
                MaxLength = 120
            };
            System.Windows.Controls.Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6)
            };
            System.Windows.Controls.Grid.SetRow(panel, 3);
            grid.Children.Add(panel);

            var ok = new Button
            {
                Content = "Сохранить",
                Width = 110,
                Margin = new Thickness(4),
                IsDefault = true
            };
            var skip = new Button
            {
                Content = "Без названия",
                Width = 130,
                Margin = new Thickness(4)
            };
            var cancel = new Button
            {
                Content = "Отмена",
                Width = 90,
                Margin = new Thickness(4),
                IsCancel = true
            };
            panel.Children.Add(ok);
            panel.Children.Add(skip);
            panel.Children.Add(cancel);

            ok.Click += (s, e) =>
            {
                string text = (textBox.Text ?? "").Trim();
                result = SnapshotLabelResult.Ok(string.IsNullOrEmpty(text) ? null : text);
                window.DialogResult = true;
                window.Close();
            };
            skip.Click += (s, e) =>
            {
                result = SnapshotLabelResult.Ok(null);
                window.DialogResult = true;
                window.Close();
            };
            cancel.Click += (s, e) =>
            {
                result = SnapshotLabelResult.Cancel();
                window.DialogResult = false;
                window.Close();
            };

            window.Content = grid;
            window.Loaded += (s, e) => textBox.Focus();
            window.ShowDialog();

            return result;
        }
    }
}