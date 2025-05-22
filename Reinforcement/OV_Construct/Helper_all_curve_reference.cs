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
            foreach (var curve in curves_ov)
            {
                XYZ startPoint_curve = curve.GetEndPoint(0);
                XYZ endPoint_curve = curve.GetEndPoint(1);

                XYZ direction_curve = (startPoint_curve - endPoint_curve).Normalize();
                bool vert_axe_curve = false;
                if (Math.Abs(direction_curve.Y) > Math.Abs(direction_curve.X))
                {
                    vert_axe_curve = true; // вертикальнее чем горизонтальнее
                }


                if (vert_axe_curve != vert_axe)// моно сделать для параллельных
                { 
                    continue; // оси не параллельны
                }

                double tek_dist = -1;
                var list_dist = new List<double>() { };
                if (vert_axe)
                {
                    // горизонтальные дистанции
                    list_dist = new List<double>
                    {
                        Math.Abs(startPoint.X - startPoint_curve.X),
                        Math.Abs(startPoint.X - endPoint_curve.X),
                        Math.Abs(endPoint.X - startPoint_curve.X),
                        Math.Abs(endPoint.X -endPoint_curve.X)
                    };
                }
                else
                {
                    list_dist = new List<double>
                    {
                        Math.Abs(startPoint.Y - startPoint_curve.Y),
                        Math.Abs(startPoint.Y - endPoint_curve.Y),
                        Math.Abs(endPoint.Y - startPoint_curve.Y),
                        Math.Abs(endPoint.Y -endPoint_curve.Y)
                    };
                }

                tek_dist = list_dist.Min();
                if (past_dist <0 || tek_dist< past_dist)
                {
                    min_curve = curve;
                }


            }
            return min_curve;

        }
    }
}