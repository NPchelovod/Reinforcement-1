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
    [Transaction(TransactionMode.Manual)]
    public class EL_panel_Light_without_boxes : IExternalCommand
    {
        //словарь команда и что мы создаем
        // имя команды: (класс, тип класса, требует ли привязки к поверхности )
        public static Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)> elementCategories { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)>()
        {
                { "в перекрытии патроны", ("патрон", "патрон", false) },
                { "в перекрытии клеммы", ("Клеммник_для_распаячной_и_", "Клеммник для распаячной и универальной коробок, шаг крепления 60", true) },
               // { "в стенах патроны", ("патрон", "патрон", true) },
                //{ "в стенах клеммы", ("Клеммник_для_распаячной", "Клеммник для распаячной и универсальной коробок, шаг крепления 60", true) }
        };

        // (имя семейства, тип) = хотя бы одно семейство которое относится к патронам и клеммникам - к создаваемым экземплярам
        public static Dictionary<(string FamilyName, string SymbolName), FamilySymbol> one_replaceable_element { get; set; } = new Dictionary<(string FamilyName, string SymbolName), FamilySymbol>();


        //словарь что мы заменяем, на какую команду заменяем данные кубики (имя семейства, тип)
        public static Dictionary<string, (string FamilyName, string SymbolName)> replace_cubics { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName)>()
        {
            { "в перекрытии патроны",("Коробка ЭЛ_КУ1301","КУ1301_патрон") },
            { "в перекрытии клеммы",("Коробка ЭЛ_КУ1301","КУ1301_клеммник")}
        };

        // (имя семейства, тип) = все заменяемые кубики

        public static Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_replace_cubics { get; set; } = new Dictionary<(string FamilyName, string SymbolName), System.Collections.Generic.List<FamilyInstance>>();
        
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

            // 1) Выбор связанной модели
            bool copy_svis_model_or_tek_model = true; // копируем из связанной модели а не из текущей или наоборот, тогда просто заменяем

            Document linkedDoc = EL_panel_step1.choice_relation_model(copy_svis_model_or_tek_model, ref message, sel, doc); //связанная модель

            if (linkedDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось получить документ связанной модели");
                return Result.Failed;
            }

            // После получения linkedDoc добавьте:
            RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => link.GetLinkDocument()?.Title == linkedDoc.Title);

            if (linkInstance == null)
            {
                TaskDialog.Show("Ошибка", "Не найден экземпляр связи");
                return Result.Failed;
            }

            Transform transform = linkInstance.GetTotalTransform(); // Критически важно!


            // 2 классификатор элементов
            // создаем (класс, типоразмер) - (ссылка на класс и на типоразмер, и из какого документа тру - из текущего)
            foreach (var dob in elementCategories)
            {
                one_replaceable_element[(dob.Value.FamilyName, dob.Value.SymbolName)] =null;
            }
            foreach (var dob in replace_cubics)
            {
                all_replace_cubics[(dob.Value.FamilyName, dob.Value.SymbolName)] = new List<FamilyInstance>();
            }


            
            // 2.1 - находим хотя бы один элемент
            // Проверка наличия семейств и типоразмеров, хотя бы один элемент
            var missingElements = new List<string>(); // не найденные в текущей модели элементы, хотя бы один

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
                        one_replaceable_element[seach_symbol.Key] = symbol;
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
                        break;
                    }
                }
            }

            // Если отсутствуют необходимые элементы
            if (missingElements.Count > 0)
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют необходимые элементы в данной модели:\n{string.Join("\n", missingElements)}");
            }
            if (proxod==0)
            {
                // ни одного семейства нет, но вдруг нам надо просто удалить из текущего проекта кубики? так что пойдем дальше
                //return Result.Failed;
            }


            // 2.2 в связанной находим все кубики
            // Создаем коллектор для поиска элементов
            var missingElements2 = new List<string>();
            var collector = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance)); // Получаем все экземпляры семейств
            proxod = 0;
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
                if (elementsToReplace.Count == 0 )
                {
                    
                    if (!missingElements2.Contains($"Семейство: {familyName}"))
                        missingElements2.Add($"Семейство: {familyName}");
                    continue;
                }
                proxod += 1;
                all_replace_cubics[seach_symbol.Key] = elementsToReplace;


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



            // 3 удаление существующих элементов сначала тех которые должны быть в итоге - светильников и тд
            collector = new FilteredElementCollector(doc)
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
                if(elementsToReplace.Count==0)
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



            //4 Создание новых элементов
            //4 Создание новых элементов
            try
            {
                using (Transaction t = new Transaction(doc, "Копирование и замена"))
                {
                    t.Start();

                    // 1. Копируем кубики из связанной модели
                    var elementsToCopy = all_replace_cubics.Values.SelectMany(list => list).ToList();
                    if (elementsToCopy.Count > 0)
                    {
                        var copyOptions = new CopyPasteOptions();
                        ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(
                            linkedDoc,
                            elementsToCopy.Select(e => e.Id).ToList(),
                            doc,
                            transform,
                            copyOptions);

                        // Получаем активный 3D вид для поиска поверхностей
                        View3D active3DView = doc.ActiveView as View3D;
                        if (active3DView == null)
                        {
                            TaskDialog.Show("Ошибка", "Требуется активный 3D вид для размещения элементов");
                            return Result.Failed;
                        }

                        // 2. Заменяем скопированные кубики
                        foreach (ElementId copiedId in copiedIds)
                        {
                            FamilyInstance cubic = doc.GetElement(copiedId) as FamilyInstance;
                            if (cubic == null) continue;

                            // Определяем тип замены
                            var cubicType = (cubic.Symbol.Family.Name, cubic.Symbol.Name);
                            var replacement = replace_cubics.FirstOrDefault(
                                x => x.Value.FamilyName.Equals(cubicType.Item1, StringComparison.OrdinalIgnoreCase) &&
                                     x.Value.SymbolName.Equals(cubicType.Item2, StringComparison.OrdinalIgnoreCase)).Key;

                            if (string.IsNullOrEmpty(replacement)) continue;

                            if (!elementCategories.TryGetValue(replacement, out var category)) continue;

                            // Получаем позицию и ориентацию
                            LocationPoint locPoint = cubic.Location as LocationPoint;
                            if (locPoint == null) continue;

                            XYZ position = locPoint.Point;
                            XYZ facingOrientation = cubic.FacingOrientation;

                            if (!one_replaceable_element.TryGetValue(
                                (category.FamilyName, category.SymbolName),
                                out FamilySymbol symbol)) continue;

                            if (symbol == null) continue;

                            try
                            {
                                if (category.RequiresHost)
                                {
                                    // Поиск поверхности для размещения
                                    Reference hostRef = FindHostSurface(doc, active3DView, position, facingOrientation);

                                    if (hostRef != null)
                                    {
                                        // Создаем элемент на поверхности
                                        FamilyInstance newInstance = doc.Create.NewFamilyInstance(
                                            hostRef,
                                            position,
                                            facingOrientation,
                                            symbol);

                                        // Корректируем позицию
                                        if (newInstance.Location is LocationPoint newLoc)
                                        {
                                            XYZ offset = position - newLoc.Point;
                                            if (offset.GetLength() > 0.001)
                                            {
                                                ElementTransformUtils.MoveElement(doc, newInstance.Id, offset);
                                            }
                                        }

                                        // Корректируем ориентацию
                                        AdjustElementOrientation(doc, newInstance, facingOrientation);
                                    }
                                    else
                                    {
                                        TaskDialog.Show("Предупреждение",
                                            $"Не найдена поверхность для размещения клеммы в точке {position}");
                                    }
                                }
                                else
                                {
                                    // Создаем свободно стоящий элемент
                                    FamilyInstance newInstance = doc.Create.NewFamilyInstance(
                                        position,
                                        symbol,
                                        StructuralType.NonStructural);

                                    // Корректируем ориентацию
                                    AdjustElementOrientation(doc, newInstance, facingOrientation);
                                }

                                // Удаляем скопированный кубик
                                doc.Delete(cubic.Id);
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка создания",
                                    $"Не удалось создать элемент: {ex.Message}\nЭлемент: {cubic.Id}");
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

            //5 удаление кубиков в текущем проекте если они были 

            collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance)); // Получаем все экземпляры семейств

            list_del_elements = new List<FamilyInstance>();
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





            TaskDialog.Show("Успех", "Замена элементов выполнена успешно");
            return Result.Succeeded;
        }

        // Оптимизированный поиск уровней
        private Level FindLevelAtPoint(Document doc, XYZ point)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - point.Z) < 0.001);
        }


        // Упрощенная корректировка ориентации
        private void AdjustElementOrientation(Document doc, FamilyInstance instance, XYZ desiredDirection)
        {
            if (instance == null || desiredDirection == null) return;

            try
            {
                XYZ currentDirection = instance.FacingOrientation;
                if (currentDirection == null) return;

                // Вычисляем угол между направлениями
                double angle = currentDirection.AngleTo(desiredDirection);

                // Если угол значительный (>1 градус)
                if (angle > 0.01745)
                {
                    // Создаем ось вращения (вертикальную)
                    LocationPoint loc = instance.Location as LocationPoint;
                    if (loc == null) return;

                    XYZ center = loc.Point;
                    Line axis = Line.CreateBound(center, center + XYZ.BasisZ);

                    // Определяем направление вращения
                    XYZ cross = currentDirection.CrossProduct(desiredDirection);
                    double sign = Math.Sign(cross.DotProduct(XYZ.BasisZ));
                    double rotationAngle = sign * angle;

                    // Применяем вращение
                    ElementTransformUtils.RotateElement(doc, instance.Id, axis, rotationAngle);
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                TaskDialog.Show("Ошибка ориентации", ex.Message);
            }
        }

        private Reference FindHostSurface(Document doc, View3D view, XYZ point, XYZ direction)
        {
            if (view == null) return null;

            try
            {
                // Создаем комбинированный фильтр для перекрытий и стен
                ElementFilter floorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
                ElementFilter wallFilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
                LogicalOrFilter combinedFilter = new LogicalOrFilter(floorFilter, wallFilter);

                // Настройка поиска поверхностей
                ReferenceIntersector refIntersector = new ReferenceIntersector(
                    combinedFilter,
                    FindReferenceTarget.Face,
                    view);

                // Ищем поверхности в двух направлениях
                ReferenceWithContext refForward = refIntersector.FindNearest(point, direction);
                ReferenceWithContext refBackward = refIntersector.FindNearest(point, direction.Negate());

                // Выбираем ближайшую подходящую поверхность
                ReferenceWithContext bestRef = null;
                if (refForward != null && refBackward != null)
                {
                    bestRef = refForward.Proximity < refBackward.Proximity ? refForward : refBackward;
                }
                else
                {
                    bestRef = refForward ?? refBackward;
                }

                // Проверяем качество поверхности
                if (bestRef != null && bestRef.Proximity < 2.0) // Максимальное расстояние 2 единицы
                {
                    return bestRef.GetReference();
                }

                return null;
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