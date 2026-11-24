using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    /// <summary>
    /// Типы стен
    /// </summary>
    public enum EWallTypes
    {
        None = 0,
        KR_Reinforcement,
        AR_Brick,
        AR_Aerocrete,
        AR_Uteplitel,
        AR_Otdelca,
        AR_VentFasad,
        AR_PGPorGKL
    }
    public enum EValuesExportData
    {
        //все 
        None = 0,

        Wall_Id,
        Wall_Name,
        Wall_EWallTypes,
        //Параметры стены самой
        Wall_Length_m,
        Wall_Width_m,
        Wall_Height_m,
        Wall_AreaNetM2,
        Wall_AreaGrossM2,
        Wall_OpeningsAreaM2,
        Wall_PlanAreaM2,
        Wall_VolumeM3,

        //Примыкающие стены
        Wall_OtherWallsIds,
        Wall_OtherWallsCount,
        Wall_OtherWallsKRIds,
        Wall_OtherWallsKRCount,
        //Параметры комнаты
        Wall_Room_Count,
        Wall_Room_Ids,
        Wall_Room_Numbers,

        //Двери и окна вместе
        Wall_DoorsAndWindows_Count,
        //Параметры дверей в стене
        Wall_Doors_Count,
        Wall_Doors_Ids,
        Wall_Doors_L_Average_m,//средняя длина двери среди всех дверей стены
        Wall_Doors_H_Average_m,//средняя высота двери среди всех дверей стены

        //Параметры окон в стене
        Wall_Window_Count,
        Wall_Window_Ids,
        Wall_Window_L_Average_m,//средняя длина двери среди всех дверей стены
        Wall_Window_H_Average_m,//средняя высота двери среди всех дверей стены

        //Тип стены
        WallType_Id,
        WallType_LayersCount,


        //Теплоизоляционная ли это стена
        Wall_Insulation_OutSideWallName, //по имени bool =true если стена наружная
        //Wall_LayersCount,//колво слоев у стены
        //Уровень
        //Wall_Level_Name,
        //Wall_Level_Elevation,


        //Дверь
        Door_Height_m,
        Door_Length_m,
        Door_Area_m2,
        Door_Level_Elevation,
        //Окно
        Window_Height_m,
        Window_Length_m,
        Window_Area_m2,
        Window_Level_Elevation,

        //Слойдвери
        WallLayer_Width_m,

        //комната
        Room_Id,
        Room_Name,
        Room_Number,
        Room_Perimeter_m,
        Room_Area_m2,
        Room_Volume_m3,


        //Уровеньъ
        Level_Id,
        Level_Name,
        Level_Elevation,

        //Армирование стен
        //Кирпич

        //Расчетные параметры
        //Начальные параметры
        Wall_Arm_DopTop_Count,//дополнительная арматура сверху - у кирпича 3 штуки

        Wall_Arm_Detailed_Obram_Length_m,//длина обрамления детелей = 1 метра
        Wall_Arm_Detailed_Obram_Count,//по 2 штуки обрамления

        Wall_Detailed_Row_Step_m,//шаг в горизонтальной плоскости деталей
        Wall_Detailed_Roof_Step_m,//шаг вдоль стены деталей

        //Вычисленные параметры 

        //Газобетон
        //Расчетные параметры
        //вычисленные параметры

        Wall_Datailed_Colums,//рядов вертикальных у деталей
        Wall_Datailed_Rows,//горизонтальных рядов у деталей

        Wall_Datailed_Hor_Count,//колво горизонтельных деталей, колво стен* кол во рядов закладных
        Wall_Datailed_Roof_Count,//==Wall_Datailed_Colums - колво вертикальных деталей
        Wall_Datailed_Count,//всего деталей==Wall_Datailed_Roof_Count+ Wall_Datailed_Hor_Count

        Wall_Arm_Rows,//рядов армирования
        Wall_Arm_Roof_Count,//рядов армирования по вертикале

        Wall_Arm_Detailes_Gross_m,//армирование деталей погонные метры кирпичного шва без вычита дверного проема который близко находится
        Wall_Arm_Roof_m,//метров армирвоания верха стены шов 
        Wall_Arm_Rows_Gross_m,//армирование без вычита

        Wall_Arm_Row_Window,
        Wall_Arm_Row_Door,
        Wall_Arm_MinesArmProem_m,
        Wall_Arm_Detailed_Mines_m, // вычитание если около дверь или окно

        Wall_Arm_Itog_m,
        Wall_Arm_Itog_m2,
        Wall_Arm_Itog_pog_m,

    }

    public static class EValuesGetter
    {
        public static readonly Dictionary<EValuesExportData, string> DictNamesParam = new Dictionary<EValuesExportData, string>()
        {
            { EValuesExportData.Wall_Length_m,"Длина стены, м" }
        
        };

        public static string GetName(EValuesExportData eValuesExportData)
        {
            if(eValuesExportData != EValuesExportData.None && DictNamesParam.TryGetValue(eValuesExportData, out var name))
            {
                return name;
            }
            return null;
        }

        public static void AddExportElementInOurExportElements(List<ExportElement> exportElementsInner,List<ExportElement> exportElementsOuter, HashSet<EValuesExportData> valuesExportDatas)
        {
            foreach (var exportElement in exportElementsOuter)
            {
                if(valuesExportDatas.Contains(exportElement.EValuesExportData))
                {
                    exportElementsInner.Add(exportElement);
                    exportElement.RelationsElements = exportElementsInner;
                }
            }
        }

    }

}
