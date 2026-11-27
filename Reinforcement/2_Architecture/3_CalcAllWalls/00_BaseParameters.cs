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
        AR_PGPorGKL,
        AR_Vitrage,
        AR_shutkarutorka,
    }
    public enum EValuesExportData
    {
        //все 
        None = 0,

        //Уровень
        Level_Id,
        Level_Name,
        Level_Elevation,

        //Сама стена
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
        Wall_Room_Types,// типы помещения

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

        // параметры витражай
        Wall_Vitrage_Count,
        Wall_Vitrage_Ids,
        Wall_Vitrage_L_Average_m,//средняя длина двери среди всех дверей стены
        Wall_Vitrage_H_Average_m,//средняя высота двери среди всех дверей стены

        //Тип стены
        WallType_Id,
        WallType_LayersCount,


        //Теплоизоляционная ли это стена
        Wall_Insulation_Number,//номер стены в общей цепочку стен наружных
        Wall_Insulation_OutSideWallName, //по имени bool =true если стена наружная
        Wall_Insulation_OutSideWallContur,
        Wall_Insulation_LengthM,
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
        //Витраж
        Vitrage_Height_m,
        Vitrage_Length_m,
        Vitrage_Area_m2,
        Vitrage_Level_Elevation,


        //Слойдвери
        WallLayer_Width_m,

        //комната
        Room_Id,
        Room_Name,
        Room_Number,
        Room_Perimeter_m,
        Room_Area_m2,
        Room_Volume_m3,


       

        //Армирование стен
        //Кирпич

        //Расчетные параметры
        //Начальные параметры

        Wall_Arm_Bool_CalcFromStepOrRows,//если 1 то из шага, иначе из рядов

        Wall_Arm_DopTop_Count,//дополнительная арматура сверху - у кирпича 3 штуки

        Wall_Arm_Detailed_Obram_Length_m,//длина обрамления детелей = 1 метра
        Wall_Arm_Detailed_Obram_Count,//по 2 штуки обрамления

        Wall_Arm_Row_Step_m,//шаг в горизонтальной плоскости арматкры

        Wall_Detailed_Row_Step_m,//шаг в горизонтальной плоскости деталей
        Wall_Detailed_Roof_Step_m,//шаг вдоль стены деталей

        //Вычисленные параметры 

        //Газобетон
        //Расчетные параметры
        //вычисленные параметры

        Wall_Detailed_Colums,//рядов вертикальных у деталей
        Wall_Detailed_Rows,//горизонтальных рядов у деталей

        Wall_Detailed_Hor_Count,//колво горизонтельных деталей, колво стен* кол во рядов закладных
        Wall_Detailed_Roof_Count,//==Wall_Detailed_Colums - колво вертикальных деталей
        Wall_Detailed_Count,//всего деталей==Wall_Detailed_Roof_Count+ Wall_Detailed_Hor_Count

        Wall_Arm_Rows,//рядов армирования
        Wall_Arm_Roof_Count,//рядов армирования по вертикале

        Wall_Arm_Detailes_Gross_m,//армирование деталей погонные метры кирпичного шва без вычита дверного проема который близко находится
        Wall_Arm_Roof_m,//метров армирвоания верха стены шов 
        Wall_Arm_Rows_Gross_m,//армирование без вычита

        Wall_Arm_Row_Window,
        Wall_Arm_Row_Door,
        Wall_Arm_Row_Vitrage,

        Wall_Arm_MinesArmProem_m,
        Wall_Arm_Detailed_Mines_m, // вычитание если около дверь или окно

        Wall_Arm_Vilaterm_Itog_m,
        Wall_Arm_Vilaterm_Itog_m2,
       
        Wall_Arm_Itog_m,
        Wall_Arm_Itog_m2,
        Wall_Arm_Itog_pog_m,

    }

    public static class EValuesGetter
    {

        

        /// <summary>
        /// Русские названия числовых параметров армирования (для заголовков экспорта).
        /// </summary>
        public static readonly Dictionary<EValuesExportData, string> DictNamesParam
    = new Dictionary<EValuesExportData, string>()
{
        // ==================== Служебное ====================
        // Если None не должен попадать в экспорт — эту строку можно не добавлять
        { EValuesExportData.None,                         "None" },

    // ==================== Уровень ====================
    { EValuesExportData.Level_Id,                     "Уровень Id" },
    { EValuesExportData.Level_Name,                   "Уровень имя" },
    { EValuesExportData.Level_Elevation,              "Уровень отметка, м" },

    // ==================== Стена: основные ====================
    { EValuesExportData.Wall_Id,                      "Стена Id" },
    { EValuesExportData.Wall_Name,                    "Стена имя" },
    { EValuesExportData.Wall_EWallTypes,              "Стена тип (наш)" },
    { EValuesExportData.Wall_Length_m,                "Стена длина, м" },
    { EValuesExportData.Wall_Width_m,                 "Стена толщина, м" },
    { EValuesExportData.Wall_Height_m,                "Стена высота, м" },
    { EValuesExportData.Wall_AreaNetM2,               "Стена площадь с проёмами, м2" },
    { EValuesExportData.Wall_AreaGrossM2,             "Стена площадь без проёмов, м2" },
    { EValuesExportData.Wall_OpeningsAreaM2,          "Стена площадь проёмов, м2" },
    { EValuesExportData.Wall_PlanAreaM2,              "Стена площадь в плане, м2" },
    { EValuesExportData.Wall_VolumeM3,                "Стена объём, м3" },
    { EValuesExportData.Wall_Insulation_OutSideWallName,"Стена наружная?" },

    // ==================== Примыкающие стены ====================
    { EValuesExportData.Wall_OtherWallsIds,           "Стена примыкающие стены, Id" },
    { EValuesExportData.Wall_OtherWallsCount,         "Стена примыкающие стены, кол-во" },
    { EValuesExportData.Wall_OtherWallsKRIds,         "Стена примыкающие стены КР, Id" },
    { EValuesExportData.Wall_OtherWallsKRCount,       "Стена примыкающие стены КР, кол-во" },

    // ==================== Комнаты стены ====================
    { EValuesExportData.Wall_Room_Count,              "Стена комнат, кол-во" },
    { EValuesExportData.Wall_Room_Ids,                "Стена комнат, Id" },
    { EValuesExportData.Wall_Room_Numbers,            "Стена комнат, номера" },
    { EValuesExportData.Wall_Room_Types,                "Стена комнат, типы помещений" },
    // ==================== Проёмы (двери и окна) ====================
    { EValuesExportData.Wall_DoorsAndWindows_Count,   "Стена двери+окна, кол-во" },
    { EValuesExportData.Wall_Doors_Count,             "Стена двери, кол-во" },
    { EValuesExportData.Wall_Doors_Ids,               "Стена двери, Id" },
    { EValuesExportData.Wall_Doors_L_Average_m,       "Стена дверь ср. длина, м" },
    { EValuesExportData.Wall_Doors_H_Average_m,       "Стена дверь ср. высота, м" },
    { EValuesExportData.Wall_Window_Count,            "Стена окна, кол-во" },
    { EValuesExportData.Wall_Window_Ids,              "Стена окна, Id" },
    { EValuesExportData.Wall_Window_L_Average_m,      "Стена окно ср. длина, м" },
    { EValuesExportData.Wall_Window_H_Average_m,      "Стена окно ср. высота, м" },

    // ==================== Витраж в стене ====================
    { EValuesExportData.Wall_Vitrage_Count,           "Стена витражи, кол-во" },
    { EValuesExportData.Wall_Vitrage_Ids,             "Стена витражи, Id" },
    { EValuesExportData.Wall_Vitrage_L_Average_m,     "Стена витраж ср. длина, м" },
    { EValuesExportData.Wall_Vitrage_H_Average_m,     "Стена витраж ср. высота, м" },


    // ==================== Тип стены / слои ====================
    { EValuesExportData.WallType_Id,                  "Тип стены Id" },
    { EValuesExportData.WallType_LayersCount,         "Тип стены слоёв, шт" },
    { EValuesExportData.WallLayer_Width_m,            "Слой толщина, м" },

    // ==================== Дверь ====================
    { EValuesExportData.Door_Height_m,                "Дверь высота, м" },
    { EValuesExportData.Door_Length_m,                "Дверь длина, м" },
    { EValuesExportData.Door_Area_m2,                 "Дверь площадь, м2" },
    { EValuesExportData.Door_Level_Elevation,         "Дверь отметка, м" },

    // ==================== Окно ====================
    { EValuesExportData.Window_Height_m,              "Окно высота, м" },
    { EValuesExportData.Window_Length_m,              "Окно длина, м" },
    { EValuesExportData.Window_Area_m2,               "Окно площадь, м2" },
    { EValuesExportData.Window_Level_Elevation,       "Окно отметка, м" },

    // ==================== Витраж ====================
    { EValuesExportData.Vitrage_Height_m,             "Витраж высота, м" },
    { EValuesExportData.Vitrage_Length_m,             "Витраж длина, м" },
    { EValuesExportData.Vitrage_Area_m2,              "Витраж площадь, м2" },
    { EValuesExportData.Vitrage_Level_Elevation,      "Витраж отметка, м" },


    // ==================== Комната ====================
    { EValuesExportData.Room_Id,                      "Комната Id" },
    { EValuesExportData.Room_Name,                    "Комната имя" },
    { EValuesExportData.Room_Number,                  "Комната номер" },
    { EValuesExportData.Room_Perimeter_m,             "Комната периметр, м" },
    { EValuesExportData.Room_Area_m2,                 "Комната площадь, м2" },
    { EValuesExportData.Room_Volume_m3,               "Комната объём, м3" },

    // ==================== Армирование: входные ====================
    { EValuesExportData.Wall_Arm_Bool_CalcFromStepOrRows, "Арм. расчёт из шага/рядов" },

    { EValuesExportData.Wall_Arm_DopTop_Count,        "Арм. доп. рядов сверху, шт" },
    { EValuesExportData.Wall_Arm_Detailed_Obram_Length_m, "Арм. обрамление детали, длина, м" },
    { EValuesExportData.Wall_Arm_Detailed_Obram_Count,    "Арм. обрамлений на деталь, шт" },
    { EValuesExportData.Wall_Arm_Row_Step_m,          "Арм. шаг рядов, м" },
    { EValuesExportData.Wall_Detailed_Row_Step_m,     "Детали шаг по горизонтали, м" },
    { EValuesExportData.Wall_Detailed_Roof_Step_m,    "Детали шаг по верху, м" },

    // ==================== Армирование: детали ====================
    { EValuesExportData.Wall_Detailed_Colums,         "Детали: столбцов, шт" },
    { EValuesExportData.Wall_Detailed_Rows,           "Детали: рядов, шт" },
    { EValuesExportData.Wall_Detailed_Hor_Count,      "Детали: горизонтальных, шт" },
    { EValuesExportData.Wall_Detailed_Roof_Count,     "Детали: по верху, шт" },
    { EValuesExportData.Wall_Detailed_Count,          "Детали: всего, шт" },

    // ==================== Армирование: расчёт ====================
    { EValuesExportData.Wall_Arm_Rows,                "Арм. рядов, шт" },
    { EValuesExportData.Wall_Arm_Roof_Count,          "Арм. по верху, шт" },

    { EValuesExportData.Wall_Arm_Detailes_Gross_m,    "Арм. деталей (без вычета), пог.м" },
    { EValuesExportData.Wall_Arm_Roof_m,              "Арм. верха стены, пог.м" },
    { EValuesExportData.Wall_Arm_Rows_Gross_m,        "Арм. рядов (без вычета), пог.м" },

    { EValuesExportData.Wall_Arm_Row_Window,          "Арм. рядов у окна, шт" },
    { EValuesExportData.Wall_Arm_Row_Door,            "Арм. рядов у двери, шт" },
     { EValuesExportData.Wall_Arm_Row_Vitrage,         "Арм. рядов у витража, шт" },
    { EValuesExportData.Wall_Arm_MinesArmProem_m,     "Арм. минус проёмы, пог.м" },
    { EValuesExportData.Wall_Arm_Detailed_Mines_m,    "Арм. деталей минус проёмы, пог.м" },

    { EValuesExportData.Wall_Arm_Vilaterm_Itog_m,     "Вилатерм итог, пог.м" },
    { EValuesExportData.Wall_Arm_Vilaterm_Itog_m2,    "Вилатерм итог, м2" },

    { EValuesExportData.Wall_Arm_Itog_m,              "Арм. итог, пог.м одного прута" },
    { EValuesExportData.Wall_Arm_Itog_m2,             "Арм. итог, м2" },
   
    { EValuesExportData.Wall_Arm_Itog_pog_m,          "Арм. итог, пог.м" },

};
        // соответствие констант и имён параметров в Revit (префикс "ЕС_" — наша компания)
        public static Dictionary<EValuesExportData, string> NamesParametersInRevit = new Dictionary<EValuesExportData, string>()
        {
            //{ EArmWall.ArmStepMM,            "ЕС_Шаг_Армирования_ММ"        }, // шаг армирования, мм
            //{ EArmWall.StepDetailsMM,        "ЕС_Шаг_Деталей_ММ"            }, // шаг деталей (крепление к перекрытию), мм
            //{ EArmWall.StepDetailsRoofMM,    "ЕС_Шаг_Деталей_Перекрытие_ММ" }, // шаг деталей у перекрытия/крыши, мм
            //{ EArmWall.ArmTopDopRow,         "ЕС_Кол_Доп_Верхних_Рядов"     }, // доп. верхние ряды армирования, шт.
            //{ EArmWall.ArmDetailArmLengthMM, "ЕС_Длина_Арматурины_Детали_ММ"       }, // длина арматурины в детали, мм
            //{ EArmWall.ArmDetailArmCount,    "ЕС_Кол_Арматурин_В_Детали"    }, // арматурин на одну деталь, шт.
            //{ EArmWall.ArmCountCrossRow,     "ЕС_Кол_Прутков_В_Ряду"        },  // количество прутков в ряду, шт.

            //{ EArmWall.DetailsWallRow, "ЕС_Кол_Деталей_рядов" }//количество рядов деталей
        };
        public static string GetName(EValuesExportData eValuesExportData)
        {
            if(eValuesExportData != EValuesExportData.None && DictNamesParam.TryGetValue(eValuesExportData, out var name))
            {
                return name;
            }
            return null;
        }
        /// <summary>
        /// Метод для переприсваивания элемента данных в другой ряд
        /// </summary>
        /// <param name="exportElementsInner"></param>
        /// <param name="exportElementsOuter"></param>
        /// <param name="valuesExportDatas"></param>
        /// <param name="notPovtor"></param>
        public static void AddExportElementInOurExportElements(List<ExportElement> exportElementsInner,List<ExportElement> exportElementsOuter, HashSet<EValuesExportData> valuesExportDatas, bool notPovtor=true)
        {
            var pastEValuesExportData = exportElementsInner.Select(x=>x.EValuesExportData).ToHashSet();
            foreach (var exportElement in exportElementsOuter)
            {
                if(valuesExportDatas.Contains(exportElement.EValuesExportData))
                {
                    //Только те - что раньше не были, иерархия вложенных данныеъ
                    if (!notPovtor||pastEValuesExportData.Add(exportElement.EValuesExportData))
                    {
                        exportElementsInner.Add(exportElement);
                        exportElement.RelationsElements = exportElementsInner;
                    }
                }
            }
        }
        /// <summary>
        /// Формирование порядка, сначала самый с максимальным посчитанным значением и чистка от дублей
        /// </summary>
        /// <param name="exportElements"></param>
        /// <returns></returns>
        public static List<ExportElement> ClearAndOrdered(List<ExportElement> exportElements)
        {
            var pastEValuesExportData = new HashSet<EValuesExportData>();

            var group = exportElements.GroupBy(x=>x.EValuesExportData);
            var answer = new List<ExportElement>();
            foreach (var gr in group)
            {
                var key = gr.Key;
                if (key == EValuesExportData.None)
                {
                    answer.AddRange(gr);
                }
                else
                {
                    var add = gr.OrderByDescending(x => x.ValueDouble).ThenByDescending(x => x.ValueString).FirstOrDefault();
                    if(add != null)
                    {
                        answer.Add(add);
                    }
                }
            }
            answer = answer.OrderBy(x=>x.EValuesExportData).ToList();
            return answer;
        }
        public static void AddExportElementInOurExportElements(List<ExportElement> exportElementsInner, List<ExportElement> exportElementsOuter)
        {
            var pastEValuesExportData = exportElementsInner.Select(x => x.EValuesExportData).ToHashSet();
            foreach (var exportElement in exportElementsOuter)
            {
                //Только те - что раньше не были, иерархия вложенных данныеъ
                if (pastEValuesExportData.Add(exportElement.EValuesExportData))
                {
                    exportElementsInner.Add(exportElement);
                    exportElement.RelationsElements = exportElementsInner;
                }
                
            }
        }
        /// <summary>
        /// Сортирует список ExportElement in-place (без создания нового списка)
        /// в порядке следования значений EValuesExportData.
        /// EValuesExportData.None ставится в конец.
        /// Возвращает тот же самый объект (или null, если data == null),
        /// чтобы удобно было писать return SortByEValuesExportData(data);
        /// </summary>
        public static List<ExportElement> SortByEValuesExportData(List<ExportElement> data)
        {
            if (data == null || data.Count < 2) return data;

            data.Sort((a, b) =>
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;

                int av = a.EValuesExportData == EValuesExportData.None
                       ? int.MaxValue : (int)a.EValuesExportData;
                int bv = b.EValuesExportData == EValuesExportData.None
                       ? int.MaxValue : (int)b.EValuesExportData;

                return av.CompareTo(bv);
            });

            return data;
        }
    }

}
