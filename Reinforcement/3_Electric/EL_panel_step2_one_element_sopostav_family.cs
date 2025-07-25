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
    public class EL_panel_step2_one_element_sopostav_family
    {
        // находит семейство по имени семейства и типа, чтобы потом его создавать
        public static Dictionary<(string FamilyName, string SymbolName), FamilySymbol> one_element_sopostav_family (Dictionary<(string FamilyName, string SymbolName), FamilySymbol> one_replaceable_element, Document doc)
        {
            // 2.1 - находим хотя бы один элемент
            // Проверка наличия семейств и типоразмеров, хотя бы один элемент

            var missingElements = new List<string>(); // не найденные в текущей модели элементы, хотя бы один
            var Dict_add = new Dictionary<(string FamilyName, string SymbolName), FamilySymbol>();
            int proxod = 0;
            foreach (var seach_symbol in one_replaceable_element)
            {
                string familyName = seach_symbol.Key.FamilyName;
                string symbolName = seach_symbol.Key.SymbolName;
                // Поиск семейства по имени
                Family family = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name.Contains(familyName));
                if (family == null)
                {
                    if (!missingElements.Contains($"Семейство: {familyName}"))
                        missingElements.Add($"Семейство: {familyName}");
                    continue;
                }
                // Поиск типоразмера по имени в этом семействе
                FamilySymbol symbol = null;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    FamilySymbol s = doc.GetElement(id) as FamilySymbol;
                    if (s != null && s.Name.Contains(symbolName))
                    {
                        proxod += 1;
                        symbol = s;
                        Dict_add[seach_symbol.Key] = symbol;
                        break;
                    }
                }
            }
            // заполняем так как в цикле нельзя было это сделать
            foreach (var seach_symbol in Dict_add)
            {
                one_replaceable_element[seach_symbol.Key] = seach_symbol.Value;
            }


            // Если отсутствуют необходимые элементы
            if (missingElements.Count > 0)
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют необходимые элементы в данной модели:\n{string.Join("\n", missingElements)}");
            }
            if (proxod == 0)
            {
                // ни одного семейства нет, но вдруг нам надо просто удалить из текущего проекта кубики? так что пойдем дальше
                //return Result.Failed;
            }

            return one_replaceable_element;


        }



    }
}
