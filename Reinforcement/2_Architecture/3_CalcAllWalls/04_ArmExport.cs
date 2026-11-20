
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spire.Xls;
using Autodesk.Revit.DB;
using System.Windows;
using WinForms = System.Windows.Forms; // алиас для WinForms
namespace Reinforcement
{
    public static class ArmExport
    {


        public static void Export(List<ECWall> eCWalls)
        {
            // 1. Группируем по уровню (по возрастанию отметки), внутри — по типу стены,
            //    внутри — сортируем по имени типоразмера
            var groups = eCWalls
                .Where(x => x != null && x.Curve != null && x.ECLevel != null)
                .GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key);

            // 2. Собираем плоский список строк данных
            var rowsData = new List<object[]>();

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

                    foreach (var wall in walls)
                    {
                        var arm = wall.ECArmWallElement;

                        rowsData.Add(new object[]
                        {
                    wall.ECLevel.LevelName ?? string.Empty,          // Уровень
                    wall.ECLevel.LevelElevation,                      // Отметка уровня
                    wall.EWallTypes.ToString(),                       // Тип стены
                    wall.WallType?.NameWall ?? string.Empty,          // Типоразмер
                    wall.Id,                                          // Id стены
                    arm?.EC_ArmRow ?? 0,                              // Кол-во рядов
                    arm?.EC_ArmDopRow ?? 0,                           // Доп. ряды (кирпич)
                    arm?.EC_ArmStepMM ?? 0,                           // Шаг армирования, мм
                    arm?.EC_ArmDetailArmLengthMM ?? 0,                // Длина арм. обрамления, мм
                    arm?.EC_ArmDetailArmCount ?? 0,                   // Кол-во арм. обрамления
                    arm?.EC_Details ?? 0,                             // Кол-во деталей
                    arm?.EC_StepDetailsMM ?? 0,                       // Шаг деталей, мм
                    arm?.EC_StepDetailsRoofMM ?? 0,                   // Шаг крепежа к потолку
                    arm?._detailsWallsInt ?? 0,                       // Кол-во боковых деталей
                    arm?._armGeneralTotalMM ?? 0,                     // Арм. основная, мм
                    arm?._armTopTotalMM ?? 0,                         // Арм. верха, мм
                    arm?._armDetaisTotalMM ?? 0,                      // Арм. обрамления, мм
                    arm?._minesArmFromWindAndDoor ?? 0,               // Вычет по проёмам (арм.)
                    arm?._minesArmDetaisFromWindAndDoor ?? 0          // Вычет по проёмам (детали)
                        });
                    }
                }
            }

            if (rowsData.Count == 0)
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }

            // 3. Диалог сохранения
            var dlg = new WinForms.SaveFileDialog
            {
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                FileName = "Армирование стен.xlsx"
            };

            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            // 4. Формируем книгу
            var workbook = new Workbook();
            workbook.CreateEmptySheets(1);
            Worksheet sheet = workbook.Worksheets[0];
            sheet.Name = "Армирование";

            string[] headers =
            {
        "Уровень",
        "Отметка уровня, м",
        "Тип стены",
        "Типоразмер",
        "Id стены",
        "Кол-во рядов армирования",
        "Доп. ряды (кирпич)",
        "Шаг армирования, мм",
        "Длина арм. обрамления, мм",
        "Кол-во арм. обрамления",
        "Кол-во деталей",
        "Шаг деталей, мм",
        "Шаг крепежа к потолку, мм",
        "Кол-во боковых деталей",
        "Арм. основная, мм",
        "Арм. верха, мм",
        "Арм. обрамления, мм",
        "Вычет по проёмам (арм.), мм",
        "Вычет по проёмам (детали), мм"
    };

            int colCount = headers.Length;

            // Шапка
            for (int c = 1; c <= colCount; c++)
            {
                sheet.Range[1, c].Text = headers[c - 1];
            }

            CellRange headerRange = sheet.Range[1, 1, 1, colCount];
            headerRange.Style.Font.IsBold = true;
            //headerRange.Style.Color = Color.FromArgb(211, 211, 211);
            headerRange.BorderAround();
            headerRange.BorderInside();

            // Данные
            for (int r = 0; r < rowsData.Count; r++)
            {
                object[] rowData = rowsData[r];
                int excelRow = r + 2; // 1-я строка — шапка

                for (int c = 0; c < colCount; c++)
                {
                    CellRange cell = sheet.Range[excelRow, c + 1];

                    if (rowData[c] is string s)
                    {
                        cell.Text = s;
                    }
                    else if (rowData[c] is double d)
                    {
                        cell.NumberValue = d;
                    }
                    else
                    {
                        cell.Value = rowData[c]?.ToString() ?? string.Empty;
                    }
                }
            }

            // Оформление числовых колонок: 2 знака после запятой
            foreach (int col in new[] { 2, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 })
            {
                sheet.Range[2, col, rowsData.Count + 1, col].NumberFormat = "0.##";
            }

            // Границы и автоширина
            CellRange dataRange = sheet.Range[1, 1, rowsData.Count + 1, colCount];
            dataRange.BorderAround();
            dataRange.BorderInside();
            dataRange.Style.Font.FontName = "Arial";
            dataRange.Style.Font.Size = 10;

            sheet.AllocatedRange.AutoFitColumns();

            // Закрепляем шапку
            sheet.FreezePanes(2, 1);

            // 5. Сохраняем
            try
            {
                workbook.SaveToFile(dlg.FileName, ExcelVersion.Version2010);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить файл: " + ex.Message);
                return;
            }

            MessageBox.Show($"Экспортировано стен: {rowsData.Count}");
        }


    }


    
}
