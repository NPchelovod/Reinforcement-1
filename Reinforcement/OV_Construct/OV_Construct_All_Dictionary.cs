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
        public static Dictionary<string, ViewPlan> Dict_level_plan_floor { get; set; } = new Dictionary<string, ViewPlan>(); // уровень и созданный план


        // словарь уровень, стринг ВШ1 = план конкретной вентшахты

        public static Dictionary<string, Dictionary<string, ViewPlan>> Dict_level_VH_plans { get; set; } = new Dictionary<string, Dictionary<string, ViewPlan>>(); // уровень и созданный план

    }
}
