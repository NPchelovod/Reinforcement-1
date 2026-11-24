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
        ArmAllM2,
        VilatermM,
        VilatermM2,


    }
    ///// <summary>
    ///// Строковые данные
    ///// </summary>
    //public enum EArmWallString
    //{
    //    LevelName,//имя уровня
    //    WallFamily,//семейство стены
    //    WallType,//типоразмер
    //    WallEWallTypes,//наш тип утеплитель или что это

    //    IdsWindAndDoors,//список Id окон и дверей
    //    IdsConnectsWalls,// список Id всех примыкающих стен
    //    IdsConnectsWallsKR,// список Id всех КР примыкающих стен
    //}

    public static class ArmElementConstants
    {
       
        // константы для кирпича
        private static Dictionary<EValuesExportData, double> ConstantsBrick = new Dictionary<EValuesExportData, double>()
        {
            { EValuesExportData. Wall_Arm_Row_Step_m,             0.600  },  // шаг армирования, мм
            { EValuesExportData.Wall_Detailed_Row_Step_m,         0.600  },  // шаг деталей, мм
            { EValuesExportData. Wall_Detailed_Roof_Step_m,     1.500 },  // шаг деталей у перекрытия/крыши, мм
            { EValuesExportData. Wall_Arm_DopTop_Count,          3.0    },  // доп. верхних рядов армирования
            { EValuesExportData. Wall_Arm_Detailed_Obram_Length_m,  1.000 },  // длина арматурины в детали, мм
            { EValuesExportData.Wall_Arm_Detailed_Obram_Count,     2    },  // на одну деталь 2 арматурины
           
        };
        //для газобетона
                private static Dictionary<EValuesExportData, double> ConstantsAerocrete = new Dictionary<EValuesExportData, double>()
        {
            { EValuesExportData. Wall_Arm_Row_Step_m,             0.750  },
            {EValuesExportData.Wall_Detailed_Row_Step_m,         0.1250 },
            { EValuesExportData. Wall_Detailed_Roof_Step_m,    3.000 },
            
        };

        /// <summary>Выбрать словарь констант по типу стены</summary>
        private static Dictionary<EValuesExportData, double> GetDict(EWallTypes eWallTypes)
        {
            if (eWallTypes == EWallTypes.AR_Brick) return ConstantsBrick;
            if (eWallTypes == EWallTypes.AR_Aerocrete) return ConstantsAerocrete;
            return null;
        }

        /// <summary>
        /// Установка константного параметра
        /// </summary>
        public static bool SetConstantsParameter(EWallTypes eWallTypes, EValuesExportData eArmWall, double value)
        {
            Dictionary<EValuesExportData, double> dict = GetDict(eWallTypes);
            if (dict == null) return false;

            if (dict.ContainsKey(eArmWall))
            {
                dict[eArmWall] = value;
                return true;
            }
            return false; // ключа нет — а добавлять его извне не даём, константы фиксированы
        }
        /// <summary>Публичное чтение одной константы (для инициализации окна)</summary>
        public static double GetConstant(EWallTypes eWallTypes, EValuesExportData key)
        {
            Dictionary<EValuesExportData, double> dict = GetDict(eWallTypes);
            if (dict == null) return 0;
            return dict.TryGetValue(key, out double v) ? v : 0;
        }
        /// <summary>
        /// Получить копию словаря констант (защита от внешней правки оригинала)
        /// </summary>
        public static Dictionary<EValuesExportData, double> GetConstantsParameters(EWallTypes eWallTypes)
        {
            Dictionary<EValuesExportData, double> dict = GetDict(eWallTypes);
            if (dict == null) return null;

            return new Dictionary<EValuesExportData, double>(dict);
        }
        public static bool GetParamFromGeomElement = true;//брать параметры из геометрии элемента
        public static bool GetParamFromECPropElement = false;// взять начальные параметры из свойств ревита
        public static bool GetParamOnlyNonZero = false;//брать только не нулевые
        public static bool FillParametersInProject = false;//заполнить параметры проекта
        /// <summary>
        /// Если у элемента заполнены параметры — берёт из него.
        /// getOnlyNonZero = true: параметр читаем только ненулевой, нулевому остаётся константа.
        /// </summary>
        public static Dictionary<EValuesExportData, double> GetConstantsParameters(
            EWallTypes eWallTypes, Element element, bool getFromElement, bool getOnlyNonZero)
        {
            Dictionary<EValuesExportData, double> dictConst = GetConstantsParameters(eWallTypes);
            if (!getFromElement || element == null) return dictConst;

            foreach (var key in dictConst.Keys)
            {
                if (!EValuesGetter.NamesParametersInRevit.TryGetValue(key, out string nameParamRevit)) continue;

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
           
            Dictionary<EValuesExportData, double> dict = GetDict(eWallTypes);
            if (dict == null) return;

            // тип элемента ищем один раз, а не в каждой итерации
            Element typeElem = null;
            ElementId typeId = element.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
                typeElem = element.Document?.GetElement(typeId);

            foreach (EValuesExportData key in dict.Keys)
            {
                
                double value = eCArmWallElement[key];
                if (!EValuesGetter.NamesParametersInRevit.TryGetValue(key, out string nameParamRevit)) continue;

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
