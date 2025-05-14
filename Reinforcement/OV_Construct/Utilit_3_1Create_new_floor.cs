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

                var sourceLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Where(level => Math.Abs(UnitUtils.ConvertFromInternalUnits(level.Elevation, units) - targetElevation) < 300).ToList(); // Учитываем погрешность

                if (sourceLevels == null)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }
                // 2. Получаем все планы этажей для найденных уровней
                var sourceViewPlans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(vp => vp.GenLevel != null && sourceLevels.Any(l => l.Id == vp.GenLevel.Id))
                    .ToList();

                if (sourceViewPlans == null)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }



                Level targetLevel = null; // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                // 4. Выводим результат
                // 2. Создаем копии планов для целевого уровня

                using (Transaction trans = new Transaction(doc, "Копирование планов этажей"))
                {
                    trans.Start();

                    foreach (ViewPlan sourcePlan in sourceViewPlans)
                    {
                        // Проверяем, не существует ли уже такой план для целевого уровня
                        bool planExists = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .Any(vp => vp.GenLevel?.Id == targetLevel.Id && vp.ViewType == sourcePlan.ViewType);

                        if (planExists)
                        {
                            TaskDialog.Show("Предупреждение", $"План типа '{sourcePlan.ViewType}' уже существует для уровня '{targetLevel.Name}'.");
                            continue;
                        }

                        // Создаем новый план этажа
                        ViewPlan newPlan = ViewPlan.Create(doc, sourcePlan.GetTypeId(), targetLevel.Id);

                        // Копируем параметры
                        foreach (Parameter param in sourcePlan.Parameters)
                        {
                            if (param.IsReadOnly) continue;

                            Parameter newParam = newPlan.LookupParameter(param.Definition.Name);
                            if (newParam != null)
                            {
                                switch (param.StorageType)
                                {
                                    case StorageType.Integer:
                                        newParam.Set(param.AsInteger());
                                        break;
                                    case StorageType.Double:
                                        newParam.Set(param.AsDouble());
                                        break;
                                    case StorageType.String:
                                        newParam.Set(param.AsString());
                                        break;
                                    case StorageType.ElementId:
                                        newParam.Set(param.AsElementId());
                                        break;
                                }
                            }
                        }

                        // Копируем графические настройки
                        newPlan.Scale = sourcePlan.Scale;
                        newPlan.CropBoxActive = sourcePlan.CropBoxActive;
                        newPlan.CropBoxVisible = sourcePlan.CropBoxVisible;
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Готово", $"Планы этажей с отметкой {targetElevation} скопированы на уровень '{targetLevel.Name}'.");

            }

            return Result.Succeeded;

        }

            

    }

}