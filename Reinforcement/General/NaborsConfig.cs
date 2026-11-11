//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.Encodings.Web;
//using System.Text.Json;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Input;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;

//namespace Reinforcement
//{
//    // ---------------------------------------------------------------
//    //  МОДЕЛИ ДАННЫХ
//    // ---------------------------------------------------------------
//    public class WorksetGroup
//    {
//        public string Name { get; set; } = "";
//        public List<string> WorksetNames { get; set; } = new List<string>();
//        public DateTime Created { get; set; } = DateTime.Now;
//        public string Author { get; set; } = "";

//        public override string ToString() => Name;
//    }

//    public class DocumentWorksetConfig
//    {
//        public string DocumentTitle { get; set; } = "";
//        public string DocumentPath { get; set; } = "";
//        public List<WorksetGroup> Groups { get; set; } = new List<WorksetGroup>();
//        public DateTime LastModified { get; set; } = DateTime.Now;
//    }

//    // ---------------------------------------------------------------
//    //  ХРАНИЛИЩЕ
//    // ---------------------------------------------------------------
//    public static class WorksetMemoryStorage
//    {
//        public static string PrimaryPath =
//            @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy\MemoryNabors";

//        public static string FolderName = "MemoryNabors";

//        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
//        {
//            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
//            WriteIndented = true
//        };

//        public static string GetFolder()
//        {
//            if (Directory.Exists(PrimaryPath))
//                return PrimaryPath;

//            string path = App_Folders.TargetPluginDir;
//            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
//                return null;

//            path = Path.Combine(path, FolderName);
//            Directory.CreateDirectory(path);
//            return path;
//        }

//        public static string FilePathForDocument(Document doc)
//        {
//            string dir = GetFolder();
//            if (string.IsNullOrEmpty(dir)) return null;

//            string key = GetDocumentKey(doc);
//            if (string.IsNullOrEmpty(key)) return null;

//            return Path.Combine(dir, key + ".json");
//        }

//        private static string GetDocumentKey(Document doc)
//        {
//            try
//            {
//                if (doc.ProjectInformation != null)
//                {
//                    string uid = doc.ProjectInformation.UniqueId;
//                    if (!string.IsNullOrEmpty(uid))
//                        return Sanitize(uid);
//                }
//            }
//            catch { }

//            if (!string.IsNullOrEmpty(doc.Title))
//                return Sanitize(doc.Title);

//            return null;
//        }

//        private static string Sanitize(string name)
//        {
//            if (string.IsNullOrEmpty(name)) return "doc";
//            var invalid = Path.GetInvalidFileNameChars();
//            var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
//            string s = new string(chars).Trim();
//            return s.Length > 80 ? s.Substring(0, 80) : s;
//        }

//        public static DocumentWorksetConfig Load(Document doc)
//        {
//            string path = FilePathForDocument(doc);
//            if (string.IsNullOrEmpty(path) || !File.Exists(path))
//                return new DocumentWorksetConfig
//                {
//                    DocumentTitle = doc.Title,
//                    DocumentPath = doc.PathName
//                };

//            try
//            {
//                string json = File.ReadAllText(path);
//                if (string.IsNullOrWhiteSpace(json))
//                    return new DocumentWorksetConfig { DocumentTitle = doc.Title };

//                var data = JsonSerializer.Deserialize<DocumentWorksetConfig>(json, Options);
//                if (data == null) return new DocumentWorksetConfig { DocumentTitle = doc.Title };
//                if (data.Groups == null) data.Groups = new List<WorksetGroup>();
//                return data;
//            }
//            catch
//            {
//                return new DocumentWorksetConfig { DocumentTitle = doc.Title };
//            }
//        }

//        public static void Save(DocumentWorksetConfig config, Document doc)
//        {
//            string path = FilePathForDocument(doc);
//            if (string.IsNullOrEmpty(path)) return;

//            try
//            {
//                config.LastModified = DateTime.Now;
//                string json = JsonSerializer.Serialize(config, Options);
//                string tmp = path + ".tmp";
//                File.WriteAllText(tmp, json);

