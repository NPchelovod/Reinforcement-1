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
            //Установка начальных параметров
            foreach (var kvp in param)
            {
                this[kvp.Key] = kvp.Value;
            }
        }
        //public double Arm_Row_Step_m = 0.600;
        //public double Arm_Roof_Step_m = 1.5;
        //public double Arm_DopTop_Count;//дополнительная арматура сверху

        public void CalculateArm()
        {
            if (ECWall == null) return;

            var type = ECWall.EWallTypes;

            var data = new List<ExportElement>();
            //1. Считаем кол-во арматуры


            //1.1 в стенах рядов арматуры

            this[EValuesExportData.Wall_Arm_Rows]= GetGridCount(ECWall.HeightM, this[EValuesExportData.Wall_Detailed_Roof_Step_m]);
            this[EValuesExportData.Wall_Arm_Rows_Gross_m] = this[EValuesExportData.Wall_Arm_Rows]*ECWall.LengthM;
            //1.2 в стенах рядов деталей

            this[EValuesExportData.Wall_Datailed_Rows] = GetGridCount(ECWall.HeightM, this[EValuesExportData.Wall_Detailed_Row_Step_m]);

            //1.3 в стенах рядов вертикальных деталей

            this[EValuesExportData.Wall_Datailed_Colums] = GetGridCount(ECWall.LengthM, this[EValuesExportData.Wall_Detailed_Roof_Step_m]);

            //2.1 нам надо найти кол-во стен, с которыми мы пересекаемся из ЖБ
            var wallsKR = ECWall.RelationsWalls.Where(x => x.EWallTypes == EWallTypes.KR_Reinforcement).ToHashSet();
            
            _otherWalls.Clear();
            ECWall.RelationsWallsKR.Clear();
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
            //2.2 установка кол-ва жб стен
            this[EValuesExportData.Wall_OtherWallsKRCount] = _otherWalls.Count;


            //2.3 колво горизонтальных деталей
            this[EValuesExportData.Wall_Datailed_Hor_Count] = this[EValuesExportData.Wall_OtherWallsKRCount] *
                this[EValuesExportData.Wall_Datailed_Rows];

            //2.4 колво вертикальных деталей
            this[EValuesExportData.Wall_Datailed_Roof_Count] = this[EValuesExportData.Wall_Datailed_Colums];

            //2.5 итого деталей колво
            this[EValuesExportData.Wall_Datailed_Count] = this[EValuesExportData.Wall_Datailed_Roof_Count] + this[EValuesExportData.Wall_Datailed_Hor_Count];



            if (type == EWallTypes.AR_Brick)
            {
                //армирование вокруг закладных
                this[EValuesExportData.Wall_Arm_Detailes_Gross_m] = this[EValuesExportData.Wall_Arm_Detailed_Obram_Length_m] * this[EValuesExportData.Wall_Arm_Detailed_Obram_Count] * this[EValuesExportData.Wall_Datailed_Count];

                //армирование верха у самого
                this[EValuesExportData.Wall_Arm_Roof_m] = ECWall.LengthM * this[EValuesExportData.Wall_Arm_DopTop_Count];

            }

            //вычитаем армирование дверей и окон
            this[EValuesExportData.Wall_Arm_MinesArmProem_m] = 0;
            foreach (var windAndDoor in ECWall.ECDoorOrWindows)
            {
                double l = windAndDoor.LengthM;
                double h = windAndDoor.HeightM ;
                double row = GetGridCount(h, this[EValuesExportData.Wall_Detailed_Roof_Step_m]);
                if (row > 0)
                {
                    
                    if(windAndDoor.BuiltInCategory==BuiltInCategory.OST_Windows)
                    {

                        this[EValuesExportData.Wall_Arm_Row_Window] = row;

                    }
                    else if(windAndDoor.BuiltInCategory ==BuiltInCategory.OST_Doors)
                    {
                        this[EValuesExportData.Wall_Arm_Row_Door] = row;
                    }

                    //дистанция вычета
                    double dist = row * l;
                    this[EValuesExportData.Wall_Arm_MinesArmProem_m] += dist;
                }
            }

            if (this[EValuesExportData.Wall_Arm_MinesArmProem_m] > this[EValuesExportData.Wall_Arm_Rows_Gross_m])
            {
                this[EValuesExportData.Wall_Arm_MinesArmProem_m] = this[EValuesExportData.Wall_Arm_Rows_Gross_m];
            }


            //вычитаем дополнительную арматуру которая в проем попала из-за дистанции
            this[EValuesExportData.Wall_Arm_Detailed_Mines_m] = 0;
            if (type == EWallTypes.AR_Brick && this[EValuesExportData.Wall_Arm_Detailes_Gross_m] > 0)
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
                            windAndDoor.EndPt.DistanceTo(sosed.point)))/1000;
                        double dif = this[EValuesExportData.Wall_Arm_Detailed_Obram_Length_m] - df;
                        if (dif < 0) { continue; }
                        //дверь или окно близки к краю — отнимаем
                        pastPoints.Add(sosed.point);
                        //кол-во перерезов
                        double co = GetGridCount(windAndDoor.HeightM, this[EValuesExportData.Wall_Detailed_Row_Step_m]);


                        this[EValuesExportData.Wall_Arm_Detailed_Mines_m] += co * dif * this[EValuesExportData.Wall_Arm_Detailed_Obram_Count];
                    }
                }

            }
            if (this[EValuesExportData.Wall_Arm_Detailed_Mines_m] > this[EValuesExportData.Wall_Arm_Detailes_Gross_m])
            {
                this[EValuesExportData.Wall_Arm_Detailed_Mines_m] = this[EValuesExportData.Wall_Arm_Detailes_Gross_m];
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
