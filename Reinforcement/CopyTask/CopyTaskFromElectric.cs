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
    public class CopyTaskFromElectric : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Инициализация Revit API
            
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;

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

            // Список требуемых семейств
            var requiredFamilies = new List<string>()
            {
                "Коробка ЭЛ_Л251",
                "Коробка ЭЛ_КУ1301",
            };

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

            // Удаление существующих коробок на активном виде
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(FamilyInstance));
            var boxesToDelete = collector
                .Cast<FamilyInstance>()
                .Where(fi => requiredFamilies.Contains(fi.Symbol.Family.Name))
                .ToList();

            // УДАЛЕНИЕ СУЩЕСТВУЮЩИХ ВИДОВ
            var viewsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.Name.Contains("40_ЭЛ"))
                .ToList();



            // Диалог подтверждения удаления
            if (boxesToDelete.Any())
            {
                string messageText = "";
                
                if (boxesToDelete.Any())
                {
                    messageText += $"Найдено {boxesToDelete.Count} коробок на активном виде.\n";
                }
                TaskDialogResult dialogResult = TaskDialog.Show(
                    "Подтверждение",
                    messageText,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                );
                

                if (dialogResult == TaskDialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc, "Удаление элементов"))
                    {
                        t.Start();

                        // Удаление коробок
                        foreach (Element box in boxesToDelete)
                        {
                            try
                            {
                                doc.Delete(box.Id);
                            }
                            catch { }
                        }

                        t.Commit();
                    }
                }
            }





            // Диалог подтверждения удаления
            if (viewsToDelete.Any())
            {
                
                string messageText = "";

                if (viewsToDelete.Any())
                {
                    messageText += $"Найдено {viewsToDelete.Count} видов с заданием ЭЛ.\n";
                }
                messageText += "Удалить их перед копированием новых?";
                TaskDialogResult dialogResult = TaskDialog.Show(
                    "Подтверждение",
                    messageText,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                );


                if (dialogResult == TaskDialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc, "Удаление элементов"))
                    {
                        t.Start();

                        // Удаление видов
                        foreach (View view in viewsToDelete)
                        {
                            try
                            {
                                doc.Delete(view.Id);
                            }
                            catch { }
                        }

                        t.Commit();
                    }
                }
            }

            // Сбор коробок из связанной модели
            var allConduitFittings = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .ToList();

            var elementsToCopy = allConduitFittings
                .Where(x => requiredFamilies.Contains(x.Symbol.Family.Name))
                .ToList();

            var excludedElements = allConduitFittings
                .Where(x => !requiredFamilies.Contains(x.Symbol.Family.Name))
                .ToList();

            

            try
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();

                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    // КОПИРОВАНИЕ КОРОБОК
                    if (elementsToCopy.Count > 0)
                    {
                        Transform transform = linkedModel.GetTotalTransform();
                        ICollection<ElementId> copiedElementIds = ElementTransformUtils.CopyElements(
                            linkedDoc,
                            elementsToCopy.Select(e => e.Id).ToList(),
                            doc,
                            transform,
                            copyOptions);
                    }



                    t.Commit();
                }

                // Вывод информации о не скопированных элементах
                if (excludedElements.Count > 0)
                {
                    TaskDialog.Show("Предупреждение",
                        $"Не скопировано элементов (неподходящие семейства): {excludedElements.Count}\n" +
                        $"ID элементов: {string.Join(", ", excludedElements.Select(x => x.Id.ToString()))}");
                }
                
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }

            bool rr = CopyView(doc, linkedDoc );
            if(!rr)
            {
                TaskDialog.Show("Ошибка","Копирование уровней не выполнено");

            }

            return Result.Succeeded;
        }


        public static bool CopyView(Document doc, Document linkedDoc, string prefix = "40_ЭЛ")
        {
            // Получение видов из связи
            var linkedViews = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => !x.IsTemplate && x.Name.Contains(prefix))
                .ToList();
            if (linkedViews.Count == 0) { return false; }

            try
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());


                    // Копирование видов и линий
                    foreach (View linkedView in linkedViews)
                    {
                        // Создание вида в целевом документе
                        View newView = null;

                        if (linkedView is ViewPlan linkedViewPlan)
                        {
                            // Получение уровня из связанного вида
                            Level linkedLevel = linkedViewPlan.GenLevel;
                            if (linkedLevel == null) continue;

                            // Поиск ближайшего уровня в целевом документе
                            Level targetLevel = FindClosestLevel(doc, linkedLevel.Elevation);
                            if (targetLevel == null) continue;

                            // Создание плана
                            newView = CreateViewPlan(doc, linkedViewPlan, targetLevel);
                        }
                        else if (linkedView is ViewDrafting linkedViewDrafting)
                        {
                            // Создание чертежного вида
                            newView = CreateViewDrafting(doc, linkedViewDrafting);
                        }

                        if (newView == null) continue;

                        // Копирование линий
                        var lines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                            .OfClass(typeof(CurveElement))
                            .ToElementIds()
                            .ToList();

                        if (lines.Count > 0)
                        {
                            try
                            {
                                ElementTransformUtils.CopyElements(
                                    linkedView,
                                    lines,
                                    newView,
                                    Transform.Identity,
                                    copyOptions);
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка", $"Не удалось скопировать линии: {ex.Message}");
                            }
                        }

                        // Установка параметра вида
                        SetViewParameter(newView, "Задание ЭЛ");
                    }

                    t.Commit();


                }


            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return false;
            }
            return true;

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
        // Вспомогательный метод для установки параметра вида
        private static void SetViewParameter(View view, string value)
        {
            try
            {
                // GUID параметра "Директория"
                var param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));
                param?.Set(value);
            }
            catch { }
        }
        // Поиск ближайшего уровня (допуск 500 мм)
        private static Level FindClosestLevel(Document doc, double sourceElevation)
        {
            const double tolerance = 0.5; // 500 мм
            Level closestLevel = null;
            double minDifference = double.MaxValue;

            // Используем кеширование для производительности
            if (_levelsCache == null)
            {
                _levelsCache = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();
            }

            foreach (Level level in _levelsCache)
            {
                double diff = Math.Abs(level.Elevation - sourceElevation);
                if (diff < tolerance && diff < minDifference)
                {
                    minDifference = diff;
                    closestLevel = level;
                }
            }

            return closestLevel;
        }
        private static List<Level> _levelsCache;
        // Создание плана

        private static ViewPlan CreateViewPlan(Document doc, ViewPlan sourceView, Level targetLevel)
        {
            try
            {
                // Получаем тип исходного вида
                ElementId sourceViewTypeId = sourceView.GetTypeId();
                if (sourceViewTypeId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Ошибка", "Исходный вид не имеет типа вида");
                    return null;
                }

                // Определяем семейство вида на основе ViewType
                ViewFamily viewFamily;
                switch (sourceView.ViewType)
                {
                    case ViewType.FloorPlan:
                        viewFamily = ViewFamily.FloorPlan;
                        break;
                    case ViewType.CeilingPlan:
                        viewFamily = ViewFamily.CeilingPlan;
                        break;
                    case ViewType.EngineeringPlan:
                        viewFamily = ViewFamily.StructuralPlan;
                        break;
                    case ViewType.AreaPlan:
                        viewFamily = ViewFamily.AreaPlan;
                        break;
                    default:
                        viewFamily = ViewFamily.FloorPlan; // Значение по умолчанию
                        break;
                }

                // Ищем подходящий тип вида в целевом документе
                ViewFamilyType viewFamilyType = null;
                var viewFamilyTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .ToList();

                foreach (var vft in viewFamilyTypes)
                {
                    if (vft.ViewFamily == viewFamily)
                    {
                        viewFamilyType = vft;
                        break;
                    }
                }

                // Если не нашли, используем первый доступный тип плана этажа
                if (viewFamilyType == null)
                {
                    foreach (var vft in viewFamilyTypes)
                    {
                        if (vft.ViewFamily == ViewFamily.FloorPlan)
                        {
                            viewFamilyType = vft;
                            break;
                        }
                    }
                }

                if (viewFamilyType == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден подходящий тип вида для плана");
                    return null;
                }

                // Создаем новый план
                ViewPlan newView = ViewPlan.Create(doc, viewFamilyType.Id, targetLevel.Id);
                newView.Name = sourceView.Name;

                // Копируем основные параметры
                try
                {
                    newView.Scale = sourceView.Scale;
                    newView.DisplayStyle = sourceView.DisplayStyle;
                }
                catch { /* Игнорируем несущественные ошибки */ }

                return newView;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания плана", ex.Message);
                return null;
            }
        }


        // Создание чертежного вида
        private static ViewDrafting CreateViewDrafting(Document doc, ViewDrafting sourceView)
        {
            // Получение типа семейства вида
            ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (viewFamilyType == null) return null;

            // Создание вида
            ViewDrafting newView = ViewDrafting.Create(doc, viewFamilyType.Id);
            newView.Name = sourceView.Name;
            return newView;
        }
    }
}
