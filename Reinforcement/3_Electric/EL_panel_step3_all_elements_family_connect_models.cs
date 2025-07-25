using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    public class EL_panel_step3_all_elements_family_connect_models
    {

        public static Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_elements_family_connect_models(Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_replace_cubics, Document linkedDoc)
        {
            // 2.2 в связанной находим все кубики
            // Создаем коллектор для поиска элементов
            var missingElements2 = new List<string>();
            var Dict_add2 = new Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>>();
            var collector = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance)); // Получаем все экземпляры семейств
            int proxod = 0;
            foreach (var seach_symbol in all_replace_cubics)
            {
                string familyName = seach_symbol.Key.FamilyName;
                string symbolName = seach_symbol.Key.SymbolName;

                // Фильтруем по имени семейства и типоразмера
                var elementsToReplace = collector
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                        fi.Symbol != null && // Проверка на null для Symbol
                        fi.Symbol.Family != null && // Проверка на null для Family
                        fi.Symbol.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                        fi.Symbol.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (elementsToReplace.Count == 0)
                {

                    if (!missingElements2.Contains($"Семейство: {familyName}"))
                        missingElements2.Add($"Семейство: {familyName}");
                    continue;
                }
                proxod += 1;
                Dict_add2[seach_symbol.Key] = elementsToReplace;


            }
            // заполняем так как в цикле нельзя было это сделать
            foreach (var seach_symbol in Dict_add2)
            {
                all_replace_cubics[seach_symbol.Key] = seach_symbol.Value;
            }


            // Если отсутствуют необходимые элементы
            if (missingElements2.Count > 0)
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют необходимые элементы в связанной модели:\n{string.Join("\n", missingElements2)}");
            }
            if (proxod == 0)
            {
                // ни одного семейства нет, но вдруг нам надо просто удалить из текущего проекта кубики? так что пойдем дальше
                TaskDialog.Show("Информация", "Коробок для замены не найдено");
                //return Result.Succeeded;
            }

            return all_replace_cubics;
        }
        


    }
}
