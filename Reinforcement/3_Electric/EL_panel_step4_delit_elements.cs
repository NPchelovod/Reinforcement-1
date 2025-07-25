using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;


namespace Reinforcement
{
    public class EL_panel_step4_delit_elements
    {

        // 3 удаление существующих элементов сначала тех которые должны быть в итоге - светильников и тд
        public static void delit_one_family(Dictionary<(string FamilyName, string SymbolName), FamilySymbol> one_replaceable_element, Document doc)
        {
            // 3 удаление существующих элементов сначала тех которые должны быть в итоге - светильников и тд

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance)); // Получаем все экземпляры семейств

            var list_del_elements = new List<FamilyInstance>();
            foreach (var seach_symbol in one_replaceable_element)
            {
                string familyName = seach_symbol.Key.FamilyName;
                string symbolName = seach_symbol.Key.SymbolName;
                var elementsToReplace = collector
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                        fi.Symbol != null && // Проверка на null для Symbol
                        fi.Symbol.Family != null && // Проверка на null для Family
                        fi.Symbol.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                        fi.Symbol.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (elementsToReplace.Count == 0)
                { continue; }
                list_del_elements.AddRange(elementsToReplace);
            }

            if (list_del_elements.Count > 0)
            {
                TaskDialogResult deleteDecision = TaskDialog.Show("Удаление элементов",
                    $"Найдено {list_del_elements.Count} существующих элементов, которые мы итак создаём. Удалить их?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (deleteDecision == TaskDialogResult.Yes)
                {
                    try
                    {
                        using (Transaction tDel = new Transaction(doc, "Удаление старых элементов"))
                        {
                            tDel.Start();
                            doc.Delete(list_del_elements.Select(e => e.Id).ToList());
                            tDel.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка удаления", $"Не удалось удалить элементы: {ex.Message}");
                    }
                }
            }
        }
        //5 удаление кубиков в текущем проекте если они были 

        public static void delit_all_family(Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_replace_cubics, Document doc)
        {
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance)); // Получаем все экземпляры семейств

            var list_del_elements = new List<FamilyInstance>();
            foreach (var seach_symbol in all_replace_cubics)
            {
                string familyName = seach_symbol.Key.FamilyName;
                string symbolName = seach_symbol.Key.SymbolName;
                var elementsToReplace = collector
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                        fi.Symbol != null && // Проверка на null для Symbol
                        fi.Symbol.Family != null && // Проверка на null для Family
                        fi.Symbol.Family.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) &&
                        fi.Symbol.Name.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (elementsToReplace.Count == 0)
                { continue; }
                list_del_elements.AddRange(elementsToReplace);
            }

            if (list_del_elements.Count > 0)
            {
                TaskDialogResult deleteDecision = TaskDialog.Show("Удаление элементов",
                    $"Найдено {list_del_elements.Count} существующих элементов кубиков, которые итак перестраивали, они наверняка не нужны в данном проекте. Удалить их?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (deleteDecision == TaskDialogResult.Yes)
                {
                    try
                    {
                        using (Transaction tDel = new Transaction(doc, "Удаление старых элементов"))
                        {
                            tDel.Start();
                            doc.Delete(list_del_elements.Select(e => e.Id).ToList());
                            tDel.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка удаления", $"Не удалось удалить элементы: {ex.Message}");
                    }
                }
            }
        }


    }
}
