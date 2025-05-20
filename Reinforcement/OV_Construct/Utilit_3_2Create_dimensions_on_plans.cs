using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(ref string message, ElementSet elements, Document doc)
        {
            var options = new Options() { ComputeReferences = true };

            foreach (var levelPlan in OV_Construct_All_Dictionary.Dict_level_plan_floor)
            {
                string currentLevel = levelPlan.Key;
                ViewPlan viewPlan = levelPlan.Value;

                using (Transaction trans = new Transaction(doc, "Create Dimensions"))
                {
                    trans.Start();

                    foreach (var ventData in OV_Construct_All_Dictionary.Dict_numOV_nearAxes)
                    {
                        int ventNumber = ventData.Key;
                        var ventInfo = OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV[ventNumber];
                        var levelList = ventInfo["spisok_level_ov"] as List<string>;

                        if (!levelList.Contains(currentLevel)) continue;

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
                                CreateDimensionBetweenElements(doc, axisId, ventElement, viewPlan, false);
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
                                CreateDimensionBetweenElements(doc, axisId, ventElement, viewPlan, true);
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

        private static void CreateDimensionBetweenElements(Document doc, ElementId axisId, Element ventElement,
            ViewPlan viewPlan, bool isHorizontalAxis)
        {
            Grid axis = doc.GetElement(axisId) as Grid;
            if (axis == null) return;

            LocationPoint ventLocation = ventElement.Location as LocationPoint;
            if (ventLocation == null) return;

            // Get axis curve
            //Curve axisCurve = (axis.Location as LocationCurve)?.Curve;

            Grid grid = doc.GetElement(axisId) as Grid;
            Curve axisCurve = grid.Curve;

            if (axisCurve == null) return;

            // Project vent point to axis
            XYZ ventPoint = ventLocation.Point;
            XYZ projectedPoint = axisCurve.Project(ventPoint).XYZPoint;

            // Create dimension line
            Line dimensionLine;
            if (isHorizontalAxis)
            {
                // Vertical dimension for horizontal axis
                dimensionLine = Line.CreateBound(
                    new XYZ(ventPoint.X, projectedPoint.Y, ventPoint.Z),
                    ventPoint);
            }
            else
            {
                // Horizontal dimension for vertical axis
                dimensionLine = Line.CreateBound(
                    new XYZ(projectedPoint.X, ventPoint.Y, ventPoint.Z),
                    ventPoint);
            }

            // Create references
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

            // Reference to vent element
            references.Append(GetVentReference(ventElement, viewPlan));

            // Create dimension
            if (references.Size == 2)
            {
                doc.Create.NewDimension(viewPlan, dimensionLine, references);
            }
        }

        private static Reference GetVentReference(Element ventElement, View view)
        {
            // Try to get reference from geometry
            Options options = new Options
            {
                ComputeReferences = true,
                View = view
            };

            GeometryElement geometry = ventElement.get_Geometry(options);
            if (geometry != null)
            {
                foreach (GeometryObject geomObj in geometry)
                {
                    if (geomObj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            try
                            {
                                return face.Reference;
                            }
                            catch { continue; }
                        }
                    }
                    else if (geomObj is Curve curve && curve.Reference != null)
                    {
                        return curve.Reference;
                    }
                }
            }

            // Fallback to element reference
            return new Reference(ventElement);
        }
    }
}