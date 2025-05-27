using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Reinforcement.Stage1.DecorViewPlan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Windows;

using System.Windows.Documents;
using static System.Windows.Forms.AxHost;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_4Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(UIDocument uidoc, ref string message, ElementSet elements, Document doc)
        {
            var options = new Options() { ComputeReferences = true };

            OV_Construct_All_Dictionary.Dict_plan_ov_axis.Clear();

            using (TransactionGroup tg = new TransactionGroup(doc, "Создание образмеривания"))
            {
                tg.Start();
                foreach (var levelPlan in OV_Construct_All_Dictionary.Dict_level_plan_floor)
                {
                    string currentLevel = levelPlan.Key;
                    ElementId viewId = levelPlan.Value;

                    var viewPlan = doc.GetElement(viewId) as View;


                    OV_Construct_All_Dictionary.Dict_plan_ov_axis[viewId] = new Dictionary<ElementId, List<ElementId>>();


                    using (Transaction trans = new Transaction(doc, "Create Dimensions"))
                    {
                        trans.Start();

                        foreach (var ventData in OV_Construct_All_Dictionary.Dict_numOV_nearAxes)
                        {
                            int ventNumber = ventData.Key;
                            var ventInfo = OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV[ventNumber];
                            var levelList = ventInfo["spisok_level_ov"] as List<string>;

                            if (!levelList.Contains(currentLevel)) continue; // текущая вентшахта на данном уровне отсутствует

                            int index = levelList.IndexOf(currentLevel);
                            var idList = ventInfo["spisok_id_ov"] as List<string>;
                            ElementId ventId = new ElementId(Convert.ToInt64(idList[index]));
                            Element ventElement = doc.GetElement(ventId);

                            /*
                            if (idList[index] != "11380097")
                            {
                                continue;
                            }
                            */


                            if (!(ventElement.Location is LocationPoint location)) continue;
                            XYZ ventPoint = location.Point;

                            // Vertical Axis
                            ElementId axisId_vert = null;
                            ElementId axisId_hor = null;
                            if (ventData.Value.ContainsKey("Vertical_Axe_ID"))
                            {
                                axisId_vert = new ElementId(Convert.ToInt64(ventData.Value["Vertical_Axe_ID"]));
                            }
                            if (ventData.Value.ContainsKey("Horizontal_Axe_ID"))
                            {
                                axisId_hor = new ElementId(Convert.ToInt64(ventData.Value["Horizontal_Axe_ID"]));
                            }




                            if (axisId_vert != null)
                            {
                                try
                                {
                                    CreateDimensionBetweenElements(uidoc, doc, axisId_vert, ventId, ventElement, viewPlan, false, axisId_hor);
                                }
                                catch (Exception ex)
                                {
                                    message += $"Vertical axis error: {ex.Message}\n";
                                }
                            }

                            if (axisId_hor != null)
                            {
                                try
                                {
                                    CreateDimensionBetweenElements(uidoc, doc, axisId_hor, ventId, ventElement, viewPlan, true, axisId_vert);
                                }
                                catch (Exception ex)
                                {
                                    message += $"Horizontal axis error: {ex.Message}\n";
                                }

                            }
                            OV_Construct_All_Dictionary.Dict_plan_ov_axis[viewId][ventId] = new List<ElementId>()
                            {
                                axisId_vert, axisId_hor
                            };


                        }

                        trans.Commit();
                    }
                }

                tg.Assimilate();
            }
            
            return Result.Succeeded;
        }

        private static void CreateDimensionBetweenElements(UIDocument uidoc, Document doc, ElementId axisId, ElementId ventId, Element ventElement,
            View viewPlan, bool isHorizontalAxis, ElementId axis_other_Id)
        {
            Grid axis = doc.GetElement(axisId) as Grid;
            if (axis == null) return;
            Curve axisCurve = axis.Curve;
            if (axisCurve == null) return;
            LocationPoint ventLocation = ventElement.Location as LocationPoint;
            if (ventLocation == null) return;

            // Project vent point to axis
            XYZ ventPoint = ventLocation.Point;
            XYZ projectedPoint = axisCurve.Project(ventPoint).XYZPoint;

            // Create dimension line
            XYZ nelin_point = new XYZ(projectedPoint.X, projectedPoint.Y, ventPoint.Z);

            Line dimensionLine = Line.CreateBound(nelin_point, ventPoint);// линия размера

            var curves_ov = Helper_all_curve_reference.Get_all_curve_reference(ventId, viewPlan);
            var curve_ov = Helper_all_curve_reference.Get_curve_nearly_curve(axisCurve, curves_ov, ventPoint);

            ReferenceArray references = new ReferenceArray();

            // добавляем оси референс
            Options geomOptions = new Options { ComputeReferences = true, View = viewPlan };

            foreach (GeometryObject geomObj in axis.get_Geometry(geomOptions))
            {
                if (geomObj is Curve curve)
                {
                    references.Append(curve.Reference);
                    break;
                }
            }
            
            var symbolReference = curve_ov.Reference;

            if (symbolReference != null)
            {
                references.Append(symbolReference);
            }

            var result = new List<ElementId>();
            // Create dimension
            Dimension dimension = null;

            int viewScale = viewPlan.Scale;
            dimensionLine = Smes_line(doc, isHorizontalAxis, ventPoint, dimensionLine, curve_ov, axis_other_Id, viewScale);

            if (references.Size >= 2)
            {
                dimension = doc.Create.NewDimension(viewPlan, dimensionLine, references);
                //uidoc.Selection.SetElementIds(result);
            }

           
            //MoveTextInDimension.Move(dimension, viewScale, viewPlan);
            

        }

        public static Line Smes_line(Document doc, bool isHorizontalAxis,XYZ ventPoint,Line dimensionLine, Curve curve_ov, ElementId axis_other_Id, int viewScale)
        {
            double size_otstup_mm = 8; //8 миллиметров на чертеже отступ
            double size_text = 4;
            // смещение размерной линии
            Line smes_line = dimensionLine;

            Grid axis = doc.GetElement(axis_other_Id) as Grid;

            Curve axis_other = axis.Curve;

            XYZ projectedPoint = axis_other.Project(ventPoint).XYZPoint;

            double sdvig = curve_ov.Length/2+ UnitUtils.ConvertToInternalUnits(size_otstup_mm* viewScale, UnitTypeId.Millimeters);
            
            double razr_dist = sdvig+ UnitUtils.ConvertToInternalUnits(size_text * viewScale, UnitTypeId.Millimeters);// если эта дистанция умещается то размер по другому делаем

            

            XYZ curve_dim_p1 = dimensionLine.GetEndPoint(0);
            XYZ curve_dim_p2 = dimensionLine.GetEndPoint(1);
            if (isHorizontalAxis)
            {
                //двигаем влево или вправо
                if (projectedPoint.X < ventPoint.X)
                {
                    // вертикальная ось левее ов, размер делаем правее, надо проверять еще дистанцию...
                    sdvig = sdvig;
                    if(ventPoint.X- projectedPoint.X> razr_dist)
                    { sdvig = -sdvig; }

                }
                else
                {
                    sdvig = -sdvig;
                    if ( projectedPoint.X-ventPoint.X > razr_dist)
                    { sdvig = -sdvig; }
                }

                var p1 = new XYZ(curve_dim_p1.X + sdvig, curve_dim_p1.Y, curve_dim_p1.Z);
                var p2 = new XYZ(curve_dim_p2.X + sdvig, curve_dim_p2.Y, curve_dim_p2.Z);
                smes_line = Line.CreateBound(p1, p2);
            }

            else
            {
                //двигаем вверх или вниз
                if (projectedPoint.Y < ventPoint.Y)
                {
                    // двигаем размер вверх
                    sdvig = sdvig;
                    if (ventPoint.Y - projectedPoint.Y > razr_dist)
                    { sdvig = -sdvig; }

                }
                else
                {
                    sdvig = -sdvig;
                    if (projectedPoint.Y - ventPoint.Y > razr_dist)
                    { sdvig = -sdvig; }
                }

                var p1 = new XYZ(curve_dim_p1.X, curve_dim_p1.Y + sdvig, curve_dim_p1.Z);
                var p2 = new XYZ(curve_dim_p2.X, curve_dim_p2.Y + sdvig, curve_dim_p2.Z);
                smes_line = Line.CreateBound(p1, p2);


            }



            return smes_line;

        }

    }
}