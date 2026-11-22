using Autodesk.Revit.DB;
using Spire.Xls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinForms = System.Windows.Forms;
namespace Reinforcement
{


     public class vvv
    {
        public void exporter()
        {
            var dlg = new WinForms.SaveFileDialog
            {
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = "ДвериОкна.xlsx"
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            using (var workbook = new Workbook())
            {
                workbook.CreateEmptySheets(1);
                var sheet = workbook.Worksheets[0];
                sheet.Name = "ДвериОкна";

                var exporter = new ExcelGroupExporter(sheet);

                // группы вы уже как-то подготовили
                // 1) получаем данные
                var allData = new List<List<ExportElement>>(ECDoorOrWindows1.Count);

                exporter.AppendGroup(ECDoorOrWindows1);
                exporter.AppendGroup(ECDoorOrWindows2);
                exporter.AppendGroup(ECDoorOrWindows3);

                exporter.FinalizeSheet();

                workbook.SaveToFile(dlg.FileName, ExcelVersion.Version2010);
            }
        }
    }

    public class ExcelGroupExporter
    {


        private readonly Worksheet _sheet;
        private readonly Dictionary<string, int> _headerToCol =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<int> _doubleCols = new HashSet<int>();
        private readonly HashSet<int> _sumCols = new HashSet<int>();

        private int _nextRow = 1;
        private bool _headersWritten = false;

        public ExcelGroupExporter(Worksheet sheet)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        }

        /// <summary>
        /// Дописывает одну группу (например, ECDoorOrWindows по уровню/типу).
        /// Первый вызов создаёт шапку из первой строки GetCalcData(),
        /// последующие — дописывают ниже. После строк группы ставит «Итого».
        /// </summary>
        public void AppendGroup(List<IExportStringDoubleData> group)
        {
            if (group == null || group.Count == 0) return;

            // 1) получаем данные
            var allData = new List<List<ExportElement>>(group.Count);
            foreach (var item in group)
            {
                var data = item?.GetCalcData();
                if (data != null && data.Count > 0)
                    allData.Add(data);
            }
            if (allData.Count == 0) return;

            // 2) шапка (один раз)
            if (!_headersWritten)
            {
                WriteHeaders(allData[0]);
                _headersWritten = true;
                _nextRow = 2;
            }

            int firstDataRow = _nextRow;

            // 3) строки группы
            foreach (var rowData in allData)
            {
                // 3a) сначала назначаем CellExcell ВСЕМ элементам строки
                foreach (var el in rowData)
                {
                    if (_headerToCol.TryGetValue(el.Name ?? string.Empty, out int col))
                        el.CellExcell = ColumnName(col) + _nextRow;
                }

                // 3b) только теперь считаем формулы и пишем
                foreach (var el in rowData)
                {
                    if (!_headerToCol.TryGetValue(el.Name ?? string.Empty, out int col))
                        continue;

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
        /// Финальное оформление листа: рамки, шрифт, ширины, freeze.
        /// Вызвать один раз после всех AppendGroup.
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

        // --- helpers -------------------------------------------------------

        private void WriteHeaders(List<ExportElement> template)
        {
            for (int i = 0; i < template.Count; i++)
            {
                int col = i + 1;
                var el = template[i];
                string header = !string.IsNullOrEmpty(el.Name) ? el.Name : ("Column " + col);

                _sheet.Range[1, col].Text = header;

                if (_headerToCol.ContainsKey(header)) continue;   // дубликат — игнор

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
            }

            var rowRange = _sheet.Range[_nextRow, 1, _nextRow, MaxCol()];
            rowRange.Style.Font.IsBold = true;
            rowRange.Style.Color = System.Drawing.Color.FromArgb(230, 230, 230);
            rowRange.BorderAround();
        }

        private int MaxCol()
        {
            int max = 0;
            foreach (var v in _headerToCol.Values) if (v > max) max = v;
            return max == 0 ? 1 : max;
        }

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