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
    /// <summary>
    /// Один снимок состояния вида: кто, когда и какие элементы были видны.
    /// </summary>
    public class MemorySnapshot
    {
        public DateTime Date { get; set; }
        public string UserName { get; set; }
        /// <summary>
        /// Пользовательское название/примечание. Может быть null или пустым —
        /// тогда в списке отображается только дата и автор.
        /// </summary>
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
        // Основной путь; если недоступен — берём каталог плагина.
        public static string PrimaryPath =
            @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy\MemoryViews";

        public static string FolderName = "MemoryViews";

        public int ViewId { get; set; }
        public string NameSheet { get; set; }     // имя вида/листа
        public string NameDocument { get; set; }  // имя документа
        public DateTime DateLastSave { get; set; }

        /// <summary>
        /// История снимков. Раньше был Dictionary с ключом-кортежем —
        /// System.Text.Json такое не умеет, поэтому List.
        /// </summary>
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

            string path = App_Apdater_1.targetPluginDir;
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

    // ---------------------------------------------------------------------
    //  СОХРАНЕНИЕ СОСТОЯНИЯ ВИДА
    // ---------------------------------------------------------------------
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

            // 1) Собираем все видимые элементы вида
            var allElements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            if (allElements.Count == 0) return Result.Succeeded;

            var currentIds = new HashSet<int>();
            foreach (var el in allElements)
            {
                if (el != null) currentIds.Add((int)el.Id.Value);
            }

            // 2) Читаем память
            ElementMemory em = GetElementMemory(viewIdValue);

            // 3) Если состояние совпадает с последним снимком — не плодим дубли
            var last = em.Snapshots.LastOrDefault();
            if (last != null && last.ElementIds.SetEquals(currentIds))
            {
                TaskDialog.Show("Сохранение вида",
                    $"Состояние вида «{view.Name}» не изменилось с последнего снимка.\n" +
                    $"Снимок от {last.Date:yyyy-MM-dd HH:mm} ({last.UserName}), элементов: {last.ElementIds.Count}.\n" +
                    "Новая запись не создавалась.");
                return Result.Succeeded;
            }
            // 3.5) Спрашиваем у пользователя название/примечание для снимка.
            SnapshotLabelResult labelResult = SnapshotLabelDialog.Ask(
                view.Name, doc.Title, currentIds.Count);

            if (labelResult.IsCancelled)
                return Result.Cancelled;

            string label = labelResult.Label;   // может быть null

            // 4) Добавляем новый снимок
            em.ViewId = viewIdValue;
            em.NameSheet = view.Name;
            em.NameDocument = doc.Title;
            em.DateLastSave = DateTime.Now;
            em.Snapshots.Add(new MemorySnapshot
            {
                Date = DateTime.Now,
                UserName = Environment.UserName,
                Label = label,                 // <-- добавили
                ElementIds = currentIds
            });

            // 5) Атомарная запись
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

            // 6) Сообщаем пользователю, что сохранение прошло успешно
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
                // Битый файл — начинаем с нуля, чтобы не падать
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

            // На листах/спецификациях скрывать нельзя
            if (view is ViewSheet || view is ViewSchedule)
            {
                TaskDialog.Show("Восстановление вида",
                    "На листах и спецификациях скрытие элементов недоступно.");
                return Result.Succeeded;
            }

            // Выделенные элементы не трогаем
            var selectedIdsInt = uidoc.Selection.GetElementIds()
                .Select(i => (int)i.Value).ToHashSet();

            // Текущие видимые элементы
            var allElements = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            if (allElements.Count == 0) return Result.Succeeded;

            // Память
            ElementMemory em = MemoryViewSave.GetElementMemory(viewIdValue);
            if (em.Snapshots == null || em.Snapshots.Count == 0)
            {
                TaskDialog.Show("Восстановление вида",
                    "Для этого вида ещё не сохранялось состояние.");
                return Result.Succeeded;
            }

            // Выбор снимка
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

            HashSet<int> keepIds = snapshot.ElementIds ?? new HashSet<int>();

            // Формируем список на скрытие
            List<ElementId> toHide = new List<ElementId>();
            foreach (var el in allElements)
            {
                int idCur = (int)el.Id.Value;
                if (keepIds.Contains(idCur)) continue;         // был в снимке
                if (selectedIdsInt.Contains(idCur)) continue;  // выделен — не трогаем
                if (el.IsHidden(view)) continue;               // уже скрыт

                toHide.Add(el.Id);
            }

            if (toHide.Count == 0)
            {
                TaskDialog.Show("Восстановление вида",
                    "Все элементы соответствуют сохранённому состоянию.");
                return Result.Succeeded;
            }

            using (Transaction t = new Transaction(doc, "Восстановление вида по памяти"))
            {
                t.Start();
                try
                {
                    // HideElements бросает исключение, если хотя бы один элемент
                    // скрыть нельзя. Скрываем по одному, ошибки игнорируем.
                    foreach (var id in toHide)
                    {
                        try { view.HideElements(new List<ElementId> { id }); }
                        catch { /* пропускаем некрыемые */ }
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

            TaskDialog.Show("Восстановление вида",
                $"Скрыто элементов: {toHide.Count}.\n" +
                $"Снимок: {snapshot.Date:yyyy-MM-dd HH:mm} ({snapshot.UserName}).");

            return Result.Succeeded;
        }
    }

    // ---------------------------------------------------------------------
    //  Простой WPF-диалог выбора снимка
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
                    listBox.Items.Add(s.ToString());
                if (snapshots.Count > 0)
                    listBox.SelectedIndex = snapshots.Count - 1;
                ok.IsEnabled = snapshots.Count > 0;
            }
            RefreshList();

            // Удаление выделенной строки по Delete — с реальной перезаписью файла.
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
        /// </summary>
        private static void SaveMemory(ElementMemory memory, int viewId)
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
    //  Диалог ввода названия снимка (возвращает результат-класс, без out/ref)
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