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
                    // Полноценный диалог с инструкцией
                    TaskDialog dialog = new TaskDialog("Выбор связанной модели");
                    dialog.MainInstruction = "Выбор исходной модели";
                    dialog.MainContent = "Пожалуйста, выберите связанную модель Revit в текущем виде, из которой будут копироваться элементы.\n\n"
                                        + "Убедитесь что:\n"
                                        + "• Связь видна в текущем виде\n"
                                        + "• Связь не скрыта фильтрами\n"
                                        + "• Вы находитесь в 3D виде или виде, где связь отображается";
                    dialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;

                    if (dialog.Show() == TaskDialogResult.Cancel)
                    {
                        message = "Выбор модели отменен пользователем";
                        return null;
                    }

                    // Выбор связанной модели
                    ISelectionFilter selFilter = new SelectionFilter();
                    Reference selection = sel.PickObject(
                        ObjectType.Element,
                        selFilter,
                        "Укажите связанную модель курсором"
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
            public bool AllowElement(Element elem)
            {
                // Разрешаем только связи Revit
                return elem is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
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
