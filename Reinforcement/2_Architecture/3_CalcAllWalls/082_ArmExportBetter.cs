using Autodesk.Revit.DB;
using Spire.Xls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinForms = System.Windows.Forms;
namespace Reinforcement
{

    //var sheetDoorsAndWindows = new ExportElementOnSheetExcell
    //{
    //    SheetName = "ДвериОкна",
    //    exportStringDoubleDatas = new List<List<IExportStringDoubleData>>
    //{
    //    ECDoorOrWindows1.Cast<IExportStringDoubleData>().ToList(),  // 1-й этаж
    //    ECDoorOrWindows2.Cast<IExportStringDoubleData>().ToList(),  // 2-й этаж
    //    ECDoorOrWindows3.Cast<IExportStringDoubleData>().ToList(),  // ...
    //}
    //};

    /// <summary>
    /// класс экспорта в один лист экселя
    /// </summary>
    public class ExportElementOnSheetExcell
    {
        //один List<IExportStringDoubleData> - это например 10 строк данных в конце которых итог, дальше идет второй List<IExportStringDoubleData>...
        public List<List<IExportStringDoubleData>> exportStringDoubleDatas;
        public string SheetName;

    }
  

    public class ExcelGroupExporter
    {
        // ---------- многолистовой экспорт ----------
        public static void Exporter(
        List<ExportElementOnSheetExcell> exportSheetsElements,
        string fileName)
        {
            if (exportSheetsElements == null || exportSheetsElements.Count == 0) return;

            var dlg = new WinForms.SaveFileDialog
            {
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = $"{fileName}.xlsx",
                OverwritePrompt = true
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            string path = dlg.FileName;
            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                bool fileExists = System.IO.File.Exists(path);
                bool loaded = false;

                using (var workbook = new Workbook())
                {
                    // --- 1) загрузка (если файл существует) ---
                    if (fileExists)
                    {
                        try
                        {
                            workbook.LoadFromFile(path);
                            loaded = true;
                        }
                        catch (Exception ex)
                        {
                            var choice = MessageBox.Show(
                                "Не удалось открыть файл:\n" + ex.Message +
                                "\n\nДа — перезаписать с нуля" +
                                "\nНет — выбрать другой файл" +
                                "\nОтмена — прервать экспорт",
                                "Ошибка открытия",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Warning);

                            if (choice == MessageBoxResult.Cancel) return;
                            if (choice == MessageBoxResult.No)
                            {
                                if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
                                path = dlg.FileName;
                                continue;   // новая попытка с новым путём
                            }
                            // Yes → работаем как с пустым файлом
                        }
                    }

                    // --- 2) подготовка листов ---
                    string tempName = null;
                    if (!loaded)
                    {
                        workbook.Worksheets.Clear();
                    }
                    else
                    {
                        tempName = RemoveMatchingSheets(workbook, exportSheetsElements);
                    }

                    // --- 3) добавление своих листов ---
                    foreach (var exportElement in exportSheetsElements)
                    {
                        Worksheet sheet = workbook.Worksheets.Add(exportElement.SheetName);
                        var exporter = new ExcelGroupExporter(sheet);
                        foreach (var exportData in exportElement.exportStringDoubleDatas)
                            exporter.AppendGroup(exportData);
                        exporter.FinalizeSheet();
                    }

                    // --- 4) удаление временного листа (если создавали) ---
                    if (tempName != null)
                    {
                        for (int i = 0; i < workbook.Worksheets.Count; i++)
                        {
                            if (string.Equals(workbook.Worksheets[i].Name, tempName,
                                              StringComparison.Ordinal))
                            {
                                workbook.Worksheets.RemoveAt(i);
                                break;
                            }
                        }
                    }

                    // --- 5) сохранение ---
                    try
                    {
                        workbook.SaveToFile(path, ExcelVersion.Version2010);
                        return;   // успех
                    }
                    catch (Exception ex)
                    {
                        var choice = MessageBox.Show(
                            "Не удалось сохранить файл:\n" + ex.Message +
                            "\n\nДа — выбрать другой файл и повторить" +
                            "\nНет — прервать",
                            "Ошибка сохранения",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (choice != MessageBoxResult.Yes) return;
                        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
                        path = dlg.FileName;
                        // новая итерация цикла
                    }
                }
            }

            MessageBox.Show(
                $"Не удалось выполнить экспорт после {maxAttempts} попыток.",
                "Экспорт", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Удаляет листы, имена которых совпадают с SheetName из экспорта.
        /// Если удаляются все листы — переименовывает один во временное имя,
        /// чтобы обойти ограничение «нельзя удалить последний лист»,
        /// и возвращает это имя (для удаления в вызывающем коде).
        /// Если удаление безопасно — возвращает null.
        /// </summary>
        private static string RemoveMatchingSheets(
            Workbook workbook,
            List<ExportElementOnSheetExcell> exportSheetsElements)
        {
            var toRemove = new HashSet<string>(
                exportSheetsElements.Select(x => x.SheetName),
                StringComparer.OrdinalIgnoreCase);

            var matching = new List<int>();
            for (int i = 0; i < workbook.Worksheets.Count; i++)
                if (toRemove.Contains(workbook.Worksheets[i].Name))
                    matching.Add(i);

            if (matching.Count == 0) return null;   // нечего удалять

            bool allWillBeRemoved = matching.Count == workbook.Worksheets.Count;

            if (allWillBeRemoved)
            {
                string tempName = "__tmp__" + Guid.NewGuid().ToString("N").Substring(0, 8);
                workbook.Worksheets[0].Name = tempName;

                for (int k = matching.Count - 1; k >= 1; k--)
                    workbook.Worksheets.RemoveAt(matching[k]);

                return tempName;
            }

            for (int k = matching.Count - 1; k >= 0; k--)
                workbook.Worksheets.RemoveAt(matching[k]);

            return null;
        }

        // ---------- состояние одного листа ----------
        private readonly Worksheet _sheet;

        private readonly Dictionary<string, int> _headerToCol =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<int> _doubleCols = new HashSet<int>();
        private readonly HashSet<int> _sumCols = new HashSet<int>();

        private int _nextHeaderCol = 1;   // следующий свободный столбец в шапке
        private int _nextRow = 2;   // следующая свободная строка
        private bool _headersWritten = false;

        public ExcelGroupExporter(Worksheet sheet)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        }

        /// <summary>
        /// Дописывает одну группу (List&lt;IExportStringDoubleData&gt;) ниже уже
        /// записанного. Новые колонки добавляются в шапку на лету.
        /// После строк группы ставит итоговую строку.
        /// </summary>
        public void AppendGroup(List<IExportStringDoubleData> group)
        {
            if (group == null || group.Count == 0) return;

            // 1) получаем данные
            var allData = new List<List<ExportElement>>(group.Count);
            var failed = new List<string>();
            foreach (var item in group)
            {
                if (item == null) continue;
                try
                {
                    var data = item.GetCalcData();
                    if (data != null && data.Count > 0)
                        allData.Add(data);
                }
                catch (Exception ex)
                {
                    failed.Add($"{item.GetType().Name}: {ex.Message}");
                }
                }
        if (failed.Count > 0)
        {
            // можно записать в лог, MessageBox, в первую строку листа — на ваш вкус
            System.Diagnostics.Debug.WriteLine(
                "GetCalcData() ошибки:\n" + string.Join("\n", failed));
        }
        if (allData.Count == 0) return;

            // 2) объединяем шапку: если появились новые колонки — заводим их
            MergeHeaders(allData[0]);
            _headersWritten = true;

            int firstDataRow = _nextRow;

            // 3) строки группы
            foreach (var rowData in allData)
            {
                // 3a) сначала адреса ВСЕМ элементам строки
                foreach (var el in rowData)
                {
                    if (_headerToCol.TryGetValue(el.Name ?? string.Empty, out int col))
                        el.CellExcell = ColumnName(col) + _nextRow;
                }

                // 3b) потом пишем (числа/строки/формулы)
                foreach (var el in rowData)
                {
                    if (!_headerToCol.TryGetValue(el.Name ?? string.Empty, out int col))
                        continue;   // колонки нет в шапке (странно, но безопасно)

                    if (el.ExportDoubleOrString)
                    {
                        el.Calculations();

                        if (el.CorrectExcellFormula && !string.IsNullOrEmpty(el.FormulaExcell))
                            _sheet.Range[_nextRow, col].Formula = "=" + el.FormulaExcell;
                        else
                            _sheet.Range[_nextRow, col].NumberValue = el.ValueDouble;
                    }
                    else
                    {
                        _sheet.Range[_nextRow, col].Text = el.ValueString ?? string.Empty;
                    }
                }
                _nextRow++;
            }

            int lastDataRow = _nextRow - 1;

            // 4) итоговая строка группы
            WriteTotalsRow(firstDataRow, lastDataRow);
            _nextRow++;
        }

        /// <summary>
        /// Финальное оформление. Вызывать один раз после всех AppendGroup.
        /// </summary>
        public void FinalizeSheet()
        {
            if (!_headersWritten) return;

            int lastRow = _nextRow - 1;
            int lastCol = MaxCol();

            var dataRange = _sheet.Range[1, 1, lastRow, lastCol];
            dataRange.BorderAround();
            dataRange.BorderInside();
            dataRange.Style.Font.FontName = "Arial";
            dataRange.Style.Font.Size = 10;

            foreach (int col in _doubleCols)
                _sheet.Range[2, col, lastRow, col].NumberFormat = "0.##";

            _sheet.AllocatedRange.AutoFitColumns();
            _sheet.FreezePanes(2, 1);
        }

        // ---------- helpers ----------

        /// <summary>
        /// Добавляет в шапку только те колонки из шаблона,
        /// которых ещё нет. Старые колонки не трогает.
        /// </summary>
        private void MergeHeaders(List<ExportElement> template)
        {
            foreach (var el in template)
            {
                string header = !string.IsNullOrEmpty(el.Name)
                    ? el.Name
                    : ("Column_" + _nextHeaderCol);

                if (_headerToCol.ContainsKey(header))
                    continue;   // колонка уже есть

                int col = _nextHeaderCol++;
                _sheet.Range[1, col].Text = header;
                _headerToCol[header] = col;

                if (el.ExportDoubleOrString) _doubleCols.Add(col);
                if (el.ExportDoubleOrString && el.SumItog) _sumCols.Add(col);
            }
        }

        private void WriteTotalsRow(int firstDataRow, int lastDataRow)
        {
            _sheet.Range[_nextRow, 1].Text = "Итого";
            _sheet.Range[_nextRow, 1].Style.Font.IsBold = true;

            foreach (int col in _sumCols)
            {
                string colLetter = ColumnName(col);
                _sheet.Range[_nextRow, col].Formula =
                    $"=SUM({colLetter}{firstDataRow}:{colLetter}{lastDataRow})";
                if (_doubleCols.Contains(col))
                    _sheet.Range[_nextRow, col].NumberFormat = "0.##";
            }

            var rowRange = _sheet.Range[_nextRow, 1, _nextRow, MaxCol()];
            rowRange.Style.Font.IsBold = true;
            rowRange.Style.Color = System.Drawing.Color.FromArgb(230, 230, 230);
            rowRange.BorderAround();
        }

        private int MaxCol() => _nextHeaderCol - 1 > 0 ? _nextHeaderCol - 1 : 1;

        public static string ColumnName(int index)
        {
            int n = index;
            string name = string.Empty;
            while (n > 0)
            {
                int rem = (n - 1) % 26;
                name = (char)('A' + rem) + name;
                n = (n - 1) / 26;
            }
            return name;
        }
    }
}
