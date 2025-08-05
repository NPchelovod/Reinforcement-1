using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
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
                    messageText += "Удалить их перед копированием новых?";
                }
                TaskDialogResult dialogResult = TaskDialog.Show(
                    "Подтверждение",
                    messageText,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                );
                

                if (dialogResult == TaskDialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc, "Удаление элементов1"))
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
                TaskDialogResult dialogResult2 = TaskDialog.Show(
                    "Подтверждение",
                    messageText,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                );


                if (dialogResult2 == TaskDialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc, "Удаление элементов2"))
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
        // Вспомогательная функция: проверка пересечения с новыми коробками
        

        public static bool CopyView(Document doc, Document linkedDoc, string prefix = "40_ЭЛ")
        {
            // Получение видов из связи
            var linkedViews = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => !x.IsTemplate && x.Name.Contains(prefix))
                .ToList();
            if (linkedViews.Count == 0) { return false; }
            // Подготовка данных для копирования линий
            

            if (linkedViews.Count > 0)
            {
                using (Transaction t = new Transaction(doc, "Копирование видов"))
                {
                    t.Start();
                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    foreach (View linkedView in linkedViews)
                    {
                        try
                        {
                            if (linkedView is ViewSection)// || linkedView is View3D)
                            {
                                // Для разрезов и 3D видов - прямое копирование
                                ICollection<ElementId> copiedViewIds = ElementTransformUtils.CopyElements(
                                    linkedDoc,
                                    new List<ElementId> { linkedView.Id },
                                    doc,
                                    Transform.Identity,
                                    copyOptions);

                                // Проверяем результат копирования
                                if (copiedViewIds == null || copiedViewIds.Count == 0)
                                {
                                    TaskDialog.Show("Ошибка", $"Не удалось скопировать вид {linkedView.Name}");
                                    continue;
                                }

                                // Получаем скопированный вид в текущем документе
                                View copiedView = doc.GetElement(copiedViewIds.First()) as View;
                                if (copiedView == null) continue;

                                // Копирование деталей
                                var lines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                                    .OfClass(typeof(CurveElement))
                                    .ToElementIds()
                                    .ToList();

                                if (lines.Count > 0)
                                {
                                    try
                                    {
                                        // Используем скопированный вид как целевой
                                        ElementTransformUtils.CopyElements(
                                            linkedView,
                                            lines,
                                            copiedView,
                                            Transform.Identity,
                                            copyOptions);
                                    }
                                    catch (Exception ex)
                                    {
                                        TaskDialog.Show("Ошибка копирования",
                                            $"Не удалось скопировать линии в вид {copiedView.Name}: {ex.Message}");
                                    }
                                }

                                SetViewParameter(copiedView, "Задание ЭЛ");


                            }
                            else if (linkedView is ViewPlan linkedViewPlan)
                            {
                                // Для планов - копирование с подбором уровней
                                Level linkedLevel = linkedViewPlan.GenLevel;
                                if (linkedLevel == null) continue;

                                // Поиск ближайшего уровня
                                Level targetLevel = FindClosestLevel(doc, linkedLevel.Elevation);
                                if (targetLevel == null) continue;

                                // Создание плана
                                ViewPlan newViewPlan = CreateViewPlan(doc, linkedViewPlan, targetLevel);
                                if (newViewPlan == null) continue;

                                // Копирование деталей
                                var lines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                                    .OfClass(typeof(CurveElement))
                                    .ToElementIds()
                                    .ToList();

                                if (lines.Count > 0)
                                {
                                    ElementTransformUtils.CopyElements(
                                        linkedView,
                                        lines,
                                        newViewPlan,
                                        Transform.Identity,
                                        copyOptions);
                                }

                                SetViewParameter(newViewPlan, "Задание ЭЛ");
                            }
                            else if (linkedView is ViewDrafting linkedViewDrafting)
                            {
                                // Для чертежных видов
                                ViewDrafting newView = CreateViewDrafting(doc, linkedViewDrafting);
                                if (newView == null) continue;

                                var lines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                                    .OfClass(typeof(CurveElement))
                                    .ToElementIds()
                                    .ToList();

                                if (lines.Count > 0)
                                {
                                    ElementTransformUtils.CopyElements(
                                        linkedView,
                                        lines,
                                        newView,
                                        Transform.Identity,
                                        copyOptions);
                                }

                                SetViewParameter(newView, "Задание ЭЛ");
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка копирования вида",
                                $"Не удалось скопировать вид {linkedView.Name}: {ex.Message}");
                        }
                    }

                    t.Commit();


                }
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
       
        // Поиск ближайшего уровня (допуск 500 мм)
        // Поиск ближайшего уровня (допуск 500 мм)
        private static Level FindClosestLevel(Document doc, double sourceElevation)
        {
            const double tolerance = 0.5; // 500 мм
            Level closestLevel = null;
            double minDifference = double.MaxValue;

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            foreach (Level level in levels)
            {
                double diff = Math.Abs(level.Elevation - sourceElevation);
                if (diff < tolerance && diff < minDifference)
                {
                    minDifference = diff;
                    closestLevel = level;
                }
            }

            // Если не найдено - создать новый уровень
            if (closestLevel == null)
            {
                using (Transaction t = new Transaction(doc, "Создание уровня"))
                {
                    t.Start();
                    closestLevel = Level.Create(doc, sourceElevation);
                    closestLevel.Name = $"Уровень {sourceElevation:0.000}";
                    t.Commit();
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
                    TaskDialog.Show("Ошибка", $"Исходный вид '{sourceView.Name}' не имеет типа вида");
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
                        TaskDialog.Show("Неподдерживаемый тип",
                            $"Тип вида {sourceView.ViewType} не поддерживается для копирования");
                        return null;
                }

                // Поиск типа вида с кешированием для производительности
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == viewFamily);

                if (viewFamilyType == null)
                {
                    TaskDialog.Show("Ошибка",
                        $"Не найден тип вида для семейства {viewFamily} в целевом документе");
                    return null;
                }

                // Проверка существования вида с таким именем
                string newViewName = sourceView.Name;
                if (new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Any(v => v.Name.Equals(newViewName)))
                {
                    // Генерация уникального имени
                    int counter = 1;
                    do
                    {
                        newViewName = $"{sourceView.Name} ({counter++})";
                    } while (new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Any(v => v.Name.Equals(newViewName)));
                }

                // Создание плана
                ViewPlan newView = ViewPlan.Create(doc, viewFamilyType.Id, targetLevel.Id);
                newView.Name = newViewName;

                // Копирование основных параметров
                try
                {
                    // Обязательные параметры
                    newView.Scale = sourceView.Scale;
                    newView.DisplayStyle = sourceView.DisplayStyle;

                    // Дополнительные параметры (с проверкой доступности)
                    /*
                    if (sourceView.CropBoxActive && newView.CanModifyCropBox)
                    {
                        newView.CropBoxActive = true;
                        newView.CropBox = sourceView.CropBox;
                    }
                    */
                    // Копирование параметра "Масштаб"
                    Parameter sourceScaleParam = sourceView.get_Parameter(BuiltInParameter.VIEW_SCALE);
                    Parameter targetScaleParam = newView.get_Parameter(BuiltInParameter.VIEW_SCALE);
                    if (sourceScaleParam != null && targetScaleParam != null && !targetScaleParam.IsReadOnly)
                    {
                        targetScaleParam.Set(sourceScaleParam.AsInteger());
                    }

                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Предупреждение",
                        $"Не удалось скопировать все параметры вида: {ex.Message}");
                }

                return newView;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Критическая ошибка",
                    $"Ошибка создания плана для уровня {targetLevel.Name}: {ex}");
                return null;
            }
        }


        // Создание чертежного вида
        private static ViewDrafting CreateViewDrafting(Document doc, ViewDrafting sourceView)
        {
            try
            {
                // Поиск типа вида
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

                if (viewFamilyType == null) return null;

                // Создание вида
                ViewDrafting newView = ViewDrafting.Create(doc, viewFamilyType.Id);
                newView.Name = sourceView.Name;

                // Копирование параметров
                try
                {
                    newView.Scale = sourceView.Scale;
                    newView.DisplayStyle = sourceView.DisplayStyle;
                }
                catch { /* Игнорируем ошибки */ }

                return newView;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания чертежа", ex.Message);
                return null;
            }
        }

        // Установка параметра вида
        private static void SetViewParameter( View view, string value)
        {
            try
            {
                Parameter param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));

                // Резервный поиск по имени
                if (param == null)
                {
                    param = view.LookupParameter("Директория")
                            ?? view.LookupParameter("Directory")
                            ?? view.LookupParameter("Project Directory");
                }

                
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    param.Set(value);
                }
                else if (param == null)
                {
                    //TaskDialog.Show("Предупреждение",
                       // $"Параметр 'Директория' не найден для вида {view.Name}");
                }
            }
            catch (Exception ex)
            {
                
            }
        }
    }
}
