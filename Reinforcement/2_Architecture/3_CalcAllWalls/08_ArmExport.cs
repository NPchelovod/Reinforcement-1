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
        public static readonly HashSet<EArmWall> SummedColumns = new HashSet<EArmWall>
        {
             EArmWall.WallLengthMM,// длина стены, мм
             EArmWall. WallAreaNetM2,// чистая площадь (с проёмами)  — HOST_AREA_COMPUTE
              EArmWall.WallAreaGrossM2,// габаритная площадь (без проёмов)
              EArmWall.WallOpeningsAreaM2,// площадь всех проёмов = Gross − Net
               EArmWall.WallPlanAreaM2,// площадь в плане: Length × Width

              EArmWall.CountDoorOrWindow,//количество окон и дверей
              EArmWall.CountKRBorder,// количество примыканий стены к стенам кр - максимум с 2 сторон
           
            
            EArmWall.DetailsRoofInt,//деталей в крыше
             EArmWall.DetailsWallRow,//рядов в стене деталей
            EArmWall.DetailsWallsInt,//деталей в стене
            EArmWall.ArmGeneralTotalMM,
            EArmWall.ArmTopTotalMM,
            EArmWall.ArmDetailsTotalMM,
            EArmWall.MinesArmFromWindAndDoorMM,
            EArmWall.MinesArmDetailsFromWindAndDoorMM,

            EArmWall.DetailsAllCount,
            EArmWall.ArmAllM,
            EArmWall.ArmAllM2

        };

        public static void Export(List<ECWall> eCWalls)
        {
            if (eCWalls == null || eCWalls.Count == 0)
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }

            var groups = eCWalls
                .Where(x => x != null && x.Curve != null && x.ECLevel != null)
                .GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups.Count == 0)
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }
            //if (!ArmElementConstants.CheckNameMaps())
            //{
            //    MessageBox.Show("Не все параметры имеют русские названия — проверьте словари.");
            //    return;
            //}

            var dlg = new WinForms.SaveFileDialog
            {
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = "Армирование стен.xlsx"
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            using (var workbook = new Workbook())
            {
                workbook.CreateEmptySheets(1);
                Worksheet sheet = workbook.Worksheets[0];
                sheet.Name = "Армирование";

                var stringParams = Enum.GetValues(typeof(EArmWallString)).Cast<EArmWallString>().ToList();
                var doubleParams = Enum.GetValues(typeof(EArmWall)).Cast<EArmWall>().ToList();
                int colCount = stringParams.Count + doubleParams.Count;

                // шапка
                int c = 1;
                foreach (var p in stringParams)
                    sheet.Range[1, c++].Text = ArmElementConstants.EArmWallStringNames[p];
                foreach (var p in doubleParams)
                    sheet.Range[1, c++].Text = ArmElementConstants.EArmWallNames[p];

                int row = 2;
                foreach (var levelGroup in groups)
                {
                    foreach (var typeGroup in levelGroup.GroupBy(x => x.EWallTypes).OrderBy(g => g.Key))
                    {
                        var walls = typeGroup
                            .OrderBy(x => x.WallType?.EWallTypes ?? 0)
                            .ThenByDescending(x => x.WidthM)
                            .ThenByDescending(x => x.WallType?.Name ?? string.Empty)
                            .ThenByDescending(x => x.RelationsWallsKR.Count)
                            .ThenByDescending(x => x.RelationsWalls.Count)
                            .ThenByDescending(x => x.ECDoorOrWindows.Count)
                            .ThenBy(x => x.AreaNetM2)
                            .ThenBy(x => x.Id)
                            .ToList();

                        int firstRowOfType = row;

                        foreach (var wall in walls)
                        {
                            var arm = wall.ECArmWallElement;
                            c = 1;
                            foreach (var p in stringParams)
                                sheet.Range[row, c++].Text = arm?[p] ?? string.Empty;
                            foreach (var p in doubleParams)
                                sheet.Range[row, c++].NumberValue = arm != null ? arm[p] : 0;
                            row++;
                        }

                        int lastRowOfType = row - 1;

                        // итоговая строка
                        sheet.Range[row, 1].Text = $"Итого: {typeGroup.Key} ({walls.Count} шт.)";
                        sheet.Range[row, 1].Style.Font.IsBold = true;

                        c = stringParams.Count + 1;
                        foreach (var p in doubleParams)
                        {
                            if (SummedColumns.Contains(p))
                            {
                                string colLetter = ColumnName(c);
                                sheet.Range[row, c].Formula =
                                    $"SUM({colLetter}{firstRowOfType}:{colLetter}{lastRowOfType})";
                            }
                            else
                            {
                                sheet.Range[row, c].Text = "";   // было 0
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

                // оформление
                CellRange dataRange = sheet.Range[1, 1, row - 1, colCount];
                dataRange.BorderAround();
                dataRange.BorderInside();
                dataRange.Style.Font.FontName = "Arial";
                dataRange.Style.Font.Size = 10;
                sheet.Range[2, stringParams.Count + 1, row - 1, colCount].NumberFormat = "0.##";

                sheet.AllocatedRange.AutoFitColumns();
                for (int i = 1; i <= stringParams.Count; i++)
                {
                    var col = sheet.Columns[i - 1];
                    if (col.ColumnWidth > 45) col.ColumnWidth = 45;
                }
                sheet.FreezePanes(2, 1);

                try
                {
                    workbook.SaveToFile(dlg.FileName, ExcelVersion.Version2010);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось сохранить файл: " + ex.Message);
                    return;
                }
            }

            int wallCount = groups.Sum(g => g.Count());
            MessageBox.Show($"Экспортировано стен: {wallCount}\nФайл: {dlg.FileName}");
        }

        // rowsCount(...) — удалить, больше не нужен

        private static int rowsCount(IEnumerable<IGrouping<double, ECWall>> groups)
        {
            return groups.SelectMany(g => g).Count();
        }
        /// <summary>
        /// Преобразует номер колонки (1-based) в буквы Excel: 1 -> "A", 27 -> "AA".
        /// </summary>
        private static string ColumnName(int index)
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
