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
using System.Windows.Input;

namespace Reinforcement.Aspirant
{
    public partial class GroundLayersWindow : Window
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static double LastOffsetMeters { get; private set; } = double.NaN;

        public static void EnsureOffsetInitialized(double basePointElevationM)
        {
            if (double.IsNaN(LastOffsetMeters))
                LastOffsetMeters = -basePointElevationM;
        }

        public double Z1Meters { get; private set; }
        public double OffsetMeters { get; private set; }
        public double XMeters { get; private set; }
        public double YMeters { get; private set; }
        public string ColumnNumber { get; private set; } = "1";

        public double EffectiveZ1Meters => Z1Meters + OffsetMeters;

        public ObservableCollection<GroundLayer> Layers { get; }
            = new ObservableCollection<GroundLayer>();

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
                $"({(-basePointElevationM):0.###} м) — отметка в Revit = Z1. " +
                $"X, Y — координаты скважины в метрах (0,0 = начало координат).";

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
        //  Превью
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
            sb.Append("X_m;").Append(TbX.Text).Append("\r\n");
            sb.Append("Y_m;").Append(TbY.Text).Append("\r\n");
            sb.Append("LayerName;Thickness_m\r\n");
            foreach (var l in Layers)
                sb.Append(CsvEscape(l.Name)).Append(';')
                  .Append(l.ThicknessM.ToString("G", Ru))
                  .Append("\r\n");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────
        //  CSV: импорт (с автоопределением кодировки)
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
                string text = ReadTextAuto(dlg.FileName);
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

        /// <summary>
        /// Читает файл, автоматически определяя кодировку:
        /// UTF-8/UTF-16 по BOM, иначе строгий UTF-8, иначе Windows-1251.
        /// Это исправляет «3а» → «3�» при файлах из русского Excel (CP1251).
        /// </summary>
        private static string ReadTextAuto(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            // BOM UTF-8
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            // BOM UTF-16 LE
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            // BOM UTF-16 BE
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            // Строгий UTF-8 без BOM — если валидный, значит это UTF-8
            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true)
                    .GetString(bytes);
            }
            catch { /* не UTF-8, идём дальше */ }

            // Fallback: Windows-1251 (русский ANSI)
            try
            {
                return Encoding.GetEncoding(1251).GetString(bytes);
            }
            catch { }

            // Последний шанс — UTF-8 без проверки
            return Encoding.UTF8.GetString(bytes);
        }

        private bool ApplyCsv(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text)) { error = "файл пуст."; return false; }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var rawLines = normalized.Split('\n');

            double? z1 = null;
            double? offset = null;
            double? x = null, y = null;
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
                    else if (key == "x_m" || key == "x")
                    {
                        if (TryParse(parts[1], out double v)) x = v;
                    }
                    else if (key == "y_m" || key == "y")
                    {
                        if (TryParse(parts[1], out double v)) y = v;
                    }
                    else if (key == "layername" || key == "name")
                    {
                        inLayers = true;
                    }
                    else
                    {
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
                        "  X_m;0\n" +
                        "  Y_m;0\n" +
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
            if (x.HasValue) TbX.Text = x.Value.ToString("0.###", Ru);
            if (y.HasValue) TbY.Text = y.Value.ToString("0.###", Ru);

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
                MessageBox.Show("Не удалось разобрать сдвиг отметки.");
                return;
            }
            if (!TryParse(TbX.Text, out double x))
            {
                MessageBox.Show("Не удалось разобрать X.");
                return;
            }
            if (!TryParse(TbY.Text, out double y))
            {
                MessageBox.Show("Не удалось разобрать Y.");
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
            XMeters = x;
            YMeters = y;
            ColumnNumber = TbColumnNumber.Text.Trim();
            LastOffsetMeters = offset;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        // ─────────────────────────────────────────────────────────
        //  SelectAll при входе в ячейку (не сбрасывая ввод)
        // ─────────────────────────────────────────────────────────

        private void GridLayers_PreparingCellForEdit(
            object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox tb &&
                e.EditingEventArgs is MouseButtonEventArgs)
            {
                // SelectAll через MouseUp, а не сразу — иначе DataGrid
                // сбрасывает выделение, ставя каретку в точку клика.
                tb.PreviewMouseLeftButtonUp += OnFirstMouseUpSelectAll;
            }
        }

        private void OnFirstMouseUpSelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.PreviewMouseLeftButtonUp -= OnFirstMouseUpSelectAll;
                tb.SelectAll();
            }
        }

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
    }

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