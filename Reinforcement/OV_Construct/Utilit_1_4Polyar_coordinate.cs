using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/*
 * Представим точку равноудаленную от всех шахт
 * нумерация шахт должна быть по кругу вокруг этой точки
 * мы находим угол от оси x до нашей вентшахты, где оси координат установлены в этой точке равноудаленной
 * 
 * 
 */

namespace Reinforcement
{
    internal class Utilit_1_4Polyar_coordinate
    {
        public static Dictionary<string, Dictionary<string, object>> Polyar_coordinate(Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts) //ref 
        {

            //Поиск средней равноудаленной точки от всех вентшахт
            double Average_coord_X = 0;
            double Average_coord_Y = 0;

            double num_OV = 0;
            foreach (var iter in Dict_ventId_Properts)
            {
                var property_OV = iter.Value;
                double coord_X = Convert.ToDouble(property_OV["coord_X"]);
                double coord_Y = Convert.ToDouble(property_OV["coord_Y"]);
                num_OV += 1;
                Average_coord_X += coord_X;
                Average_coord_Y += coord_Y;


            }
            Average_coord_X /= num_OV;
            Average_coord_Y /= num_OV;

            
            // определение полярных координат каждой вентшахты
            foreach (var iter in Dict_ventId_Properts)
            {
                var id_OV = iter.Key;
                var property_OV = iter.Value;

                double coord_X = Convert.ToDouble(property_OV["coord_X"]);
                double coord_Y = Convert.ToDouble(property_OV["coord_Y"]);

                double radius_distance = Math.Sqrt(Math.Pow(coord_X - Average_coord_X, 2) + Math.Pow(coord_Y - Average_coord_Y, 2));
                double radius_angl_gradus = 0;

                if (coord_X - Average_coord_X != 0)
                {
                    double radius_tang = Math.Abs((coord_Y - Average_coord_Y) / (coord_X - Average_coord_X));
                    double radius_angl_radians = Math.Atan(radius_tang);
                    radius_angl_gradus = radius_angl_radians * (180 / Math.PI);

                    if (Average_coord_X > coord_X && coord_Y > Average_coord_Y)
                    {
                        radius_angl_gradus = 180 - radius_angl_gradus;
                    }

                    else if (Average_coord_X > coord_X && coord_Y < Average_coord_Y)
                    {
                        radius_angl_gradus += 180;
                    }

                    else if (Average_coord_X < coord_X && coord_Y < Average_coord_Y)
                    {
                        radius_angl_gradus = 360 - radius_angl_gradus;
                    }
                }
                else if (coord_Y < Average_coord_Y)
                {
                    radius_angl_gradus = -90;
                }
                else
                {
                    radius_angl_gradus = 90;
                }
                Dict_ventId_Properts[id_OV]["radius_distance"] = radius_distance;
                Dict_ventId_Properts[id_OV]["radius_angl_gradus"] = radius_angl_gradus;


            }
            return Dict_ventId_Properts;
        }



    }
}
