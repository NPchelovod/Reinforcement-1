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

            // Проверка активного вида
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");
                return Result.Failed;
            }

            // Выбор связанной модели
            ISelectionFilter selFilter = new SelectionFilter();
            Reference selection;
            try
            {
                selection = sel.PickObject(ObjectType.Element, selFilter, "Выберите связанную модель ЭЛ");
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
            if (linkedModel == null)
            {
                TaskDialog.Show("Ошибка", "Выбранный элемент не является связанной моделью");
                return Result.Failed;
            }

            Document linkedDoc = linkedModel.GetLinkDocument();
            if (linkedDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось получить документ связанной модели");
                return Result.Failed;
            }

            // Классификация элементов
            var elementCategories = new Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)>()
{
                { "в перекрытии патроны", ("патрон", "патрон", false) },
                { "в перекрытии клеммы", ("Клеммник_для_распаячной_и_", "Клеммник для распаячной и универальной коробок, шаг крепления 60", true) },
               // { "в стенах патроны", ("патрон", "патрон", true) },
                //{ "в стенах клеммы", ("Клеммник_для_распаячной", "Клеммник для распаячной и универсальной коробок, шаг крепления 60", true) }
            };

            var dict_box_elements = elementCategories.ToDictionary(
                k => k.Key,
                v => new List<FamilyInstance>());

            // Проверка наличия семейств и типоразмеров
            var missingElements = new List<string>();
            var requiredSymbols = new Dictionary<string, FamilySymbol>();

            foreach (var category in elementCategories)
            {
                string familyName = category.Value.FamilyName;
                string symbolName = category.Value.SymbolName;
                bool requiresHost = category.Value.RequiresHost;

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
                    if (s != null && (s.Name.Equals(symbolName)|| s.Name.Contains(symbolName)))
                    {
                        symbol = s;
                        break;
                    }
                }

                // Если не нашли по точному совпадению, попробуем частичное
                if (symbol == null)
                {
                    foreach (ElementId id in family.GetFamilySymbolIds())
                    {
                        FamilySymbol s = doc.GetElement(id) as FamilySymbol;
                        if (s != null && s.Name.Contains(symbolName))
                        {
                            symbol = s;
                            break;
                        }
                    }
                }

                if (symbol == null)
                {
                    if (!missingElements.Contains($"Типоразмер '{symbolName}' в семействе '{familyName}'"))
                        missingElements.Add($"Типоразмер '{symbolName}' в семействе '{familyName}'");
                }
                else
                {
                    // Активируем типоразмер, если он не активен
                    if (!symbol.IsActive)
                    {
                        try
                        {
                            using (Transaction tActivate = new Transaction(doc, "Activate Symbol"))
                            {
                                tActivate.Start();
                                symbol.Activate();
                                tActivate.Commit();
                            }
                        }
                        catch
                        {
                            TaskDialog.Show("Предупреждение",
                                $"Не удалось активировать типоразмер '{symbolName}' в семействе '{familyName}'");
                        }
                    }

                    // Сохраняем найденный типоразмер
                    string key = $"{familyName}|{symbolName}";
                    requiredSymbols[key] = symbol;
                }
            }

            // Если отсутствуют необходимые элементы
            if (missingElements.Count > 0)
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют необходимые элементы:\n{string.Join("\n", missingElements)}");
                return Result.Failed;
            }











            // Поиск элементов в связанной модели
            string taskSymbolName = "КУ1301";
            string patron = "_патрон";
            string klemm = "_клем";

            var elementsToReplace = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .Where(x => x.Symbol.Name.Contains(taskSymbolName))
                .ToList();

            if (elementsToReplace.Count == 0)
            {
                TaskDialog.Show("Информация", "Коробок для замены не найдено");
                return Result.Succeeded;
            }

            // Классификация элементов
            Transform transform = linkedModel.GetTotalTransform();
            foreach (var element in elementsToReplace)
            {
                string locationType;
                bool isPatron = element.Symbol.Name.IndexOf(patron, StringComparison.OrdinalIgnoreCase) >= 0;
                bool isKlemm = element.Symbol.Name.IndexOf(klemm, StringComparison.OrdinalIgnoreCase) >= 0;

                // Определяем ориентацию с учетом трансформации связи
                XYZ transformedOrientation = transform.OfVector(element.FacingOrientation).Normalize();

                if (Math.Abs(transformedOrientation.Z) > 0.9) // Вертикальная ориентация
                {
                    locationType = isPatron ? "в стенах патроны" :
                                isKlemm ? "в стенах клеммы" : "в стенах патроны";
                }
                else // Горизонтальная ориентация
                {
                    locationType = isPatron ? "в перекрытии патроны" :
                                isKlemm ? "в перекрытии клеммы" : "в перекрытии патроны";
                }

                if (dict_box_elements.ContainsKey(locationType))
                {
                    dict_box_elements[locationType].Add(element);
                }
            }

            // Удаление существующих элементов
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>();

            var boxesToDelete = collector
                .Where(fi => elementCategories.Values.Any(c => c.FamilyName.Contains(fi.Symbol.Family.Name)))
                .ToList();

            if (boxesToDelete.Count > 0)
            {
                TaskDialogResult deleteDecision = TaskDialog.Show("Удаление элементов",
                    $"Найдено {boxesToDelete.Count} существующих элементов. Удалить их?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (deleteDecision == TaskDialogResult.Yes)
                {
                    try
                    {
                        using (Transaction tDel = new Transaction(doc, "Удаление старых элементов"))
                        {
                            tDel.Start();
                            doc.Delete(boxesToDelete.Select(e => e.Id).ToList());
                            tDel.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка удаления", $"Не удалось удалить элементы: {ex.Message}");
                    }
                }
            }

            // Создание новых элементов
            try
            {
                using (Transaction t = new Transaction(doc, "Создание новых элементов"))
                {
                    t.Start();

                    foreach (var category in elementCategories)
                    {

                        
                        string categoryKey = category.Key;
                        var elementsInCategory = dict_box_elements[categoryKey];
                        string familyName = category.Value.FamilyName;
                        string symbolName = category.Value.SymbolName;
                        bool requiresHost = category.Value.RequiresHost;

                        if (elementsInCategory.Count == 0) continue;
                        // Получаем заранее сохраненный типоразмер
                        // Получаем заранее найденный типоразмер
                         string key = $"{familyName}|{symbolName}";

                        if (!requiredSymbols.TryGetValue(key, out FamilySymbol symbol))
                        {
                            TaskDialog.Show("Ошибка", $"Не найден типоразмер: {symbolName} в семействе {familyName}");
                            continue;
                        }



                        foreach (var element in elementsInCategory)
                        {
                            try
                            {
                                LocationPoint locPoint = element.Location as LocationPoint;
                                if (locPoint == null) continue;

                                XYZ pointInLink = locPoint.Point;
                                XYZ pointInHost = transform.OfPoint(pointInLink);
                                XYZ facingInHost = transform.OfVector(element.FacingOrientation).Normalize();

                                if (requiresHost)
                                {
                                    Reference hostRef = FindHostSurface(doc, activeView as View3D, pointInHost, facingInHost);
                                    if (hostRef != null)
                                    {
                                        FamilyInstance newInstance = doc.Create.NewFamilyInstance(
                                            hostRef, pointInHost, facingInHost, symbol);

                                        // Корректировка ориентации
                                        AdjustElementOrientation(doc, newInstance, facingInHost);
                                    }
                                    else
                                    {
                                        TaskDialog.Show("Предупреждение",
                                            $"Не найдена поверхность для размещения клеммы в точке {pointInHost}");
                                    }
                                }
                                else
                                {
                                    // Для элементов без опоры находим ближайший уровень
                                    Level level = FindLevelAtPoint(doc, pointInHost);
                                    if (level == null)
                                    {
                                        level = new FilteredElementCollector(doc)
                                            .OfClass(typeof(Level))
                                            .FirstElement() as Level;
                                        if (level == null)
                                        {
                                            TaskDialog.Show("Ошибка", "Не найден уровень для размещения элемента");
                                            continue;
                                        }
                                    }

                                    // Создаем экземпляр на уровне
                                    FamilyInstance newInstance = doc.Create.NewFamilyInstance(
                                        pointInHost, symbol, level, StructuralType.NonStructural);

                                    // Корректируем ориентацию
                                    AdjustElementOrientation(doc, newInstance, facingInHost);
                                }
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка создания",
                                    $"Элемент {element.Id}: {ex.Message}");
                            }
                        }
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Критическая ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }

            TaskDialog.Show("Успех", "Замена элементов выполнена успешно");
            return Result.Succeeded;
        }

        private Level FindLevelAtPoint(Document doc, XYZ point)
        {
            // Ищем уровень по высоте Z
            double elevation = point.Z;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();
        }

        private void AdjustElementOrientation(Document doc, FamilyInstance instance, XYZ desiredDirection)
        {
            if (instance == null || desiredDirection == null) return;

            try
            {
                XYZ currentDirection = instance.FacingOrientation;
                if (currentDirection == null) return;

                // Рассчитываем угол между текущим и желаемым направлением
                double angle = currentDirection.AngleTo(desiredDirection);

                // Если угол значительный, корректируем ориентацию
                if (angle > 0.1)
                {
                    // Определяем ось вращения (перпендикуляр к плоскости XY)
                    XYZ rotationAxis = XYZ.BasisZ;

                    // Определяем центр вращения
                    LocationPoint locPoint = instance.Location as LocationPoint;
                    XYZ center = locPoint.Point;

                    // Создаем линию оси вращения
                    Line axisLine = Line.CreateUnbound(center, rotationAxis);

                    // Поворачиваем элемент
                    ElementTransformUtils.RotateElement(doc, instance.Id, axisLine, angle);
                }
            }
            catch
            {
                // Игнорируем ошибки корректировки ориентации
            }
        }

        private Reference FindHostSurface(Document doc, View3D view, XYZ point, XYZ direction)
        {
            if (view == null) return null;

            try
            {
                // Фильтр для перекрытий и стен
                LogicalOrFilter filter = new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls));

                ReferenceIntersector refIntersector = new ReferenceIntersector(
                    filter,
                    FindReferenceTarget.Face,
                    view);

                ReferenceWithContext ref1 = refIntersector.FindNearest(point, direction);
                ReferenceWithContext ref2 = refIntersector.FindNearest(point, -direction);

                // Выбираем ближайшую поверхность
                if (ref1 != null && ref2 != null)
                {
                    return ref1.Proximity < ref2.Proximity ? ref1.GetReference() : ref2.GetReference();
                }
                return ref1?.GetReference() ?? ref2?.GetReference();
            }
            catch
            {
                return null;
            }
        }

        // Фильтр выбора (только связи)
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ point) => false;
        }
    }
}