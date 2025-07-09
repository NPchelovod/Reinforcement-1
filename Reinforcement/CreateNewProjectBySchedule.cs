using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CreateNewProjectBySchedule : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            
            //Получаем текущее приложение Revit
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;

            //Указываем путь к папке, содержащей шаблон
            string templatePathDirectory = @"Y:\Revit\_ШаблоныРегламентыЕнисейстрой";

            // Ищем все файлы с расширением .rte в указанной директории
            string[] templateFiles = Directory.GetFiles(templatePathDirectory, "*.rte");

            // Проверяем, найден ли хотя бы один файл .rte
            if (templateFiles.Length == 0)
            {
                TaskDialog.Show("Ошибка", "В указанной директории не найдено файлов с расширением .rte.");
                return Result.Failed;
            }

            // Берем первый найденный файл .rte
            string templatePath = templateFiles[0];

            // Указываем путь для сохранения нового файла
            string newFilePath = @"C:\Users\Public\Documents\Новый проект ЕС_КР.rvt";

            try
            {
                // Создаем новый документ на основе шаблона
                Document newDoc = app.NewProjectDocument(templatePath);

                // Настройки сохранения
                SaveAsOptions saveOptions = new SaveAsOptions();
                saveOptions.OverwriteExistingFile = true; // Перезаписать файл, если он существует

                // Сохраняем новый документ
                newDoc.SaveAs(newFilePath, saveOptions);

                // Закрываем новый документ (чтобы его можно было открыть заново)
                newDoc.Close(false);

                // Открываем сохраненный файл в Revit
                uiapp.OpenAndActivateDocument(newFilePath);


                TaskDialog.Show("Успех", "Новый файл успешно создан, сохранен и открыт!");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // В случае ошибки выводим сообщение
                TaskDialog.Show("Ошибка", "Не удалось создать или открыть новый файл: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}





          


    
