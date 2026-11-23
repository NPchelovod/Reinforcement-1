using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    public enum EValuesExportData
    {
        //все 
        None = 0,

        //Параметры стены самой
        Wall_L,
        Wall_W,
        Wall_H,
        Wall_AreaNetM2,
        Wall_AreaGrossM2,
        Wall_OpeningsAreaM2,
        Wall_PlanAreaM2,

        //Параметры комнаты
        Wall_Room_Count,

        //Параметры дверей в стене
        Wall_Doors_Count,
        Wall_Doors_L_Average,//средняя длина двери среди всех дверей стены
        Wall_Doors_H_Average,//средняя высота двери среди всех дверей стены

        //Параметры окон в стене
        Wall_Window_Count,
        Wall_Window_L_Average,//средняя длина двери среди всех дверей стены
        Wall_Window_H_Average,//средняя высота двери среди всех дверей стены

        //Теплоизоляционная ли это стена
        Wall_Insulation_OutSideWallName //по имени
    }

    public static class EValuesGetter
    {
        public static readonly Dictionary<EValuesExportData, string> DictNamesParam = new Dictionary<EValuesExportData, string>()
        {
            { EValuesExportData.Wall_L,"Длина стены, м" }
        
        };

        public static string GetName(EValuesExportData eValuesExportData)
        {
            if(eValuesExportData != EValuesExportData.None && DictNamesParam.TryGetValue(eValuesExportData, out var name))
            {
                return name;
            }
            return null;
        }

    }

}
