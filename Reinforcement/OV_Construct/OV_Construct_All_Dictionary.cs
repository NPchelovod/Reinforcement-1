using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public static class OV_Construct_All_Dictionary
    {
        public static string Prefix_plan_floor { get; set; } = "ОВ_ВШ_(";
        public static Dictionary<string, Dictionary<string, object>> Dict_Axis { get; set; } = new Dictionary<string, Dictionary<string, object>>();
        // создание словаря уровень - id вентшахты на уровне
        // var Dict_level_ventsId = new Dictionary<string, List<string>>();
        public static Dictionary<string, List<string>> Dict_level_ventsId { get; set; } = new Dictionary<string, List<string>>();

        // создание словаря уровень - id вентшахты на уровне
        public static Dictionary<string, Dictionary<string, object>> Dict_ventId_Properts { get; set; } = new Dictionary<string, Dictionary<string, object>>();
        // Группировка вентшахт как они стоят друг над другом
        public static Dictionary<int, Dictionary<string, object>> Dict_Grup_numOV_spisokOV { get; set; } = new Dictionary<int, Dictionary<string, object>>();

        // создаёт лист с типоразмерами вентшахт
        public static List<List<double>> List_Size_OV { get; set; } = new List<List<double>>();

        //создаёт словарь - номер группы вентшахт, лист( ближайшие оси А и 1)

        public static Dictionary<int, Dictionary<string, string>> Dict_numOV_nearAxes { get; set; } = new Dictionary<int, Dictionary<string, string>>();

        // создаёт словарь номер по порядку согласно радиальному расположению - номер группы вентшахты

        public static Dictionary<int, int> Dict_numerateOV { get; set; } = new Dictionary<int, int>();

        // Повторны этажей
        public static Dictionary<string, List<string>> Dict_sovpad_level { get; set; } = new Dictionary<string, List<string>>(); // номер этажа и типовые этажи в составе него


        // словарь уровень - {имя созданного плана этажа =, id плана этажа =}
        public static Dictionary<string, ElementId> Dict_level_plan_floor { get; set; } = new Dictionary<string, ElementId>(); // уровень и созданный план


        // id шахты ов - номер 

        

        // id созданного уровн: id вентшахты на уровне^ id осей смежных
        public static Dictionary<ElementId, Dictionary<ElementId, List<ElementId>>> Dict_plan_ov_axis { get; set; } = new Dictionary<ElementId, Dictionary<ElementId, List<ElementId>>>();


        // словарь уровень, стринг ВШ1 = план конкретной вентшахты

        public static Dictionary<string, Dictionary<string, ElementId>> Dict_level_VH_plans { get; set; } = new Dictionary<string, Dictionary<string, ElementId>>(); // уровень и созданный план



        public static void ClearAll()
        {
            Dict_Axis.Clear();
            Dict_level_ventsId.Clear();
            Dict_ventId_Properts.Clear();
            Dict_Grup_numOV_spisokOV.Clear();
            List_Size_OV.Clear();
            Dict_numOV_nearAxes.Clear();
            Dict_numerateOV.Clear();
            Dict_sovpad_level.Clear();
            Dict_level_plan_floor.Clear();
            Dict_level_VH_plans.Clear();
            Dict_plan_ov_axis.Clear();
        }

    }


}
