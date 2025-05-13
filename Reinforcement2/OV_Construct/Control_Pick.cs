using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;


/*
 * просто проверочка
 * 
 * 
 */

namespace Reinforcement
{
    internal class Control_Pick
    {
        public static void MControl_Pick(UIDocument uidoc, Document doc, ForgeTypeId units) //ref 
        {
            

            // Получение всех данныех конкретной вентшахты

            Reference myRef = uidoc.Selection.PickObject(ObjectType.Element, "Выберите элемент для вывода его Id");
            Element tek_vent1 = doc.GetElement(myRef);

            ElementId vent_Id1 = tek_vent1.Id;

            Location location1 = tek_vent1.Location;
            LocationPoint tek_locate1 = tek_vent1.Location as LocationPoint; // текущая локация вентканала
            XYZ tek_locate_point1 = tek_locate1.Point; // текущая координата расположения

            var coord_X1 = UnitUtils.ConvertFromInternalUnits(tek_locate_point1.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
            var coord_Y1 = UnitUtils.ConvertFromInternalUnits(tek_locate_point1.Y, units);
            var coord_Z1 = UnitUtils.ConvertFromInternalUnits(tek_locate_point1.Z, units);

            //var tek_locate_point_metres = new List<double>(3) {coord_X, coord_Y, coord_Z};

            var tek_locate_point_metres1 = new XYZ(coord_X1, coord_Y1, coord_Z1);

            double tek_locate_point_rot1 = tek_locate1.Rotation; // угол поворота

            var tek_width1 = tek_vent1.LookupParameter("ADSK_Отверстие_Высота").AsValueString();
            var tek_height1 = tek_vent1.LookupParameter("ADSK_Отверстие_Ширина").AsValueString();
            var tek_level1 = tek_vent1.LookupParameter("Уровень").AsValueString();

            //TaskDialog.Show("Hello world!", vents.Count.ToString());
            TaskDialog.Show("Hello world!", $"вент id {vent_Id1} ширина {tek_width1.ToString()}, высота {tek_height1.ToString()},позиция {tek_locate_point_metres1.ToString()}, поворот {tek_locate_point_rot1.ToString()}");

        }

    }
}
