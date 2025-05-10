using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;


/*
 * Поиск совпадающих вентшахт на уровнях, которые можно сгруппировать
 */

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_1Create_new_plan_floor
    {
        static AddInId addinId = new AddInId(new Guid("424E29F8-20DE-49CB-8CF0-8627879F97C3"));
        public Result Create_new_plan_floor( ExternalCommandData commandData, ref string message, ElementSet elements) //ref 

        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            UIApplication uiApp = commandData.Application;

            try
            {
                // 1. Получаем уровень для создания плана
                Level level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();

                if (level == null)
                {
                    TaskDialog.Show("Ошибка", "В проекте не найдены уровни");
                    return Result.Failed;
                }

                // 2. Получаем тип вида для плана этажа
                ElementId viewFamilyTypeId = GetFloorPlanViewTypeId(doc);
                if (viewFamilyTypeId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Ошибка", "Не найден тип вида для плана этажа");
                    return Result.Failed;
                }

                // 3. Создаем новый план этажа
                ViewPlan newPlan = null;

                using (Transaction trans = new Transaction(doc, "Создать план этажа"))
                {
                    trans.Start();

                    newPlan = ViewPlan.Create(doc, viewFamilyTypeId, level.Id);

                    if (newPlan == null)
                    {
                        TaskDialog.Show("Ошибка", "Не удалось создать план этажа");
                        return Result.Failed;
                    }

                    // Устанавливаем имя плана
                    newPlan.Name = "Мой новый план";

                    // Устанавливаем масштаб (1:100)
                    newPlan.Scale = 105;

                    trans.Commit();
                }

                // 4. Скрываем все элементы на новом плане
                if (newPlan != null)
                {
                    using (Transaction trans = new Transaction(doc, "Скрыть элементы"))
                    {
                        trans.Start();

                        var collector = new FilteredElementCollector(doc, newPlan.Id)
                            .WhereElementIsNotElementType()
                            .Where(e => e.CanBeHidden(newPlan));

                        List<ElementId> elementsToHide = collector.Select(e => e.Id).ToList();

                        if (elementsToHide.Count > 0)
                        {
                            newPlan.HideElements(elementsToHide);
                        }

                        trans.Commit();
                    }
                }

                uiApp.ActiveUIDocument.ActiveView = newPlan;
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        // Метод для получения ID типа вида плана этажа
        private ElementId GetFloorPlanViewTypeId(Document doc)
        {
            // Получаем все типы видов плана этажа
            var viewFamilyTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .Where(x => x.ViewFamily == ViewFamily.FloorPlan);

            // Возвращаем первый найденный или InvalidElementId
            return viewFamilyTypes.FirstOrDefault()?.Id ?? ElementId.InvalidElementId;
        }
    }
}



          

















