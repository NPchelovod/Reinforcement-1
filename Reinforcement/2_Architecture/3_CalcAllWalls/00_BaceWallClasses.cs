using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
    /// <summary>
    /// Класс для экспорта в эксель данных
    /// </summary>
    public class ExportElement
    {
        
        /// <summary>
        /// номер для взаимных ссылок и записи формулы лучше чтобы он бы строчным, иначе можно запутаться. Например "а" или "б"
        /// </summary>
        public string Number;
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
        public bool SumItog = false;

        public ExportElement( string name, double valueD, List<ExportElement> relationsElements, string number="", string formula="")
        {
            Number= number;
            Name = name;
            ValueDouble = valueD;
            ExportDoubleOrString = true;
            RelationsElements = relationsElements;
            Formula = formula;
        }
        public ExportElement( string name, string valueS, List<ExportElement> relationsElements)
        {
            //Number= number;
            Name = name;
            ValueString = valueS;
            ExportDoubleOrString = false;
            RelationsElements = relationsElements;
        }
        /// <summary>
        /// Список к которому мы принадлежим чтобы можно было посчитаться автоматически
        /// </summary>
        public List<ExportElement> RelationsElements = null;
        /// <summary>
        /// Формула вида (а+б)*2
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

            var numberToCell = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var el in RelationsElements)
            {
                if (string.IsNullOrEmpty(el.Number)) continue;
                numberToCell[el.Number] = el.CellExcell;
            }

            // Токен: буква/_, затем буквы/цифры/_ . Жадный * → "l1" целиком.
            var tokenPattern = new Regex(@"[\p{L}_][\p{L}\p{N}_]*");
            // Похоже на Excel-ячейку: A1, F2, AA12, XFD1048576
            var cellPattern = new Regex(@"^[A-Za-z]{1,3}[0-9]+$");

            var missingCell = new HashSet<string>();
            var unknownIds = new HashSet<string>();

            FormulaExcell = tokenPattern.Replace(Formula, m =>
            {
                string token = m.Value;

                // 1) приоритет: если это Number — подменяем на его CellExcell
                if (numberToCell.TryGetValue(token, out string cell))
                {
                    if (string.IsNullOrEmpty(cell))
                    {
                        missingCell.Add(token);
                        return token;
                    }
                    return cell;
                }

                // 2) не Number, но написано как Excel-ячейка → значит это уже готовое
                //    имя ячейки, вписанное руками в Formula. Оставляем как есть.
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
                string msg = "Идентификаторы не найдены в RelationsElements: " + string.Join(", ", unknownIds);
                ErrorMessageFormula = string.IsNullOrEmpty(ErrorMessageFormula)
                    ? msg
                    : ErrorMessageFormula + "; " + msg;
            }
        }

        public bool IsCalculations=false;
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
        string NameThisClass{ get; }
        List<ExportElement> GetCalcData();
    }
    public interface IElement
    {
        Element Element {  get; set; }
        ElementId ElementId { get; set; }
        long Id { get; set; }
        string Name { get; set; }
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

        public List<ExportElement> GetCalcData()
        {
            var winds = ECDoorOrWindows.Where(x => x.BuiltInCategory == BuiltInCategory.OST_Windows).ToList();
            var doors = ECDoorOrWindows.Where(x => x.BuiltInCategory == BuiltInCategory.OST_Doors).ToList();

            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("Стены Id", Id,data),

                new ExportElement("Стены Имя", Name,data),
                new ExportElement("Стены наш тип", EWallTypes.ToString(),data),

                new ExportElement("Стены длина, м", LengthM,data),
                new ExportElement("Стены высота, м", HeightM,data),
                new ExportElement("Стены толщина, м", WidthM,data),

                new ExportElement("Стены площадь с проемами, м2", AreaNetM2,data),
                new ExportElement("Стены площадь без проемов, м2", AreaGrossM2,data),
                new ExportElement("Стены площадь проемов, м2", OpeningsAreaM2,data),
                new ExportElement("Стены площадь в плане, м2", PlanAreaM2,data),

                new ExportElement("Стена наружная?", ECInsulation.OutWallFromNAmeMaterial ? 1 : 0,data),

                new ExportElement("Стена примыкающие стены, Id", string.Join(",", RelationsWalls.Select(x => x.Id)),data),
                new ExportElement("Стена примыкающие стены кр, Id", string.Join(",", RelationsWallsKR.Select(x => x.Id)),data),

                new ExportElement("Стена окна и двери, Id", string.Join(",", ECDoorOrWindows.Select(x => x.Id)),data),
                new ExportElement("Стена окна и двери, колво", ECDoorOrWindows.Count,data),

                new ExportElement("Стена окна, колво", winds.Count,data),
                  new ExportElement("Стена окна, Id", string.Join(",", winds.Select(x => x.Id)),data),

                new ExportElement("Стена двери, колво", doors.Count,data),
                new ExportElement("Стена двери, Id", string.Join(",", doors.Select(x => x.Id)),data),

                new ExportElement("Стена окно, средняя высота, м", winds.Select(x => x.HeightM).Average(),data),
                new ExportElement("Стена окно, средняя длина, м", winds.Select(x => x.LengthM).Average(),data),

                new ExportElement("Стена дверь, средняя высота, м", doors.Select(x => x.HeightM).Average(),data),
                new ExportElement("Стена дверь, средняя длина, м", doors.Select(x => x.LengthM).Average(),data),
                }
            );

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

        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
            new List<ExportElement>() 
            {
            new ExportElement("Уровень Id",Id,data ),
            new ExportElement("Уровень Имя",Name,data ),
            new ExportElement("Уровень отметка", LevelElevation,data ),

            });

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
        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("СтеныТип_Id",Id ,data),
                new ExportElement("СтеныТип_Семейство",FamilyName,data ),
                new ExportElement("СтеныТип_Типоразмер",Name,data ),
                new ExportElement("СтеныТип_ТипоразмерНаш",EWallTypes.ToString() ,data),
                new ExportElement("СтеныТип_Толщина, м",WidthM ,data),
                new ExportElement("СтеныТип_Колво_Слоев",ECLayerWalls.Count,data ),
               
            });
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECWallType);
        }
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
        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("ДверьОкно_Id",Id,data ),
                new ExportElement("ДверьОкно_Это дверь?",BuiltInCategory==BuiltInCategory.OST_Doors?1:0,data ),
                new ExportElement("ДверьОкно_Высота, м",HeightM,data,"h" ),
                new ExportElement("ДверьОкно_Длина, м",LengthM ,data,"l"),
                new ExportElement("ДверьОкно_Площадь, м2",AreaM2,data,"A","l*h" ),
                new ExportElement("ДверьОкно_Отметка, м",OtmZ,data ),
            });

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

        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("СтеныСлой",Id,data ),
                new ExportElement("СтеныСлойИндекс",Index,data ),
                new ExportElement("СтеныСлойТолщинаМ",WidthM,data ),

            });

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
        public bool OutWallFromNAmeMaterial = false;//наружная ли стена, при анализе имени стены
        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("Стена_Утепление_Имя_Наружное",OutWallFromNAmeMaterial?1:0,data ),
               
            });

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

        public List<ExportElement> GetCalcData()
        {
            var data = new List<ExportElement>();
            data.AddRange(
                new List<ExportElement>() {
                new ExportElement("Комната_Id",Id,data),
                new ExportElement("Комната_Номер",Number,data),
                new ExportElement("Комната_Имя",Name,data),
                new ExportElement("Комната_Площадь, м2",AreaM2,data),
                new ExportElement("Комната_Периметр, м",PerimeterM,data),
                new ExportElement("Комната_Объем, м",VolumeM3,data),
            });

            return data;
        }
        //добавь еще уровень помещения
        public string NameThisClass
        {
            get => nameof(ECRoom);
        }
    }


   
}
