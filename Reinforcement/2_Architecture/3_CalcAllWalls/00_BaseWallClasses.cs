using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RootFinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Reinforcement
{
    

    

    /// <summary>
    /// Класс для экспорта в эксель данных
    /// </summary>
    public class ExportElement
    {
        /// <summary>
        /// Переменная для вычислений
        /// </summary>
        public EValuesExportData EValuesExportData = EValuesExportData.None;

        /// <summary>
        /// Имя которое будет при экспорте
        /// </summary>
        public string Name;
        /// <summary>
        /// Значения которое будет экспортировано как число
        /// </summary>
        public double ValueDouble;
        /// <summary>
        /// Значение которое будет экспортировано как строка
        /// </summary>
        public string ValueString;
        /// <summary>
        /// что мы экспортируем число или строку
        /// </summary>
        public bool ExportDoubleOrString=false;
        /// <summary>
        /// Подводить итори - суммировать все строки??
        /// </summary>
        public bool SumItog = true;

        public ExportElement( string name, double valueD, List<ExportElement> relationsElements, EValuesExportData eValuesExportData= 0, string formula="")
        {
            EValuesExportData = eValuesExportData;
            Name = name;
            ValueDouble = valueD;
            ExportDoubleOrString = true;
            RelationsElements = relationsElements;
            Formula = formula;
            GeneralInitial();
           
            
        }
        public ExportElement( string name, string valueS, List<ExportElement> relationsElements, EValuesExportData eValuesExportData = 0)
        {
            
            EValuesExportData = eValuesExportData;
            Name = name;
            ValueString = valueS;
            ExportDoubleOrString = false;
            RelationsElements = relationsElements;
            GeneralInitial();
        }

        private void GeneralInitial()
        {
            //проверка других 
            string otherName = EValuesGetter.GetName(EValuesExportData);
            if (!string.IsNullOrEmpty(otherName))
            {
                Name = otherName;
            }
        }
        /// <summary>
        /// Список к которому мы принадлежим чтобы можно было посчитаться автоматически формулы по переменным в списке
        /// </summary>
        public List<ExportElement> RelationsElements = null;
        /// <summary>
        /// Формула вида Wall_L*Wall_W*Wall_H - переменные из EValuesExportData
        /// </summary>
        public string Formula;
        public void Calculations()
        {
            IsCalculations = false;
            FormulaExcell = null;
            CorrectExcellFormula = true;
            ErrorMessageFormula = null;
            ValueDoubleCalculated = 0;

            if (string.IsNullOrEmpty(Formula)) return;

            IsCalculations = true;

            if (RelationsElements == null)
            {
                CorrectExcellFormula = false;
                ErrorMessageFormula = "RelationsElements не задан";
                return;
            }

            // Карта: EValuesExportData -> CellExcell
            // Если для одной переменной в строке оказалось несколько элементов,
            // приоритет — у того, у кого CellExcell уже назначен.
            var evalToCell = new Dictionary<EValuesExportData, string>();
            foreach (var el in RelationsElements)
            {
                if (el.EValuesExportData == EValuesExportData.None) continue;

                if (evalToCell.TryGetValue(el.EValuesExportData, out var existing)
                    && !string.IsNullOrEmpty(existing))
                    continue;   // уже есть хорошее значение

                evalToCell[el.EValuesExportData] = el.CellExcell;
            }

            // Токен: буква/_, затем буквы/цифры/_ . Жадный * → Wall_L целиком.
            var tokenPattern = new Regex(@"[\p{L}_][\p{L}\p{N}_]*");
            // Похоже на Excel-ячейку: A1, F2, AA12, XFD1048576
            var cellPattern = new Regex(@"^[A-Za-z]{1,3}[0-9]+$");

            var missingCell = new HashSet<string>();
            var unknownIds = new HashSet<string>();

            FormulaExcell = tokenPattern.Replace(Formula, m =>
            {
                string token = m.Value;

                // 1) приоритет: токен — имя EValuesExportData
                if (Enum.TryParse<EValuesExportData>(token, ignoreCase: false, out var eval)
                    && eval != EValuesExportData.None
                    && Enum.IsDefined(typeof(EValuesExportData), eval))
                {
                    if (evalToCell.TryGetValue(eval, out string cell))
                    {
                        if (string.IsNullOrEmpty(cell))
                        {
                            missingCell.Add(token);
                            return token;
                        }
                        return cell;
                    }

                    // enum распознан, но такого элемента в RelationsElements нет
                    unknownIds.Add(token);
                    return token;
                }

                // 2) не переменная, но написано как Excel-ячейка → оставляем как есть
                if (cellPattern.IsMatch(token))
                    return token;

                // 3) всё прочее — неизвестный идентификатор
                unknownIds.Add(token);
                return token;
            });

            if (missingCell.Count > 0)
            {
                CorrectExcellFormula = false;
                ErrorMessageFormula = "Не задана CellExcell для: " + string.Join(", ", missingCell);
            }
            if (unknownIds.Count > 0)
            {
                CorrectExcellFormula = false;
                string msg = "Переменные не найдены в RelationsElements: " + string.Join(", ", unknownIds);
                ErrorMessageFormula = string.IsNullOrEmpty(ErrorMessageFormula)
                    ? msg
                    : ErrorMessageFormula + "; " + msg;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool IsCalculations=false;
        /// <summary>
        /// Итоговое значение посчитанное из Excell
        /// </summary>
        public double ValueDoubleCalculated;//посчитанная по формуле значение
        /// <summary>
        /// Ячейка в Excell которую занимает данная ячейка, например A1
        /// </summary>
        public string CellExcell;
        /// <summary>
        /// преобразованная в формулу Excell формула из Formula с заменой Number на CellExcell
        /// </summary>
        public string FormulaExcell;//формула для екселя
        /// <summary>
        /// Все ячейки удалось сопоставить
        /// </summary>
        public bool CorrectExcellFormula = false;
        /// <summary>
        /// если CorrectExcellFormula = false то в чем ошибка
        /// </summary>
        public string ErrorMessageFormula;

    }
    public interface IExportStringDoubleData
    {
        /// <summary>
        /// Собственное имя класса
        /// </summary>
        string NameThisClass{ get; } 
        /// <summary>
        /// подсчет и возврат 
        /// </summary>
        /// <returns></returns>
        List<ExportElement> GetCalcData();
        /// <summary>
        /// его можно снаружи обнулять для подсчета GetCalcData() заново иначе 
        /// </summary>
        List<ExportElement> _exportElements { get; set; }
    }
    public interface IElement
    {
        Element Element {  get; set; }
        ElementId ElementId { get; set; }
        long Id { get; set; }
        string Name { get; set; }
    }

    public class ECParameters
    {
        //параметры элемента для доступа к нему
        private static string MarkParam = "Марка";
        private static string ADSKGroopParam = "ADSK_Группирование";
        private static string CommentariiParam = "Комментарии";

        public string Mark;
        public string ADSKGroop;
        public string Commentarii;
        public void Initialization(Element element)
        {
            if(element == null) {  return; }
            Parameter mark = element.LookupParameter(MarkParam);
            Parameter group = element.LookupParameter(ADSKGroopParam);
            Parameter comm = element.LookupParameter(CommentariiParam);
            if (mark != null)
            {
                Mark=mark.AsString();
            }
            if (group != null)
            {
                ADSKGroopParam = mark.AsString();
            }
            if (comm != null)
            {
                CommentariiParam = mark.AsString();
            }
        }
    }


    /// <summary>
    /// Конкретный экземпляр стены
    /// </summary>
    public class ECWall: IElement,IExportStringDoubleData
    {
        public string NameThisClass
        {
            get => nameof(ECWall);
        }

        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }

        public Wall Wall;//element as Wall;

        public Curve Curve = null;//  XYZ startPoint = curve

        public ECLevel ECLevel = new ECLevel();//параметры уровня
        public ECWallType WallType = new ECWallType();//параметры типоразмера стены
        public ECArmWallElement ECArmWallElement = new ECArmWallElement();//армирование если надо армировать
        public EWallTypes EWallTypes => WallType.EWallTypes;

        public ECInsulation ECInsulation = new ECInsulation();//параметры теплоизоляции, наружная ли это стелна и тд

        // --- Другие величины 
        public ECParameters ECParameters = new ECParameters();

        public HashSet<ECRoom> ECRooms = new HashSet<ECRoom>();//комнаты к которым принадлежит стена
        // --- Геометрия ---
        public double LengthM;              // длина оси
        public double HeightM;              // фактическая высота
        public double VolumeM3;             // объём с учётом проёмов
        public double WidthM => WallType?.WidthM ?? 0.0;

        // --- Площади ---
        public double AreaNetM2;            // чистая площадь (с проёмами)  — HOST_AREA_COMPUTED
        public double AreaGrossM2;          // габаритная площадь (без проёмов)
        public double OpeningsAreaM2;       // площадь всех проёмов = Gross − Net
        public double PlanAreaM2;           // площадь в плане: Length × Width



        public HashSet<ECDoorOrWindow> ECDoorOrWindows = new HashSet<ECDoorOrWindow>();

        //зависимые друг от друга стены, которые касаются друг друга надо найти данные стены
        public HashSet<ECWall> RelationsWalls = new HashSet<ECWall>();
        public HashSet<ECWall> RelationsWallsKR = new HashSet<ECWall>();

        public List<ExportElement> _exportElements { get; set; } =null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) {  return _exportElements; } //опасно так как могу для других дел брать..

            var winds = ECDoorOrWindows.Where(x => x.TypeInsidersWalls==ETypeInsidersWalls.Window).ToList();
            var doors = ECDoorOrWindows.Where(x => x.TypeInsidersWalls == ETypeInsidersWalls.Door).ToList();
            var vitrage = ECDoorOrWindows.Where(x => x.TypeInsidersWalls == ETypeInsidersWalls.Vitrage).ToList();
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                    new ExportElement("Стены Уровень Имя",  ECLevel.Name,data, EValuesExportData.Level_Name),
                    new ExportElement("Стены Уровень Отметка",  ECLevel.LevelElevation,data, EValuesExportData.Level_Elevation),

                new ExportElement("Стены Id", Id.ToString(),data, EValuesExportData.Wall_Id),

                new ExportElement("Стены Имя", Name,data, EValuesExportData.Wall_Name),
                new ExportElement("Стены наш тип", EWallTypes.ToString(),data, EValuesExportData.Wall_EWallTypes),

                new ExportElement("Стены длина, м", LengthM,data,EValuesExportData.Wall_Length_m),
                new ExportElement("Стены высота, м", HeightM,data,EValuesExportData.Wall_Height_m),
                new ExportElement("Стены толщина, м", WidthM,data,EValuesExportData.Wall_Width_m),

                new ExportElement("Стены площадь с проемами, м2", AreaNetM2,data,EValuesExportData.Wall_AreaNetM2),
                new ExportElement("Стены площадь без проемов, м2", AreaGrossM2,data,EValuesExportData.Wall_AreaGrossM2),
                new ExportElement("Стены площадь проемов, м2", OpeningsAreaM2,data,EValuesExportData.Wall_OpeningsAreaM2,
                "Wall_AreaGrossM2-Wall_AreaNetM2"),
                new ExportElement("Стены площадь в плане, м2", PlanAreaM2,data,EValuesExportData.Wall_PlanAreaM2,
                "Wall_Length_m*Wall_Width_m" ),
                new ExportElement("Стены объём, м3", VolumeM3,data,EValuesExportData.Wall_VolumeM3),

                new ExportElement("Стена наружная?", ECInsulation.OutWallFromNAmeMaterial ? 1 : 0, data,EValuesExportData.Wall_Insulation_OutSideWallName),

                new ExportElement("Стена примыкающие стены, Id", string.Join(",", RelationsWalls.Select(x => x.Id)),data,EValuesExportData.Wall_OtherWallsIds),
                new ExportElement("Стена примыкающие стены, колво", RelationsWalls.Count,data,EValuesExportData.Wall_OtherWallsCount),
                new ExportElement("Стена примыкающие стены кр, Id", string.Join(",", RelationsWallsKR.Select(x => x.Id)),data,EValuesExportData.Wall_OtherWallsKRIds),
                new ExportElement("Стена примыкающие стены кр, колво", string.Join(",", RelationsWallsKR.Count),data,EValuesExportData.Wall_OtherWallsKRCount),
                new ExportElement("Стена окна и двери, Id", string.Join(",", ECDoorOrWindows.Select(x => x.Id)),data),
                new ExportElement("Стена окна и двери, колво", ECDoorOrWindows.Count,data, EValuesExportData.Wall_DoorsAndWindows_Count),

                new ExportElement("Стена окна, колво", winds.Count,data, EValuesExportData.Wall_Window_Count),
                  new ExportElement("Стена окна, Id", string.Join(",", winds.Select(x => x.Id)),data, EValuesExportData.Wall_Window_Ids),

                new ExportElement("Стена двери, колво", doors.Count,data,EValuesExportData.Wall_Doors_Count),
                new ExportElement("Стена двери, Id", string.Join(",", doors.Select(x => x.Id)),data, EValuesExportData.Wall_Doors_Ids),

                new ExportElement("Стена витражи, колво", vitrage.Count,data,EValuesExportData.Wall_Vitrage_Count),
                new ExportElement("Стена витражи, Id", string.Join(",", vitrage.Select(x => x.Id)),data, EValuesExportData.Wall_Vitrage_Count),


                new ExportElement("Стена окно, средняя высота, м",
                    winds.Select(x => x.HeightM).DefaultIfEmpty(0).Average(), data,EValuesExportData.Wall_Window_H_Average_m),
                new ExportElement("Стена окно, средняя длина, м",
                    winds.Select(x => x.LengthM).DefaultIfEmpty(0).Average(), data,EValuesExportData.Wall_Window_L_Average_m),
                new ExportElement("Стена дверь, средняя высота, м",
                    doors.Select(x => x.HeightM).DefaultIfEmpty(0).Average(), data,EValuesExportData.Wall_Doors_H_Average_m),
                new ExportElement("Стена дверь, средняя длина, м",
                    doors.Select(x => x.LengthM).DefaultIfEmpty(0).Average(), data,EValuesExportData.Wall_Doors_L_Average_m),

                new ExportElement("Стена комнаты, колво комнат",ECRooms.Count,data,EValuesExportData.Wall_Room_Count),
                 new ExportElement("Стена комнаты, Id", string.Join(",", ECRooms.Select(x => x.Id)),data,EValuesExportData.Wall_Room_Ids),
                 new ExportElement("Стена комнаты, номера", string.Join(",", ECRooms.Select(x => x.Number)),data, EValuesExportData.Wall_Room_Numbers),
                  new ExportElement("Стена комнаты, типы помещений", string.Join(",", ECRooms.Select(x => x.TypeOfRoom)),data, EValuesExportData.Wall_Room_Types),
                }
            );
            _exportElements = data;
            return data;
        }
    }
    /// <summary>
    /// Конкретный экземпляр уровня
    /// </summary>
    public class ECLevel: IElement, IExportStringDoubleData
    {
        public Element Element { get; set; } //Level
        public ElementId ElementId { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }//имя уровня

        public Level Level { get; set; }

        
        public double LevelElevation;

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var data = new List<ExportElement>();
            data.AddRange(
            new List<ExportElement>() 
            {
            new ExportElement("Уровень Id",Id.ToString(),data,EValuesExportData.Level_Id ),
            new ExportElement("Уровень Имя",Name,data,EValuesExportData.Level_Name ),
            new ExportElement("Уровень отметка", LevelElevation,data, EValuesExportData.Level_Elevation ){SumItog=false},

            });
            _exportElements = data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECLevel);
        }
    }
    /// <summary>
    /// Типоразмер стены, у многих стен он одинаковый
    /// </summary>
    public class ECWallType: IElement, IExportStringDoubleData
    {
        //тип стены, 
        public string Name { get; set; } //типоразмера имя стены
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public long Id { get; set; } // Рекомендуется использовать long для ID в Revit 2024+
        public string FamilyName;
        public EWallTypes EWallTypes = 0;

        public double WidthM;

        public List<ECLayerWall> ECLayerWalls = new List<ECLayerWall>();//послойный состав
        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("СтеныТип_Id",Id.ToString() ,data),
                new ExportElement("СтеныТип_Семейство",FamilyName,data ),
                new ExportElement("СтеныТип_Типоразмер",Name,data ),
                new ExportElement("СтеныТип_ТипоразмерНаш",EWallTypes.ToString() ,data),
                new ExportElement("СтеныТип_Толщина, м",WidthM ,data, EValuesExportData.Wall_Width_m),
                new ExportElement("СтеныТип_Колво_Слоев",ECLayerWalls.Count,data, EValuesExportData.WallType_LayersCount ),
               
            });
            _exportElements = data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECWallType);
        }
    }

    public enum ETypeInsidersWalls
    { 
        None=0,
        Window,
        Door,
        Vitrage,// витраж
    }

    public class ECDoorOrWindow: IElement, IExportStringDoubleData
    {
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public long Id { get; set; }  // Рекомендуется использовать long для ID в Revit 2024+
        public string Name { get; set; }
        //и надо бы имя еще и что-то еще???
        public double HeightM;
        public double LengthM;
        public double AreaM2;

        public double OtmZ;//отметка от нуля??

        public XYZ StartPt;
        public XYZ EndPt;
        public BuiltInCategory BuiltInCategory;
        public HashSet<ECWall> ECWalls = new HashSet<ECWall>();//каким стенам принадлежит


        public ETypeInsidersWalls TypeInsidersWalls;

        public ECParameters ECParameters = new ECParameters();

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var data = new List<ExportElement>();
            
            if (TypeInsidersWalls== ETypeInsidersWalls.Door)
            {
                data.AddRange(
                    new List<ExportElement>() {
                new ExportElement("Дверь",Id.ToString(),data ),
                
                new ExportElement("Дверь_Высота, м",HeightM,data, EValuesExportData.Door_Height_m),
                new ExportElement("Дверь_Длина, м",LengthM ,data,EValuesExportData.Door_Length_m),
                new ExportElement("Дверь_Площадь, м2",AreaM2,data ,EValuesExportData.Door_Area_m2, "Door_Height_m*Door_Length_m"),
                new ExportElement("Дверь_Отметка, м",OtmZ,data,EValuesExportData.Door_Level_Elevation ){SumItog=false},
                });
            }
            else if(TypeInsidersWalls == ETypeInsidersWalls.Window)
            {
                data.AddRange(
                    new List<ExportElement>() {
                new ExportElement("Окно_Id",Id.ToString(),data ),
                new ExportElement("Окно_Высота, м",HeightM,data,EValuesExportData.Window_Height_m),
                new ExportElement("Окно_Длина, м",LengthM ,data,EValuesExportData.Window_Length_m),
                new ExportElement("Окно_Площадь, м2",AreaM2,data,EValuesExportData.Window_Area_m2, "Window_Height_m*Window_Length_m"),
                new ExportElement("Окно_Отметка, м",OtmZ,data, EValuesExportData.Window_Level_Elevation ){SumItog=false},
                });
            }
            else if (TypeInsidersWalls == ETypeInsidersWalls.Vitrage)
            {
                data.AddRange(
                    new List<ExportElement>() {
                new ExportElement("Витраж_Id",Id.ToString(),data ),
                new ExportElement("Витраж_Высота, м",HeightM,data,EValuesExportData.Vitrage_Height_m),
                new ExportElement("Витраж_Длина, м",LengthM ,data,EValuesExportData.Vitrage_Length_m),
                new ExportElement("Витраж_Площадь, м2",AreaM2,data,EValuesExportData.Vitrage_Area_m2, "Vitrage_Height_m*Vitrage_Length_m"),
                new ExportElement("Витраж_Отметка, м",OtmZ,data, EValuesExportData.Vitrage_Level_Elevation ){SumItog=false},
                });
            }

            _exportElements = data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECDoorOrWindow);
        }
    }

    public class ECLayerWall: IElement, IExportStringDoubleData
    {
        public ECWallType ECWallType;
        //один слой стены
        public int Index;                        // индекс слоя: 0 — самый наружный (слева в редакторе структуры)
        public double WidthM;                    // толщина слоя, м (0 у мембранных слоёв)
        public MaterialFunctionAssignment Function; // Structure, Insulation/Fill, Finish1/Finish2, Membrane, Substrate...
        public bool IsCore;                      // несущий слой (core)
        public bool IsVariable;                  // "переменный" слой (подстраивается по высоте)
        public bool Wraps;                       // слой оборачивается вокруг вставок (окон/дверей)

        public Element Element { get; set; }
        public ElementId ElementId { get; set; }              // может быть InvalidElementId (-1) — тогда материал берётся у категории
        public long Id { get; set; }

        public string Name { get; set; }// имя материала
        public string MaterialClass;             // "Бетон", "Кирпич", "Газобетон" — как в шаблоне проекта
        public string MaterialDescription;
        public Color MaterialColor;

        public double? DensityKgM3;              // кг/м³, из структурного актива материала
        public double? ThermalConductivityWmK;   // Вт/(м·К), из теплового актива

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("СтеныСлой",Id.ToString(),data ),
                new ExportElement("СтеныСлойИндекс",Index,data ){SumItog=false},
                new ExportElement("СтеныСлойТолщинаМ",WidthM,data,EValuesExportData.WallLayer_Width_m ),

            });
            _exportElements = data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECLayerWall);
        }
    }

    public class ECInsulation: IExportStringDoubleData
    {
        public ECWall ECWall;

        public bool OutWallFromNAmeMaterial=false;
        public bool InWallFromNAmeMaterial=false;

        public int Number;// номер в цепочку стен
        public bool OutWallFromContur => Number > 0;

        public bool OutWall => OutWallFromNAmeMaterial || OutWallFromContur;

        public void Initializator(ECWall eCWall)
        {
            ECWall = eCWall;
            LengthInsideM = ECWall.LengthM;
            OutWallFromNAmeMaterial = InsulationsWalls.InsulationNameWall(eCWall);
            InWallFromNAmeMaterial = InsulationsWalls.InNameWall(eCWall);
        }
        public double LengthInsideM;   // длина по внутренней грани (видимая изнутри)

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var wallsExportElement = ECWall.GetCalcData();


            var data = new List<ExportElement>();

            EValuesGetter.AddExportElementInOurExportElements(data, wallsExportElement, new HashSet<EValuesExportData>()
            {
                EValuesExportData.Wall_Id, EValuesExportData.Wall_Name, EValuesExportData.Wall_EWallTypes ,
                 EValuesExportData.Level_Name,EValuesExportData.Level_Elevation,
                EValuesExportData.Wall_Length_m, EValuesExportData.Wall_Width_m, EValuesExportData.Wall_Height_m,
            });


            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("Стена_Утепление_Имя_Наружное",OutWallFromNAmeMaterial?1:0,data,EValuesExportData.Wall_Insulation_OutSideWallName ),
                new ExportElement("Стена_Утепление_Контур_Наружный",OutWallFromContur?1:0,data,EValuesExportData.Wall_Insulation_OutSideWallContur ),
                new ExportElement("Стена_Утепление_Номер в контуре",Number,data,EValuesExportData. Wall_Insulation_Number ),

                new ExportElement("Стена_Утепление_Длина изнутри, м",LengthInsideM,data,EValuesExportData.Wall_Insulation_LengthM ),
            });
            _exportElements = data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECInsulation);
        }
    }
    public class ECRoom: IElement, IExportStringDoubleData
    {
        public long Id { get; set; }
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }

        public string Name { get; set; }
        public string Number;
        public double AreaM2;                 // площадь в кв. футах (внутренние единицы)
        public double VolumeM3;            // объём в куб. футах
        public double PerimeterM;//периметр
        public Room Room;

        public string TypeOfRoom;//ADSK_Тип помещения

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            if (_exportElements != null) { return _exportElements; }

            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("Комната_Id",Id.ToString(),data,EValuesExportData.Room_Id),
                new ExportElement("Комната_Номер",Number,data,EValuesExportData.Room_Number),
                new ExportElement("Комната_Имя",Name,data,EValuesExportData.Room_Name),
                new ExportElement("Комната_Площадь, м2",AreaM2,data, EValuesExportData.Room_Area_m2),
                new ExportElement("Комната_Периметр, м",PerimeterM,data, EValuesExportData.Room_Perimeter_m),
                new ExportElement("Комната_Объем, м",VolumeM3,data, EValuesExportData.Room_Volume_m3),
                 new ExportElement("ADSK_Тип помещения",TypeOfRoom,data),
            });
            _exportElements = data;
            return data;
        }
        //добавь еще уровень помещения
        public string NameThisClass
        {
            get => nameof(ECRoom);
        }
    }


   
}
