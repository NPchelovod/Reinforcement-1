using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
/*
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(UIDocument uidoc, ref string message, ElementSet elements, Document doc)
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



        private static void ProcessAxis(UIDocument uidoc, Document doc, object axisIdObj, ElementId ventId,
    Element ventElement, ViewPlan viewPlan, bool isHorizontal, ref string message)
        {
            try
            {
                ElementId axisId = new ElementId(Convert.ToInt64(axisIdObj));
                CreateDimensionBetweenElements(uidoc, doc, axisId, ventId, ventElement, viewPlan, isHorizontal);
            }
            catch (Exception ex)
            {
                message += $"{(isHorizontal ? "Horizontal" : "Vertical")} axis error: {ex.Message}\n";
            }
        }

        private static void CreateDimensionBetweenElements(UIDocument uidoc, Document doc, ElementId axisId,
            ElementId ventId, Element ventElement, ViewPlan viewPlan, bool isHorizontalAxis)
        {
            Grid axis = doc.GetElement(axisId) as Grid;
            if (axis == null) return;

            Curve axisCurve = axis.Curve as Curve;
            if (axisCurve == null) return;

            LocationPoint ventLocation = ventElement.Location as LocationPoint;
            if (ventLocation?.Point == null) return;

            XYZ ventPoint = ventLocation.Point;
            XYZ projectedPoint = axisCurve.Project(ventPoint).XYZPoint;
            Line dimensionLine = CreateDimensionLine(ventPoint, projectedPoint, isHorizontalAxis);

            ReferenceArray references = new ReferenceArray();
            AddAxisReference(axis, viewPlan, references);
            AddVentReferences(ventElement, viewPlan, references, isHorizontalAxis);

            if (references.Size >= 2)
            {
                Dimension dim = doc.Create.NewDimension(viewPlan, dimensionLine, references);
                uidoc.Selection.SetElementIds(new List<ElementId> { dim.Id });
            }
        }

        private static Line CreateDimensionLine(XYZ ventPoint, XYZ projectedPoint, bool isHorizontalAxis)
        {
            return isHorizontalAxis
                ? Line.CreateBound(new XYZ(ventPoint.X, projectedPoint.Y, ventPoint.Z), ventPoint)
                : Line.CreateBound(new XYZ(projectedPoint.X, ventPoint.Y, ventPoint.Z), ventPoint);
        }

        private static void AddAxisReference(Grid axis, View view, ReferenceArray references)
        {
            Options options = new Options { ComputeReferences = true, View = view };
            foreach (GeometryObject geom in axis.get_Geometry(options))
            {
                Curve curve = geom as Curve;
                if (curve != null && curve.Reference != null)
                {
                    references.Append(curve.Reference);
                    return;
                }
            }
        }

        private static void AddVentReferences(Element ventElement, View view, ReferenceArray references, bool isHorizontalAxis)
        {
            Options options = new Options
            {
                ComputeReferences = true,
                View = view,
                IncludeNonVisibleObjects = true
            };

            GeometryElement symbolGeometry = ventElement.get_Geometry(options);
            if (symbolGeometry == null) return;

            foreach (GeometryObject geomObj in symbolGeometry)
            {
                Reference faceRef;
                if (TryGetFaceReference(geomObj, isHorizontalAxis, out faceRef))
                {
                    references.Append(faceRef);
                    return;
                }

                Reference curveRef;
                if (TryGetCurveReference(geomObj, isHorizontalAxis, out curveRef))
                {
                    references.Append(curveRef);
                    return;
                }
            }

            references.Append(new Reference(ventElement));
        }

        private static bool TryGetFaceReference(GeometryObject geomObj, bool isHorizontal, out Reference reference)
        {
            reference = null;
            Solid solid = geomObj as Solid;
            if (solid == null || solid.Faces.IsEmpty) return false;

            foreach (Face face in solid.Faces)
            {
                PlanarFace planarFace = face as PlanarFace;
                if (planarFace != null)
                {
                    XYZ normal = planarFace.FaceNormal.Normalize();
                    bool isVerticalFace = Math.Abs(normal.Z) < 0.001;

                    if (isHorizontal && isVerticalFace && Math.Abs(normal.Y) > 0.9 ||
                        !isHorizontal && isVerticalFace && Math.Abs(normal.X) > 0.9)
                    {
                        reference = planarFace.Reference;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetCurveReference(GeometryObject geomObj, bool isHorizontal, out Reference reference)
        {
            reference = null;
            Curve curve = geomObj as Curve;
            if (curve == null) return false;

            XYZ direction = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            bool isVerticalCurve = Math.Abs(direction.Z) < 0.001;
            bool isOrthogonal = isHorizontal
                ? Math.Abs(direction.X) > 0.9
                : Math.Abs(direction.Y) > 0.9;

            if (isVerticalCurve && isOrthogonal)
            {
                reference = curve.Reference;
                return true;
            }
            return false;
        }
    }
}

/* Create references
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

var instance = (FamilyInstance)doc.GetElement(ventId);
var geom = instance.get_Geometry(geomOptions);
references.Insert(instance.GetReferences(FamilyInstanceReferenceType.Left)[0], references.Size);
references.Insert(instance.GetReferences(FamilyInstanceReferenceType.Right)[0], references.Size);

// Reference to vent element
//references.Append(GetVentReference(ventElement, viewPlan));
var result = new List<ElementId>();
// Create dimension




if (references.Size >= 2)
{
    result.Add(doc.Create.NewDimension(viewPlan, dimensionLine, references).Id);
    uidoc.Selection.SetElementIds(result);
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
*/