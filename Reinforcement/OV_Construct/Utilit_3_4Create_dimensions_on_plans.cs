using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Documents;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_4Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(UIDocument uidoc, ref string message, ElementSet elements, Document doc)
        {
            var options = new Options() { ComputeReferences = true };

            foreach (var levelPlan in OV_Construct_All_Dictionary.Dict_level_plan_floor)
            {
                string currentLevel = levelPlan.Key;
                ElementId viewId = levelPlan.Value;

                var viewPlan = doc.GetElement(viewId) as View;


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

                        if (!(ventElement.Location is LocationPoint location)) continue;
                        XYZ ventPoint = location.Point;

                        // Vertical Axis
                        if (ventData.Value.ContainsKey("Vertical_Axe_ID"))
                        {
                            try
                            {
                                ElementId axisId = new ElementId(Convert.ToInt64(ventData.Value["Vertical_Axe_ID"]));
                                CreateDimensionBetweenElements(uidoc, doc, axisId, ventId, ventElement, viewPlan, false);
                            }
                            catch (Exception ex)
                            {
                                message += $"Vertical axis error: {ex.Message}\n";
                            }
                        }

                        // Horizontal Axis
                        if (ventData.Value.ContainsKey("Horizontal_Axe_ID"))
                        {
                            try
                            {
                                ElementId axisId = new ElementId(Convert.ToInt64(ventData.Value["Horizontal_Axe_ID"]));
                                CreateDimensionBetweenElements(uidoc, doc, axisId, ventId, ventElement, viewPlan, true);
                            }
                            catch (Exception ex)
                            {
                                message += $"Horizontal axis error: {ex.Message}\n";
                            }
                        }
                    }

                    trans.Commit();
                }
            }

            return Result.Succeeded;
        }

        private static void CreateDimensionBetweenElements(UIDocument uidoc, Document doc, ElementId axisId, ElementId ventId, Element ventElement,
            View viewPlan, bool isHorizontalAxis)
        {
            Grid axis = doc.GetElement(axisId) as Grid;
            if (axis == null) return;

            LocationPoint ventLocation = ventElement.Location as LocationPoint;
            if (ventLocation == null) return;

            // Get axis curve
            //Curve axisCurve = (axis.Location as LocationCurve)?.Curve;
            var Element_axe = doc.GetElement(axisId);

            Grid grid = Element_axe as Grid;
            Curve axisCurve = grid.Curve;

            if (axisCurve == null) return;

            // Project vent point to axis
            XYZ ventPoint = ventLocation.Point;
            XYZ projectedPoint = axisCurve.Project(ventPoint).XYZPoint;

            // Create dimension line
            XYZ nelin_point = new XYZ(projectedPoint.X, projectedPoint.Y, ventPoint.Z);// координата если ось под углом

            Line dimensionLine = Line.CreateBound(nelin_point, ventPoint);// линия для нелинейного размера, просто так

            var curves_ov = Helper_all_curve_reference.Get_curve_reference(ventId, viewPlan);
            var curve_ov = Helper_all_curve_reference.Get_curve_nearly_curve(axisCurve, curves_ov);

            ReferenceArray references = new ReferenceArray();

            // Reference to axis
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
            if (references.Size >= 2)
            {
                result.Add(doc.Create.NewDimension(viewPlan, dimensionLine, references).Id);
                //uidoc.Selection.SetElementIds(result);
            }
        }

    }
}