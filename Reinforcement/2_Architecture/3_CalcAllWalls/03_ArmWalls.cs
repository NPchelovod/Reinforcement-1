using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    public class ECArmWallElement
    {
        //элемент армирования    
        public ECWall ECWall=null;// ссылка на стену которой принадлежит данный элемент армирвоания

        public double EC_ArmRow = 0;//количество рядов армирования на этаж
        public double EC_ArmDopRow = 0;//колво рядов армирования дополнительно (для кирпича)
        public double EC_ArmStepMM = 0;
        public double EC_ArmDetailArmLengthMM = 0;//длина арматуры обрамления закладной
        public double EC_ArmDetailArmCount = 0;//колво доп арматуры

        public double EC_Details = 0;
        public double EC_StepDetailsMM = 0;

        public double EC_StepDetailsRoofMM = 0;//шаг крепежа плоскости стены к потолку

        public void InitialValueSet()
        {
            if(ECWall == null) return;
            var type = ECWall.EWallTypes;
          
            //надо бы проверять заполненные ли параметры
            if (type == EWallTypes.AR_Brick)
            {
                EC_ArmStepMM = 600;
                EC_StepDetailsMM = 600;

                EC_StepDetailsRoofMM = 1500;
                EC_ArmDopRow = 3;
                EC_ArmDetailArmLengthMM = 1000;
                EC_ArmDetailArmCount = 2;//на одну деталь 2 арматурины
            }
            else if(type == EWallTypes.AR_Aerocrete)
            {
                EC_ArmStepMM = 750;
                EC_StepDetailsMM = 1250;
                EC_StepDetailsRoofMM = 3000;
            }

        }
        private static double tolerencePoint = RevitAPI.ToFoot(10);

        public double _wallHeightMM;
        public double _wallLenghtMM;
        public double _wallWidthMM;

        public double _detailsRoofInt;
        public List<(XYZ point, ECWall wall)> _otherWalls = new List<(XYZ, ECWall)> ();//те к кому мы свои торцом подходим

        public double _detailsWallsInt;

        public double _armGeneralTotalMM;// арм основная без учетов проемов
        public double _armTopTotalMM;// арм верха кирпичной стены
        public double _armDetaisTotalMM;//арматура обрамления закладных у кирпичной стены


        public double _minesArmFromWindAndDoor;
        public double _minesArmDetaisFromWindAndDoor;

        public void CalculateArm()
        {
            if (ECWall == null) return;

            var type = ECWall.EWallTypes;

            _wallHeightMM = ECWall.HeightM * 1000;
            _wallLenghtMM = ECWall.LengthM * 1000;
            _wallWidthMM = ECWall.WidthM * 1000;

            //1. Считаем кол-во деталей
            _detailsRoofInt = GetGridCount(_wallLenghtMM, EC_StepDetailsRoofMM);

            //нам надо найти кол-во стен с которыми мы пересекаемся из жб
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
            //детали боковые сбоку стены
            _detailsWallsInt = GetGridCount(_wallHeightMM, EC_StepDetailsMM) * _otherWalls.Count();


            EC_ArmRow = GetGridCount(_wallHeightMM, EC_ArmStepMM);
            _armGeneralTotalMM = EC_ArmRow * _wallLenghtMM; //колво рядов на длину

            _armTopTotalMM = 0;
            _armDetaisTotalMM = 0;
            if (type == EWallTypes.AR_Brick)
            {
                _armTopTotalMM += EC_ArmDopRow * _wallLenghtMM;//дополнительная арматура вверху стены кирпичной

                _armDetaisTotalMM += EC_ArmDetailArmLengthMM * EC_ArmDetailArmCount * _detailsWallsInt;

                //тут надо проанализировать двери и тд вычитания
            }

            //вычитаем армирвоание дверей и окон
            _minesArmFromWindAndDoor = 0;
            foreach (var windAndDoor in ECWall.ECDoorOrWindows)
            {
                double l = windAndDoor.LengthM;
                double h = windAndDoor.HeightM;
                double row = GetGridCount(h, EC_ArmStepMM);
                double dist = row * l;
                _minesArmFromWindAndDoor += dist;
            }

            if(_minesArmFromWindAndDoor > _armGeneralTotalMM)
            {
                _minesArmFromWindAndDoor = _armGeneralTotalMM;
            }

            _minesArmDetaisFromWindAndDoor = 0;
            if (type == EWallTypes.AR_Brick && _armDetaisTotalMM > 0)
            {
                List<XYZ> pastPoints = new List<XYZ>();
                //вычитаем доп армирвоание если его дверь пересекает или окно
                foreach (var sosed in _otherWalls)
                {
                    foreach (var windAndDoor in ECWall.ECDoorOrWindows)
                    {

                        //Смысл блока — «если дверь ближе 1000 мм к примыканию, отнимаем ту часть арматуры обрамления, которая реально не влезла»
                        //определяем дистанцию

                        //!!! Может быть задковение так то надо координату проверять...

                        double df =RevitAPI.ToMm( Math.Min(windAndDoor.StartPt.DistanceTo(sosed.point), windAndDoor.EndPt.DistanceTo(sosed.point)));
                        double dif = EC_ArmDetailArmLengthMM-df;
                        if (dif < 0) { continue; }
                        //дверь или окно близки к краю, отнимаем
                        pastPoints.Add(sosed.point);
                        //колво перерезов
                        double co = GetGridCount(windAndDoor.HeightM * 1000, EC_StepDetailsMM);
                        _minesArmDetaisFromWindAndDoor += co * dif * EC_ArmDetailArmCount;
                    }
                }

                if(_minesArmDetaisFromWindAndDoor > _armDetaisTotalMM)
                {
                    _minesArmDetaisFromWindAndDoor = _armDetaisTotalMM;
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

            //обрезаем наши стены
            eCWalls = GetArmCalcWalls(doc, eCWalls);

            //находим пересечения с ними - все стены друг с другом
            IntersectWalls.CalcIntersects(doc, eCWalls);
            //затем инициируем начальные данные или сразу рассчитываем??
            //но тут можно записывать начальные данные

            //рассчитываем
            CalculateArm(doc, eCWalls);
            ArmExport.Export(eCWalls);
            return eCWalls;
        }

        /// <summary>
        /// Получение только тех стен которые будут участвовать в наших расчетах
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="eCWalls"></param>
        /// <returns></returns>
        private static List<ECWall> GetArmCalcWalls(Document doc, List<ECWall> eCWalls)
        {
            return eCWalls.Where(x => EWallTypesForCalc.Contains(x.EWallTypes)).ToList();
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
