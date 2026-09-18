using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Reinforcement
{
    public partial class StatisticsWindow : Window
    {
        private readonly DataTable _table;
        public static StatLoad StatLoad { get; private set; } = null;
        public StatisticsWindow(DataTable table, string title = "Статистика")
        {
            InitializeComponent();

            _table = table ?? new DataTable();
            HeaderText.Text = title;

            StatsGrid.ItemsSource = _table.DefaultView;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void StatsGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // 1) Чиним путь привязки — используем индексер DataRowView,
            //    чтобы точки/скобки в именах столбцов не ломали Binding
            if (e.Column is DataGridBoundColumn bound)
            {
                bound.Binding = new System.Windows.Data.Binding($"[{e.PropertyName}]");
            }

            // 2) Форматирование даты и выравнивание чисел (по желанию)
            if (e.PropertyType == typeof(DateTime) && e.Column is DataGridTextColumn dt)
            {
                dt.Binding = new System.Windows.Data.Binding($"[{e.PropertyName}]")
                {
                    StringFormat = "dd.MM.yyyy"
                };
            }
            else if (e.PropertyType == typeof(int) && e.Column is DataGridTextColumn it)
            {
                it.ElementStyle = new Style(typeof(System.Windows.Controls.TextBlock))
                {
                    Setters =
            {
                new Setter(System.Windows.Controls.TextBlock.TextAlignmentProperty,
                           System.Windows.TextAlignment.Right)
            }
                };
            }
        }
        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_table == null || _table.Columns.Count == 0)
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
                sb.AppendLine(string.Join(";",
                    _table.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));

                foreach (DataRow row in _table.Rows)
                {
                    var cells = row.ItemArray.Select(v =>
                        (v?.ToString() ?? "").Replace(";", ","));
                    sb.AppendLine(string.Join(";", cells));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, "Готово.", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка экспорта",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}