using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


/*
 * у вентшахты прямоугольной есть 4 угла вот их координаты можно определить
 * вентшахта может быть повернута на некий угол, поэтому это учитываем
 * угол показывается в ревите относительно оси Y влево, 0 угол это когда шахта в высоту по высоте..
 * эти координаты углов необходимы для сопоставлния вентшахт на уровнях
 */

namespace Reinforcement
{
    internal class Utilit_1_5Rotate_coordinate
    {
        public static Dictionary<string, Dictionary<string, object>> Rotate_coordinate(Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts) //ref 
        {
            List<double> Return_povorot_coord(double X0, double Y0, double Angle_otnos_Y)
            {
                // координата одного из углов вентшахты ОВ
                var XY = new List<double>();
                if (Angle_otnos_Y==0)
                {
                    XY.Add(X0);
                    XY.Add(Y0);

                }
                else
                {
                    double radians = Angle_otnos_Y * Math.PI / 180;
                    double X1 = X0 * Math.Cos(radians) - Y0 * Math.Sin(radians);
                    double Y1 = X0 * Math.Sin(radians) + Y0 * Math.Cos(radians);
                    XY.Add(X0);
                    XY.Add(Y0);
                }
                
                return XY;
            }

      

            foreach (var iter in Dict_ventId_Properts)
            {
                var id_OV = iter.Key;
                var property_OV = iter.Value;
                double coord_X = Convert.ToDouble(property_OV["coord_X"]);
                double coord_Y = Convert.ToDouble(property_OV["coord_Y"]);
                double tek_width = Convert.ToDouble(property_OV["tek_width"]);
                double tek_height = Convert.ToDouble(property_OV["tek_height"]);
                double tek_locate_point_rot_gradus = Convert.ToDouble(property_OV["tek_locate_point_rot_gradus"]);

                // 4 угла вентшахты, нахождение их даже если шахта повернута

                // начинаем с левого нижнего угла
                var Point1 = Return_povorot_coord((coord_X - tek_width / 2), (coord_Y - tek_height / 2), tek_locate_point_rot_gradus);
                var Point2 = Return_povorot_coord((coord_X - tek_width / 2), (coord_Y + tek_height / 2), tek_locate_point_rot_gradus);
                var Point3 = Return_povorot_coord((coord_X + tek_width / 2), (coord_Y + tek_height / 2), tek_locate_point_rot_gradus);
                var Point4 = Return_povorot_coord((coord_X + tek_width / 2), (coord_Y - tek_height / 2), tek_locate_point_rot_gradus);

                var X_Point = new List<double>(4);
                X_Point.Add(Point1[0]);
                X_Point.Add(Point2[0]);
                X_Point.Add(Point3[0]);
                X_Point.Add(Point4[0]);

                var Y_Point = new List<double>(4);

                Y_Point.Add(Point1[1]);
                Y_Point.Add(Point2[1]);
                Y_Point.Add(Point3[1]);
                Y_Point.Add(Point4[1]);

                double coord_min_X = X_Point.Min();
                double coord_max_X = X_Point.Max();
                double coord_min_Y = Y_Point.Min();
                double coord_max_Y = Y_Point.Max();

                Dict_ventId_Properts[id_OV]["coord_min_X"] = coord_min_X;// левая координата отверстия
                Dict_ventId_Properts[id_OV]["coord_max_X"] = coord_max_X;// правая координата отверстия
                Dict_ventId_Properts[id_OV]["coord_min_Y"] = coord_min_Y;// нижняя координата отверстия
                Dict_ventId_Properts[id_OV]["coord_max_Y"] = coord_max_Y;// верхняя координата отверстия
                
                Dict_ventId_Properts[id_OV]["Point1"] = Point1;
                Dict_ventId_Properts[id_OV]["Point2"] = Point2;
                Dict_ventId_Properts[id_OV]["Point3"] = Point3;
                Dict_ventId_Properts[id_OV]["Point4"] = Point4;

            }

        
            return Dict_ventId_Properts;
        }




    }
}
