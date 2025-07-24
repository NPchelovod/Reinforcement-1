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
    public class EL_panel_step1
    {
        //выбор связанной модели или текущая выдается обратно...
        public static Document
            choice_relation_model(
            bool copy_svis_model_or_tek_model,
            ref string message,
            Selection sel,
            Document doc)
        {
            try
            {
                if (copy_svis_model_or_tek_model)
                {
                    // Выбор связанной модели
                    ISelectionFilter selFilter = new SelectionFilter();
                    Reference selection = sel.PickObject(
                        ObjectType.Element,
                        selFilter,
                        "Выберите связанную модель для копирования"
                    );

                    RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
                    if (linkedModel == null)
                    {
                        message = "Ошибка: Выбранный элемент не является связанной моделью";
                        return null; // Возвращаем null при ошибке
                    }

                    return linkedModel.GetLinkDocument(); // Возвращаем документ связанной модели
                }
                else
                {
                    return doc; // Возвращаем текущий документ
                }
            }
            catch (OperationCanceledException)
            {
                message = "Операция отменена пользователем";
                return null; // Возвращаем null при отмене
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                return null; // Возвращаем null при исключении
            }
        }

        // Фильтр выбора (только связи)
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ point) => false;
        }


        // нахождение хотя бы одного элемента и заполнение словаря
        // для создания экземпляра
        public static void seach_one_symbol()
        {
            var missingElements = new List<string>(); // не найденные в текущей модели элементы, хотя бы один
            int proxod = 0;

        }




    }
}
