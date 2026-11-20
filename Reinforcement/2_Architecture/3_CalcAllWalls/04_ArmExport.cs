using Autodesk.Revit.DB;
using Spire.Xls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Reinforcement
{
    public static class ArmExport
    {
        /// <summary>
        /// Колонки-суммы для итоговой строки по каждому типу стены.
        /// </summary>
        private static readonly HashSet<EArmWall> SummedColumns = new HashSet<EArmWall>
        {
            EArmWall.DetailsRoofInt,
            EArmWall.DetailsWallsInt,
            EArmWall.ArmGeneralTotalMM,
            EArmWall.ArmTopTotalMM,
            EArmWall.ArmDetailsTotalMM,
            EArmWall.MinesArmFromWindAndDoor,
            EArmWall.MinesArmDetailsFromWindAndDoor
        };

        public static void Export(List<ECWall> eCWalls)
        {
            // 1. Группируем: уровень (по отметке) → тип стены → сортировка по типоразмеру
            var groups = eCWalls
                .Where(x => x != null && x.Curve != null && x.ECLevel != null)
                .GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key);

            if (!groups.Any())
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }

            // 2. Диалог сохранения
            var dlg = new WinForms.SaveFileDialog
            {
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = "Армирование стен.xlsx"
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            // 3. Формируем книгу
            var workbook = new Workbook();
            workbook.CreateEmptySheets(1);
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Армирование";

            // --- Порядок колонок: сначала строковые, потом числовые ---
            var stringParams = Enum.GetValues(typeof(EArmWallString)).Cast<EArmWallString>().ToList();
            var doubleParams = Enum.GetValues(typeof(EArmWall)).Cast<EArmWall>().ToList();

            int colCount = stringParams.Count + doubleParams.Count;

            // Шапка из словарей
            int c = 1;
            foreach (var p in stringParams)
                sheet.Range[1, c++].Text = ECArmWallElement.EArmWallStringNames[p];

            foreach (var p in doubleParams)
                sheet.Range[1, c++].Text = ECArmWallElement.EArmWallNames[p];

            CellRange headerRange = sheet.Range[1, 1, 1, colCount];
            headerRange.Style.Font.IsBold = true;
            headerRange.BorderAround();
            headerRange.BorderInside();

            // 4. Заполнение данных
            int row = 2;
            int startDataRow = 2; // первая строка данных (для формул итогов)

            foreach (var levelGroup in groups)
            {
                var byType = levelGroup
                    .GroupBy(x => x.EWallTypes)
                    .OrderBy(g => g.Key);

                foreach (var typeGroup in byType)
                {
                    var walls = typeGroup
                        .OrderBy(x => x.WallType?.NameWall ?? string.Empty)
                        .ThenBy(x => x.Id)
                        .ToList();

                    int firstRowOfType = row;

                    foreach (var wall in walls)
                    {
                        var arm = wall.ECArmWallElement;

                        c = 1;
                        // строковые значения
                        foreach (var p in stringParams)
                        {
                            string val = string.Empty;
                            if (arm != null)
                                val = arm[p] ?? string.Empty;
                            sheet.Range[row, c++].Text = val;
                        }

                        // числовые значения
                        foreach (var p in doubleParams)
                        {
                            double val = arm != null ? arm[p] : 0;
                            sheet.Range[row, c++].NumberValue = val;
                        }

                        row++;
                    }

                    // --- Итоговая строка по типу стены ---
                    int lastRowOfType = row - 1;

                    sheet.Range[row, 1].Text =
                        $"Итого: {typeGroup.Key} ({walls.Count} шт.)";
                    sheet.Range[row, 1].Style.Font.IsBold = true;

                    // строковые колонки оставляем пустыми,
                    // числовые: только те, что в SummedColumns
                    c = stringParams.Count + 1;
                    foreach (var p in doubleParams)
                    {
                        if (SummedColumns.Contains(p))
                        {
                            char colLetter = (char)('A' + c - 1); // колонки до Z — нам хватает
                            sheet.Range[row, c].Formula =
                                $"SUM({colLetter}{firstRowOfType}:{colLetter}{lastRowOfType})";
                        }
                        else
                        {
                            sheet.Range[row, c].NumberValue = 0;
                        }
                        c++;
                    }

                    CellRange totalRange = sheet.Range[row, 1, row, colCount];
                    totalRange.Style.Font.IsBold = true;
                    totalRange.Style.Color = System.Drawing.Color.FromArgb(230, 230, 230);
                    totalRange.BorderAround();

                    row++;
                }
            }

            // 5. Оформление
            CellRange dataRange = sheet.Range[1, 1, row - 1, colCount];
            dataRange.BorderAround();
            dataRange.BorderInside();
            dataRange.Style.Font.FontName = "Arial";
            dataRange.Style.Font.Size = 10;

            // числовые колонки: 2 знака после запятой
            sheet.Range[2, stringParams.Count + 1, row - 1, colCount].NumberFormat = "0.##";

            sheet.AllocatedRange.AutoFitColumns();
            sheet.FreezePanes(2, 1);

            // 6. Сохраняем
            try
            {
                workbook.SaveToFile(dlg.FileName, ExcelVersion.Version2010);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить файл: " + ex.Message);
                return;
            }

            MessageBox.Show($"Экспортировано стен: {rowsCount(groups)}");
        }

        private static int rowsCount(IEnumerable<IGrouping<double, ECWall>> groups)
        {
            return groups.SelectMany(g => g).Count();
        }
    }
}
