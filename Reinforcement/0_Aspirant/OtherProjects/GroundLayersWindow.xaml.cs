using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Reinforcement.Aspirant
{
    public partial class GroundLayersWindow : Window
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        // ─────────────────────────────────────────────────────────
        //  Статика сессии: сдвиг сохраняется между запусками команды.
        //  При первом показе инициализируется как −PBP.Elevation —
        //  именно это делает Position.Z = Z1 + сдвиг == отметка Z1.
        // ─────────────────────────────────────────────────────────
        public static double LastOffsetMeters { get; private set; } = double.NaN;

        public static void EnsureOffsetInitialized(double basePointElevationM)
        {
            if (double.IsNaN(LastOffsetMeters))
                LastOffsetMeters = -basePointElevationM;
        }

        // ─────────────────────────────────────────────────────────
        //  Публичные результаты (читаются после ShowDialog() == true)
        // ─────────────────────────────────────────────────────────

        public double Z1Meters { get; private set; }
        public double OffsetMeters { get; private set; }
        public string ColumnNumber { get; private set; } = "1";

        /// <summary>Position.Z верхней точки (м) = Z1 + Offset.</summary>
        public double EffectiveZ1Meters => Z1Meters + OffsetMeters;

        public ObservableCollection<GroundLayer> Layers { get; }
            = new ObservableCollection<GroundLayer>();

        // ─────────────────────────────────────────────────────────
        //  Конструктор
        // ─────────────────────────────────────────────────────────

        public GroundLayersWindow(double basePointElevationM = 0.0)
        {
            InitializeComponent();

            GridLayers.ItemsSource = Layers;
            Layers.Add(new GroundLayer { ThicknessM = 1.0, Name = "Слой 1" });
            Layers.CollectionChanged += (_, __) => UpdateSummary();

            EnsureOffsetInitialized(basePointElevationM);
            TbZeroPoint.Text = LastOffsetMeters.ToString("0.###", Ru);

            TbBasePointInfo.Text =
                $"Справка: PBP.Elevation = {basePointElevationM:0.###} м. " +
                $"Position.Z = Z1 + сдвиг. По умолчанию сдвиг = −PBP.Elevation " +
                $"({(-basePointElevationM):0.###} м) — это даёт отметку в Revit = Z1. " +
                $"Сдвиг хранится в статическом поле (в пределах сессии Revit).";

            UpdateSummary();
            UpdateZPreview();
        }

        // ─────────────────────────────────────────────────────────
        //  Слои
        // ─────────────────────────────────────────────────────────

        private void AddLayer_Click(object sender, RoutedEventArgs e)
        {
            int n = Layers.Count + 1;
            Layers.Add(new GroundLayer { ThicknessM = 1.0, Name = $"Слой {n}" });
            UpdateSummary();
        }

        private void RemoveLayer_Click(object sender, RoutedEventArgs e)
        {
            if (GridLayers.SelectedItem is GroundLayer l) Layers.Remove(l);
            else if (Layers.Count > 0) Layers.RemoveAt(Layers.Count - 1);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            double total = Layers.Sum(l => l.ThicknessM);
            TbSummary.Text = $"Слоёв: {Layers.Count}, суммарная толщина: " +
                             $"{total.ToString("F3", Ru)} м";
        }

        // ─────────────────────────────────────────────────────────
        //  Живой превью Position.Z
        // ─────────────────────────────────────────────────────────

        private void TbZ_TextChanged(object sender, TextChangedEventArgs e)
            => UpdateZPreview();

        private void UpdateZPreview()
        {
            if (TbZPreview == null) return;

            bool okZ1 = TryParse(TbZ1.Text, out double z1);
            bool okOff = TryParse(TbZeroPoint.Text, out double off);

            if (okZ1 && okOff)
            {
                double finalZ = z1 + off;
                string sign = off > 0 ? "+" : (off < 0 ? "−" : "");
                TbZPreview.Text =
                    $"Position.Z верхней точки = {finalZ:0.###} м " +
                    $"(Z1 {z1:0.###} {sign} сдвиг {Math.Abs(off):0.###})";
            }
            else
            {
                TbZPreview.Text = "Введите корректные Z1 и сдвиг.";
            }
        }

        // ─────────────────────────────────────────────────────────
        //  CSV: экспорт
        // ─────────────────────────────────────────────────────────

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|Все файлы (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"GroundColumn_{TbColumnNumber.Text}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, BuildCsv(), new UTF8Encoding(true));
                MessageBox.Show("Экспортировано:\n" + dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка экспорта: " + ex.Message);
            }
        }

        private string BuildCsv()
        {
            var sb = new StringBuilder();
            sb.Append("Z1_m;").Append(TbZ1.Text).Append("\r\n");
            sb.Append("Offset_m;").Append(TbZeroPoint.Text).Append("\r\n");
            sb.Append("ColumnNumber;").Append(TbColumnNumber.Text).Append("\r\n");
            sb.Append("LayerName;Thickness_m\r\n");
            foreach (var l in Layers)
                sb.Append(CsvEscape(l.Name)).Append(';')
                  .Append(l.ThicknessM.ToString("G", Ru))
                  .Append("\r\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────
        //  CSV: импорт
        // ─────────────────────────────────────────────────────────

        private void ImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|Все файлы (*.*)|*.*",
                DefaultExt = "csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string text = File.ReadAllText(dlg.FileName);
                if (ApplyCsv(text, out string error))
                    MessageBox.Show("Импортировано.");
                else
                    MessageBox.Show("Не удалось разобрать CSV:\n" + error,
                        "Импорт CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка импорта: " + ex.Message);
            }
        }

        private bool ApplyCsv(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text)) { error = "файл пуст."; return false; }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var rawLines = normalized.Split('\n');

            double? z1 = null;
            double? offset = null;
            string colNum = null;
            var layers = new List<GroundLayer>();
            bool inLayers = false;

            foreach (var raw in rawLines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;

                char sep = line.IndexOf(';') >= 0 ? ';'
                         : line.IndexOf('\t') >= 0 ? '\t'
                         : ',';
                var parts = SplitCsv(line, sep);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim().ToLowerInvariant();

                if (!inLayers)
                {
                    if (key == "z1_m" || key == "z1")
                    {
                        if (TryParse(parts[1], out double v)) z1 = v;
                    }
                    else if (key == "offset_m" || key == "offset")
                    {
                        if (TryParse(parts[1], out double v)) offset = v;
                    }
                    else if (key == "columnnumber" || key == "номер")
                    {
                        colNum = parts[1].Trim();
                    }
                    else if (key == "layername" || key == "name")
                    {
                        inLayers = true;
                    }
                    else
                    {
                        // Нет заголовка LayerName — значит это уже слой
                        if (TryParse(parts[1], out double t) &&
                            !string.IsNullOrWhiteSpace(parts[0]))
                        {
                            layers.Add(new GroundLayer
                            {
                                Name = parts[0].Trim(),
                                ThicknessM = t
                            });
                        }
                    }
                }
                else
                {
                    if (!TryParse(parts[1], out double t)) continue;
                    string name = parts[0].Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    layers.Add(new GroundLayer { Name = name, ThicknessM = t });
                }
            }

            if (z1 == null && layers.Count == 0)
            {
                error = "в файле не найдено ни Z1_m, ни слоёв.\n" +
                        "Ожидаемый формат:\n" +
                        "  Z1_m;209,79\n" +
                        "  Offset_m;-211,1\n" +
                        "  ColumnNumber;1\n" +
                        "  LayerName;Thickness_m\n" +
                        "  Песок;2,5";
                return false;
            }
            if (z1 == null) { error = "не найдена строка 'Z1_m;<значение>'."; return false; }
            if (layers.Count == 0)
            {
                error = "не найдено ни одного слоя после 'LayerName;Thickness_m'.";
                return false;
            }

            TbZ1.Text = z1.Value.ToString("0.###", Ru);
            if (offset.HasValue) TbZeroPoint.Text = offset.Value.ToString("0.###", Ru);
            if (!string.IsNullOrEmpty(colNum)) TbColumnNumber.Text = colNum;

            Layers.Clear();
            foreach (var l in layers) Layers.Add(l);
            UpdateSummary();
            UpdateZPreview();
            return true;
        }

        // ─────────────────────────────────────────────────────────
        //  OK / Cancel
        // ─────────────────────────────────────────────────────────

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParse(TbZ1.Text, out double z1))
            {
                MessageBox.Show("Не удалось разобрать Z1.\n" +
                                "Введите число, например: 209.79 или 209,79.");
                return;
            }
            if (!TryParse(TbZeroPoint.Text, out double offset))
            {
                MessageBox.Show("Не удалось разобрать сдвиг отметки.\n" +
                                "Введите число, например: -211.1 или -211,1.");
                return;
            }
            if (string.IsNullOrWhiteSpace(TbColumnNumber.Text))
            {
                MessageBox.Show("Номер колонки не может быть пустым."); return;
            }
            if (Layers.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один слой."); return;
            }
            if (Layers.Any(l => l.ThicknessM <= 0 ||
                               string.IsNullOrWhiteSpace(l.Name)))
            {
                MessageBox.Show("Толщины должны быть > 0, имена — не пустыми.");
                return;
            }

            Z1Meters = z1;
            OffsetMeters = offset;
            ColumnNumber = TbColumnNumber.Text.Trim();
            LastOffsetMeters = offset; // запоминаем в статике сессии
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        // ─────────────────────────────────────────────────────────
        //  Утилиты
        // ─────────────────────────────────────────────────────────

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool need = s.IndexOf(';') >= 0 || s.IndexOf('"') >= 0 ||
                        s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0;
            if (!need) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string[] SplitCsv(string line, char sep)
        {
            var result = new List<string>();
            var cur = new StringBuilder();
            bool inQ = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQ)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    { cur.Append('"'); i++; }
                    else if (c == '"') inQ = false;
                    else cur.Append(c);
                }
                else
                {
                    if (c == '"') inQ = true;
                    else if (c == sep) { result.Add(cur.ToString()); cur.Clear(); }
                    else cur.Append(c);
                }
            }
            result.Add(cur.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Парсит число в формате «209.79», «209,79», «-211.1», «-211,1».
        /// Порядок: InvariantCulture (точка) → ru-RU (запятая) → replace ','→'.'.
        /// </summary>
        private static bool TryParse(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim().Replace(" ", "");

            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return true;
            if (double.TryParse(t, NumberStyles.Float, Ru, out v))
                return true;
            if (double.TryParse(t.Replace(',', '.'), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out v))
                return true;
            return false;
        }

        /// <summary>
        /// SelectAll при входе в редактирование — без этого ввод «1» + «10.1»
        /// даёт «10.11» или «1.00010.1».
        /// </summary>
        private void GridLayers_PreparingCellForEdit(
            object sender,
            DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox tb)
                tb.SelectAll();
        }
    }

    /// <summary>
    /// Принимает и «1,2», и «1.2», отображает без хвостовых нулей.
    /// С UpdateSourceTrigger=LostFocus не «стирает» ввод во время набора.
    /// </summary>
    public class FlexibleDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d.ToString("0.###", culture ?? CultureInfo.CurrentCulture);
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            string s = value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(s)) return 0.0;

            string normalized = s.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double v))
                return v;

            if (double.TryParse(s, NumberStyles.Float,
                                culture ?? CultureInfo.CurrentCulture, out v))
                return v;

            return 0.0;
        }
    }
}