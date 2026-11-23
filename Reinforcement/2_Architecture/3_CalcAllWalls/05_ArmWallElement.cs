using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    

    public class ECArmWallElement : IExportStringDoubleData
    {
        public static readonly int countEValuesExportData = Enum.GetNames(typeof(EValuesExportData)).Length;
        
        private readonly double[] values = new double[countEValuesExportData];
        
      
        public double this[EValuesExportData param]
        {
            get => values[(int)param];
            set => values[(int)param] = value;
        }

        /// <summary>
        /// элемент армирования - стена
        /// </summary>
        public ECWall ECWall = null; // ссылка на стену, которой принадлежит данный элемент армирования

        private static double tolerencePoint = RevitAPI.ToFoot(10);

        public List<(XYZ point, ECWall wall)> _otherWalls = new List<(XYZ, ECWall)>(); // те, к кому мы своим торцом подходим

        public void InitialValueSet()
        {
            if (ECWall == null) return;
            var type = ECWall.EWallTypes;

            //начальные параметры для расчета заполняем
            var param = ArmElementConstants.GetConstantsParameters(type, ECWall.Element, ArmElementConstants.GetParamFromECPropElement, ArmElementConstants.GetParamOnlyNonZero);
            if(param == null) return;
            foreach (var kvp in param)
            {
                this[kvp.Key] = kvp.Value;
            }
        }
        public double Arm_Row_Step_m = 0.600;
        public double Arm_Roof_Step_m = 1.5;
        public double Arm_DopTop_Count;//дополнительная арматура сверху

        public void CalculateArm()
        {
            if (ECWall == null) return;

            var type = ECWall.EWallTypes;


            //1. Считаем кол-во арматуры
            //1.1  в крыши рядов
            this[EValuesExportData.Wall_Arm_Roofs] = GetGridCount(ECWall.LengthM, this[EValuesExportData.Wall_Arm_Brick_Row_Step_m]);
            //1.2 в стенах рядов арматуры
            this[EValuesExportData.Wall_Arm_Rows]= GetGridCount(ECWall.HeightM, this[EValuesExportData.Wall_Detailed_Brick_Roof_Step_m]);

            //нам надо найти кол-во стен, с которыми мы пересекаемся из ЖБ
            var wallsKR = ECWall.RelationsWalls.Where(x => x.EWallTypes == EWallTypes.KR_Reinforcement).ToHashSet();
            
            _otherWalls.Clear();
            if (wallsKR != null && wallsKR.Count > 0)
            {
               
                foreach (var wall in wallsKR)
                {
                    if (wall!= ECWall&& IntersectWalls.IsEndOfWall1TouchingWall2(ECWall, wall, out XYZ touchingPoint))
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
                            ECWall.RelationsWallsKR.Add(wall);
                            if (_otherWalls.Count == 2)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            //детали боковые сбоку стены
            this[EArmWall.DetailsWallRow] = GetGridCount(this[EArmWall.WallHeightMM], this[EArmWall.StepDetailsMM]);


            this[EArmWall.DetailsWallsInt] = this[EArmWall.DetailsWallRow] * _otherWalls.Count;

            this[EArmWall.ArmRow] = GetGridCount(this[EArmWall.WallHeightMM], this[EArmWall.ArmStepMM]);
            this[EArmWall.ArmGeneralTotalMM] = this[EArmWall.ArmRow] * this[EArmWall.WallLengthMM]; // кол-во рядов на длину

            this[EArmWall.ArmTopTotalMM] = 0;
            this[EArmWall.ArmDetailsTotalMM] = 0;
            if (type == EWallTypes.AR_Brick)
            {
                this[EArmWall.ArmTopTotalMM] += this[EArmWall.ArmTopDopRow] * this[EArmWall.WallLengthMM]; // дополнительная арматура вверху кирпичной стены

                //1000*2*
                this[EArmWall.ArmDetailsTotalMM] += this[EArmWall.ArmDetailArmLengthMM] * this[EArmWall.ArmDetailArmCount] * this[EArmWall.DetailsWallsInt];

                //тут надо проанализировать двери и т.д. — вычитания
            }

            //вычитаем армирование дверей и окон
            this[EArmWall.MinesArmFromWindAndDoorMM] = 0;
            //if(ArmElementConstants.)
            //пока обнуляем их для надежности
            this[EArmWall.ArmRowsCutByWindow] = 0;
            this[EArmWall.ArmRowsCutByDoor] = 0;
            this[EArmWall.ArmRowsCutByCurtain] = 0;
            foreach (var windAndDoor in ECWall.ECDoorOrWindows)
            {
                double l = windAndDoor.LengthM * 1000;
                double h = windAndDoor.HeightM * 1000;
                double row = GetGridCount(h, this[EArmWall.ArmStepMM]);
                if (row > 0)
                {
                    
                    if(windAndDoor.BuiltInCategory==BuiltInCategory.OST_Windows)
                    {
                        
                        this[EArmWall.ArmRowsCutByWindow] = row;
                            
                    }
                    else if(windAndDoor.BuiltInCategory ==BuiltInCategory.OST_Doors)
                    {
                        this[EArmWall.ArmRowsCutByDoor] = row;
                    }


                    double dist = row * l;
                    this[EArmWall.MinesArmFromWindAndDoorMM] += dist;

                }
            }

            if (this[EArmWall.MinesArmFromWindAndDoorMM] > this[EArmWall.ArmGeneralTotalMM])
            {
                this[EArmWall.MinesArmFromWindAndDoorMM] = this[EArmWall.ArmGeneralTotalMM];
            }

            this[EArmWall.MinesArmDetailsFromWindAndDoorMM] = 0;
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
                        this[EArmWall.MinesArmDetailsFromWindAndDoorMM] += co * dif * this[EArmWall.ArmDetailArmCount];
                    }
                }

                if (this[EArmWall.MinesArmDetailsFromWindAndDoorMM] > this[EArmWall.ArmDetailsTotalMM])
                {
                    this[EArmWall.MinesArmDetailsFromWindAndDoorMM] = this[EArmWall.ArmDetailsTotalMM];
                }
            }


            //подсчитываем итого
            //итого деталей
            this[EArmWall.DetailsAllCount] = this[EArmWall.DetailsRoofInt] + this[EArmWall.DetailsWallsInt];
            //итого арматуры
            double lenArmAllM = Math.Max(0, (this[EArmWall.ArmGeneralTotalMM] + this[EArmWall.ArmTopTotalMM] + this[EArmWall.ArmDetailsTotalMM] - this[EArmWall.MinesArmDetailsFromWindAndDoorMM] - this[EArmWall.MinesArmFromWindAndDoorMM]) / 1000);
            this[EArmWall.ArmAllM] = lenArmAllM * this[EArmWall.ArmCountCrossRow];

            //если арматура раскладывается в два взаимно перпендикулярных направления в одном шве, умножение на ширину даст не площадь сетки, а площадь «поверхности, покрытой стержнями одной ориентации»
            this[EArmWall.ArmAllM2] = this[EArmWall.ArmAllM] * this[EArmWall.WallWidthMM] / 1000;
        }


        public static double GetGridCount(double height, double step)
        {
            //количество сеток по высоте стены
            if (step <= 0)
                return 0;

            return (int)Math.Floor(height / step);
        }

        public List<ExportElement> _exportElements { get; set; } = null;
        public List<ExportElement> GetCalcData()
        {
            //if (_exportElements != null) {  return _exportElements; } опасно так как могу для других дел брать..

            var data = new List<ExportElement>()
            {
              
            };

            var wallsExportElement = ECWall.GetCalcData();

            EValuesGetter.AddExportElementInOurExportElements(data, wallsExportElement, new HashSet<EValuesExportData>()
            { 
                EValuesExportData.Wall_Id, EValuesExportData.Wall_Name, EValuesExportData.Wall_EWallTypes ,
                EValuesExportData.Wall_Length_m, EValuesExportData.Wall_Width_m, EValuesExportData.Wall_Height_m,
                EValuesExportData.Wall_AreaNetM2, EValuesExportData.Wall_AreaGrossM2, EValuesExportData.Wall_OpeningsAreaM2,
                EValuesExportData.Wall_PlanAreaM2, EValuesExportData. Wall_VolumeM3,

                 EValuesExportData.Wall_OtherWallsIds, EValuesExportData.  Wall_OtherWallsCount,
                EValuesExportData.Wall_OtherWallsKRIds, EValuesExportData.Wall_OtherWallsKRCount, EValuesExportData.Wall_Room_Count,

                 EValuesExportData.Wall_Doors_Ids,EValuesExportData.Wall_Doors_Count,
                EValuesExportData.Wall_Doors_L_Average_m,EValuesExportData.Wall_Doors_H_Average_m,

                 EValuesExportData.Wall_Window_Ids,EValuesExportData.Wall_Window_Count,
                 EValuesExportData.Wall_Window_L_Average_m,EValuesExportData.Wall_Window_H_Average_m,

            });

            foreach (var item in ArmElementConstants.EArmWallStringNames)
            {
                var a = new ExportElement(item.Value, this[item.Key], data);
                data.Add(a);
            }
            foreach (var item in ArmElementConstants.EArmWallNames)
            {
                var a = new ExportElement(item.Value, this[item.Key], data);
                data.Add(a);
            }


            _exportElements= data;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECArmWallElement);
        }
    }
}
