using Autodesk.Revit.DB;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Reinforcement
{
    

    public static class ArmElementConstants
    {
       
        // константы для кирпича
        private static Dictionary<EValuesExportData, double> ConstantsBrick = new Dictionary<EValuesExportData, double>()
        {
            { EValuesExportData. Wall_Arm_Bool_CalcFromStepOrRows,             0  },  //если 1 то из шага, иначе из рядов

            //1 Вариант - из шага надо определять Wall_Arm_Bool_CalcFromStepOrRows=1
            { EValuesExportData. Wall_Arm_Row_Step_m,             0.600  },  // шаг армирования, м
            { EValuesExportData.Wall_Detailed_Row_Step_m,         1.250  },  // шаг деталей, м

            //2 Вариант - из колва надо определять все остальное, быстрее и лучше Wall_Arm_Bool_CalcFromStepOrRows=0
             { EValuesExportData. Wall_Arm_Rows,            2.0  },  //рядов армирования
            { EValuesExportData. Wall_Detailed_Rows,         2.0  },  // рядов деталей

             { EValuesExportData.Wall_Arm_Row_Window,  1  }, // рядов арматуры перерезает
              { EValuesExportData.Wall_Arm_Row_Door,  1  }, // рядов арматуры перерезает
               { EValuesExportData.Wall_Arm_Row_Vitrage,  1  }, // рядов арматуры перерезает


            { EValuesExportData. Wall_Detailed_Roof_Step_m,     1.500 },  // шаг деталей у перекрытия/крыши, м

            { EValuesExportData. Wall_Arm_DopTop_Count,          3.0    },  // доп. верхних рядов армирования
            { EValuesExportData. Wall_Arm_Detailed_Obram_Length_m,  1.000 },  // длина арматурины в детали, м
            { EValuesExportData.Wall_Arm_Detailed_Obram_Count,     2    },  // на одну деталь 2 арматурины
           
        };
        //для газобетона
                private static Dictionary<EValuesExportData, double> ConstantsAerocrete = new Dictionary<EValuesExportData, double>()
        {
            { EValuesExportData. Wall_Arm_Bool_CalcFromStepOrRows,             0  },  //если 1 то из шага, иначе из рядов


            //1 Вариант - из шага надо определять Wall_Arm_Bool_CalcFromStepOrRows=1
            { EValuesExportData. Wall_Arm_Row_Step_m,             0.600  },  // шаг армирования, м
            {EValuesExportData.Wall_Detailed_Row_Step_m,         1.250 },  // шаг деталей, м

            //2 Вариант - из колва надо определять
             { EValuesExportData. Wall_Arm_Rows,            2.0  },  //рядов армирования
            { EValuesExportData. Wall_Detailed_Rows,         2.0  },  // рядов деталей

             { EValuesExportData.Wall_Arm_Row_Window,  1  }, // рядов арматуры перерезает
              { EValuesExportData.Wall_Arm_Row_Door,  1  }, // рядов арматуры перерезает
               { EValuesExportData.Wall_Arm_Row_Vitrage,  1  }, // рядов арматуры перерезает



            { EValuesExportData. Wall_Detailed_Roof_Step_m,    3.000 },// шаг деталей у перекрытия/крыши, м
            
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

        public static bool ExportDetailCalculation = true;//экспорт в расчет деталей количества 

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
