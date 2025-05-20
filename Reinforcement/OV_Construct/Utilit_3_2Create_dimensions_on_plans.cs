using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Create_dimensions_on_plans
    {
        public static Result Create_dimensions_on_plans(ref string message, ElementSet elements, Document doc)
        {
            foreach (var j in OV_Construct_All_Dictionary.Dict_level_plan_floor)
            {
                using (Transaction trans = new Transaction(doc, "Create Multiple Dimensions"))
                {
                    trans.Start();

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
                    trans.Commit();
                }
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
            
            Line dimensionLine = Line.CreateBound(projectedPoint, ventPoint);
            if (!isHorizontalAxe)
            {
                // Для вертикальной оси - горизонтальный размер
                dimensionLine = Line.CreateBound(
                    new XYZ(projectedPoint.X, ventPoint.Y, projectedPoint.Z),
                    ventPoint);
            }
            else
            {
                // Для горизонтальной оси - вертикальный размер
                dimensionLine = Line.CreateBound(
                    new XYZ(ventPoint.X, projectedPoint.Y, projectedPoint.Z),
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

    }
}
