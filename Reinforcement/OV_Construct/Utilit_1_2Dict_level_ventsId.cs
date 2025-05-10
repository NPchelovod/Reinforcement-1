using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Autodesk.Revit.DB;


/*
 * у вентшахты прямоугольной есть 4 угла вот их координаты можно определить
 * вентшахта может быть повернута на некий угол, поэтому это учитываем
 * угол показывается в ревите относительно оси Y влево, 0 угол это когда шахта в высоту по высоте..
 * эти координаты углов необходимы для сопоставлния вентшахт на уровнях
 */

namespace Reinforcement
{
    internal class Utilit_1_2Dict_level_ventsId
    {
        public static Dictionary<string, List<string>> Create_Dict_level_ventsId(Document doc, List<ElementId> vents, ForgeTypeId units) //ref 
        {

            // создание словаря уровень - id вентшахты на уровне

            var Dict_level_ventsId = new Dictionary<string, List<string>>();

            foreach (ElementId vent_Id in vents)
            {
                Element tek_vent = doc.GetElement(vent_Id);

                // определение всех параметров текущей вентшахты

                Location location = tek_vent.Location;
                LocationPoint tek_locate = tek_vent.Location as LocationPoint; // текущая локация вентканала
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения


                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);


                double tek_width = Convert.ToDouble(tek_vent.LookupParameter("ADSK_Отверстие_Ширина").AsValueString());
                double tek_height = Convert.ToDouble(tek_vent.LookupParameter("ADSK_Отверстие_Высота").AsValueString());
                string tek_level = tek_vent.LookupParameter("Уровень").AsValueString();

                // запись в словарь
                if (Dict_level_ventsId.ContainsKey(coord_Z.ToString()) == false)
                {
                    Dict_level_ventsId[coord_Z.ToString()] = new List<string>();
                }

                Dict_level_ventsId[coord_Z.ToString()].Add(vent_Id.ToString());
            }

            return Dict_level_ventsId;
        }
    }
}






