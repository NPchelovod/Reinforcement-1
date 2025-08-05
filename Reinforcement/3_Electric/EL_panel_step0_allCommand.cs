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
    public static class EL_panel_step0_allCommand
    {
        // (имя семейства, тип) = хотя бы одно семейство которое относится к патронам и клеммникам - к создаваемым экземплярам
        public static Dictionary<(string FamilyName, string SymbolName), FamilySymbol> one_replaceable_element { get; set; } = new Dictionary<(string FamilyName, string SymbolName), FamilySymbol>();
        // (имя семейства, тип) = все заменяемые кубики

        public static Dictionary<(string FamilyName, string SymbolName), List<FamilyInstance>> all_replace_cubics { get; set; } = new Dictionary<(string FamilyName, string SymbolName), System.Collections.Generic.List<FamilyInstance>>();

        public static Document linkedDoc = null;
        public static bool CopyTask(ExternalCommandData commandData, ref string message, ElementSet elements, Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)> elementCategories, Dictionary<string, (string FamilyName, string SymbolName)> replace_cubics)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;



            all_replace_cubics.Clear();
            one_replaceable_element.Clear();

            // Проверка активного вида
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");
                return false;
            }

            // 1) Выбор связанной модели
            bool copy_svis_model_or_tek_model = true; // копируем из связанной модели а не из текущей или наоборот, тогда просто заменяем

            linkedDoc = EL_panel_step1_connected_model.choice_relation_model(copy_svis_model_or_tek_model, ref message, sel, doc); //связанная модель

            if (linkedDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось получить документ связанной модели");
                return false;
            }

            // После получения linkedDoc добавьте:
            RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => link.GetLinkDocument()?.Title == linkedDoc.Title);

            if (linkInstance == null)
            {
                TaskDialog.Show("Ошибка", "Не найден экземпляр связи");
                return false;
            }

            Transform transform = linkInstance.GetTotalTransform(); // Критически важно!


            // 2 классификатор элементов
            // создаем (класс, типоразмер) - (ссылка на класс и на типоразмер, и из какого документа тру - из текущего)
            foreach (var dob in elementCategories)
            {
                one_replaceable_element[(dob.Value.FamilyName, dob.Value.SymbolName)] = null;
            }
            foreach (var dob in replace_cubics)
            {
                all_replace_cubics[(dob.Value.FamilyName, dob.Value.SymbolName)] = new List<FamilyInstance>();
            }



            // 2.1 - находим хотя бы один элемент
            // Проверка наличия семейств и типоразмеров, хотя бы один элемент

            one_replaceable_element = EL_panel_step2_one_element_sopostav_family.one_element_sopostav_family(one_replaceable_element, doc);



            // 2.2 в связанной находим все кубики

            all_replace_cubics = EL_panel_step3_all_elements_family_connect_models.all_elements_family_connect_models(all_replace_cubics, linkedDoc);


            // 3 удаление существующих элементов сначала тех которые должны быть в итоге - светильников и тд

            EL_panel_step4_delit_elements.delit_one_family(one_replaceable_element, doc);
            //5 удаление кубиков в текущем проекте если они были 

            EL_panel_step4_delit_elements.delit_all_family(all_replace_cubics, doc);


            //4 Создание новых элементов
            try
            {

                // Перед основной транзакцией активируем все необходимые типоразмеры
                using (Transaction prepTrans = new Transaction(doc, "Подготовка типоразмеров"))
                {
                    prepTrans.Start();

                    foreach (var symbolPair in one_replaceable_element)
                    {
                        FamilySymbol symbol = symbolPair.Value;
                        if (symbol != null && !symbol.IsActive && symbol.IsValidObject)
                        {
                            try
                            {
                                symbol.Activate();
                            }
                            catch
                            {
                                // Игнорируем ошибки для отдельных типоразмеров
                                TaskDialog.Show("Ошибка Revit", $"Ошибка активации: {symbol.Name}");
                            }
                        }
                    }

                    doc.Regenerate();
                    prepTrans.Commit();
                }


                using (Transaction t = new Transaction(doc, "Копирование и замена"))
                {
                    t.Start();
                    // Получаем активный 3D вид для поиска поверхностей
                    View3D active3DView = doc.ActiveView as View3D;
                    if (active3DView == null)
                    {
                        // Попробуем найти любой доступный 3D вид
                        active3DView = new FilteredElementCollector(doc)
                            .OfClass(typeof(View3D))
                            .Cast<View3D>()
                            .FirstOrDefault(v => !v.IsTemplate && v.Name == "3D Вид");

                        if (active3DView == null)
                        {
                            TaskDialog.Show("Ошибка", "Не найден подходящий 3D вид в текущей модели");
                        }
                        return false;
                    }
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

                        // Проверяем наличие отражения в трансформации
                        double determinant = GetTransformDeterminant(transform);
                        bool isMirrored = determinant < 0;

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

                            // Получаем позицию и ориентацию из скопированного элемента
                            LocationPoint locPoint = cubic.Location as LocationPoint;
                            if (locPoint == null) continue;

                            XYZ position = locPoint.Point; // Уже в координатах текущего документа
                            XYZ facingOrientation = cubic.FacingOrientation;

                            // Корректировка ориентации при отражении
                            if (isMirrored)
                            {
                                facingOrientation = facingOrientation.Negate();
                            }

                            if (!one_replaceable_element.TryGetValue(
                                (category.FamilyName, category.SymbolName),
                                out FamilySymbol symbol)) continue;

                            if (symbol == null) continue;

                            try
                            {
                                FamilyInstance newInstance = null;

                                if (category.RequiresHost)
                                {
                                    // Поиск поверхности для размещения
                                    Reference hostRef = FindHostSurface(doc, active3DView, position, facingOrientation);

                                    if (hostRef != null)
                                    {
                                        newInstance = doc.Create.NewFamilyInstance(
                                            hostRef,
                                            position,
                                            facingOrientation,
                                            symbol);
                                    }
                                    else
                                    {
                                        // Создаем без хоста, если поверхность не найдена
                                        newInstance = doc.Create.NewFamilyInstance(
                                            position,
                                            symbol,
                                            StructuralType.NonStructural);
                                    }
                                }
                                else
                                {
                                    // Создаем свободно стоящий элемент
                                    newInstance = doc.Create.NewFamilyInstance(
                                        position,
                                        symbol,
                                        StructuralType.NonStructural);
                                }

                                // Корректируем ориентацию
                                if (newInstance != null)
                                {
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
                return false;
            }

            


            TaskDialog.Show("Успех", "Замена элементов выполнена успешно");
            return true;
        }
        // Вспомогательная функция для определения отражения в трансформации
        private static double GetTransformDeterminant(Transform transform)
        {
            double a = transform.BasisX.X;
            double b = transform.BasisX.Y;
            double c = transform.BasisX.Z;

            double d = transform.BasisY.X;
            double e = transform.BasisY.Y;
            double f = transform.BasisY.Z;

            double g = transform.BasisZ.X;
            double h = transform.BasisZ.Y;
            double i = transform.BasisZ.Z;

            return a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        }
        // Оптимизированный поиск уровней
        private static Level FindLevelAtPoint(Document doc, XYZ point)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - point.Z) < 0.001);
        }


        // Упрощенная корректировка ориентации
        private static void AdjustElementOrientation(Document doc, FamilyInstance instance, XYZ desiredDirection)
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

        private static Reference FindHostSurface(Document doc, View3D view, XYZ point, XYZ direction)
        {
            if (view == null)
            {
                TaskDialog.Show("Ошибка", "Активный вид не является 3D видом");
                return null;
            }

            if (point == null || point.IsZeroLength())
            {
                TaskDialog.Show("Ошибка", "Нулевые координаты точки");
                return null;
            }

            // Если направление не задано, используем вертикальное
            if (direction == null || direction.IsZeroLength())
            {
                direction = XYZ.BasisZ;
            }
            else
            {
                direction = direction.Normalize();
            }

            try
            {
                // Создаем комбинированный фильтр для перекрытий, стен и потолков
                List<ElementFilter> filters = new List<ElementFilter>
                {
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementCategoryFilter(BuiltInCategory.OST_Ceilings),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),// можно без этих двух
                     new ElementCategoryFilter(BuiltInCategory.OST_GenericModel)
                };

                LogicalOrFilter combinedFilter = new LogicalOrFilter(filters);

                // Настройка поиска поверхностей
                ReferenceIntersector refIntersector = new ReferenceIntersector(
                    combinedFilter,
                    FindReferenceTarget.Face,
                    view)
                {
                    // Увеличиваем дальность поиска
                    FindReferencesInRevitLinks = true
                };

                // Ищем поверхности в 6 направлениях
                List<ReferenceWithContext> references = new List<ReferenceWithContext>
                {
                    refIntersector.FindNearest(point, direction),
                    refIntersector.FindNearest(point, -direction),
                    refIntersector.FindNearest(point, XYZ.BasisX),
                    refIntersector.FindNearest(point, -XYZ.BasisX),
                    refIntersector.FindNearest(point, XYZ.BasisY),
                    refIntersector.FindNearest(point, -XYZ.BasisY),
                    refIntersector.FindNearest(point, XYZ.BasisZ),
                    refIntersector.FindNearest(point, -XYZ.BasisZ)
                };

                // Выбираем ближайшую подходящую поверхность
                ReferenceWithContext bestRef = references
                    .Where(r => r != null)
                    .OrderBy(r => r.Proximity)
                    .FirstOrDefault();

                // Проверяем качество поверхности
                if (bestRef != null && bestRef.Proximity < 5.0) // Максимальное расстояние 5 единиц
                {
                    return bestRef.GetReference();
                }

                // Дополнительный поиск по вертикали
                ReferenceWithContext verticalRef = refIntersector.FindNearest(point, XYZ.BasisZ) ??
                                                  refIntersector.FindNearest(point, -XYZ.BasisZ);

                if (verticalRef != null && verticalRef.Proximity < 10.0)
                {
                    return verticalRef.GetReference();
                }

                return null;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка поиска", $"Не удалось найти поверхность: {ex.Message}");
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
