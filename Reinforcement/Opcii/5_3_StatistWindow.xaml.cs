using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
namespace Reinforcement
{
    public class StringSortElement
    {
        public double ValSort1 = 0; //возможные сортировочные значения
        public double ValSort2 = 0;
        public double ValSort3 = 0;

        public string NameValSort1 = "A"; public string NameValSort2 = "B"; public string NameValSort3 = "C";

        public string Text = "";// основной текст

    }

    public partial class StatistWindow : Window
    {
        public StatistWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Здесь загружаем сохранённые настройки в поля.
            InitReportKinds();
            InitTextReportKinds();      // ← добавлено
            ReportTextBox.Text = "Нажмите «Показать текст» после загрузки статистики.";
        }
        // Экземпляр-поставщик. Создаётся по кнопке "Вгрузить статистику".
        public static StatLoad StatLoad { get; private set; } = null;
        


        private void SaveButtonCSV_Click(object sender, RoutedEventArgs e)
        {
            if (stringSortElements == null || stringSortElements.Count == 0)
                return;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV|*.csv",
                FileName = $"stats_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();

                // Заголовок — берём имена полей из первого элемента
                // (если имена одинаковые для всех, этого достаточно)
                var first = stringSortElements[0];
                sb.AppendLine(string.Join(";",
                    first.NameValSort1,
                    first.NameValSort2,
                    first.NameValSort3,
                    "Text"));

                foreach (var el in stringSortElements)
                {
                    // Text оборачиваем в кавычки, экранируем внутренние кавычки и заменяем ; на ,
                    string safeText = el.Text
                        .Replace("\"", "\"\"")    // экранируем кавычки по стандарту CSV
                        .Replace(";", ",");       // меняем ; на , чтобы не ломать разделители

                    // Если есть переносы строк — оборачиваем в кавычки
                    if (safeText.Contains("\n"))
                        safeText = $"\"{safeText}\"";

                    sb.AppendLine(string.Join(";",
                        el.ValSort1.ToString(CultureInfo.InvariantCulture),
                        el.ValSort2.ToString(CultureInfo.InvariantCulture),
                        el.ValSort3.ToString(CultureInfo.InvariantCulture),
                        safeText));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, "Готово.", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка сохранения",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SaveButtonFB_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //надо открыть окно для FB
            }
            catch (System.Exception ex)
            {

            
            }

         }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InitReportKinds()
        {
            var items = new List<KeyValuePair<ReportKind, string>>
                {
                    new KeyValuePair<ReportKind, string>(ReportKind.UsersPerFilePerDay,  "Пользователи × Файлы × Дни"),
                    new KeyValuePair<ReportKind, string>(ReportKind.ClicksPerFilePerDay, "Клики × Файлы × Дни"),
                    new KeyValuePair<ReportKind, string>(ReportKind.UsersPerDay,         "Пользователи × Дни"),
                    new KeyValuePair<ReportKind, string>(ReportKind.FilesPerUser,        "Файлы × Пользователи")
                };

            ReportKindCombo.ItemsSource = items;
            ReportKindCombo.DisplayMemberPath = "Value";
            ReportKindCombo.SelectedValuePath = "Key";
            ReportKindCombo.SelectedIndex = 0;
        }
        private ReportKind CurrentKind =>
            ReportKindCombo.SelectedValue is ReportKind k
                ? k
                : ReportKind.UsersPerFilePerDay;

