using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ChangeScheduleFont : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            // Получаем все ViewSchedule в документе
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> schedules = collector.OfClass(typeof(ViewSchedule)).ToElements();

            using (Transaction t = new Transaction(doc, "Изменение шрифта в спецификациях"))
            {
                t.Start();

                // Перебираем все ViewSchedule
                foreach (ViewSchedule schedule in schedules)
                {
                    TableData tableData = schedule.GetTableData();

                    // Обрабатываем секцию Title (название таблицы)
                    TableSectionData titleSection = tableData.GetSectionData(SectionType.Header);
                    if (titleSection != null && titleSection.NumberOfRows > 0)
                    {
                        ProcessTableSection(titleSection, doc);
                    }

                    // Обрабатываем секцию Header (заголовки столбцов)
                    TableSectionData headerSection = tableData.GetSectionData(SectionType.Body);
                    if (headerSection != null && headerSection.NumberOfRows > 0)
                    {
                        ProcessTableSection(headerSection, doc);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }

        private void ProcessTableSection(TableSectionData sectionData, Document doc)
        {
            for (int row = 0; row < sectionData.NumberOfRows; row++)
            {
                for (int column = 0; column < sectionData.NumberOfColumns; column++)
                {
                    try
                    {
                        // Получаем текущий стиль ячейки
                        TableCellStyle cellStyle = sectionData.GetTableCellStyle(row, column);

                        // Меняем шрифт на ISOCPEUR
                        cellStyle.FontName = "ISOCPEUR";

                        // Применяем изменённый стиль обратно
                        sectionData.SetCellStyle(row, column, cellStyle);
                    }
                    catch (System.Exception ex)
                    {
                        // Выводим ошибку для конкретной ячейки
                        TaskDialog.Show("Ошибка", $"Ошибка при изменении стиля ячейки [Row: {row}, Column: {column}]\n{ex.Message}");
                    }
                }
            }
        }
    }
}
