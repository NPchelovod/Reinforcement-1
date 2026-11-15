using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Reinforcement.Aspirant
{
    public class FloorContourData
    {
        public ElementId FloorId { get; set; }
        public string FloorName { get; set; }
        public int LevelId { get; set; }
        /// <summary>Внешний контур в мм (без отверстий).</summary>
        public List<XYZ> OuterContourMm { get; set; }
        /// <summary>Отверстия/внутренние контуры в мм (если есть).</summary>
        public List<List<XYZ>> InnerLoopsMm { get; set; }
        /// <summary>Способ получения: "Sketch" или "Geometry".</summary>
        public string Source { get; set; }

        public FloorContourData()
        {
            OuterContourMm = new List<XYZ>();
            InnerLoopsMm = new List<List<XYZ>>();
        }

        
        public double AreaMm { get { Initializator(false); return _areaMm; } }


        private double _areaMm = 0;
        public double AreaOuterMm { get { Initializator(false); return _areaOuterMm; } }
        private double _areaOuterMm;
        public double AreaInnerMm { get { Initializator(false); return _areaInnerMm; } }
        private double _areaInnerMm;
        private bool _isInitial=false;
        public void Initializator(bool force=true)
        {
            //если тру то принудительно иначе нет
            if(_isInitial && !force) { return; }
            _isInitial=true;
            //считаем данные
            _areaOuterMm = XYZListExtensions.GetArea(OuterContourMm);
            foreach (var li in InnerLoopsMm)
            {
                _areaInnerMm+= XYZListExtensions.GetArea(li);
            }
            _areaMm = _areaOuterMm - _areaInnerMm;
            if(_areaMm<=0)
            {
                //ошибка явная
                ErrorAnalyse.Error(null, "if(_areaMm<=0)) FloorContourData()");
                _areaMm = 0;
            }
        }


        //методы для получения контуров плит с вида
        public static List<FloorContourData> GetAllFloorsContoursInView(Document doc, View view)
    {
        var result = new List<FloorContourData>();
        if (doc == null || view == null) return result;

        // Коллектор с привязкой к виду — фильтрует только видимые на этом виде элементы
        var floors = new FilteredElementCollector(doc, view.Id)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Floor))
            .Cast<Floor>();

        foreach (Floor floor in floors)
        {
            var data = new FloorContourData
            {
                FloorId = floor.Id,
                FloorName = floor.Name,
            };

            try
            {
                // Пробуем получить контур
                List<List<XYZ>> loops;
                string source;
                if (TryGetContourFromSketch(floor, out loops))
                {
                    source = "Sketch";
                }
                else if (TryGetContourFromGeometry(floor, out loops))
                {
                    source = "Geometry";
                }
                else
                {
                    continue; // контур получить не удалось
                }

                data.Source = source;

                // Первый loop — внешний контур, остальные — отверстия
                if (loops.Count > 0)
                {
                    data.OuterContourMm = ToMmAndDedupe(loops[0]);
                    for (int i = 1; i < loops.Count; i++)
                    {
                        var hole = ToMmAndDedupe(loops[i]);
                        if (hole.Count >= 3)
                            data.InnerLoopsMm.Add(hole);
                    }
                }
            }
            catch (Exception ex)
            {
                // Логируйте ошибку по своему усмотрению
                System.Diagnostics.Debug.WriteLine($"Floor {floor.Id}: {ex.Message}");
            }

            result.Add(data);
        }

        return result;
    }
        private static bool TryGetContourFromSketch(Floor floor, out List<List<XYZ>> loops)
        {
            loops = null;
            Document doc = floor.Document;

            Sketch sketch = null;

            #if REVIT2022_OR_NEWER
                if (floor.SketchId != ElementId.InvalidElementId)
                    sketch = doc.GetElement(floor.SketchId) as Sketch;
                #endif

            if (sketch == null)
            {
                // Fallback для старых версий и сложных случаев
                var depIds = floor.GetDependentElements(new ElementClassFilter(typeof(Sketch)));
                if (depIds != null && depIds.Count > 0)
                    sketch = doc.GetElement(depIds.First()) as Sketch;
            }

            if (sketch == null || sketch.Profile == null || sketch.Profile.Size == 0)
                return false;

            loops = new List<List<XYZ>>();
            for (int i = 0; i < sketch.Profile.Size; i++)
            {
                var curveArray = sketch.Profile.get_Item(i);
                var pts = new List<XYZ>();
                foreach (Curve c in curveArray)
                {
                    // Tessellate корректно работает и с линиями, и с дугами
                    var tess = c.Tessellate();
                    for (int k = 0; k < tess.Count - 1; k++) // без последней точки (совпадает с началом следующей)
                        pts.Add(tess[k]);
                }
                if (pts.Count >= 3)
                    loops.Add(pts);
            }
            return loops.Count > 0;
        }

        private const double FtToMm = 304.8;
        private const double TolMm = 0.1; // допуск для дедупликации, мм

        private static List<XYZ> ToMmAndDedupe(List<XYZ> pointsFt)
        {
            var result = new List<XYZ>(pointsFt.Count);
            foreach (var p in pointsFt)
            {
                var mm = new XYZ(p.X * FtToMm, p.Y * FtToMm, p.Z * FtToMm);
                bool dup = result.Any(q =>
                    Math.Abs(q.X - mm.X) < TolMm &&
                    Math.Abs(q.Y - mm.Y) < TolMm &&
                    Math.Abs(q.Z - mm.Z) < TolMm);
                if (!dup) result.Add(mm);
            }
            return result;
        }

        private static bool TryGetContourFromGeometry(Floor floor, out List<List<XYZ>> loops)
        {
            loops = null;

            IList<Reference> faceRefs = HostObjectUtils.GetBottomFaces(floor);
            if (faceRefs == null || faceRefs.Count == 0)
                faceRefs = HostObjectUtils.GetTopFaces(floor);
            if (faceRefs == null || faceRefs.Count == 0)
                return false;

            foreach (Reference faceRef in faceRefs)
            {
                PlanarFace face = floor.GetGeometryObjectFromReference(faceRef) as PlanarFace;
                if (face == null) continue;

                var edgeLoops = face.EdgeLoops;
                if (edgeLoops.Size == 0) continue;

                loops = new List<List<XYZ>>();
                for (int i = 0; i < edgeLoops.Size; i++)
                {
                    var edgeArray = edgeLoops.get_Item(i);
                    var pts = new List<XYZ>();
                    foreach (Edge edge in edgeArray)
                    {
                        // AsCurve().Tessellate() даёт точки вдоль ребра
                        var tess = edge.AsCurve().Tessellate();
                        for (int k = 0; k < tess.Count - 1; k++)
                            pts.Add(tess[k]);
                    }
                    if (pts.Count >= 3)
                        loops.Add(pts);
                }
                return loops.Count > 0;
            }

            return false;
        }




    }
}
