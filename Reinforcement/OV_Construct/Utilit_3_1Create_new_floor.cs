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

                if (sourceLevels == null || sourceLevels.Count == 0)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }
                // 3. Получаем все планы этажей для найденных уровней
                var sourceViewPlans = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(vp => vp.GenLevel != null && sourceLevels.Any(l => l.Id == vp.GenLevel.Id))
                    .ToList();

                if (sourceViewPlans == null || sourceViewPlans.Count == 0)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }

               

                // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                // 4. Выводим результат
                // 2. Создаем копии планов для целевого уровня

                // 4. Создаем копии планов с префиксом ОВ_
                using (Transaction trans = new Transaction(doc, "Копирование планов этажей"))
                {
                    trans.Start();

                    foreach (ViewPlan sourcePlan in sourceViewPlans)
                    {


                        // Определяем целевой уровень (тот же, что и у исходного плана)
                        Level targetLevel = sourcePlan.GenLevel;

                        // Проверяем, не существует ли уже такой план с префиксом ОВ_ для целевого уровня
                        string newPlanName = "ОВ_" + sourcePlan.Name;

                        bool planExists = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .Any(vp => vp.Name.Equals(newPlanName) && vp.GenLevel?.Id == targetLevel.Id && vp.ViewType == sourcePlan.ViewType);

                        if (planExists)
                        {
                            TaskDialog.Show("Предупреждение", $"План '{newPlanName}' уже существует для уровня '{targetLevel.Name}'.");
                            continue;
                        }

                        // Создаем новый план этажа
                        ViewPlan newPlan = ViewPlan.Create(doc, sourcePlan.GetTypeId(), targetLevel.Id);

                        // Устанавливаем имя с префиксом ОВ_
                        try
                        {
                            newPlan.Name = newPlanName;
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка переименования", $"Не удалось установить имя '{newPlanName}': {ex.Message}");
                            continue;
                        }

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

                        // один план на этаж
                        break;
                    }

                    trans.Commit();
                }

                TaskDialog.Show("Готово", $"Планы этажей с отметкой {targetElevation} созданы с префиксом ОВ_.");
            }

            return Result.Succeeded;

        }


    }

}