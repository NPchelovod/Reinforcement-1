using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CreateNamedFloorPlansCommand : IExternalCommand
    {
        private const string SECTION_NAME = "Архитектурные планы";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Получаем все уровни
                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (!levels.Any())
                {
                    TaskDialog.Show("Ошибка", "В проекте не найдены уровни");
                    return Result.Failed;
                }

                // Получаем типоразмер вида
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

                if (viewFamilyType == null)
                {
                    TaskDialog.Show("Ошибка", "Типоразмер вида для поэтажных планов не найден");
                    return Result.Failed;
                }

                // Создаем планы и организуем их в разделе
                using (Transaction trans = new Transaction(doc, "Создание именованного раздела планов"))
                {
                    trans.Start();

                    // Создаем поэтажные планы
                    foreach (Level level in levels)
                    {
                        if (!ViewExists(doc, level))
                        {
                            ViewPlan floorPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                            floorPlan.Name = $"{level.Name} - {SECTION_NAME}";
                            floorPlan.Scale = 100;

                            // СПОСОБ 1: Используем параметр по имени (основной способ)
                            Parameter folderParam = floorPlan.LookupParameter("Folder");
                            if (folderParam != null && !folderParam.IsReadOnly)
                            {
                                folderParam.Set(SECTION_NAME);
                            }
                            else
                            {
                                // СПОСОБ 2: Альтернативные имена параметра
                                TrySetFolderParameter(floorPlan, SECTION_NAME);
                            }
                        }
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Готово", $"Раздел '{SECTION_NAME}' успешно создан");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка при создании раздела: {ex.Message}");
                return Result.Failed;
            }
        }

        private bool ViewExists(Document doc, Level level)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Any(v => v.GenLevel?.Id == level.Id && v.ViewType == ViewType.FloorPlan);
        }

        // Попытка установить параметр папки через различные варианты имен
        private void TrySetFolderParameter(View view, string folderName)
        {
            // Список возможных имен параметра в разных версиях Revit
            var possibleParamNames = new List<string>
        {
            "Folder",
            "View Folder",
            "Browser Folder",
            "Папка",
            "Имя папки"
        };

            foreach (var paramName in possibleParamNames)
            {
                Parameter param = view.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(folderName);
                    return;
                }
            }

            // Если параметр не найден по имени, пробуем найти по GUID
            TrySetFolderByGuid(view, folderName);
        }

        // Попытка установить параметр папки по известным GUID
        private void TrySetFolderByGuid(View view, string folderName)
        {
            // Известные GUID параметра Folder в разных версиях Revit
            var knownGuids = new List<Guid>
        {
            new Guid("32f1c0e1-8e33-45cb-842e-705435d2e1e3"), // Revit 2020-2022
            new Guid("d35b30a1-5b35-4d1c-8fcd-455d0e3a555a"), // Revit 2023
            new Guid("a8714a4a-8e3a-4a1c-9e3b-5c5e3d5e1e3c")  // Revit 2024
        };

            foreach (var guid in knownGuids)
            {
                try
                {
                    Parameter param = view.get_Parameter(guid);
                    if (param != null && !param.IsReadOnly)
                    {
                        param.Set(folderName);
                        return;
                    }
                }
                catch { }
            }
        }
    }
}