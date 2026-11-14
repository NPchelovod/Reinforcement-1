using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

public class CCC
{
    public List<XYZ> GetFloorContourPoints(Floor floor)
    {
        var points = new List<XYZ>();
        if (floor == null) return points;

        // --- Попытка 1: через Sketch (надёжно для перекрытий, созданных вручную) ---
        Sketch sketch = null;
        if (floor.SketchId != ElementId.InvalidElementId) // Revit 2022+
        {
            sketch = floor.Document.GetElement(floor.SketchId) as Sketch;
        }
        else
        {
            // Для версий до 2022 или если SketchId пуст — ищем зависимый Sketch
            var depIds = floor.GetDependentElements(new ElementClassFilter(typeof(Sketch)));
            if (depIds != null && depIds.Count > 0)
                sketch = floor.Document.GetElement(depIds.First()) as Sketch;
        }

        if (sketch != null && sketch.Profile.Size > 0)
        {
            // Первый CurveArray в Profile — внешний контур (дырки игнорируются)
            CurveArray outerLoop = sketch.Profile.get_Item(0);
            foreach (Curve curve in outerLoop)
            {
                // Для линий достаточно начальной точки, для дуг — можно тесселировать
                if (curve is Line line)
                    points.Add(line.GetEndPoint(0));
                else
                    points.AddRange(curve.Tessellate().Take(curve.Tessellate().Count - 1)); // без дубля конца
            }
            return points; // Успешно вышли
        }

        // --- Попытка 2: через геометрию (если эскиза нет) ---
        // Берём нижнюю грань (для горизонтального перекрытия — это плоскость)
        IList<Reference> faceRefs = HostObjectUtils.GetBottomFaces(floor);
        if (faceRefs == null || faceRefs.Count == 0)
            faceRefs = HostObjectUtils.GetTopFaces(floor);

        foreach (Reference faceRef in faceRefs)
        {
            PlanarFace face = floor.GetGeometryObjectFromReference(faceRef) as PlanarFace;
            if (face == null) continue;

            // EdgeLoops: первый цикл — внешний контур
            EdgeArrayArray loops = face.EdgeLoops;
            if (loops.Size == 0) continue;

            EdgeArray outerEdges = loops.get_Item(0);
            foreach (Edge edge in outerEdges)
            {
                // У каждого ребра два конца; берём начальную вершину, чтобы не дублировать
                points.Add(edge.AsCurve().GetEndPoint(0));
            }
            break; // Нашли внешний контур на первой подходящей грани
        }

        return points;
    }

   
    public Floor CreateStructuralFloor(
        Document doc,
        CurveLoop profile,
        FloorType floorType,
        Level level)
    {
        // 1. Проверяем входные данные
        if (doc == null || profile == null || floorType == null || level == null)
            return null;

        // 2. Оборачиваем контур в список (метод требует IList<CurveLoop>)
        IList<CurveLoop> profiles = new List<CurveLoop> { profile };

        // 3. Создаём несущее перекрытие
        //    isStructural: true — ключевой флаг
        //    slopeArrow: null, slope: 0 — перекрытие горизонтальное
        Floor structuralFloor = Floor.Create(
            doc,
            profiles,
            floorType.Id,
            level.Id,
            isStructural: true,   // <-- несущее перекрытие
            slopeArrow: null,
            slope: 0.0
        );

        return structuralFloor;
    }

    //// Находим первый попавшийся тип перекрытия (в реальном коде лучше искать по имени)
    //FloorType floorType = new FilteredElementCollector(doc)
    //    .OfClass(typeof(FloorType))
    //    .Cast<FloorType>()
    //    .FirstOrDefault();

    //// Находим уровень по имени
    //Level level = new FilteredElementCollector(doc)
    //    .OfClass(typeof(Level))
    //    .Cast<Level>()
    //    .FirstOrDefault(l => l.Name == "Уровень 1"); // укажите нужное имя
}