        private void ReportKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatLoad != null)
                StatLoad.Kind = CurrentKind;
        }

        private void ServerOnlyCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (StatLoad != null)
                StatLoad.ServerOnly = ServerOnlyCheck.IsChecked == true;
        }

        // ==== Кнопки ====

        private void LoadStat_Click(object sender, RoutedEventArgs e)
        {
            // Собираем статистику по файлам (ваш существующий метод)
            // Если GetFilesProgress заполняет DictFileDataLook — вызываем его.
            // GetFilesProgress(serverModel: ServerOnlyCheck.IsChecked == true);

            // Создаём StatLoad и кладём в него данные
            StatLoad = new StatLoad(ServerOnlyCheck.IsChecked == true,DaysBack, DaysFreshBack);        // ← добавлено);
            //StatLoad = new StatLoad
            //{
            //    // FileDataDict = DictFileDataLook,   // ← ваши поля
            //    // LookUsersList = LookUsersList,
            //    Kind = CurrentKind,
            //    ServerOnly = ServerOnlyCheck.IsChecked == true
            //};

            StatusText.Text = $"Данные загружены. Файлов: {StatLoad.DictFileDataLook.Count}";
            StatusText.Foreground = System.Windows.Media.Brushes.Green;
        }

        private void GetStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (StatLoad == null)
            {
                MessageBox.Show(this,
                    "Сначала нажмите «Вгрузить статистику».",
                    "Статистика",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Обновляем выбранный вид на актуальный из ComboBox
            StatLoad.Kind = CurrentKind;
            StatLoad.ServerOnly = ServerOnlyCheck.IsChecked == true;

            var table = StatLoad.GetDataTable(); // сюда точку останова чтобы отладить таблицу

            if (table == null || table.Columns.Count == 0 || table.Rows.Count == 0)
            {
                MessageBox.Show(this,
                    "Нет данных для отображения.",
                    "Статистика",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new StatisticsWindow(table, $"Статистика: {ReportKindCombo.Text}");

            // Привязываем как дочернее к текущему окну — корректно через Handle
            new WindowInteropHelper(window).Owner = new WindowInteropHelper(this).Handle;

            window.Show();
        }
        private void InitTextReportKinds()
        {
            var items = new List<KeyValuePair<TextReportKind, string>>
    {
        new KeyValuePair<TextReportKind, string>(TextReportKind.Summary, "Краткая сводка"),
        new KeyValuePair<TextReportKind, string>(TextReportKind.ByUsers, "По пользователям"),
        new KeyValuePair<TextReportKind, string>(TextReportKind.ByFiles, "По файлам"),
        new KeyValuePair<TextReportKind, string>(TextReportKind.ByDays,  "По дням")
    };

            TextReportKindCombo.ItemsSource = items;
            TextReportKindCombo.DisplayMemberPath = "Value";
            TextReportKindCombo.SelectedValuePath = "Key";
            TextReportKindCombo.SelectedIndex = 0;
        }

        private TextReportKind CurrentTextKind =>
            TextReportKindCombo.SelectedValue is TextReportKind k
                ? k
                : TextReportKind.Summary;
        private void ShowTextStat_Click(object sender, RoutedEventArgs e)
        {
            if (StatLoad == null)
            {
                MessageBox.Show(this,
                    "Сначала нажмите «Вгрузить статистику».",
                    "Текстовая статистика",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                ReportTextBox.Text = BuildTextReport(StatLoad, CurrentTextKind);
                ReportTextBox.ScrollToHome(); // в начало
            }
            catch (Exception ex)
            {
                ReportTextBox.Text = "Ошибка формирования текста:\r\n" + ex;
            }
        }
        public static List<StringSortElement> stringSortElements { get; set; } = new List<StringSortElement>();
        private static string BuildTextReport(StatLoad load, TextReportKind kind)
        {
            var sb = new StringBuilder();

            if (load.DictFileDataLook == null || load.DictFileDataLook.Count == 0)
                return "Нет загруженных данных.";

            switch (kind)
            {
                case TextReportKind.Summary:
                    {
                        sb.AppendLine("=== КРАТКАЯ СВОДКА ===");
                        sb.AppendLine($"Файлов:        {load.DictFileDataLook.Count}");
                        sb.AppendLine($"Пользователей: {load.LookUsersList.Count}");
                        sb.AppendLine($"Только серверные: {(load.ServerOnly ? "да" : "нет")}");
                        sb.AppendLine();
                        sb.AppendLine("Файл                                | Пользователей | Кликов");
                        sb.AppendLine(new string('-', 72));
                        foreach (var kv in load.DictFileDataLook)
                        {
                            int users = kv.Value.UserNames.Count;
                            int clicks = kv.Value.DatesProjectCountClick.Values.Sum();
                            sb.AppendLine($"{Trunc(kv.Value.FileName, 34),-34} | {users,13} | {clicks,6}");
                        }
                        break;
                    }

                case TextReportKind.ByUsers:
                    {

                        // 1. Сначала собираем все элементы в список
                        stringSortElements = new List<StringSortElement>();

                        foreach (var user in load.LookUsersList)
                        {
                            //дата максимальной продуктивности
                            // Топ-3 дат с наибольшей суммой посещений
                            // Топ-3 дат
                            var top3Dates = user.DocsDateUse
                                .Select(kvp => new { Date = kvp.Key, Sum = kvp.Value.Sum(v => v.Value) })
                                .OrderByDescending(x => x.Sum)
                                .Take(3)
                                .ToList();

                           

                            //самые используемые дома
                            var top3Houses = user.DocsDateUse
                            .SelectMany(kvp => kvp.Value)
                            .GroupBy(x => x.Key)
                            .Select(g => new { Address = g.Key, TotalVisits = g.Sum(x => x.Value) })
                            .OrderByDescending(x => x.TotalVisits)
                            .Take(3)
                            .ToList();
                            int totalFiles = user.DocsDateUse.Values.Sum(d => d.Count);
                            int totalClicks = user.DocsDateUse.Values.Sum(d => d.Values.Sum());

                            var element = new StringSortElement
                            {
                                ValSort1 = totalClicks, // по этому полю будем сортировать
                                Text = $"Пользователь: {user.UserName}\n" +
               $"  Дней: {user.DocsDateUse.Count}\n" +
               $"  Файлов: {totalFiles}\n" +
               $"  Кликов: {totalClicks}\n" +
               "  Целевые объекты:\n"
                            };

                            if (top3Houses.Count == 0)
                            {
                                element.Text += "    (нет данных)\n";
                            }
                            else
                            {
                                foreach (var h in top3Houses)
                                {
                                    element.Text += $"    {h.Address} — {h.TotalVisits} посещений\n";
                                }
                            }

                            element.Text += "  Продуктивные дни:\n";
                            if (top3Dates.Count == 0)
                            {
                                element.Text += "    (нет данных)\n";
                            }
                            else
                            {
                                foreach (var d in top3Dates)
                                {
                                    string dateWrite = DateRus(d.Date);
                                    element.Text += $"    {dateWrite} — {d.Sum} посещений\n";
                                }
                            }

                            stringSortElements.Add(element);
                        }
                        // 2. Сортируем по кликам (по убыванию)
                        sb.AppendLine("=== ПО ПОЛЬЗОВАТЕЛЯМ ===");
                        stringSortElements.Sort((a, b) => b.ValSort1.CompareTo(a.ValSort1));
                        foreach (var el in stringSortElements)
                        {
                            sb.Append(el.Text); // уже содержит переносы строк
                            sb.AppendLine();    // дополнительная пустая строка между пользователями
                        }
                        break;
                    }

                case TextReportKind.ByFiles:
                    {
                        sb.AppendLine("=== ПО ФАЙЛАМ ===");
                        foreach (var kv in load.DictFileDataLook)
                        {
                            var f = kv.Value;
                            sb.AppendLine($"{f.FileName}");
                            sb.AppendLine($"{f.FolderProject}");
                            sb.AppendLine($"  Пользователи: {string.Join(", ", f.UserNames)}");
                            sb.AppendLine($"  Дней активности: {f.DatesUsers.Count}");
                            sb.AppendLine($"  Кликов всего:    {f.DatesProjectCountClick.Values.Sum()}");
                            sb.AppendLine();
                        }
                        break;
                    }

                case TextReportKind.ByDays:
                    {
                        sb.AppendLine("=== ПО ДНЯМ ===");
                        var allDates = load.DictFileDataLook.Values
                            .SelectMany(f => f.DatesUsers.Keys)
                            .Distinct()
                            .OrderBy(d => d);

                        foreach (var d in allDates)
                        {
                            int users = load.DictFileDataLook.Values
                                .Where(f => f.DatesUsers.ContainsKey(d))
                                .SelectMany(f => f.DatesUsers[d])
                                .Distinct().Count();
                            int clicks = load.DictFileDataLook.Values
                                .Where(f => f.DatesProjectCountClick.ContainsKey(d))
                                .Sum(f => f.DatesProjectCountClick[d]);

                            sb.AppendLine($"{d:dd.MM.yyyy}: пользователей {users,3}, кликов {clicks,5}");
                        }
                        break;
                    }
            }

            return sb.ToString();
        }
        public static string DateRus(DateTime date)
        {
            return date.ToString("dd MMM, ddd, yyyy", new CultureInfo("ru-RU"));
        }
        
        private static string Trunc(string s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n - 1) + "…");

        private int DaysBack
        {
            get
            {
                if (int.TryParse(DaysBackBox.Text, out int n) && n > 0)
                    return n;
                return 0; // 0 = без ограничения по дате
            }
        }
        private int DaysFreshBack
        {
            get
            {
                if (int.TryParse(DaysFreshBackBox.Text, out int n) && n > 0)
                    return n;
                return 0; // 0 = без ограничения по дате
            }
        }


        private void DaysBackBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DaysBackBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DaysBackBox.Text, out int n) || n < 1)
                DaysBackBox.Text = "30";
            else if (n > 3650)
                DaysBackBox.Text = "3650";
        }
        private void DaysOutBackBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DaysBackBox.Text, out int n) || n < 1)
                DaysBackBox.Text = "0";
            else if (n > 3650)
                DaysBackBox.Text = "3650";
        }
    }
}