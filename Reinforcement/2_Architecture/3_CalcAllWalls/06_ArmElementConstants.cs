using Autodesk.Revit.DB;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Reinforcement
{
    //данные double которые могут быть выгружены
    /// <summary>
    /// Параметры элемента армирования стены (индексы массива значений ECArmWallElement).
    /// </summary>
    public enum EArmWall
    {
        LevelHM,// отметка уровня
        WallId,//Id стены
        WallHeightMM,          // высота стены, мм
        WallLengthMM,          // длина стены, мм
        WallWidthMM,           // толщина стены, мм
        WallAreaNetM2,// чистая площадь (с проёмами)  — HOST_AREA_COMPUTE
        WallAreaGrossM2,// габаритная площадь (без проёмов)
        WallOpeningsAreaM2,// площадь всех проёмов = Gross − Net
        WallPlanAreaM2,// площадь в плане: Length × Width

        CountDoorOrWindow,//количество окон и дверей
        CountKRBorder,// количество примыканий стены к стенам кр - максимум с 2 сторон

        //константные параметры для расчета

        ArmStepMM,             // шаг рядов армирования, мм
        ArmTopDopRow,             // количество дополнительных рядов армирования у потолка (для кирпича)

        ArmDetailArmLengthMM,  // длина арматуры обрамления закладной, мм
        ArmDetailArmCount,     // количество арматурин на одну деталь обрамления 2 штуки обычно
        ArmCountCrossRow,//=2 - кол-во стержней в одном шве кирпичном - всю погонную длину умножаем на это кол-во

        ArmRowsCutByWindow,//рядов перерезают окна
        ArmRowsCutByDoor,//рядов перерезают двери
        ArmRowsCutByCurtain,//рядов перерезают витражи

        StepDetailsMM,         // шаг деталей крепежа, мм
        StepDetailsRoofMM,     // шаг крепежа плоскости стены к потолку, мм


        //посчитанные данные
        DetailsRoofInt,        // количество крепежа по длине стены (плоскость к потолку)
        DetailsWallRow,//рядов в стене деталей
        DetailsWallsInt,       // количество боковых деталей (у примыканий к ЖБ-стенам)
        DetailsAllCount,// суммарное кол-во деталей

        ArmRow,                // количество рядов армирования на этаж
        ArmGeneralTotalMM,     // армирование основное, мм (без учёта проёмов)
        ArmTopTotalMM,         // армирование верха кирпичной стены, мм
        ArmDetailsTotalMM,     // арматура обрамления закладных кирпичной стены, мм

        MinesArmFromWindAndDoorMM,      // вычет основного армирования по проёмам, мм
        MinesArmDetailsFromWindAndDoorMM, // вычет арматуры обрамления по проёмам, мм

        ArmAllM,//длина арматуры общая пог метры, наверно надо на 2 домножить
        ArmAllM2


    }
    ///// <summary>
    ///// Строковые данные
    ///// </summary>
    public enum EArmWallString
    {
        LevelName,//имя уровня
        WallFamily,//семейство стены
        WallType,//типоразмер
        WallEWallTypes,//наш тип утеплитель или что это

        IdsWindAndDoors,//список Id окон и дверей
        IdsConnectsWalls,// список Id всех примыкающих стен
        IdsConnectsWallsKR,// список Id всех КР примыкающих стен
    }

    public static class ArmElementConstants
    {
        /// <summary>
        /// Русские названия числовых параметров армирования (для заголовков экспорта).
        /// </summary>
        public static readonly Dictionary<EArmWall, string> EArmWallNames = new Dictionary<EArmWall, string>
    {
        { EArmWall.LevelHM,                        "Отметка уровня, м" },
        { EArmWall.WallId,                         "Id стены" },
        { EArmWall.WallHeightMM,                   "Высота стены, мм" },
        { EArmWall.WallLengthMM,                   "Длина стены, мм" },
        { EArmWall.WallWidthMM,                    "Толщина стены, мм" },
        { EArmWall.WallAreaNetM2,                  "Чистая площадь (с проёмами), м²" },
        { EArmWall.WallAreaGrossM2,                "Габаритная площадь (без проёмов), м²" },
        { EArmWall.WallOpeningsAreaM2,             "Площадь проёмов, м²" },
        { EArmWall.WallPlanAreaM2,                 "Площадь в плане, м²" },

        { EArmWall.CountDoorOrWindow,              "Кол-во окон и дверей" },
        { EArmWall.CountKRBorder,                  "Кол-во примыканий к стенам КР" },

        { EArmWall.ArmRow,                         "Кол-во рядов армирования" },
        { EArmWall.ArmTopDopRow,                   "Доп. ряды армирования (кирпич)" },
        { EArmWall.ArmStepMM,                      "Шаг армирования, мм" },
        { EArmWall.ArmDetailArmLengthMM,           "Длина арматуры обрамления деталей, мм" },
        { EArmWall.ArmDetailArmCount,              "Кол-во арматурин обрамления на деталь" },
        { EArmWall.ArmCountCrossRow,               "Кол-во стержней в одном шве" },
        { EArmWall.ArmRowsCutByWindow,             "Ряды арм., перерезанные окнами" },      // — новое
        { EArmWall.ArmRowsCutByDoor,               "Ряды арм., перерезанные дверями" },     // — новое
        { EArmWall.ArmRowsCutByCurtain,            "Ряды арм., перерезанные витражами" },   // — новое
        { EArmWall.DetailsAllCount,                "Кол-во деталей крепежа" },
        { EArmWall.StepDetailsMM,                  "Шаг деталей крепежа, мм" },
        { EArmWall.StepDetailsRoofMM,              "Шаг крепежа к потолку, мм" },

        { EArmWall.DetailsRoofInt,                 "Кол-во крепежа по длине (потолок)" },
        { EArmWall.DetailsWallRow,                 "Рядов в стене деталей" },               // — новое
        { EArmWall.DetailsWallsInt,                "Кол-во боковых деталей" },

        { EArmWall.ArmGeneralTotalMM,              "Армирование основное, длина шва, мм" },
        { EArmWall.ArmTopTotalMM,                  "Армирование верха, длина шва, мм" },
        { EArmWall.ArmDetailsTotalMM,              "Арматура обрамления закладных, длина шва, мм" },

        { EArmWall.MinesArmFromWindAndDoorMM,        "Вычет армирования по проёмам, длина шва, мм" },
        { EArmWall.MinesArmDetailsFromWindAndDoorMM, "Вычет обрамления по проёмам, длина шва, мм" },
        { EArmWall.ArmAllM,                        "Полная арматура (длина шва*арматур в шве), м" },
        { EArmWall.ArmAllM2,                       "Полная арматура (длина шва*арматур в шве), м²" }
    };
        /// <summary>
        /// Русские названия строковых параметров армирования.
        /// </summary>
        public static readonly Dictionary<EArmWallString, string> EArmWallStringNames = new Dictionary<EArmWallString, string>
        {
            { EArmWallString.LevelName,          "Имя уровня" },
            { EArmWallString.WallFamily,         "Семейство стены" },
            { EArmWallString.WallType,           "Типоразмер стены" },
            { EArmWallString.WallEWallTypes,     "Тип стены" },

            { EArmWallString.IdsWindAndDoors,    "Id окон и дверей" },
            { EArmWallString.IdsConnectsWalls,   "Id примыкающих стен" },
            { EArmWallString.IdsConnectsWallsKR, "Id примыкающих стен КР" }
        };



        
        // соответствие констант и имён параметров в Revit (префикс "ЕС_" — наша компания)
        private static Dictionary<EArmWall, string> NamesParametersInRevit = new Dictionary<EArmWall, string>()
        {
            { EArmWall.ArmStepMM,            "ЕС_Шаг_Армирования_ММ"        }, // шаг армирования, мм
            { EArmWall.StepDetailsMM,        "ЕС_Шаг_Деталей_ММ"            }, // шаг деталей (крепление к перекрытию), мм
            { EArmWall.StepDetailsRoofMM,    "ЕС_Шаг_Деталей_Перекрытие_ММ" }, // шаг деталей у перекрытия/крыши, мм
            { EArmWall.ArmTopDopRow,         "ЕС_Кол_Доп_Верхних_Рядов"     }, // доп. верхние ряды армирования, шт.
            { EArmWall.ArmDetailArmLengthMM, "ЕС_Длина_Арматурины_Детали_ММ"       }, // длина арматурины в детали, мм
            { EArmWall.ArmDetailArmCount,    "ЕС_Кол_Арматурин_В_Детали"    }, // арматурин на одну деталь, шт.
            { EArmWall.ArmCountCrossRow,     "ЕС_Кол_Прутков_В_Ряду"        },  // количество прутков в ряду, шт.

            { EArmWall.DetailsWallRow, "ЕС_Кол_Деталей_рядов" }//количество рядов деталей
        };

        // константы для кирпича
        private static Dictionary<EArmWall, double> ConstantsBrick = new Dictionary<EArmWall, double>()
        {
            { EArmWall.ArmStepMM,             600  },  // шаг армирования, мм
            { EArmWall.StepDetailsMM,         600  },  // шаг деталей, мм
            { EArmWall.StepDetailsRoofMM,     1500 },  // шаг деталей у перекрытия/крыши, мм
            { EArmWall.ArmTopDopRow,          3    },  // доп. верхних рядов армирования
            { EArmWall.ArmDetailArmLengthMM,  1000 },  // длина арматурины в детали, мм
            { EArmWall.ArmDetailArmCount,     2    },  // на одну деталь 2 арматурины
            { EArmWall.ArmCountCrossRow,      2    },  // количество прутков в ряду
            { EArmWall.ArmRow,                2    },  // кол-во рядов армирования на этаж
          
            { EArmWall.ArmRowsCutByWindow,    1    },  // ряды, перерезанные окнами
            { EArmWall.ArmRowsCutByDoor,      1    },  // ряды, перерезанные дверями
            { EArmWall.ArmRowsCutByCurtain,   1    },   // ряды, перерезанные витражами
            { EArmWall.DetailsWallRow,2 },//количество рядов деталей
        };
        //для газобетона
                private static Dictionary<EArmWall, double> ConstantsAerocrete = new Dictionary<EArmWall, double>()
        {
            { EArmWall.ArmStepMM,             750  },
            { EArmWall.StepDetailsMM,         1250 },
            { EArmWall.StepDetailsRoofMM,     3000 },
            { EArmWall.ArmCountCrossRow,      2    },
            { EArmWall.ArmRow,                2    },
           
            { EArmWall.ArmRowsCutByWindow,    1    },
            { EArmWall.ArmRowsCutByDoor,      1    },
            { EArmWall.ArmRowsCutByCurtain,   1    },
            { EArmWall.DetailsWallRow,2 },//количество рядов деталей
        };

        /// <summary>Выбрать словарь констант по типу стены</summary>
        private static Dictionary<EArmWall, double> GetDict(EWallTypes eWallTypes)
        {
            if (eWallTypes == EWallTypes.AR_Brick) return ConstantsBrick;
            if (eWallTypes == EWallTypes.AR_Aerocrete) return ConstantsAerocrete;
            return null;
        }

        /// <summary>
        /// Установка константного параметра
        /// </summary>
        public static bool SetConstantsParameter(EWallTypes eWallTypes, EArmWall eArmWall, double value)
        {
            Dictionary<EArmWall, double> dict = GetDict(eWallTypes);
            if (dict == null) return false;

            if (dict.ContainsKey(eArmWall))
            {
                dict[eArmWall] = value;
                return true;
            }
            return false; // ключа нет — а добавлять его извне не даём, константы фиксированы
        }
        /// <summary>Публичное чтение одной константы (для инициализации окна)</summary>
        public static double GetConstant(EWallTypes eWallTypes, EArmWall key)
        {
            Dictionary<EArmWall, double> dict = GetDict(eWallTypes);
            if (dict == null) return 0;
            return dict.TryGetValue(key, out double v) ? v : 0;
        }
        /// <summary>
        /// Получить копию словаря констант (защита от внешней правки оригинала)
        /// </summary>
        public static Dictionary<EArmWall, double> GetConstantsParameters(EWallTypes eWallTypes)
        {
            Dictionary<EArmWall, double> dict = GetDict(eWallTypes);
            if (dict == null) return null;

            return new Dictionary<EArmWall, double>(dict);
        }
        public static bool GetParamFromGeomElement = true;//брать параметры из геометрии элемента
        public static bool GetParamFromECPropElement = false;// взять начальные параметры из свойств ревита
        public static bool GetParamOnlyNonZero = false;//брать только не нулевые
        public static bool FillParametersInProject = false;//заполнить параметры проекта
        /// <summary>
        /// Если у элемента заполнены параметры — берёт из него.
        /// getOnlyNonZero = true: параметр читаем только ненулевой, нулевому остаётся константа.
        /// </summary>
        public static Dictionary<EArmWall, double> GetConstantsParameters(
            EWallTypes eWallTypes, Element element, bool getFromElement, bool getOnlyNonZero)
        {
            Dictionary<EArmWall, double> dictConst = GetConstantsParameters(eWallTypes);
            if (!getFromElement || element == null) return dictConst;

            foreach (var key in dictConst.Keys)
            {
                if (!NamesParametersInRevit.TryGetValue(key, out string nameParamRevit)) continue;

                // параметр может лежать на экземпляре или на типе — проверяем оба варианта
                Parameter param = element.LookupParameter(nameParamRevit);
                if (param == null)
                {
                    Element typeElem = element.Document?.GetElement(element.GetTypeId());
                    if (typeElem == null) continue;
                    param = typeElem.LookupParameter(nameParamRevit);
                }
                if (param == null || !param.HasValue) continue;

                double val;
                switch (param.StorageType)
                {
                    case StorageType.Double: val = param.AsDouble(); break;
                    case StorageType.Integer: val = param.AsInteger(); break;
                    case StorageType.String:
                        if (double.TryParse(param.AsString()?.Replace(',', '.'),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out val))
                            break;
                        continue;
                    default: continue;
                }

                // нулевое значение не считаем заполненным — оставляем константу
                if (getOnlyNonZero && val == 0) continue;

                dictConst[key] = val;
            }

            return dictConst;
        }

        /// <summary>
        /// Установка параметров самому элементу по данным расчёта.
        /// Транзакция должна быть открыта вызывающим кодом.
        /// </summary>
        /// <param name="eWallTypes">Тип стены (кирпич/газобетон) — определяет набор ключей</param>
        /// <param name="eCArmWallElement">Объект с рассчитанными значениями по ключам EArmWall</param>
        /// <param name="element">Элемент Revit (стена), куда пишем</param>
        public static void SetParametersInWallElement(EWallTypes eWallTypes, ECArmWallElement eCArmWallElement, Element element)
        {
            if (!FillParametersInProject) { return; }
            if (element == null || eCArmWallElement == null) return;
           
            Dictionary<EArmWall, double> dict = GetDict(eWallTypes);
            if (dict == null) return;

            // тип элемента ищем один раз, а не в каждой итерации
            Element typeElem = null;
            ElementId typeId = element.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
                typeElem = element.Document?.GetElement(typeId);

            foreach (EArmWall key in dict.Keys)
            {
                
                double value = eCArmWallElement[key];
                if (!NamesParametersInRevit.TryGetValue(key, out string nameParamRevit)) continue;

                // сначала экземпляр, потом тип
                Parameter param = element.LookupParameter(nameParamRevit);
                if (param == null && typeElem != null)
                    param = typeElem.LookupParameter(nameParamRevit);

                if (param == null || param.IsReadOnly) continue;

                switch (param.StorageType)
                {
                    case StorageType.Double:
                        param.Set(value);
                        break;

                    case StorageType.Integer:
                        param.Set((int)Math.Round(value));
                        break;

                    case StorageType.String:
                        param.Set(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        break;

                    default:
                        // ElementId и прочие типы сюда не пишем
                        break;
                }
            }
        }

    }
}
