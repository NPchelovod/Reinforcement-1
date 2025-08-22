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
    public class Utilit_1_3Dict_ventId_Properts
    {
        public static Dictionary<string, Dictionary<string, object>> Create_Utilit_Dict_ventId_Properts(Document doc, List<ElementId> vents, ForgeTypeId units) //ref 
        {

            // создание словаря уровень - id вентшахты на уровне

            var Dict_ventId_Properts = new Dictionary<string, Dictionary<string, object>>();


            foreach (ElementId vent_Id in vents)
            {
                Element tek_vent = doc.GetElement(vent_Id);

                // определение всех параметров текущей вентшахты

                Location location = tek_vent.Location;
                LocationPoint tek_locate = tek_vent.Location as LocationPoint; // текущая локация вентканала
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

                double coord_X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                //var tek_locate_point_metres = new List<double>(3) {coord_X, coord_Y, coord_Z};

                XYZ tek_locate_point_metres = new XYZ(coord_X, coord_Y, coord_Z);

                double tek_locate_point_rot = tek_locate.Rotation; // угол поворота
                //приводим угол наклона к углу в пределаъ 180 грудусов
                double tek_locate_point_rot_gradus = tek_locate_point_rot * (360 / (2 * Math.PI));
                while (true)
                {
                    if (tek_locate_point_rot_gradus > 180)
                    {
                        tek_locate_point_rot_gradus -= 90;
                    }
                    else break;
                }

                double tek_width = Convert.ToDouble(tek_vent.LookupParameter("Ширина").AsValueString());
                double tek_height = Convert.ToDouble(tek_vent.LookupParameter("Длина").AsValueString());
                string tek_level = tek_vent.LookupParameter("Уровень").AsValueString();

                


                // запись в словарь
                /*
                List<string> parametrs_vent = new List<string>() { vent_Id.ToString(), tek_level, coord_X.ToString(), coord_Y.ToString(), coord_Z.ToString(), tek_width, tek_height, tek_locate_point_rot.ToString() };
                */
                var parametrs_vent = new Dictionary<string, object>()
                {
                    { "vent_Id", vent_Id.ToString()},
                    {"tek_level",tek_level},
                    {"coord_X",coord_X},
                    {"coord_Y",coord_Y},
                    {"coord_Z",coord_Z},
                    {"tek_width",tek_width },
                    {"tek_height",tek_height},
                    {"tek_locate_point_rot",tek_locate_point_rot},
                    {"tek_locate_point_rot_gradus",tek_locate_point_rot_gradus},
                    {"coord_min_X", 0}, // левая координата отверстия
                    {"coord_max_X",0},// правая координата отверстия
                    {"coord_min_Y",0},// нижняя координата отверстия
                    {"coord_max_Y",0}, // верхняя координата отверстия
                    {"radius_distance", 0}, // дистанция от условного центра всех вентшахт, пока 0, нужен для сортировки по змейки расположения
                    {"radius_angl_gradus", 0}, // угол относительно условного центра всех вентшахт, пока 0
                    {"nearly_axis_X", ""}, // id оси близкой к X
                    {"nearly_axis_Y", ""},
                };


                Dict_ventId_Properts[vent_Id.ToString()] = parametrs_vent;


            }


            // поиск центра здания относительно вентшахт и расстояние до него
            Dict_ventId_Properts = Utilit_1_4Polyar_coordinate.Polyar_coordinate(Dict_ventId_Properts);
            // поворот координат и определение крайних точек шахты
            Dict_ventId_Properts = Utilit_1_5Rotate_coordinate.Rotate_coordinate(Dict_ventId_Properts);


            return Dict_ventId_Properts;
        }
    }
}






