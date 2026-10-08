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

namespace Reinforcement
{
    /// <summary>
    /// Один снимок состояния вида: кто, когда и какие элементы были видны.
    /// </summary>
    public class MemorySnapshot
    {
        public DateTime Date { get; set; }
        public string UserName { get; set; }
        public HashSet<int> ElementIds { get; set; } = new HashSet<int>();

        public override string ToString()
            => $"{Date:yyyy-MM-dd HH:mm}  |  {UserName,-20}  |  элементов: {ElementIds.Count}";
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

            // 4) Добавляем новый снимок
            em.ViewId = viewIdValue;
            em.NameSheet = view.Name;
            em.NameDocument = doc.Title;
            em.DateLastSave = DateTime.Now;
            em.Snapshots.Add(new MemorySnapshot
            {
                Date = DateTime.Now,
                UserName = Environment.UserName,
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
                    $"Элементов сохранено: {currentIds.Count}\n" +
                    $"Снимок № {em.Snapshots.Count} от {DateTime.Now:yyyy-MM-dd HH:mm}\n",// +
                    //$"Файл: {pathInfo}",
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
                snapshot = SnapshotChooser.Choose(em.Snapshots);
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
        public static MemorySnapshot Choose(List<MemorySnapshot> snapshots)
        {
            MemorySnapshot result = null;

            var window = new Window
            {
                Title = "Выбор сохранённого состояния вида",
                Width = 560,
                Height = 340,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var listBox = new ListBox();
            foreach (var s in snapshots)
                listBox.Items.Add(s.ToString());
            listBox.SelectedIndex = snapshots.Count - 1; // по умолчанию — самый свежий
            System.Windows.Controls.Grid.SetRow(listBox, 0);
            grid.Children.Add(listBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6)
            };

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

            ok.Click += (s, e) =>
            {
                if (listBox.SelectedIndex >= 0)
                    result = snapshots[listBox.SelectedIndex];
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
            System.Windows.Controls.Grid.SetRow(panel, 1);
            grid.Children.Add(panel);

            window.Content = grid;
            window.ShowDialog();

            return result;
        }
    }
}