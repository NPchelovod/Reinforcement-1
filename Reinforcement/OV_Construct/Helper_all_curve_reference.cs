using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Windows.Controls;
using System.Net;

namespace Reinforcement
{
    public class Helper_all_curve_reference
    {
        public static List<Curve> Get_curve_reference(ElementId ventId, View viewPlan)
        { //return new List<Curve> (); 
            Document doc = RevitAPI.Document;
            var instance = (FamilyInstance)doc.GetElement(ventId);

            Options geomOptions = new Options { ComputeReferences = true, View = viewPlan };

            // Получить все GeometryInstance из geometryElement
            var geometryElement = instance.get_Geometry(geomOptions);
            var geometryInstances = geometryElement
                .Where(x => x is GeometryInstance)
                .Cast<GeometryInstance>()
                .ToList();

            // Собрать все ссылки из InstanceGeometry и SymbolGeometry
            var allSymbolReferences = new List<Reference>();
            var allsymbolCurve = new List<Curve>();

            foreach (var geometryInstance in geometryInstances)
            {
                /*
                // Обработать InstanceGeometry
                var instanceGeometry = geometryInstance.GetInstanceGeometry();
                var instanceCurves = instanceGeometry?
                    .Where(x => x is Curve)
                    .Cast<Curve>()
                    .ToList();

                if (instanceCurves != null)
                {
                    foreach (var curve in instanceCurves)
                    {
                        var reference = curve.Reference;
                        // Используйте reference при необходимости
                    }
                }*/

                // Обработать SymbolGeometry
                // Ваш текущий код захватывает только кривые на верхнем уровне, но пропускает кривые, вложенные в Solid, Mesh или другие элементы.
                var symbolGeometry = geometryInstance.GetSymbolGeometry();
                var symbolCurves = symbolGeometry?
                    .Where(x => x is Curve)
                    .Cast<Curve>()
                    .ToList();

                if (symbolCurves != null)
                {
                    foreach (var symbolCurve in symbolCurves)
                    {
                        allsymbolCurve.Add(symbolCurve);
                        allSymbolReferences.Add(symbolCurve.Reference);

                    }
                }
            }

            return allsymbolCurve;

        }
        public static List<Curve> Get_all_curve_reference(ElementId ventId, View viewPlan)
        {
            // прям все линии
            var allsymbolCurve = new List<Curve>();

            Document doc = RevitAPI.Document;
            var instance = (FamilyInstance)doc.GetElement(ventId);
            if (instance == null)
                return allsymbolCurve;

            Options geomOptions = new Options { ComputeReferences = true, View = viewPlan };

            // Получить все GeometryInstance из geometryElement
            var geometryElement = instance.get_Geometry(geomOptions);
            if (geometryElement == null)
                return allsymbolCurve;


            foreach (GeometryObject geomObj in geometryElement)
            {
                ExtractCurvesFromGeometry(geomObj, allsymbolCurve);
            }

            return allsymbolCurve;

        }
        private static void ExtractCurvesFromGeometry(GeometryObject geomObj, List<Curve> curveList)
        {
            switch (geomObj)
            {
                // Если это кривая (Line, Arc, NurbsCurve и т. д.)
                case Curve curve:
                    curveList.Add(curve);
                    break;

                // Если это Solid (Extrusion, Sweep, Loft и др.)
                case Solid solid:
                    if (solid?.Faces != null)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            foreach (EdgeArray edgeLoop in face.EdgeLoops)
                            {
                                foreach (Edge edge in edgeLoop)
                                {
                                    curveList.Add(edge.AsCurve());
                                }
                            }
                        }
                    }
                    break;

                // Если это Mesh (например, импортированная геометрия)
                case Mesh mesh:
                    if (mesh != null)
                    {
                        for (int i = 0; i < mesh.NumTriangles; i++)
                        {
                            MeshTriangle triangle = mesh.get_Triangle(i);
                            curveList.Add(Line.CreateBound(triangle.get_Vertex(0), triangle.get_Vertex(1)));
                            curveList.Add(Line.CreateBound(triangle.get_Vertex(1), triangle.get_Vertex(2)));
                            curveList.Add(Line.CreateBound(triangle.get_Vertex(2), triangle.get_Vertex(0)));
                        }
                    }
                    break;

                // Если это вложенный GeometryInstance (например, вложенное семейство)
                case GeometryInstance geomInstance:
                    var symbolGeom = geomInstance.GetSymbolGeometry();
                    if (symbolGeom != null)
                    {
                        foreach (GeometryObject nestedGeom in symbolGeom)
                        {
                            ExtractCurvesFromGeometry(nestedGeom, curveList);
                        }
                    }
                    break;

                // Если это группа геометрии (GeometryElement)
                case GeometryElement geomElement:
                    foreach (GeometryObject nestedGeom in geomElement)
                    {
                        ExtractCurvesFromGeometry(nestedGeom, curveList);
                    }
                    break;
            }
        }
        public static bool NearlyEqual(double a, double b, double epsilon = 1e-6)
        {
            return Math.Abs(Math.Abs(a) - Math.Abs(b)) < epsilon;
        }
        public static Curve Get_curve_nearly_curve(Curve axisCurve, List<Curve> curves_ov)
        {
            // ближайшая из линий к другой линии (оси)
            XYZ startPoint = axisCurve.GetEndPoint(0);
            XYZ endPoint = axisCurve.GetEndPoint(1);

            XYZ direction = (endPoint - startPoint).Normalize();

            bool vert_axe = false;
            if (Math.Abs(direction.Y) > Math.Abs(direction.X))
            {
                vert_axe = true; // вертикальнее чем горизонтальнее
            }

            Curve min_curve = null;
            double past_dist = -1;
            double epsilon = 1e-9;
            foreach (var curve in curves_ov)
            {
                XYZ startPoint_curve = curve.GetEndPoint(0);
                XYZ endPoint_curve = curve.GetEndPoint(1);

                XYZ direction_curve = (startPoint_curve - endPoint_curve).Normalize();
                bool vert_axe_curve = false;
                /*
                if (Math.Abs(direction_curve.Y) > Math.Abs(direction_curve.X))
                {
                    vert_axe_curve = true; // вертикальнее чем горизонтальнее
                }
                */

                //if (vert_axe_curve != vert_axe)// моно сделать для параллельных
                if(!NearlyEqual(direction.X,direction_curve.X)|| !NearlyEqual(direction.Y, direction_curve.Y))
                { 
                    continue; // оси не параллельны
                }

                XYZ startPoint_curve_norm = new XYZ(startPoint_curve.X, startPoint_curve.Y,0);
                XYZ projectedPoint = axisCurve.Project(startPoint_curve).XYZPoint;
                XYZ projectedPoint_norm = new XYZ(projectedPoint.X, projectedPoint.Y, 0);
                
                double tek_dist = projectedPoint_norm.DistanceTo(startPoint_curve_norm);
                //double tek_dist = projectedPoint.DistanceTo(startPoint_curve);
                if (min_curve == null || tek_dist<past_dist)
                {
                    min_curve = curve;
                    past_dist= tek_dist;

                }


            }
            return min_curve;

        }
    }
}