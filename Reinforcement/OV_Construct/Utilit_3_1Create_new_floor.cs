using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    internal class Utilit_3_1Create_new_floor 
    {
        public static Result Create_new_floor(Dictionary<string, List<string>> Dict_sovpad_level, ForgeTypeId units, ref string message, ElementSet elements, Document doc)
        {

            foreach (var otm in Dict_sovpad_level)
            {
                // 1. Задаём искомую высотную отметку (в единицах проекта)
                double targetElevation = Convert.ToDouble(otm.Key); // Например, 3 метра

                // 2. Находим уровень с заданной отметкой

                Level targetLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault(level => Math.Abs(UnitUtils.ConvertFromInternalUnits(level.Elevation, units) - targetElevation) < 300); // Учитываем погрешность

                if (targetLevel == null)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }

                // 3. Находим все планы этажей, связанные с этим уровнем
                var viewPlans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(vp => vp.GenLevel != null && vp.GenLevel.Id == targetLevel.Id)
                    .ToList();

                // 4. Выводим результат
                if (viewPlans.Count == 0)
                {
                    TaskDialog.Show("Результат", $"Нет планов этажей для уровня '{targetLevel.Name}'.");
                    continue;
                }
                else
                {
                    string result = $"Планы этажей для уровня '{targetLevel.Name}':\n";
                    result += string.Join("\n", viewPlans.Select(vp => vp.Name));
                    TaskDialog.Show("Найдено", result);

                    /*
                    // из всех планов стремимся найти планы несущих конструкций раздела general
                    // 4. Ищем планы из семейства "General" (архитектурные)
                    var generalPlans = viewPlans
                        .Where(vp => vp.ViewFamily == ViewFamily.FloorPlan) // Архитектурные/общие
                        .ToList();
                    // vs.LookupParameter(":Наименование раздела")?.Set("КР.7")
                    var ustan_plan = viewPlans[0];
                    foreach (var viewPlan in viewPlans)
                    {
                        if viewPlan.
                    }
                    */

                }

            }
            return Result.Succeeded;

        }

    }

}