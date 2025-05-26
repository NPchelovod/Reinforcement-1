using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using ExcelDataReader.Log;
using Autodesk.Revit.Attributes;





// создание выносок типоразмеров шахт по планам по координатам
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_5Vinosky_ov_type_size
    {
        
        public static Result Create_vinosky_on_plans()
        {
            var options = new Options() { ComputeReferences = true };
            Document doc = RevitAPI.Document;
            foreach (var levelPlan in OV_Construct_All_Dictionary.Dict_plan_ov_axis)
            {
                
                ElementId viewId = levelPlan.Key;

                var viewPlan = doc.GetElement(viewId) as View;
                Options geomOptions = new Options { ComputeReferences = true, View = viewPlan };

                using (Transaction trans = new Transaction(doc, "Create vinosky"))
                {
                    trans.Start();

                    foreach (var ventData in OV_Construct_All_Dictionary.Dict_plan_ov_axis[viewId])
                    {
                        // идем по вентшахтам на этаже
                        
                        
                        ElementId ventId = ventData.Key;
                        Element ventElement = doc.GetElement(ventId);

                        // получаем длину и ширину шахты

                        var Data = OV_Construct_All_Dictionary.Dict_ventId_Properts[ventId.ToString()];
                        double tek_width = Convert.ToDouble(Data["tek_width"]);
                        double tek_height = Convert.ToDouble(Data["tek_height"]);

                        int num_poz = 0;
                        bool naiden = false;
                        // ищем какой это типоразмер шахты
                        for (int i = 0; i < OV_Construct_All_Dictionary.List_Size_OV.Count; i++)
                        {
                            num_poz = i + 1;
                            var list_name = new List<double>(2)
                            {
                                tek_width,tek_height
                            };
                            if (AreEqual(list_name[0],OV_Construct_All_Dictionary.List_Size_OV[num_poz - 1][0]) && AreEqual(list_name[1],OV_Construct_All_Dictionary.List_Size_OV[num_poz - 1][1]))
                            {
                                naiden = true;
                                break; // нашли номер размера нашей шахты
                            }

                        }
                        if (!naiden)
                        {
                            // катастрофа не может такого быть
                        }

                        LocationPoint ventLocation = ventElement.Location as LocationPoint;

                        // Установка маркировки
                        ventElement.LookupParameter("ADSK_Позиция").Set(num_poz.ToString());
                        
                        //запись марки в свойства шахты
                        Parameter markParam = ventElement.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                        /*
                        if (markParam != null && !markParam.IsReadOnly)
                        {
                            markParam.Set(num_poz.ToString());
                        }
                        */
                        // создание
                        SetElementMark(ventElement, ventElement.Id.ToString());
                        CreateTagForElement(doc, viewPlan, ventElement);
                        // Project vent point to axis
                        XYZ ventPoint = ventLocation.Point;
                        // Создание позиции метки с небольшим смещением
                        XYZ tagPos = new XYZ(
                            ventPoint.X + 2,
                            ventPoint.Y + 2,
                            ventPoint.Z
                        );


                    }
                    trans.Commit();
                }
            }

            return Result.Succeeded;


           
        }
        public static bool AreEqual(double a, double b, double epsilon = 1)
        {
            return Math.Abs(a - b) < epsilon;
        }

        private static void SetElementMark(Element element, string markValue)
        {
            Parameter markParam = element.LookupParameter("ADSK_Позиция"); // Или "Mark" для англоязычных версий
            if (markParam != null && !markParam.IsReadOnly)
            {
                //markParam.Set(markValue);
            }
        }

        private static void CreateTagForElement(Document doc, View view, Element element)
        {
            int viewScale = view.Scale;

            double size_otstup_mm = 5;
            double otxod = UnitUtils.ConvertToInternalUnits(size_otstup_mm * viewScale, UnitTypeId.Millimeters);
            // Проверка возможности создания метки на данном виде
            if (view is ViewPlan && element.Location is LocationPoint locPoint)
            {
                // Создание метки с небольшим смещением
                XYZ tagPosition = new XYZ(
                    locPoint.Point.X + otxod,
                    locPoint.Point.Y + otxod,
                    locPoint.Point.Z
                );

                // Создание независимой метки
                IndependentTag tag = IndependentTag.Create(
                    doc,
                    view.Id,
                    new Reference(element),
                    false,
                    TagMode.TM_ADDBY_CATEGORY,
                    TagOrientation.Horizontal,
                    tagPosition
                );

                // Настройка внешнего вида метки (опционально)
                if (tag != null)
                {
                    tag.LeaderEndCondition = LeaderEndCondition.Free;
                }
            }
        }
    }
}

