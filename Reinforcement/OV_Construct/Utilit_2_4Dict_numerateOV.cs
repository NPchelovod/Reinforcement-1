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
 * создаёт словарь номер по порядку согласно радиальному расположению - номер группы вентшахты
 * 
 * так как дальше мы переделываем вентшахты, данны словарь затем не нужен!
 * 
 */

namespace Reinforcement
{
    public class Utilit_2_4Dict_numerateOV
    {
        public static Dictionary<int, int> Create_Dict_numerateOV( Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV) //ref 
        {
            var Dict_numerateOV = new Dictionary<int,int >();


            double min_x = -1;
            double min_y = -1;
            double min_radius_angl = -1;
            double min_radius_angl_corner = -1;

            double min_radius_distance = -1;

            int num_kray_OV = 0; // номер группы крайней левой нижней
            // вначале ищем левую нижнюю - min x, min y
            foreach (var iterator in Dict_Grup_numOV_spisokOV)
            {
                int num_grup_OV = iterator.Key;
                var parametrs_OV_grup = iterator.Value;

                // координаты центральной точки вентшахты
                double coord_X = Convert.ToDouble(parametrs_OV_grup["tek_coord_X"]);
                double coord_Y = Convert.ToDouble(parametrs_OV_grup["tek_coord_Y"]);

                double radius_angl_gradus = Convert.ToDouble(parametrs_OV_grup["tek_radius_angl_gradus"]);
                double radius_distance = Convert.ToDouble(parametrs_OV_grup["tek_radius_distance"]);
                //if (min_x == -1 || ((min_x- coord_X) + (min_y- coord_Y))>0)
                if (min_x == -1 || ((min_x- coord_X) + (min_y- coord_Y))>0)
                {
                    num_kray_OV = num_grup_OV;
                    min_x = coord_X;
                    min_y = coord_Y;
                    min_radius_angl = radius_angl_gradus;
                    min_radius_angl_corner = radius_angl_gradus; // это будет условным началом отчета
                    min_radius_distance = radius_distance;
                }

            }

            

            var size_OV_grup = Dict_Grup_numOV_spisokOV.Count;


            var sort_num_angle = new List<int>();
            int num_gr = 0;
            while (true)
            {
                if (size_OV_grup <= sort_num_angle.Count)
                { break; }

                // сортируем по углу поворота
                double min_angle = -1;
                foreach (var iterator in Dict_Grup_numOV_spisokOV)
                {
                    int num_grup_OV = iterator.Key;

                    if (sort_num_angle.Contains(num_grup_OV))
                    { continue; }

                    var parametrs_OV_grup = iterator.Value;

                    double radius_angl_gradus = Convert.ToDouble(parametrs_OV_grup["tek_radius_angl_gradus"]);
                    if(min_angle==-1|| radius_angl_gradus<= min_angle)
                    {
                        min_angle = radius_angl_gradus;
                        num_gr = num_grup_OV;
                    }

                }

                sort_num_angle.Add(num_gr);

            }

            // Лентой сдвигаем относительно оптимального крайнего левого
            int i_tek = -1;
            foreach (var iterator in sort_num_angle)
            {
                i_tek += 1;
                //ищем наш элемент

                if (iterator== num_kray_OV)
                {
                    break;
                }

            }
            // тереь режим по нашей штучке и заполняем массив в ряду
            var sort_num_angle_otn = new List<int>();

            for (int i = i_tek; i < sort_num_angle.Count; i++)
            {
                var index = i;
                sort_num_angle_otn.Add(sort_num_angle[index]);
            }

            for (int i = 0; i < i_tek; i++)
            {
                var index = i;
                sort_num_angle_otn.Add(sort_num_angle[index]);
            }

            int num_new = 0;
            foreach (var iterator in sort_num_angle_otn)
            {
                num_new += 1;
                Dict_numerateOV[num_new] = iterator;
            }

            return Dict_numerateOV;
        }
             

    }
}






