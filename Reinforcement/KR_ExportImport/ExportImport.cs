using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{


    [Transaction(TransactionMode.Manual)]
    public class ExportImport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData); // ваш существующий код инициализации

            // Создаём и показываем окно выбора Excel
            var excelWindow = new ExcelImportWindow();
            bool? result = excelWindow.ShowDialog();

            if (result == true)
            {
                string filePath = excelWindow.SelectedFilePath;
                string sheetName = excelWindow.SelectedSheetName;

                // TODO: здесь вызовите метод формирования спецификации из выбранного листа
                // Например: CreateSpecificationFromExcel(filePath, sheetName);
                //TaskDialog.Show("Успех", $"Выбран файл: {filePath}\nЛист: {sheetName}");
                return Result.Succeeded;
            }
            else
            {
                return Result.Succeeded;
            }
        }
    }
 }
