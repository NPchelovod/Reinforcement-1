using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Reinforcement
{
    public class EL_panel_step3_all_elements_family_connect_models
    {

        public static Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_elements_family_connect_models(
    Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_replace_cubics,
    Document linkedDoc)
        {
            var missingElements2 = new List<string>();
            var Dict_add2 = new Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>>();

            
            // Создаем коллектор ОДИН РАЗ
            var collector = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol != null && fi.Symbol.Family != null)
                .ToList();  // Материализуем коллекцию

            // Создаем lookup-таблицу с нормализацией регистра
            // Создаем lookup-таблицу с нормализацией регистра
            var lookupTable = new Dictionary<(string Family, string Symbol), List<FamilyInstance>>();
            // Заполняем lookup-таблицу с приведением к верхнему регистру
            foreach (var fi in collector)
            {
                // Нормализуем регистр для ключа
                var normalizedKey = (
                    Family: fi.Symbol.Family.Name.ToUpperInvariant(),
                    Symbol: fi.Symbol.Name.ToUpperInvariant()
                );

                if (!lookupTable.TryGetValue(normalizedKey, out var list))
                {
                    list = new List<FamilyInstance>();
                    lookupTable[normalizedKey] = list;
                }
                list.Add(fi);
            }

            // Обрабатываем запросы с нормализацией входных ключей
            foreach (var key in all_replace_cubics.Keys)
            {
                // Нормализуем регистр для поиска
                var searchKey = (
                    Family: key.FamilyName.ToUpperInvariant(),
                    Symbol: key.SymbolName.ToUpperInvariant()
                );

                if (lookupTable.TryGetValue(searchKey, out var elements))
                {
                    // Используем оригинальный ключ (без нормализации) для результата
                    Dict_add2[key] = elements;
                }
                else
                {
                    string familyInfo = $"Семейство: {key.FamilyName}";
                    if (!missingElements2.Contains(familyInfo))
                        missingElements2.Add(familyInfo);
                }
            }

            // Логирование отсутствующих элементов (при необходимости)
            if (missingElements2.Count > 0)
            {
                Debug.WriteLine($"Отсутствуют семейства: {string.Join(", ", missingElements2)}");
            }

            // Здесь можно обработать missingElements2 (например, логирование)


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
            

            return all_replace_cubics;
        }
        


    }
}
