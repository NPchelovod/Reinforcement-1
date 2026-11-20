using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement.Aspirant;
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

        ArmRow,                // количество рядов армирования на этаж
        ArmDopRow,             // количество дополнительных рядов армирования (для кирпича)
        ArmStepMM,             // шаг рядов армирования, мм
        ArmDetailArmLengthMM,  // длина арматуры обрамления закладной, мм
        ArmDetailArmCount,     // количество арматурин на одну деталь обрамления
        Details,               // количество деталей крепежа
        StepDetailsMM,         // шаг деталей крепежа, мм
        StepDetailsRoofMM,     // шаг крепежа плоскости стены к потолку, мм

       

        DetailsRoofInt,        // количество крепежа по длине стены (плоскость к потолку)
        DetailsWallsInt,       // количество боковых деталей (у примыканий к ЖБ-стенам)

        ArmGeneralTotalMM,     // армирование основное, мм (без учёта проёмов)
        ArmTopTotalMM,         // армирование верха кирпичной стены, мм
        ArmDetailsTotalMM,     // арматура обрамления закладных кирпичной стены, мм

        MinesArmFromWindAndDoor,      // вычет основного армирования по проёмам, мм
        MinesArmDetailsFromWindAndDoor // вычет арматуры обрамления по проёмам, мм
    }
    /// <summary>
    /// Строковые данные
    /// </summary>
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


    public class ECArmWallElement
    {
        public static readonly int countEArmWall = Enum.GetNames(typeof(EArmWall)).Length;
        public static readonly int countEArmWallString = Enum.GetNames(typeof(EArmWallString)).Length;
        private readonly double[] values = new double[countEArmWall];
        private readonly string[] valuesStr = new string[countEArmWallString];
        /// <summary>
        /// Доступ к параметру армирования по индексу enum.
        /// Пример: el[EArmWall.ArmStepMM] = 600;
        /// </summary>
        public double this[EArmWall type]
        {
            get => values[(int)type];
            set => values[(int)type] = value;
        }
        public string this[EArmWallString type]
        {
            get => valuesStr[(int)type];
            set => valuesStr[(int)type] = value;
        }

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
            { EArmWall.ArmDopRow,                      "Доп. ряды армирования (кирпич)" },
            { EArmWall.ArmStepMM,                      "Шаг армирования, мм" },
            { EArmWall.ArmDetailArmLengthMM,           "Длина арматуры обрамления, мм" },
            { EArmWall.ArmDetailArmCount,              "Кол-во арматурин на деталь" },
            { EArmWall.Details,                        "Кол-во деталей крепежа" },
            { EArmWall.StepDetailsMM,                  "Шаг деталей крепежа, мм" },
            { EArmWall.StepDetailsRoofMM,              "Шаг крепежа к потолку, мм" },

            { EArmWall.DetailsRoofInt,                 "Кол-во крепежа по длине (потолок)" },
            { EArmWall.DetailsWallsInt,                "Кол-во боковых деталей" },

            { EArmWall.ArmGeneralTotalMM,              "Армирование основное, мм" },
            { EArmWall.ArmTopTotalMM,                  "Армирование верха, мм" },
            { EArmWall.ArmDetailsTotalMM,              "Арматура обрамления закладных, мм" },

            { EArmWall.MinesArmFromWindAndDoor,        "Вычет армирования по проёмам, мм" },
            { EArmWall.MinesArmDetailsFromWindAndDoor, "Вычет обрамления по проёмам, мм" }
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

        //элемент армирования
        public ECWall ECWall = null; // ссылка на стену, которой принадлежит данный элемент армирования

        private static double tolerencePoint = RevitAPI.ToFoot(10);

        public List<(XYZ point, ECWall wall)> _otherWalls = new List<(XYZ, ECWall)>(); // те, к кому мы своим торцом подходим

        public void InitialValueSet()
        {
            if (ECWall == null) return;
            var type = ECWall.EWallTypes;

            //надо бы проверять заполненные ли параметры
            if (type == EWallTypes.AR_Brick)
            {
                this[EArmWall.ArmStepMM] = 600;
                this[EArmWall.StepDetailsMM] = 600;

                this[EArmWall.StepDetailsRoofMM] = 1500;
                this[EArmWall.ArmDopRow] = 3;
                this[EArmWall.ArmDetailArmLengthMM] = 1000;
                this[EArmWall.ArmDetailArmCount] = 2; // на одну деталь 2 арматурины
            }
            else if (type == EWallTypes.AR_Aerocrete)
            {
                this[EArmWall.ArmStepMM] = 750;
                this[EArmWall.StepDetailsMM] = 1250;
                this[EArmWall.StepDetailsRoofMM] = 3000;
            }
        }

        public void CalculateArm()
        {
            if (ECWall == null) return;

            var type = ECWall.EWallTypes;

           
            this[EArmWallString.LevelName] = ECWall.ECLevel.LevelName;
            this[EArmWallString.WallEWallTypes]= type.ToString();
            this[EArmWallString.WallFamily] = ECWall.WallType.FamilyName;
            this[EArmWallString.WallType] = ECWall.WallType.NameWall;
            this[EArmWallString.IdsWindAndDoors]= string.Join(", ", ECWall.ECDoorOrWindows.Select(x=>x.Id));
            this[EArmWallString.IdsConnectsWalls] = string.Join(", ", ECWall.RelationsWalls.Select(x => x.Id));
            this[EArmWall.LevelHM] = ECWall.ECLevel.LevelElevation;
            this[EArmWall.WallId]= ECWall.Id;

           this[EArmWall.WallHeightMM] = ECWall.HeightM * 1000;
            this[EArmWall.WallLengthMM] = ECWall.LengthM * 1000;
            this[EArmWall.WallWidthMM] = ECWall.WidthM * 1000;
            this[EArmWall.WallAreaNetM2] = ECWall.AreaNetM2;
            this[EArmWall.WallAreaGrossM2] = ECWall.AreaGrossM2;
            this[EArmWall.WallOpeningsAreaM2] = ECWall.OpeningsAreaM2;
            this[EArmWall.WallPlanAreaM2] = ECWall.PlanAreaM2;
            this[EArmWall.CountDoorOrWindow]= ECWall.ECDoorOrWindows.Count;

            //1. Считаем кол-во деталей
            this[EArmWall.DetailsRoofInt] = GetGridCount(this[EArmWall.WallLengthMM], this[EArmWall.StepDetailsRoofMM]);

            //нам надо найти кол-во стен, с которыми мы пересекаемся из ЖБ
            var wallsKR = ECWall.RelationsWalls.Where(x => x.EWallTypes == EWallTypes.KR_Reinforcement).ToList();

            _otherWalls.Clear();
            if (wallsKR != null && wallsKR.Count > 0)
            {
                foreach (var wall in wallsKR)
                {
                    if (IntersectWalls.IsEndOfWall1TouchingWall2(ECWall, wall, out XYZ touchingPoint))
                    {
                        //проверяем прошлые стены
                        bool adp = true;
                        foreach (var point in _otherWalls)
                        {
                            double dist = point.point.DistanceTo(touchingPoint);
                            if (dist < tolerencePoint)
                            {
                                adp = false;
                                break;
                            }
                        }
                        if (adp)
                        {
                            _otherWalls.Add((touchingPoint, wall));
                            if (_otherWalls.Count == 2)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            

           this[EArmWallString.IdsConnectsWallsKR] = string.Join(", ", _otherWalls.Select(x => x.wall.Id));

            this[EArmWall.CountKRBorder] = _otherWalls.Count;
            //детали боковые сбоку стены
            this[EArmWall.DetailsWallsInt] = GetGridCount(this[EArmWall.WallHeightMM], this[EArmWall.StepDetailsMM]) * _otherWalls.Count();

            this[EArmWall.ArmRow] = GetGridCount(this[EArmWall.WallHeightMM], this[EArmWall.ArmStepMM]);
            this[EArmWall.ArmGeneralTotalMM] = this[EArmWall.ArmRow] * this[EArmWall.WallLengthMM]; // кол-во рядов на длину

            this[EArmWall.ArmTopTotalMM] = 0;
            this[EArmWall.ArmDetailsTotalMM] = 0;
            if (type == EWallTypes.AR_Brick)
            {
                this[EArmWall.ArmTopTotalMM] += this[EArmWall.ArmDopRow] * this[EArmWall.WallLengthMM]; // дополнительная арматура вверху кирпичной стены

                this[EArmWall.ArmDetailsTotalMM] += this[EArmWall.ArmDetailArmLengthMM] * this[EArmWall.ArmDetailArmCount] * this[EArmWall.DetailsWallsInt];

                //тут надо проанализировать двери и т.д. — вычитания
            }

            //вычитаем армирование дверей и окон
            this[EArmWall.MinesArmFromWindAndDoor] = 0;
            foreach (var windAndDoor in ECWall.ECDoorOrWindows)
            {
                double l = windAndDoor.LengthM;
                double h = windAndDoor.HeightM;
                double row = GetGridCount(h, this[EArmWall.ArmStepMM]);
                double dist = row * l;
                this[EArmWall.MinesArmFromWindAndDoor] += dist;
            }

            if (this[EArmWall.MinesArmFromWindAndDoor] > this[EArmWall.ArmGeneralTotalMM])
            {
                this[EArmWall.MinesArmFromWindAndDoor] = this[EArmWall.ArmGeneralTotalMM];
            }

            this[EArmWall.MinesArmDetailsFromWindAndDoor] = 0;
            if (type == EWallTypes.AR_Brick && this[EArmWall.ArmDetailsTotalMM] > 0)
            {
                List<XYZ> pastPoints = new List<XYZ>();
                //вычитаем доп. армирование, если его дверь пересекает или окно
                foreach (var sosed in _otherWalls)
                {
                    foreach (var windAndDoor in ECWall.ECDoorOrWindows)
                    {
                        //Смысл блока — «если дверь ближе 1000 мм к примыканию,
                        //отнимаем ту часть арматуры обрамления, которая реально не влезла»
                        //определяем дистанцию

                        //!!! Может быть задевание — тогда надо координату проверять...

                        double df = RevitAPI.ToMm(Math.Min(
                            windAndDoor.StartPt.DistanceTo(sosed.point),
                            windAndDoor.EndPt.DistanceTo(sosed.point)));
                        double dif = this[EArmWall.ArmDetailArmLengthMM] - df;
                        if (dif < 0) { continue; }
                        //дверь или окно близки к краю — отнимаем
                        pastPoints.Add(sosed.point);
                        //кол-во перерезов
                        double co = GetGridCount(windAndDoor.HeightM * 1000, this[EArmWall.StepDetailsMM]);
                        this[EArmWall.MinesArmDetailsFromWindAndDoor] += co * dif * this[EArmWall.ArmDetailArmCount];
                    }
                }

                if (this[EArmWall.MinesArmDetailsFromWindAndDoor] > this[EArmWall.ArmDetailsTotalMM])
                {
                    this[EArmWall.MinesArmDetailsFromWindAndDoor] = this[EArmWall.ArmDetailsTotalMM];
                }
            }
        }


        public static double GetGridCount(double height, double step)
        {
            //количество сеток по высоте стены
            if (step <= 0)
                return 0;

            return (int)Math.Floor(height / step);
        }
    }


    [Transaction(TransactionMode.Manual)]
    public class ArmWalls : IExternalCommand
    {
        public Result Execute(
           ExternalCommandData commandData,
           ref string message,
           ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;
            Document doc = uidoc.Document;
            //сбор всех стен
            var eCWalls = ReadAndGet(doc);


            //затем мои методы подсчета, мой алгоритм
            return Result.Succeeded;
        }

        public static List<ECWall> ReadAndGet(Document doc)
        {
            var eCWalls = CalcWalls.GetWalls(doc);

            //обрезаем наши стены тут стены КР есть
            eCWalls = GetArmCalcWalls(eCWalls);

            //находим пересечения с ними - все стены друг с другом
            IntersectWalls.CalcIntersects(doc, eCWalls);
            //затем инициируем начальные данные или сразу рассчитываем??
            //но тут можно записывать начальные данные

            //рассчитываем
            CalculateArm(doc, eCWalls);

            eCWalls = GetArmExportWalls(eCWalls);
            ArmExport.Export(eCWalls);

            //возврат стен не КР!
            return eCWalls;
        }

        /// <summary>
        /// Получение только тех стен которые будут участвовать в наших расчетах
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="eCWalls"></param>
        /// <returns></returns>
        private static List<ECWall> GetArmCalcWalls( List<ECWall> eCWalls)
        {
            return eCWalls.Where(x => EWallTypesForCalc.Contains(x.EWallTypes)).ToList();
        }
        private static List<ECWall> GetArmExportWalls(List<ECWall> eCWalls)
        {
            return eCWalls.Where(x => ArmEWallTypes.Contains(x.EWallTypes)).ToList();
        }

        private static HashSet<EWallTypes> EWallTypesForCalc = new HashSet<EWallTypes>() { EWallTypes.KR_Reinforcement, EWallTypes.AR_Brick, EWallTypes.AR_Aerocrete };

        public static HashSet<EWallTypes> ArmEWallTypes = new HashSet<EWallTypes>() { EWallTypes.AR_Brick, EWallTypes.AR_Aerocrete };


        private static void CalculateArm(Document doc, List<ECWall> eCWalls)
        {
            foreach (var wall in eCWalls)
            {
                var type = wall.EWallTypes;
                if (!ArmEWallTypes.Contains(type)) { continue; }
                CalcOneArm(wall, type);
            }
        }
        private static void CalcOneArm(ECWall wall, EWallTypes eWallTypes)
        {
            var el = wall.ECArmWallElement;
            el.ECWall = wall;
            el.InitialValueSet();
            el.CalculateArm();//расчет армирования стены
        }

    }
}
