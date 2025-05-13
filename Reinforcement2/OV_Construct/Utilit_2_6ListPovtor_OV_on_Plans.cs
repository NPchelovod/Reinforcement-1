using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Autodesk.Revit.DB;


/*
 * Поиск совпадающих вентшахт на уровнях, которые можно сгруппировать
 */

namespace Reinforcement
{
    internal class Utilit_2_6ListPovtor_OV_on_Plans
    {
        public static Dictionary<string, List<string>> Create_ListPovtor_OV_on_Plan( Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV, Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts) //ref 

        {

            var Dict_sovpad_level = new Dictionary<string, List<string>>(); // номер этажа и типовые этажи в составе него

            // вначале создаем отсортированный список всех уровней используемых шахтами, так как могут различаться по уровням и тд
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

            var all_levels_ov = new List<int>();

            foreach (var iter in Dict_Grup_numOV_spisokOV)
            {
                foreach (var iter2 in ObjToList(iter.Value["spisok_level_ov"]))
                {
                    int tek_level = Convert.ToInt32(iter2);
                    if (all_levels_ov.Contains(tek_level) == false)
                    {
                        all_levels_ov.Add(tek_level);
                    }

                }
  
            }

            all_levels_ov.Sort();
            //теперь ищем чтобы на предыдущем уровне совпадение было с данным


            string paster_level = "";
            
            for (int i =0; i<all_levels_ov.Count();i++) 
            {
                int iterator = i;
                var tek_level = all_levels_ov[iterator].ToString();
                

                var dob_list = new List<string>();
                if (iterator == 0)
                {
                    Dict_sovpad_level[tek_level] = dob_list;
                    paster_level = tek_level; // для следующих она может быть предыдущей
                    continue;
                }

                var past_level = all_levels_ov[iterator - 1].ToString();
                bool level_is_unik = false;

                // осуществляем цикл сравнения с предыдущем уровнем
                
                foreach (var iter in Dict_Grup_numOV_spisokOV)
                {
                    // проверка если уровня предыдущего несуществовало
                    var list_sravn_levels = ObjToList(iter.Value["spisok_level_ov"]);

                    // условие появления или исчезания шахты - значит уровень уникален
                    if (list_sravn_levels.Contains(past_level) == false && list_sravn_levels.Contains(tek_level) == true || list_sravn_levels.Contains(past_level) == true && list_sravn_levels.Contains(tek_level) == false)
                    {
                        level_is_unik = true;
                        break;
                    }

                }

                double pogres_mm = 5;

                if (level_is_unik == false)
                {
                    // тогда более сложную проверку выполняем на сопоставление изменения вентшахт типоразмера
                    foreach (var iter in Dict_Grup_numOV_spisokOV)
                    {
                        var list_sravn_levels = ObjToList(iter.Value["spisok_level_ov"]);

                        if (list_sravn_levels.Contains(tek_level) == false) // значит такого уровня там нет
                        { continue; }

                        int index = -1;
                        foreach (var iter2 in list_sravn_levels)
                        {
                            index += 1;
                            if (iter2 == tek_level)
                            { break; } // нашли текущий индекс для id вентшахты
                        }

                        var past_id_OV = ObjToList(iter.Value["spisok_id_ov"])[index - 1];
                        var tek_id_OV = ObjToList(iter.Value["spisok_id_ov"])[index];

                        double coord_X_past = Convert.ToDouble(Dict_ventId_Properts[past_id_OV]["coord_X"]);
                        double coord_Y_past = Convert.ToDouble(Dict_ventId_Properts[past_id_OV]["coord_Y"]);
                        double tek_width_past = Convert.ToDouble(Dict_ventId_Properts[past_id_OV]["tek_width"]);
                        double tek_height_past = Convert.ToDouble(Dict_ventId_Properts[past_id_OV]["tek_height"]);

                        double coord_X_new = Convert.ToDouble(Dict_ventId_Properts[tek_id_OV]["coord_X"]);
                        double coord_Y_new = Convert.ToDouble(Dict_ventId_Properts[tek_id_OV]["coord_Y"]);
                        double tek_width_new = Convert.ToDouble(Dict_ventId_Properts[tek_id_OV]["tek_width"]);
                        double tek_height_new = Convert.ToDouble(Dict_ventId_Properts[tek_id_OV]["tek_height"]);


                        double razn1 = Math.Abs(coord_X_new - coord_X_past);
                        double razn2 = Math.Abs(coord_Y_new - coord_Y_past);
                        double razn3 = Math.Abs(tek_width_new - tek_width_past);
                        double razn4 = Math.Abs(tek_height_new - tek_height_past);

                        var sravn = new List<double>();
                        sravn.Add(razn1);
                        sravn.Add(razn2);
                        sravn.Add(razn3);
                        sravn.Add(razn4);

                        double max_razbros = sravn.Max();
                        if (max_razbros > pogres_mm)
                        {
                            level_is_unik = true;
                            break;
                        }

                    }

                }

                if (level_is_unik)
                {
                    Dict_sovpad_level[tek_level] = dob_list;

                    paster_level = tek_level; // для следующих она может быть предыдущей
                }
                else
                {
                    if (Dict_sovpad_level.ContainsKey(past_level))
                    {
                        Dict_sovpad_level[past_level].Add(tek_level);
                    }
                    else
                    {
                        // несколько этажей с повторами
                        Dict_sovpad_level[paster_level].Add(tek_level);
                    }
                        
                }



            }

            return Dict_sovpad_level;

        }

    }
}






