using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(ref string message, ElementSet elements, Document doc)
        {
            var options = new Options()
            {
                ComputeReferences = true
            };
            using (Transaction trans = new Transaction(doc, "Create Simple Dimension"))
            {
                trans.Start();
                foreach (var j in OV_Construct_All_Dictionary.Dict_level_plan_floor)
                {

                    string otm_tek = j.Key;
                    ViewPlan newViewPlan = j.Value; // план, на который надо разместить размеры

                    foreach (var i in OV_Construct_All_Dictionary.Dict_numOV_nearAxes)
                    {
                        int num_vh = i.Key;
                        var dat_vh = OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV[num_vh];
                        var spisok_level_ov = dat_vh["spisok_level_ov"] as List<string>;

                        if (!spisok_level_ov.Contains(otm_tek))
                            continue;

                        int index = spisok_level_ov.IndexOf(otm_tek);
                        var spisok_id_ov = dat_vh["spisok_id_ov"] as List<string>;
                        var id_ov = Convert.ToInt64(spisok_id_ov[index]);
                        ElementId elementId_ov = new ElementId(id_ov);

                        Element tek_vent = doc.GetElement(elementId_ov);
                        LocationPoint tek_locate = tek_vent.Location as LocationPoint;
                        XYZ tek_locate_point = tek_locate.Point;


                        // Для вертикальной оси
                        if (i.Value.ContainsKey("Vertical_Axe_ID"))
                        {
                            try
                            {
                                var Vertical_Axe_ID = Convert.ToInt64(i.Value["Vertical_Axe_ID"]);
                                ElementId elementId_Vert_Axe = new ElementId(Vertical_Axe_ID);
                                Construct_dimensions(doc, elementId_Vert_Axe, tek_vent, tek_locate_point, newViewPlan, false);
                            }
                            catch (Exception ex)
                            {
                                message += $"Ошибка с вертикальной осью: {ex.Message}\n";
                            }
                        }

                        // Для горизонтальной оси
                        if (i.Value.ContainsKey("Horizontal_Axe_ID"))
                        {
                            try
                            {
                                var Horizontal_Axe_ID = Convert.ToInt64(i.Value["Horizontal_Axe_ID"]);
                                ElementId elementId_Hor_Axe = new ElementId(Horizontal_Axe_ID);

                                Construct_dimensions(doc, elementId_Hor_Axe, tek_vent, tek_locate_point, newViewPlan, true);
                            }
                            catch (Exception ex)
                            {
                                message += $"Ошибка с горизонтальной осью: {ex.Message}\n";
                            }
                        }

                    }
                }
                trans.Commit();
            }
            return Result.Succeeded;
        }

        private static void Construct_dimensions(Document doc, ElementId elementId_Axe, Element ventElement,
         XYZ ventPoint, ViewPlan viewPlan, bool isHorizontalAxe)
        {
            
            Grid grid = doc.GetElement(elementId_Axe) as Grid;
            if (grid == null) return;
            Curve gridCurve = grid.Curve;

            // Get start and end points of the grid line
            XYZ startPoint = gridCurve.GetEndPoint(0);
            XYZ endPoint = gridCurve.GetEndPoint(1);

            // Получаем кривую оси
            Line gridLine = Line.CreateBound(startPoint, endPoint);
            XYZ projectedPoint = gridLine.Project(ventPoint).XYZPoint;
            //projectedPoint.Z = ventPoint.Z;
            Line dimensionLine = Line.CreateBound(projectedPoint, ventPoint);

            try
            {
                CreateSimpleDimension(doc, viewPlan, projectedPoint, ventPoint);
            }
            catch (Exception ex) { }
            //catch (Exception ex) { TaskDialog.Show("ош", ex.Message + ex.StackTrace); }

            if (!isHorizontalAxe)
            {
                // Для вертикальной оси - горизонтальный размер
                dimensionLine = Line.CreateBound(
                    new XYZ(projectedPoint.X, ventPoint.Y, ventPoint.Z),
                    ventPoint);
            }
            else
            {
                // Для горизонтальной оси - вертикальный размер
                dimensionLine = Line.CreateBound(
                    new XYZ(ventPoint.X, projectedPoint.Y, ventPoint.Z),
                    ventPoint);
            }

            ReferenceArray references = new ReferenceArray();

            // Получаем Reference для оси Grid (альтернативные способы)
            Reference gridReference = new Reference(grid);

            references.Append(gridReference);

            // Reference на вентшахту
            Reference ventReference =  new Reference(ventElement);
            references.Append(ventReference);
            // Reference ventReference = GetVentReference(ventElement, !isHorizontalAxe) ?? new Reference(ventElement);
            //references.Append(ventReference);
            // Создаем размер
            if (references.Size == 2)
            {
                Dimension newDimension = doc.Create.NewDimension(viewPlan, dimensionLine, references);
                if (newDimension == null)
                {
                    TaskDialog.Show("Ошибка", "Не удалось создать размер");
                }
            }
                
            
        }
        public static void CreateSimpleDimension(Document doc, ViewPlan view, XYZ point1, XYZ point2)
        {


            /// 1. Создаем временные модели линий для привязки
            SketchPlane sketchPlane = view.SketchPlane ?? SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin));

            // Создаем вертикальные линии в точках
            Line line1 = Line.CreateBound(point1, point1 + XYZ.BasisZ);
            Line line2 = Line.CreateBound(point2, point2 + XYZ.BasisZ);

            ModelCurve modelCurve1 = doc.Create.NewModelCurve(line1, sketchPlane);
            ModelCurve modelCurve2 = doc.Create.NewModelCurve(line2, sketchPlane);

            // 2. Получаем Reference через геометрию
            ReferenceArray refs = new ReferenceArray();

            Options geomOptions = new Options();
            geomOptions.ComputeReferences = true;

            foreach (GeometryObject geomObj in modelCurve1.get_Geometry(geomOptions))
            {
                if (geomObj is Curve curve)
                {
                    refs.Append(curve.Reference);
                    break;
                }
            }

            foreach (GeometryObject geomObj in modelCurve2.get_Geometry(geomOptions))
            {
                if (geomObj is Curve curve)
                {
                    refs.Append(curve.Reference);
                    break;
                }
            }

            // 3. Создаем линию для отображения размера
            XYZ midPoint = (point1 + point2) / 2;
            XYZ direction = (point2 - point1).Normalize();
            XYZ perpendicular = new XYZ(-direction.Y, direction.X, 0);

            Line dimensionLine = Line.CreateBound(
                midPoint - perpendicular * 2,
                midPoint + perpendicular * 2);

            // 4. Создаем размер
            if (refs.Size == 2)
            {
                try
                {
                    Dimension newDimension = doc.Create.NewDimension(view, dimensionLine, refs);

                    // (Опционально) Удаляем временные линии после создания размера
                    doc.Delete(modelCurve1.Id);
                    doc.Delete(modelCurve2.Id);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Ошибка", ex.Message);
                }
            }

        }
    }
}