//                if (File.Exists(path))
//                    File.Replace(tmp, path, null, true);
//                else
//                    File.Move(tmp, path);
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show(
//                    "Не удалось сохранить конфигурацию рабочих наборов:\n" + ex.Message,
//                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }
//    }

//    // ---------------------------------------------------------------
//    //  КОМАНДА
//    // ---------------------------------------------------------------
//    [Transaction(TransactionMode.Manual)]
//    public class NaborsConfig : IExternalCommand
//    {
//        public Result Execute(
//            ExternalCommandData commandData,
//            ref string message,
//            ElementSet elements)
//        {
//            RevitAPI.Initialize(commandData);
//            UIDocument uidoc = RevitAPI.UiDocument;
//            if (uidoc == null) return Result.Cancelled;

//            Document doc = uidoc.Document;

//            if (!doc.IsWorkshared)
//            {
//                TaskDialog.Show("Рабочие наборы",
//                    "Документ не является рабочим (не workshared).\n" +
//                    "Глобальное управление рабочими наборами доступно только для моделей с включённой " +
//                    "совместной работой.");
//                return Result.Succeeded;
//            }

//            if (string.IsNullOrEmpty(doc.PathName) || !File.Exists(doc.PathName))
//            {
//                TaskDialog.Show("Рабочие наборы",
//                    "Документ не сохранён на диск.\n" +
//                    "Сохраните файл перед использованием команды — для переоткрытия нужен путь к .rvt.");
//                return Result.Succeeded;
//            }

//            if (doc.IsFamilyDocument)
//            {
//                TaskDialog.Show("Рабочие наборы",
//                    "Команда не поддерживается в документах семейств.");
//                return Result.Succeeded;
//            }

//            var worksets = new FilteredWorksetCollector(doc)
//                .OfKind(WorksetKind.UserWorkset)
//                .ToWorksets()
//                .OrderBy(ws => ws.Name, StringComparer.CurrentCultureIgnoreCase)
//                .ToList();

//            if (worksets.Count == 0)
//            {
//                TaskDialog.Show("Рабочие наборы",
//                    "В документе нет пользовательских рабочих наборов.");
//                return Result.Succeeded;
//            }

//            var config = WorksetMemoryStorage.Load(doc);
//            var window = new NaborsConfigWindow(doc, config, worksets);
//            window.ShowDialog();

//            // Окно закрылось. Если пользователь нажал «Применить» — переоткрываем документ.
//            if (window.PendingReopenNames != null)
//            {
//                return PerformReopen(doc, window.PendingReopenNames, ref message);
//            }

//            return Result.Succeeded;
//        }

//        // -----------------------------------------------------------
//        //  Основная операция: закрыть текущий документ и открыть заново
//        //  с конфигурацией WorksetConfiguration.
//        // -----------------------------------------------------------
//        private static Result PerformReopen(
//            Document doc,
//            HashSet<string> openNames,
//            ref string message)
//        {
//            // 1. Сохраняем, если есть изменения
//            if (doc.IsModified)
//            {
//                try
//                {
//                    doc.Save();
//                }
//                catch (Exception ex)
//                {
//                    message = "Не удалось сохранить документ перед переоткрытием: " + ex.Message;
//                    return Result.Failed;
//                }
//            }

//            // 2. Собираем WorksetId для открытия (по именам)
//            var openIds = new List<WorksetId>();
//            var allWorksets = new FilteredWorksetCollector(doc)
//                .OfKind(WorksetKind.UserWorkset)
//                .ToWorksets();

//            foreach (var ws in allWorksets)
//                if (openNames.Contains(ws.Name))
//                    openIds.Add(ws.Id);

//            // 3. Конфигурация: закрыть все, открыть нужные
//            var worksetConfig = new WorksetConfiguration(
//                WorksetConfigurationOption.CloseAllWorksets);
//            worksetConfig.Open(openIds);

//            var openOptions = new OpenOptions();
//            openOptions.SetOpenWorksetsConfiguration(worksetConfig);
//            openOptions.DetachFromCentralOption = DetachFromCentralOption.DoNotDetach;

//            string path = doc.PathName;
//            var app = doc.Application;

//            // 4. Создаём «заглушку» (пустой проект), чтобы деактивировать текущий документ
//            Document placeholder;
//            try
//            {
//                placeholder = app.NewProjectDocument(UnitSystem.Metric);
//            }
//            catch (Exception ex)
//            {
//                message = "Не удалось создать промежуточный документ для деактивации: " + ex.Message;
//                return Result.Failed;
//            }

//            // 5. Закрываем исходный документ
//            try
//            {
//                doc.Close(false);
//            }
//            catch (Exception ex)
//            {
//                try { if (placeholder.IsValidObject) placeholder.Close(false); } catch { }
//                message = "Не удалось закрыть текущий документ: " + ex.Message;
//                return Result.Failed;
//            }

//            // 6. Открываем с новой конфигурацией
//            bool opened = false;
//            try
//            {
//                var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(path);
//                UIApplication uiapp = RevitAPI.UiApplication;
//                uiapp.OpenAndActivateDocument(modelPath, openOptions, false);
//                opened = true;
//            }
//            catch (Exception ex)
//            {
//                message = "Не удалось открыть документ с новой конфигурацией рабочих наборов:\n"
//                          + ex.Message;
//            }
//            finally
//            {
//                // Закрываем заглушку при любом исходе
//                try { if (placeholder != null && placeholder.IsValidObject) placeholder.Close(false); }
//                catch { }
//            }

//            return opened ? Result.Succeeded : Result.Failed;
//        }
//    }

//    // ---------------------------------------------------------------
//    //  ОКНО
//    // ---------------------------------------------------------------
//    internal sealed class NaborsConfigWindow : Window
//    {
//        private readonly Document _doc;
//        private readonly DocumentWorksetConfig _config;
//        private readonly List<Workset> _worksets;

//        private readonly ListBox _groupList;
//        private readonly System.Windows.Controls.TextBox _filterBox;
//        private readonly StackPanel _checkPanel;
//        private readonly List<CheckBox> _checkboxes = new List<CheckBox>();
//        private readonly Button _saveBtn;
//        private readonly Button _applyBtn;
//        private readonly Button _deleteBtn;
//        private readonly Button _newBtn;
//        private readonly TextBlock _status;

//        /// <summary>
//        /// Если пользователь нажал «Применить», здесь лежит набор имён рабочих наборов,
//        /// которые должны остаться открытыми. Команда выполнит переоткрытие документа
//        /// уже после закрытия этого окна. null — никаких действий не требуется.
//        /// </summary>
//        public HashSet<string> PendingReopenNames { get; private set; }

//        public NaborsConfigWindow(
//            Document doc,
//            DocumentWorksetConfig config,
//            List<Workset> worksets)
//        {
//            _doc = doc;
//            _config = config;
//            _worksets = worksets;

//            Title = $"Рабочие наборы — {doc.Title}";
//            Width = 820;
//            Height = 560;
//            WindowStartupLocation = WindowStartupLocation.CenterScreen;
//            MinWidth = 680;
//            MinHeight = 420;

//            var main = new System.Windows.Controls.Grid();
//            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
//            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
//            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
//            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

//            // ---------------- ЛЕВАЯ ПАНЕЛЬ: группы ----------------
//            var leftPanel = new System.Windows.Controls.Grid();
//            leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            leftPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
//            leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

//            var leftHeader = new TextBlock
//            {
//                Text = "Сохранённые группы:",
//                Margin = new Thickness(10, 10, 10, 6),
//                FontWeight = FontWeights.SemiBold
//            };
//            System.Windows.Controls.Grid.SetRow(leftHeader, 0);
//            leftPanel.Children.Add(leftHeader);

//            _groupList = new ListBox { Margin = new Thickness(10, 0, 10, 0) };
//            _groupList.MouseDoubleClick += OnGroupDoubleClick;
//            _groupList.SelectionChanged += OnGroupSelected;
//            System.Windows.Controls.Grid.SetRow(_groupList, 1);
//            leftPanel.Children.Add(_groupList);

//            var leftButtons = new StackPanel
//            {
//                Orientation = Orientation.Horizontal,
//                Margin = new Thickness(10, 6, 10, 10)
//            };
//            _newBtn = new Button { Content = "Новая", Width = 90, Margin = new Thickness(0, 0, 6, 0) };
//            _deleteBtn = new Button { Content = "Удалить", Width = 90 };
//            _newBtn.Click += OnNewGroup;
//            _deleteBtn.Click += OnDeleteGroup;
//            leftButtons.Children.Add(_newBtn);
//            leftButtons.Children.Add(_deleteBtn);
//            System.Windows.Controls.Grid.SetRow(leftButtons, 2);
//            leftPanel.Children.Add(leftButtons);

//            System.Windows.Controls.Grid.SetColumn(leftPanel, 0);
//            main.Children.Add(leftPanel);

//            // ---------------- ПРАВАЯ ПАНЕЛЬ: рабочие наборы ----------------
//            var rightPanel = new System.Windows.Controls.Grid();
//            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

//            var rightHeader = new TextBlock
//            {
//                Text = "Рабочие наборы (отметьте те, что должны остаться ОТКРЫТЫМИ):",
//                Margin = new Thickness(10, 10, 10, 6),
//                FontWeight = FontWeights.SemiBold,
//                TextWrapping = TextWrapping.Wrap
//            };
//            System.Windows.Controls.Grid.SetRow(rightHeader, 0);
//            rightPanel.Children.Add(rightHeader);

//            _filterBox = new System.Windows.Controls.TextBox
//            {
//                Margin = new Thickness(10, 0, 10, 8),
//                Height = 24,
//                ToolTip = "Фильтр по имени рабочего набора"
//            };
//            _filterBox.TextChanged += (s, e) => ApplyFilter();
//            System.Windows.Controls.Grid.SetRow(_filterBox, 1);
//            rightPanel.Children.Add(_filterBox);

//            var quickPanel = new StackPanel
//            {
//                Orientation = Orientation.Horizontal,
//                Margin = new Thickness(10, 0, 10, 6)
//            };
//            var checkAll = new Button { Content = "Выбрать все", Height = 24, Margin = new Thickness(0, 0, 6, 0) };
//            var checkNone = new Button { Content = "Снять все", Height = 24 };
//            checkAll.Click += (s, e) => CheckAll(true);
//            checkNone.Click += (s, e) => CheckAll(false);
//            quickPanel.Children.Add(checkAll);
//            quickPanel.Children.Add(checkNone);
//            System.Windows.Controls.Grid.SetRow(quickPanel, 2);
//            rightPanel.Children.Add(quickPanel);

//            var scroll = new ScrollViewer
//            {
//                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
//                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
//                Margin = new Thickness(10, 0, 10, 8)
//            };
//            _checkPanel = new StackPanel();
//            scroll.Content = _checkPanel;
//            System.Windows.Controls.Grid.SetRow(scroll, 3);
//            rightPanel.Children.Add(scroll);

//            foreach (var ws in _worksets)
//            {
//                var cb = new CheckBox
//                {
//                    Content = ws.Name,
//                    Tag = ws.Name,
//                    Margin = new Thickness(0, 3, 0, 3)
//                };
//                _checkboxes.Add(cb);
//                _checkPanel.Children.Add(cb);
//            }

//            System.Windows.Controls.Grid.SetColumn(rightPanel, 1);
//            main.Children.Add(rightPanel);

//            // ---------------- НИЖНЯЯ ПАНЕЛЬ ----------------
//            var bottom = new System.Windows.Controls.Grid { Margin = new Thickness(10, 4, 10, 10) };
//            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
//            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

//            _status = new TextBlock
//            {
//                VerticalAlignment = VerticalAlignment.Center,
//                Foreground = System.Windows.Media.Brushes.DimGray,
//                TextTrimming = TextTrimming.CharacterEllipsis,
//                TextWrapping = TextWrapping.Wrap
//            };
//            System.Windows.Controls.Grid.SetColumn(_status, 0);
//            bottom.Children.Add(_status);

//            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
//            _saveBtn = new Button { Content = "Сохранить", Width = 110, Margin = new Thickness(4, 0, 4, 0) };
//            _applyBtn = new Button { Content = "Применить", Width = 110, Margin = new Thickness(4, 0, 4, 0), IsDefault = true };
//            var closeBtn = new Button { Content = "Закрыть", Width = 90, IsCancel = true };

//            _saveBtn.Click += OnSaveGroup;
//            _applyBtn.Click += OnApplyGroup;
//            closeBtn.Click += (s, e) => Close();

//            btnPanel.Children.Add(_saveBtn);
//            btnPanel.Children.Add(_applyBtn);
//            btnPanel.Children.Add(closeBtn);
//            System.Windows.Controls.Grid.SetColumn(btnPanel, 1);
//            bottom.Children.Add(btnPanel);

//            System.Windows.Controls.Grid.SetRow(bottom, 1);
//            System.Windows.Controls.Grid.SetColumnSpan(bottom, 2);
//            main.Children.Add(bottom);

//            Content = main;

//            RefreshGroups();
//        }

//        // ---------- вспомогательные ----------
//        private void SetStatus(string text)
//        {
//            _status.Text = text ?? "";
//        }

//        private void ApplyFilter()
//        {
//            string filter = (_filterBox.Text ?? "").Trim();
//            foreach (var cb in _checkboxes)
//            {
//                bool visible = string.IsNullOrEmpty(filter) ||
//                    cb.Content.ToString().IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0;
//                cb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
//            }
//        }

//        private List<CheckBox> VisibleCheckboxes =>
//            _checkboxes.Where(cb => cb.Visibility == Visibility.Visible).ToList();

//        private void CheckAll(bool state)
//        {
//            foreach (var cb in VisibleCheckboxes) cb.IsChecked = state;
//        }

//        // ---------- группы ----------
//        private void RefreshGroups(string selectName = null)
//        {
//            _groupList.Items.Clear();
//            foreach (var g in _config.Groups)
//                _groupList.Items.Add(g);

//            if (selectName != null)
//            {
//                var match = _config.Groups.FirstOrDefault(g =>
//                    string.Equals(g.Name, selectName, StringComparison.OrdinalIgnoreCase));
//                if (match != null) _groupList.SelectedItem = match;
//            }
//            else if (_groupList.Items.Count > 0)
//            {
//                _groupList.SelectedIndex = 0;
//            }

//            _deleteBtn.IsEnabled = _groupList.SelectedItem != null;
//        }

//        private void OnGroupSelected(object sender, SelectionChangedEventArgs e)
//        {
//            var group = _groupList.SelectedItem as WorksetGroup;
//            var names = new HashSet<string>(
//                group?.WorksetNames ?? new List<string>(),
//                StringComparer.OrdinalIgnoreCase);

//            foreach (var cb in _checkboxes)
//                cb.IsChecked = names.Contains((string)cb.Tag);

//            _deleteBtn.IsEnabled = group != null;
//            SetStatus(group == null
//                ? "Выберите группу или создайте новую."
//                : $"Группа «{group.Name}»: отмечено {names.Count} из {_worksets.Count}.");
//        }

//        private void OnNewGroup(object sender, RoutedEventArgs e)
//        {
//            string name = SimpleInputDialog.Ask(this, "Новая группа", "Название группы:");
//            if (string.IsNullOrWhiteSpace(name)) return;
//            name = name.Trim();

//            if (_config.Groups.Any(g =>
//                string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
//            {
//                MessageBox.Show(this,
//                    "Группа с таким названием уже существует.",
//                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
//                return;
//            }

//            var ng = new WorksetGroup
//            {
//                Name = name,
//                WorksetNames = new List<string>(),
//                Created = DateTime.Now,
//                Author = App_Folders.UserName
//            };
//            _config.Groups.Add(ng);
//            WorksetMemoryStorage.Save(_config, _doc);
//            RefreshGroups(name);
//            SetStatus($"Создана группа «{name}». Отметьте рабочие наборы и нажмите «Сохранить».");
//        }

//        private void OnDeleteGroup(object sender, RoutedEventArgs e)
//        {
//            var group = _groupList.SelectedItem as WorksetGroup;
//            if (group == null) return;

//            if (MessageBox.Show(this,
//                    $"Удалить группу «{group.Name}»?",
//                    "Удаление группы",
//                    MessageBoxButton.YesNo, MessageBoxImage.Question)
//                != MessageBoxResult.Yes) return;

//            _config.Groups.Remove(group);
//            WorksetMemoryStorage.Save(_config, _doc);
//            RefreshGroups();
//            SetStatus($"Группа «{group.Name}» удалена.");
//        }

//        private void OnSaveGroup(object sender, RoutedEventArgs e)
//        {
//            var group = _groupList.SelectedItem as WorksetGroup;
//            if (group == null)
//            {
//                MessageBox.Show(this,
//                    "Сначала выберите группу (или создайте новую).",
//                    "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
//                return;
//            }

//            var selected = _checkboxes
//                .Where(cb => cb.IsChecked == true)
//                .Select(cb => (string)cb.Tag)
//                .ToList();

//            group.WorksetNames = selected;
//            WorksetMemoryStorage.Save(_config, _doc);
//            SetStatus($"Группа «{group.Name}» сохранена. Рабочих наборов: {selected.Count}.");
//        }

//        // ---------- применение: переоткрытие документа ----------
//        private void OnApplyGroup(object sender, RoutedEventArgs e)
//        {
//            var group = _groupList.SelectedItem as WorksetGroup;
//            if (group == null)
//            {
//                MessageBox.Show(this,
//                    "Сначала выберите группу.",
//                    "Применение", MessageBoxButton.OK, MessageBoxImage.Information);
//                return;
//            }

//            var names = new HashSet<string>(
//                _checkboxes.Where(cb => cb.IsChecked == true)
//                           .Select(cb => (string)cb.Tag),
//                StringComparer.OrdinalIgnoreCase);

//            if (names.Count == 0)
//            {
//                if (MessageBox.Show(this,
//                        "В группе не отмечено ни одного рабочего набора.\n" +
//                        "ВСЕ пользовательские рабочие наборы будут ЗАКРЫТЫ. Продолжить?",
//                        "Применение", MessageBoxButton.YesNo, MessageBoxImage.Warning)
//                    != MessageBoxResult.Yes) return;
//            }

//            string warning =
//                "Документ будет сохранён и ПЕРЕОТКРЫТ с новой конфигурацией рабочих наборов.\n\n" +
//                $"Открыто будет: {names.Count} из {_worksets.Count}.\n" +
//                $"Закрыто будет: {_worksets.Count - names.Count}.\n\n" +
//                "Несохранённые изменения будут сохранены автоматически.\n\nПродолжить?";

//            if (MessageBox.Show(this, warning,
//                    "Переоткрытие документа",
//                    MessageBoxButton.YesNo, MessageBoxImage.Question,
//                    MessageBoxResult.No) != MessageBoxResult.Yes)
//                return;

//            PendingReopenNames = names;
//            DialogResult = true;
//            Close();
//        }

//        private void OnGroupDoubleClick(object sender, MouseButtonEventArgs e)
//        {
//            if (_groupList.SelectedItem == null) return;
//            OnApplyGroup(sender, e);
//        }
//    }

//    // ---------------------------------------------------------------
//    //  ДИАЛОГ ВВОДА ИМЕНИ
//    // ---------------------------------------------------------------
//    internal static class SimpleInputDialog
//    {
//        public static string Ask(Window owner, string title, string prompt)
//        {
//            string result = null;

//            var w = new Window
//            {
//                Title = title,
//                Width = 420,
//                Height = 170,
//                WindowStartupLocation = WindowStartupLocation.CenterOwner,
//                Owner = owner,
//                ResizeMode = ResizeMode.NoResize,
//                ShowInTaskbar = false
//            };

//            var grid = new System.Windows.Controls.Grid();
//            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
//            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
//            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

//            var lbl = new TextBlock { Text = prompt, Margin = new Thickness(10, 10, 10, 4) };
//            System.Windows.Controls.Grid.SetRow(lbl, 0);
//            grid.Children.Add(lbl);

//            var tb = new System.Windows.Controls.TextBox
//            {
//                Margin = new Thickness(10, 0, 10, 8),
//                MaxLength = 100
//            };
//            System.Windows.Controls.Grid.SetRow(tb, 1);
//            grid.Children.Add(tb);

//            var panel = new StackPanel
//            {
//                Orientation = Orientation.Horizontal,
//                HorizontalAlignment = HorizontalAlignment.Right,
//                Margin = new Thickness(8)
//            };
//            System.Windows.Controls.Grid.SetRow(panel, 3);
//            grid.Children.Add(panel);

//            var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(4, 0, 4, 0), IsDefault = true };
//            var cancel = new Button { Content = "Отмена", Width = 90, IsCancel = true };
//            panel.Children.Add(ok);
//            panel.Children.Add(cancel);

//            ok.Click += (s, e) =>
//            {
//                result = tb.Text?.Trim();
//                w.DialogResult = true;
//                w.Close();
//            };
//            cancel.Click += (s, e) => { w.DialogResult = false; w.Close(); };

//            w.Content = grid;
//            w.Loaded += (s, e) => tb.Focus();
//            w.ShowDialog();

//            return result;
//        }
//    }
//}