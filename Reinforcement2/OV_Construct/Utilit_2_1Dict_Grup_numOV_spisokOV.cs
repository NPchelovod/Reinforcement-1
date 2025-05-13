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
    internal class Utilit_2_1Dict_Grup_numOV_spisokOV
    {
        public static Dictionary<int, Dictionary<string, object>> Create_Dict_Grup_numOV_spisokOV(Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts, Dictionary<string, List<string>> Dict_level_ventsId) //ref 
        {
            double porges_mm = 15; // погрешность несовпада в мм шахт

            // из строки обратно масив листа
            List<string> ObjToList(object array)
            {
                var result = new List<string>();
                if (array is IEnumerable<string> valueSet)
                {
                    result.AddRange(valueSet);
                }
                return result;
            }

            bool Cond( double x1, double y1, double width1, double height1,
                                     double x2, double y2, double width2, double height2)
            {
                return x1 < x2 + width2 + porges_mm &&
                            x1 + width1 + porges_mm > x2 &&
                            y1 < y2 + height2 + porges_mm &&
                            y1 + height1 + porges_mm > y2;
            }


            // сортировка словаря уровней по возрастанию уровня
            var list_level_sort = new List<int>();
            foreach (var iter in Dict_level_ventsId)
            {
                list_level_sort.Add(Convert.ToInt32(iter.Key));
            }
            list_level_sort.Sort();

            // создание словаря уровень - id вентшахты на уровне

            var Dict_Grup_numOV_spisokOV = new Dictionary<int, Dictionary<string, object>>();

            var list_grup_isclud = new List<int>();
            int num_OV = 0;

            foreach (int iter in list_level_sort)
            {

                Console.WriteLine(iter); 
                // идём по уровням
                string tek_level = iter.ToString();
                var list_id_OV = Dict_level_ventsId[tek_level];

                // создаем лист состыковки внтшахт если там не оказалось элемента в конце то эта вентшахта не может прололжаться она была оборвана на какомто этаже
                var list_grup_ov_proslo = new List<int>();

                foreach (var id_OV in list_id_OV)
                {
                    // внутри текущего уровня идём по шахтам

                    var property_OV = Dict_ventId_Properts[id_OV];
                    double coord_X = Convert.ToDouble(property_OV["coord_X"]);
                    double coord_Y = Convert.ToDouble(property_OV["coord_Y"]);
                    double tek_width = Convert.ToDouble(property_OV["tek_width"]);
                    double tek_height = Convert.ToDouble(property_OV["tek_height"]);

                    double coord_min_X = Convert.ToDouble(property_OV["coord_min_X"]);
                    double coord_max_X = Convert.ToDouble(property_OV["coord_max_X"]);
                    double coord_min_Y = Convert.ToDouble(property_OV["coord_min_Y"]);
                    double coord_max_Y = Convert.ToDouble(property_OV["coord_max_Y"]);

                    double radius_angl_gradus = Convert.ToDouble(property_OV["radius_angl_gradus"]);
                    double radius_distance = Convert.ToDouble(property_OV["radius_distance"]);


                    if (num_OV == 0)
                    {
                        num_OV += 1;
                        var spisok_ov = new List<string>();
                        spisok_ov.Add(id_OV);

                        list_grup_ov_proslo.Add(num_OV);

                        var spisok_level_ov = new List<string>();
                        spisok_level_ov.Add(tek_level);

                        var parametrs_OV_grup = new Dictionary<string, object>()
                        {

                            { "num_OV_Group", num_OV},
                            { "tek_coord_X", coord_X },
                            { "tek_coord_Y", coord_Y },
                            { "tek_coord_min_X", coord_min_X },
                            { "tek_coord_max_X", coord_max_X },
                            { "tek_coord_min_Y", coord_min_Y },
                            { "tek_coord_max_Y", coord_max_Y },
                            { "tek_radius_angl_gradus", radius_angl_gradus},
                            { "tek_radius_distance", radius_distance},
                            { "spisok_id_ov", spisok_ov},
                            { "spisok_level_ov", spisok_level_ov}
                        };

                        Dict_Grup_numOV_spisokOV[num_OV] = parametrs_OV_grup;
                    }

                    else
                    {
                        // осуществляем попытку поиска среди Dict_Grup_numOV_spisokOV[num_OV]


                        // сначала ищем самую близкую шруппу шахт из всех
                        double distance = -1;
                        int num_grup_sovpad = 0;
                        bool new_grup = true;
                        foreach (var iter_grup in Dict_Grup_numOV_spisokOV)
                        {
                            //условие толковон но бывает часто просто ов не наносят
                            
                            if (list_grup_isclud.Contains(iter_grup.Key)) // эта группа прерывает на предыдущех этажах
                                { continue; }
                            if (list_grup_ov_proslo.Contains(iter_grup.Key)) // уже на этом этаже использовалась
                                { continue; }

                            var Data_grup = iter_grup.Value;
                            double X = Convert.ToDouble(Data_grup["tek_coord_X"]);

                            double Y = Convert.ToDouble(Data_grup["tek_coord_Y"]);

                            double distance_new = Math.Abs((X - coord_X)) + Math.Abs((Y - coord_Y));

                            if (distance == -1 || distance > distance_new)
                            {
                                distance = distance_new;
                                num_grup_sovpad = iter_grup.Key;
                            }

                        }

                        if (distance == -1)
                        {
                            num_grup_sovpad = 1;
                        }
                        
                        // теперь проверяем насколько она нам подходила
                        var Data_grup1 = Dict_Grup_numOV_spisokOV[num_grup_sovpad];
                        double X1 = Convert.ToDouble(Data_grup1["tek_coord_X"]);
                        double Y1 = Convert.ToDouble(Data_grup1["tek_coord_Y"]);
                        double tek_coord_min_X = Convert.ToDouble(Data_grup1["tek_coord_min_X"]);
                        double tek_coord_max_X = Convert.ToDouble(Data_grup1["tek_coord_max_X"]);
                        double tek_coord_min_Y = Convert.ToDouble(Data_grup1["tek_coord_min_Y"]);
                        double tek_coord_max_Y = Convert.ToDouble(Data_grup1["tek_coord_max_Y"]);


                        // грубое условие на поиск группировки
                        if (new_grup == true && distance < 2 * (tek_height + tek_width))
                        {

                            if (X1 == coord_X & Y1 == coord_Y)
                            {
                                new_grup = false; // совпало значит неччего новое городить мы попали

                            }
                            // условие не далее полугабаритов вентшахты от центра

                            else if (X1 < (coord_max_X + porges_mm) & coord_min_X < (X1 + porges_mm) & Y1 < (coord_max_Y + porges_mm) & (Y1 + porges_mm) > coord_min_Y)
                            {
                                new_grup = false;
                                // далее можно более сложную проверку добавть
                            }
                            else
                            {

                                bool cond = Cond(coord_min_X, coord_min_Y, coord_max_X - coord_min_X, coord_max_Y - coord_min_Y,
                                     tek_coord_min_X, tek_coord_max_Y, tek_coord_max_X - tek_coord_min_X, tek_coord_max_Y - tek_coord_min_Y);

                                if (cond)
                                {
                                    new_grup = false;
                                }
                            }
                        }




                        if (new_grup)
                        {
                            num_OV += 1;
                            list_grup_ov_proslo.Add(num_OV);

                            var spisok_ov = new List<string>();
                            spisok_ov.Add(id_OV);

                            var spisok_level_ov = new List<string>();
                            spisok_level_ov.Add(tek_level);


                            var parametrs_OV_grup = new Dictionary<string, object>()
                            {

                                { "num_OV_Group", num_OV},
                                { "tek_coord_X", coord_X },
                                { "tek_coord_Y", coord_Y },
                                { "tek_coord_min_X", coord_min_X },
                                { "tek_coord_max_X", coord_max_X },
                                { "tek_coord_min_Y", coord_min_Y },
                                { "tek_coord_max_Y", coord_max_Y },
                                { "tek_radius_angl_gradus", radius_angl_gradus},
                                { "tek_radius_distance", radius_distance},
                                { "spisok_id_ov", spisok_ov},
                               
                                { "spisok_level_ov", spisok_level_ov}

                            };

                            Dict_Grup_numOV_spisokOV[num_OV] = parametrs_OV_grup;
                        }

                        else
                        {
                            list_grup_ov_proslo.Add(num_grup_sovpad);
                            // добавляем к существующему, обновляя списки
                            var spisok_ov = ObjToList(Dict_Grup_numOV_spisokOV[num_grup_sovpad]["spisok_id_ov"]);
                            spisok_ov.Add(id_OV);

                            var spisok_level_ov = ObjToList(Dict_Grup_numOV_spisokOV[num_grup_sovpad]["spisok_level_ov"]);

                            spisok_level_ov.Add(tek_level);


                            var parametrs_OV_grup = new Dictionary<string, object>()
                            {

                                { "num_OV_Group", num_grup_sovpad},
                                { "tek_coord_X", coord_X },
                                { "tek_coord_Y", coord_Y },
                                { "tek_coord_min_X", coord_min_X },
                                { "tek_coord_max_X", coord_max_X },
                                { "tek_coord_min_Y", coord_min_Y },
                                { "tek_coord_max_Y", coord_max_Y },
                                { "tek_radius_angl_gradus", radius_angl_gradus},
                                { "tek_radius_distance", radius_distance},
                                { "spisok_id_ov", spisok_ov},
                                { "spisok_level_ov", spisok_level_ov}
                             };

                            Dict_Grup_numOV_spisokOV[num_grup_sovpad] = parametrs_OV_grup;

      
                        }
                    }
                }

                foreach(var iterator in Dict_Grup_numOV_spisokOV)
                {
                    //идея что если на этом уровне шахта не причпокнулась к комуто то шахта предыдущего уровня не может идти далее
                    if (list_grup_ov_proslo.Contains(iterator.Key)==false)
                    {
                        list_grup_isclud.Add(iterator.Key);
                    }
                }

            }
            // удаление единичных шахт ОВ, которые всего один раз в плите - диагноз не ВШ

            double min_povtor_contain = 2; // столько повторов значит вентшахта

            var list_del_OV = new List<int>();

            foreach (var iter in Dict_Grup_numOV_spisokOV)
            {
                if (ObjToList(iter.Value["spisok_id_ov"]).Count < min_povtor_contain)
                {
                    list_del_OV.Add(iter.Key);
                }
            }

            foreach (var iter in list_del_OV)
            {
                Dict_Grup_numOV_spisokOV.Remove(iter);
            }

            return Dict_Grup_numOV_spisokOV;
        }

    }
}






