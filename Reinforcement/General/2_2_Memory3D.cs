using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    /// <summary>Один снимок границ 3D вида.</summary>
    public class SectionBoxSnapshot
    {
        public DateTime Date { get; set; }
        public string UserName { get; set; }
        public string Label { get; set; } = string.Empty;

        /// <summary>Были ли границы включены на момент снимка.</summary>
        public bool IsActive { get; set; }

        public double[] Min { get; set; }
        public double[] Max { get; set; }

        // Transform: Origin + BasisX/Y/Z. Каждый — массив из 3 координат.
        public double[] TOrigin { get; set; }
        public double[] TBasisX { get; set; }
        public double[] TBasisY { get; set; }
        public double[] TBasisZ { get; set; }

        public override string ToString()
        {
            string core = $"{Date:yyyy-MM-dd HH:mm}  |  {UserName,-20}  |  {(IsActive ? "вкл" : "выкл")}";
            return string.IsNullOrWhiteSpace(Label) ? core : $"[{Label}]  " + core;
        }
    }

    public class SectionBoxMemory
    {
        public static string PrimaryPath =
            @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy\MemoryViews";

        public static string FolderName = "Memory3DSectionBoxes";

        public int ViewId { get; set; }
        public string NameView { get; set; }
        public string NameDocument { get; set; }
        public DateTime DateLastSave { get; set; }

        public List<SectionBoxSnapshot> Snapshots { get; set; } = new List<SectionBoxSnapshot>();

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

        /// <summary>Актуальный путь: {id}_border.json</summary>
        public static string FilePath(int viewId)
        {
            string dir = GetPath();
            if (string.IsNullOrEmpty(dir)) return null;
            return Path.Combine(dir, viewId + "_border.json");
        }
        /// <summary>Старый путь: {id}.json (только для чтения/удаления)</summary>
        public static string LegacyFilePath(int viewId)
        {
            string dir = GetPath();
            if (string.IsNullOrEmpty(dir)) return null;
            return Path.Combine(dir, viewId + ".json");
        }


        /// <summary>
        /// Возвращает путь к существующему файлу памяти:
        /// сначала новый {id}_border.json, если его нет — старый {id}.json.
        /// Если ничего нет — null.
        /// </summary>
        public static string FindExistingPath(int viewId)
        {
            string newPath = FilePath(viewId);
            if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
                return newPath;

            string oldPath = LegacyFilePath(viewId);
            if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
                return oldPath;

            return null;
        }

        // --- утилиты сериализации ---
        public static double[] ToArray(XYZ p)
            => p == null ? null : new[] { p.X, p.Y, p.Z };

        public static XYZ FromArray(double[] a)
            => (a == null || a.Length != 3) ? null : new XYZ(a[0], a[1], a[2]);
    }

    [Transaction(TransactionMode.Manual)]
    public class SectionBoxSave : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Границы 3D вида",
                    "Откройте ортогональный 3D вид (перспективный не поддерживается).");
                return Result.Cancelled;
            }

            int viewIdValue = (int)view3D.Id.Value;

            // Читаем текущие границы
            BoundingBoxXYZ box = view3D.GetSectionBox();
            if (box == null)
            {
                TaskDialog.Show("Границы 3D вида", "У вида нет границ (Section Box).");
                return Result.Succeeded;
            }

            SectionBoxMemory mem = GetMemory(viewIdValue);

            // Спрашиваем название
            SectionBoxLabelResult labelResult =
                SectionBoxLabelDialog.Ask(view3D.Name, doc.Title, view3D.IsSectionBoxActive);
            if (labelResult.IsCancelled) return Result.Cancelled;

            // Добавляем снимок
            mem.ViewId = viewIdValue;
            mem.NameView = view3D.Name;
            mem.NameDocument = doc.Title;
            mem.DateLastSave = DateTime.Now;
            mem.Snapshots.Add(new SectionBoxSnapshot
            {
                Date = DateTime.Now,
                UserName = Environment.UserName,
                Label = labelResult.Label,
                IsActive = view3D.IsSectionBoxActive,
                Min = SectionBoxMemory.ToArray(box.Min),
                Max = SectionBoxMemory.ToArray(box.Max),
                TOrigin = SectionBoxMemory.ToArray(box.Transform?.Origin),
                TBasisX = SectionBoxMemory.ToArray(box.Transform?.BasisX),
                TBasisY = SectionBoxMemory.ToArray(box.Transform?.BasisY),
                TBasisZ = SectionBoxMemory.ToArray(box.Transform?.BasisZ)
            });

            string filePath = SectionBoxMemory.FilePath(viewIdValue);
            if (string.IsNullOrEmpty(filePath))
            {
                message = "Не удалось определить путь для файла памяти границ.";
                return Result.Failed;
            }

            try
            {
                string json = JsonSerializer.Serialize(mem, SectionBoxMemory.Options);
                string tmp = filePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(filePath))
                    File.Replace(tmp, filePath, null, true);
                else
                    File.Move(tmp, filePath);

                // Убираем устаревший файл старого формата {id}.json,
                // чтобы данные не задваивались.
                string legacy = SectionBoxMemory.LegacyFilePath(viewIdValue);
                if (!string.IsNullOrEmpty(legacy) && File.Exists(legacy))
                {
                    try { File.Delete(legacy); } catch { /* не критично */ }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Сохранение границ", $"Ошибка записи:\n{ex.Message}");
                return Result.Failed;
            }

            TaskDialog.Show("Границы 3D вида",
                $"Границы сохранены.\nСнимок № {mem.Snapshots.Count} от {DateTime.Now:yyyy-MM-dd HH:mm}." +
                (string.IsNullOrWhiteSpace(labelResult.Label) ? "" : $"\nНазвание: {labelResult.Label}"));

            return Result.Succeeded;
        }

        public static SectionBoxMemory GetMemory(int viewId)
        {
            string target = SectionBoxMemory.FindExistingPath(viewId);
            if (string.IsNullOrEmpty(target))
                return new SectionBoxMemory { ViewId = viewId };

            try
            {
                string json = File.ReadAllText(target);
                if (string.IsNullOrWhiteSpace(json)) return new SectionBoxMemory { ViewId = viewId };
                var data = JsonSerializer.Deserialize<SectionBoxMemory>(json, SectionBoxMemory.Options);
                if (data == null) return new SectionBoxMemory { ViewId = viewId };
                if (data.Snapshots == null) data.Snapshots = new List<SectionBoxSnapshot>();
                return data;
            }
            catch { return new SectionBoxMemory { ViewId = viewId }; }
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class SectionBoxCorrect : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Границы 3D вида", "Откройте ортогональный 3D вид.");
                return Result.Cancelled;
            }

            int viewIdValue = (int)view3D.Id.Value;
            SectionBoxMemory mem = SectionBoxSave.GetMemory(viewIdValue);
            if (mem.Snapshots == null || mem.Snapshots.Count == 0)
            {
                TaskDialog.Show("Границы 3D вида", "Для этого вида ещё не сохранялось границ.");
                return Result.Succeeded;
            }

            SectionBoxSnapshot snapshot;
            if (mem.Snapshots.Count == 1)
                snapshot = mem.Snapshots[0];
            else
            {
                snapshot = SectionBoxChooser.Choose(mem, viewIdValue);
                if (snapshot == null) return Result.Cancelled;
            }

            // Восстанавливаем BoundingBoxXYZ
            BoundingBoxXYZ box = new BoundingBoxXYZ();
            box.Min = SectionBoxMemory.FromArray(snapshot.Min);
            box.Max = SectionBoxMemory.FromArray(snapshot.Max);

            if (snapshot.TOrigin != null && snapshot.TBasisX != null &&
                snapshot.TBasisY != null && snapshot.TBasisZ != null)
            {
                Transform t = Transform.Identity;
                t.Origin = SectionBoxMemory.FromArray(snapshot.TOrigin);
                t.BasisX = SectionBoxMemory.FromArray(snapshot.TBasisX);
                t.BasisY = SectionBoxMemory.FromArray(snapshot.TBasisY);
                t.BasisZ = SectionBoxMemory.FromArray(snapshot.TBasisZ);
                box.Transform = t;
            }

            using (Transaction t = new Transaction(doc, "Восстановление границ 3D вида"))
            {
                t.Start();
                view3D.SetSectionBox(box);
                view3D.IsSectionBoxActive = snapshot.IsActive;
                t.Commit();
            }

            TaskDialog.Show("Границы 3D вида",
                $"Границы восстановлены.\n" +
                $"Снимок: {snapshot.Date:yyyy-MM-dd HH:mm} ({snapshot.UserName}), " +
                $"состояние: {(snapshot.IsActive ? "включены" : "выключены")}.");

            return Result.Succeeded;
        }
    }
    internal static class SectionBoxChooser
    {
        public static SectionBoxSnapshot Choose(SectionBoxMemory memory, int viewId)
        {
            SectionBoxSnapshot result = null;
            var snapshots = memory.Snapshots ?? new List<SectionBoxSnapshot>();

            var window = new System.Windows.Window
            {
                Title = "Выбор сохранённых границ 3D вида",
                Width = 620,
                Height = 400,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var hint = new System.Windows.Controls.TextBlock
            {
                Text = "Выделите строку и нажмите Delete, чтобы удалить снимок границ.",
                Margin = new System.Windows.Thickness(8, 8, 8, 4),
                Foreground = System.Windows.Media.Brushes.DimGray,
                FontStyle = System.Windows.FontStyles.Italic,
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
            System.Windows.Controls.Grid.SetRow(hint, 0);
            grid.Children.Add(hint);

            var listBox = new System.Windows.Controls.ListBox { Margin = new System.Windows.Thickness(8, 0, 8, 0) };
            System.Windows.Controls.Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(6)
            };
            System.Windows.Controls.Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            var ok = new System.Windows.Controls.Button
            {
                Content = "Восстановить",
                Width = 120,
                Margin = new System.Windows.Thickness(4),
                IsDefault = true
            };
            var cancel = new System.Windows.Controls.Button
            {
                Content = "Отмена",
                Width = 90,
                Margin = new System.Windows.Thickness(4),
                IsCancel = true
            };
            panel.Children.Add(ok);
            panel.Children.Add(cancel);

            void RefreshList()
            {
                listBox.Items.Clear();
                foreach (var s in snapshots) listBox.Items.Add(s.ToString());
                if (snapshots.Count > 0) listBox.SelectedIndex = snapshots.Count - 1;
                ok.IsEnabled = snapshots.Count > 0;
            }
            RefreshList();

            window.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Delete) return;
                int idx = listBox.SelectedIndex;
                if (idx < 0 || idx >= snapshots.Count) return;

                var toRemove = snapshots[idx];
                var confirm = System.Windows.MessageBox.Show(window,
                    $"Удалить снимок границ от {toRemove.Date:yyyy-MM-dd HH:mm} ({toRemove.UserName})?\n\n" +
                    "Изменение будет немедленно записано в файл. Действие необратимо.",
                    "Удаление снимка границ",
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                e.Handled = true;
                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                snapshots.RemoveAt(idx);
                memory.Snapshots = snapshots;
                SaveMemory(memory, viewId);
                RefreshList();

                if (snapshots.Count == 0)
                {
                    System.Windows.MessageBox.Show(window,
                        "Все снимки границ удалены. Файл памяти стёрт.",
                        "Удаление", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
            cancel.Click += (s, e) => { window.DialogResult = false; window.Close(); };

            window.Content = grid;
            window.ShowDialog();
            return result;
        }

        private static void SaveMemory(SectionBoxMemory memory, int viewId)
        {
            string path = SectionBoxMemory.FilePath(viewId);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (memory.Snapshots == null || memory.Snapshots.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                string json = JsonSerializer.Serialize(memory, SectionBoxMemory.Options);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Replace(tmp, path, null, true);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Не удалось сохранить изменения:\n" + ex.Message,
                    "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }


    }
    internal sealed class SectionBoxLabelResult
    {
        public bool IsCancelled { get; private set; }
        public string Label { get; private set; }
        public static SectionBoxLabelResult Cancel() => new SectionBoxLabelResult { IsCancelled = true };
        public static SectionBoxLabelResult Ok(string label) => new SectionBoxLabelResult { Label = label };
    }

    internal static class SectionBoxLabelDialog
    {
        public static SectionBoxLabelResult Ask(string viewName, string docTitle, bool isActive)
        {
            SectionBoxLabelResult result = SectionBoxLabelResult.Cancel();

            var window = new System.Windows.Window
            {
                Title = "Название снимка границ",
                Width = 480,
                Height = 220,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var info = new System.Windows.Controls.TextBlock
            {
                Margin = new System.Windows.Thickness(10, 10, 10, 4),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Text = $"Вид: {viewName}\nДокумент: {docTitle}\nГраницы: {(isActive ? "включены" : "выключены")}"
            };
            System.Windows.Controls.Grid.SetRow(info, 0); grid.Children.Add(info);

            var prompt = new System.Windows.Controls.TextBlock
            {
                Text = "Название / примечание (необязательно):",
                Margin = new System.Windows.Thickness(10, 6, 10, 2)
            };
            System.Windows.Controls.Grid.SetRow(prompt, 1); grid.Children.Add(prompt);

            var textBox = new System.Windows.Controls.TextBox
            {
                Margin = new System.Windows.Thickness(10, 0, 10, 8),
                AcceptsReturn = false,
                MaxLength = 120
            };
            System.Windows.Controls.Grid.SetRow(textBox, 2); grid.Children.Add(textBox);

            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(6)
            };
            System.Windows.Controls.Grid.SetRow(panel, 3); grid.Children.Add(panel);

            var ok = new System.Windows.Controls.Button { Content = "Сохранить", Width = 110, Margin = new System.Windows.Thickness(4), IsDefault = true };
            var skip = new System.Windows.Controls.Button { Content = "Без названия", Width = 130, Margin = new System.Windows.Thickness(4) };
            var cancel = new System.Windows.Controls.Button { Content = "Отмена", Width = 90, Margin = new System.Windows.Thickness(4), IsCancel = true };
            panel.Children.Add(ok); panel.Children.Add(skip); panel.Children.Add(cancel);

            ok.Click += (s, e) =>
            {
                string text = (textBox.Text ?? "").Trim();
                result = SectionBoxLabelResult.Ok(string.IsNullOrEmpty(text) ? null : text);
                window.DialogResult = true; window.Close();
            };
            skip.Click += (s, e) => { result = SectionBoxLabelResult.Ok(null); window.DialogResult = true; window.Close(); };
            cancel.Click += (s, e) => { result = SectionBoxLabelResult.Cancel(); window.DialogResult = false; window.Close(); };

            window.Content = grid;
            window.Loaded += (s, e) => textBox.Focus();
            window.ShowDialog();
            return result;
        }
    }
}