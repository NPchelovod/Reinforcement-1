using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class EL_panek_Light_without_boxes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;

            RevitAPI.Initialize(commandData);

            // Проверка активного вида
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");
                return Result.Failed;
            }

            // Выбор связанной модели
            ISelectionFilter selFilter = new SelectionFilter();
            TransparentNotificationWindow.ShowNotification("Выберите связанную модель ЭЛ", uidoc, 5);

            Reference selection;
            try
            {
                selection = sel.PickObject(ObjectType.Element, selFilter);
            }
            catch
            {
                return Result.Cancelled;
            }

            RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
            Document linkedDoc = linkedModel.GetLinkDocument();


            //спискки которые заполняем
            var dict_box_elements = new Dictionary<string, List<FamilyInstance>>()
            {
                { "в перекрытии патроны", new List<FamilyInstance>() },
                { "в перекрытии клеммы", new List<FamilyInstance>() },
                { "в стенах", new List<FamilyInstance>() }
            };

            // лист элементов и на какое семейство заменить, содержащее данный текст в FamilyInstance.Name
            var dict_replace_family = new Dictionary<string, string>()
            {
                { "в перекрытии патроны", "Патрон"},
                { "в перекрытии клеммы","Клеммник для распаячной и универальной коробок, шаг крепления 60" }
            };

            // Список требуемых семейств
            var requiredFamilies = new List<string>()
            {
                //"Коробка ЭЛ_Л251",
                "Коробка ЭЛ_КУ1301",
            };
            //также в них добавляем заменяемые семейства
            foreach (var family in dict_replace_family)
            { 
                if(family.Value.Count()>3)
                {
                    requiredFamilies.Add(family.Value);
                }
            }


            // Проверка наличия семейств в проекте
            var missingFamilies = requiredFamilies
                .Where(familyName => !new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Any(f => f.Name.Equals(familyName)))
                    .ToList();
            if (missingFamilies.Any())
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют следующие семейства:\n{string.Join("\n", missingFamilies)}");
                return Result.Failed;
            }

            /* Получение типоразмера
            string lightingSymbolName = "160х40_12Вт_ip54";
            var lightingSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(x => x.FamilyName.Equals(lightingSymbolName));
            */

            string taskSymbolName = "КУ1301"; //содержит данное слово
            string patron = "_патрон";
            string klemm = "_клем";
            // Получение элементов для копирования всех
            var elementsToReplace = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .Where(x => x.Symbol.Name == taskSymbolName)
                .ToList();

            if (elementsToReplace.Count == 0)
            {
                // нет таких элементикусов
                TaskDialog.Show("Ошибка", "Коробок для замены нет");
                return Result.Failed;
            }

            
            
            

            //РАЗДЕЛЯЕМ ИХ НА СВЕТИЛЬНИКИ И КЛЕММЫ
            XYZ taskSlabXYZ = new XYZ(1, 0, 0); // в перекрытии
            int ne_proslo = 0; // какие не попали ни в клеммы ни в патроны и тп
            foreach (var element in elementsToReplace)
            {
                //в перекрытии
                if (element.FacingOrientation == taskSlabXYZ)
                {
                    if (element.Symbol.Name.Contains(patron))
                    {
                        // заменяем на патрон
                        dict_box_elements["в перекрытии патроны"].Add(element);
                    }
                    else if (element.Symbol.Name.Contains(klemm))
                    {
                        // заменяем на клемму
                        dict_box_elements["в перекрытии клеммы"].Add(element);
                    }
                    else
                    {
                        // те кто не попали, может ошибка их кол-во надо вывести
                        ne_proslo += 1;
                    }
                }
                else
                {
                    // в стене
                    dict_box_elements["в стенах"].Add(element);
                }
            }

            // Удаление существующих коробок на активном виде
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(FamilyInstance));
            var boxesToDelete = collector
                .Cast<FamilyInstance>()
                .Where(fi => requiredFamilies.Contains(fi.Symbol.Family.Name))
                .ToList();


            try
            {
                using (Transaction t = new Transaction(doc, "Копирование из задания коробок с заменой в светильники и тд"))
                {
                    t.Start();

                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    //// Копирование элементов
                    foreach (var element_list in dict_box_elements)
                    {
                        string type_copy = element_list.Key;
                        // имя семейства на которое заменяем
                        FamilySymbol lightingSymbol;

                        if (dict_replace_family.TryGetValue(type_copy, out var name_replace_sem))
                        {
                            // например, "Патрон"
                            if (name_replace_sem != null && name_replace_sem.Count()>1)
                            {
                                //ищем данное семейство , если его нет, то continue, есть - заменяем на него
                                lightingSymbol = new FilteredElementCollector(doc)
                                .OfClass(typeof(FamilySymbol))
                                .Cast<FamilySymbol>()
                                .FirstOrDefault(x => x.FamilyName.Contains(name_replace_sem));
                            }
                            else { continue; }
                        }
                        else
                        {
                            continue;
                        }


                        foreach (var element in element_list.Value)
                        {

                            var location = element.Location as LocationPoint;
                            if (location == null) continue;

                            XYZ point = location.Point;

                            // Создание экземпляра семейства
                            doc.Create.NewFamilyInstance(point, lightingSymbol, StructuralType.NonStructural);
                        }
                    }
                                                                      
                    t.Commit();

                    // Вывод информации о не скопированных элементах
                    //if (excludedElements.Count > 0)
                    //{
                    //    TaskDialog.Show("Предупреждение",
                    //        $"Не скопировано элементов: {excludedElements.Count}\n" +
                    //        $"ID элементов: {string.Join(", ", excludedElements.Select(x => x.Id.ToString()))}");
                    //}
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        // Фильтр выбора (только связи)
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ point) => false;
        }

        // Обработчик дублирования типов
        public class DuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
