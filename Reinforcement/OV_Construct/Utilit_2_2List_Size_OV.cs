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
 * создаёт лист с типоразмерами вентшахт
 */

namespace Reinforcement
{
    internal class Utilit_2_2List_Size_OV
    {
        public static List<List<double>> Create_List_Size_OV(Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts, Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV) //ref 
        {
            List<string> ObjToList(object array)
            {
                var result = new List<string>();
                if (array is IEnumerable<string> valueSet)
                {
                    result.AddRange(valueSet);
                }
                return result;
            }

            // Словарь ширина - высота ОВ шахты

            var Dict_Size_OV = new Dictionary<double, List<double>>();
            var list_all_width = new List<double>();

            foreach (var iter in Dict_Grup_numOV_spisokOV)
            {

                // список вентшахт текущей группы
                var tek_spisok_ov = ObjToList(iter.Value["spisok_id_ov"]);

              
                foreach (string iter_id in tek_spisok_ov)
                {
                    double tek_width = Convert.ToDouble(Dict_ventId_Properts[iter_id]["tek_width"]);
                    double tek_height = Convert.ToDouble(Dict_ventId_Properts[iter_id]["tek_height"]);

                    if (Dict_Size_OV.ContainsKey(tek_width) == false)
                    {
                        // создаём новый ключ и добавляем значение
                        Dict_Size_OV[tek_width] = new List<double>();
                        Dict_Size_OV[tek_width].Add(tek_height);

                        list_all_width.Add(tek_width);
                    }
                    else if (Dict_Size_OV[tek_width].Contains(tek_height) == false)
                    {
                        // добавляем значение к существубщему ключу
                        Dict_Size_OV[tek_width].Add(tek_height);

                    }
                }

            }

            // теперь мы должны от меньшего к большему
            var List_Size_OV = new List<List<double>>();


            var iscl_dict = new List<double>();

            var size_Dict = Dict_Size_OV.Count;

            var sort_list_all_width = from i in list_all_width orderby i select i;

            foreach (double wid in sort_list_all_width)
            {


                // и проходим по текущей ширине также находя минимум
                var sort_size_height = from i in Dict_Size_OV[wid] orderby i select i;

                foreach (double hei in sort_size_height)
                {
                    var dob_list = new List<double>();
                    dob_list.Add(wid);
                    dob_list.Add(hei);

                    List_Size_OV.Add(dob_list);
                }

            }
            return List_Size_OV;

        }

    }
}






