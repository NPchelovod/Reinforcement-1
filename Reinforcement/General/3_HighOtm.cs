using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class HighOtm : IExternalCommand
    {
        // 1 мм в футах (внутренние единицы Revit)
        private const double Tol = 1.0 / 304.8;

        // 200 мм в футах — расстояние, в пределах которого стыки считаются совпадающими
        private const double MergeDistance = 200.0 / 304.8;
        //Понял логику — ставим отметки на обоих концах трубы, если её Z-координаты различаются, а затем «схлопываем» дубли на стыках, если они находятся в пределах 200 мм и имеют одинаковую Z.
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = doc.ActiveView;

            // 1. Получаем выделенные элементы
            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Ошибка", "Не выбрано ни одного элемента.");
                return Result.Failed;
            }
            View3D view3D = GetOrCreate3DView(doc);
            // 2. Находим типоразмер высотной отметки
            SpotDimensionType spotElevationType = new FilteredElementCollector(doc)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .FirstOrDefault(t => t.Name.Equals("ADSK_Стрелка_Проектная_Вверх",
                    StringComparison.OrdinalIgnoreCase));

            if (spotElevationType == null)
            {
                TaskDialog.Show("Ошибка",
                    "Не найден типоразмер высотной отметки 'ADSK_Стрелка_Проектная_Вверх'.");
                return Result.Failed;
            }

            // 3. Собираем точки начала и конца всех кривых.
            //    candidates — только там, где Z начала и конца различаются.
            //    allEndpoints — все концы (для fallback, если высоты везде одинаковые).
            var candidates = new List<(XYZ point, Reference reference)>();
            var allEndpoints = new List<(XYZ point, Reference reference)>();
            // Опции для получения геометрии с ссылками.
            // IncludeNonVisibleObjects = true — чтобы получить и невидимую осевую линию.
            var geomOptions = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,   // <-- важно
                View = activeView
            };
            foreach (ElementId id in selectedIds)
            {
                Element elem = doc.GetElement(id);

                // 1) Координаты начала/конца берём из LocationCurve — они корректны.
                if (!(elem?.Location is LocationCurve locCurve)) continue;
                Curve locationCurve = locCurve.Curve;
                if (locationCurve == null) continue;

                XYZ start = locationCurve.GetEndPoint(0);
                XYZ end = locationCurve.GetEndPoint(1);

                // 2) Ссылки (Reference) ищем в геометрии элемента.
                
                // Пример для начальной точки
                XYZ rayDirection = activeView.UpDirection.Negate(); // смотрим вниз
                XYZ rayDir = view3D.UpDirection.Negate();
                Reference refStart = FindFaceReference(view3D, start, rayDir);
                Reference refEnd = FindFaceReference(view3D, end, rayDir);
                GeometryElement geometry = elem.get_Geometry(geomOptions);
                if (geometry != null)
                {
                    foreach (GeometryObject geoObj in geometry)
                    {
                        // Нас интересуют только кривые (осевая линия трубы).
                        if (!(geoObj is Curve refCurve)) continue;

                        // Сравниваем концы геометрической кривой с концами LocationCurve.
                        // Если совпадают с допуском — это та самая осевая линия.
                        XYZ refStartPt = refCurve.GetEndPoint(0);
                        XYZ refEndPt = refCurve.GetEndPoint(1);

                        bool matchesStart = refStartPt.DistanceTo(start) < Tol
                                            || refEndPt.DistanceTo(start) < Tol;
                        bool matchesEnd = refStartPt.DistanceTo(end) < Tol
                                          || refEndPt.DistanceTo(end) < Tol;

                        if (matchesStart && matchesEnd)
                        {
                            // Пытаемся получить стабильные ссылки на концы кривой.
                            refStart = refCurve.GetEndPointReference(
                                refStartPt.DistanceTo(start) < Tol ? 0 : 1);
                            refEnd = refCurve.GetEndPointReference(
                                refEndPt.DistanceTo(end) < Tol ? 1 : 0);
                            break; // нашли нужную кривую — выходим из цикла геометрии
                        }
                    }
                }

                // Собираем все концы для fallback (только если есть ссылка).
                if (refStart != null) allEndpoints.Add((start, refStart));
                if (refEnd != null) allEndpoints.Add((end, refEnd));

                // Ставим отметки в начале И в конце, если высоты различаются.
                if (Math.Abs(start.Z - end.Z) > Tol)
                {
                    if (refStart != null) candidates.Add((start, refStart));
                    if (refEnd != null) candidates.Add((end, refEnd));
                }
            }
            // 4. Если высоты везде одинаковые — ставим одну отметку на крайнем элементе.
            bool fallback = false;
            if (candidates.Count == 0)
            {
                if (allEndpoints.Count == 0)
                {
                    TaskDialog.Show("Информация",
                        "Не найдено элементов с криволинейной геометрией.");
                    return Result.Succeeded;
                }

                // Центр всех концов
                XYZ centroid = new XYZ(
                    allEndpoints.Average(p => p.point.X),
                    allEndpoints.Average(p => p.point.Y),
                    allEndpoints.Average(p => p.point.Z));

                // Берём точку, максимально удалённую от центра — это «край» цепочки
                var extreme = allEndpoints
                    .OrderByDescending(p => p.point.DistanceTo(centroid))
                    .First();
                //var extreme = allEndpoints.OrderBy(p => p.point.Y).ThenBy(p.point.X).First();
                candidates.Add(extreme);
                fallback = true;
            }

            // 5. Схлопываем дубли: если точка уже есть в списке в пределах MergeDistance
            //    (200 мм) и имеет ту же Z — пропускаем её.
            //    Порядок обхода сохраняет первую встреченную точку.
            var elevationPoints = new List<(XYZ point, Reference reference)>();

            foreach (var cand in candidates)
            {
                bool isDuplicate = elevationPoints.Any(existing =>
                    Math.Abs(existing.point.Z - cand.point.Z) < Tol &&
                    existing.point.DistanceTo(cand.point) < MergeDistance);

                if (!isDuplicate)
                    elevationPoints.Add(cand);
            }

            // 6. Создаём высотные отметки в транзакции
            int created = 0;
            var errors = new List<string>();
            using (Transaction trans = new Transaction(doc, "Расстановка высотных отметок"))
            {
                trans.Start();

                foreach (var (point, reference) in elevationPoints)
                {
                    // Точка излома выноски (смещение вверх на 1 м)
                    //XYZ bend = point + XYZ.BasisZ * 1.0;
                    //// Конец выноски
                    //XYZ endLeader = bend + XYZ.BasisX * 1.0;
                    XYZ viewRight = activeView.RightDirection;
                    XYZ viewUp = activeView.UpDirection;

                    XYZ bend = point + viewUp * 1.0;
                    XYZ endLeader = bend + viewRight * 1.0;
                    
                    try
                    {
                        SpotDimension spot = doc.Create.NewSpotElevation(
                            activeView,
                            reference,
                            point,
                            bend,
                            endLeader,
                            point,
                            true); // с выноской

                        if (spot != null)
                        {
                            spot.SpotDimensionType = spotElevationType;
                            created++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Не удалось создать отметку в точке ({point.X:F3}, {point.Y:F3}, {point.Z:F3}): {ex.Message}");
                        errors.Add($"{point.X:F3},{point.Y:F3},{point.Z:F3}: {ex.GetType().Name} — {ex.Message}");
                    }
                }

                trans.Commit();
            }
            if (errors.Count > 0)
            {
                TaskDialog.Show("Ошибки",
                    $"Создано: {created}, ошибок: {errors.Count}\n\n" +
                    string.Join("\n", errors.Take(10)));
            }
            // 7. Отчёт
            string info = fallback
                ? $"Высоты везде одинаковые — поставлена одна отметка на крайнем элементе.\n" +
                  $"Создано отметок: {created}."
                : $"Расставлено высотных отметок: {created} из {candidates.Count} кандидатов.\n" +
                $"ошибок: {errors.Count}\n\n" +
                    string.Join("\n", errors.Take(10));

            TaskDialog.Show("Готово", info);
            return Result.Succeeded;
        }

        private Reference FindFaceReference(View3D view3D, XYZ point, XYZ direction)
        {
            // Создаём искатель, который возвращает только грани (Face)
            var intersector = new ReferenceIntersector(view3D)
            {
                TargetType = FindReferenceTarget.Face
            };

            // Находим ближайшее пересечение луча с гранями
            ReferenceWithContext hit = intersector.FindNearest(point, direction);

            // Возвращаем ссылку (или null, если ничего не нашли)
            return hit?.GetReference();
        }
        private View3D GetOrCreate3DView(Document doc)
        {
            // 1. Пытаемся найти существующий 3D-вид (например, "{3D}")
            View3D view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name == "{3D}");

            // 2. Если не нашли — берём любой нешаблонный 3D-вид
            if (view3D == null)
            {
                view3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);
            }

            // 3. Если 3D-видов нет вообще — создаём изометрический
            if (view3D == null)
            {
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

                if (viewFamilyType != null)
                {
                    using (Transaction t = new Transaction(doc, "Создание 3D-вида"))
                    {
                        t.Start();
                        view3D = View3D.CreateIsometric(doc, viewFamilyType.Id);
                        t.Commit();
                    }
                }
            }

            return view3D;
        }
    }
}