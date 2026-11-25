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

        private bool initial = false;
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
            initial = true;
        }
        //public double Arm_Row_Step_m = 0.600;
        //public double Arm_Roof_Step_m = 1.5;
        //public double Arm_DopTop_Count;//дополнительная арматура сверху

        public void CalculateArm()
        {
            if (ECWall == null ||!initial) return;

            var type = ECWall.EWallTypes;

            var data = new List<ExportElement>();
            //1. Считаем кол-во арматуры
            bool armIsStep = this[EValuesExportData.Wall_Arm_Bool_CalcFromStepOrRows] > 0.1;//значит армирование считаем из шага, иначе - шаг предопределен
            bool calcDet=ArmElementConstants.ExportDetailCalculation;
            //1.1 в стенах рядов арматуры
            if (armIsStep)
            {
               
                this[EValuesExportData.Wall_Arm_Rows] = GetGridCount(ECWall.HeightM, this[EValuesExportData.Wall_Arm_Row_Step_m]);
                data.Add(new ExportElement("стены армирования рядов горизонтальных, штук", this[EValuesExportData.Wall_Arm_Rows], data,
                    EValuesExportData.Wall_Arm_Rows));
            }
            //арматура как будто нет окон и дверей
            this[EValuesExportData.Wall_Arm_Rows_Gross_m] = this[EValuesExportData.Wall_Arm_Rows]*ECWall.LengthM;
            data.Add(new ExportElement("стены армирование рядов, без вычета, пог.м ", this[EValuesExportData.Wall_Arm_Rows_Gross_m], data,
                EValuesExportData.Wall_Arm_Rows_Gross_m, "Wall_Arm_Rows*Wall_Length_m"));

            //1.2 в стенах рядов деталей
            if (armIsStep)
            {
                this[EValuesExportData.Wall_Detailed_Rows] = GetGridCount(ECWall.HeightM, this[EValuesExportData.Wall_Detailed_Row_Step_m]);
            }

            if (calcDet)
            {
                data.Add(new ExportElement("стены деталей рядов", this[EValuesExportData.Wall_Detailed_Rows], data,
                    EValuesExportData.Wall_Detailed_Rows));
            }
            //1.3 в стенах рядов вертикальных деталей
            this[EValuesExportData.Wall_Detailed_Colums] = GetGridCount(ECWall.LengthM, this[EValuesExportData.Wall_Detailed_Roof_Step_m]);
            if (calcDet)
            {
                data.Add(new ExportElement("стены деталей столбцов", this[EValuesExportData.Wall_Detailed_Colums], data,
                    EValuesExportData.Wall_Detailed_Colums));
            }
            
            
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
            data.Add(new ExportElement("стены соседних кр стен, штук", this[EValuesExportData.Wall_OtherWallsKRCount], data,
                EValuesExportData.Wall_OtherWallsKRCount));

            //2.3 колво горизонтальных деталей
            this[EValuesExportData.Wall_Detailed_Hor_Count] = this[EValuesExportData.Wall_OtherWallsKRCount] *
                this[EValuesExportData.Wall_Detailed_Rows];
            if (calcDet)
            {
                data.Add(new ExportElement("стены детали рядов, штук", this[EValuesExportData.Wall_Detailed_Hor_Count], data,
               EValuesExportData.Wall_Detailed_Hor_Count, "Wall_OtherWallsKRCount*Wall_Detailed_Rows"));
            }
            //2.4 колво вертикальных деталей
            this[EValuesExportData.Wall_Detailed_Roof_Count] = this[EValuesExportData.Wall_Detailed_Colums];
            if (calcDet)
            {
                data.Add(new ExportElement("стены детали потолка, штук", this[EValuesExportData.Wall_Detailed_Roof_Count], data,
               EValuesExportData.Wall_Detailed_Roof_Count, "Wall_Detailed_Colums"));
            }


            //2.5 итого деталей колво
            this[EValuesExportData.Wall_Detailed_Count] = this[EValuesExportData.Wall_Detailed_Roof_Count] + this[EValuesExportData.Wall_Detailed_Hor_Count];
            if (calcDet)
            {
                data.Add(new ExportElement("стены детали колво, штук", this[EValuesExportData.Wall_Detailed_Count], data,
              EValuesExportData.Wall_Detailed_Count, "Wall_Detailed_Roof_Count+Wall_Detailed_Hor_Count"));
            }


            //if (type == EWallTypes.AR_Brick)
            
            //армирование вокруг закладных
            this[EValuesExportData.Wall_Arm_Detailes_Gross_m] = this[EValuesExportData.Wall_Arm_Detailed_Obram_Length_m] * this[EValuesExportData.Wall_Arm_Detailed_Obram_Count] * this[EValuesExportData.Wall_Detailed_Hor_Count];
                
            //армирование верха у самого
            this[EValuesExportData.Wall_Arm_Roof_m] = ECWall.LengthM * this[EValuesExportData.Wall_Arm_DopTop_Count];


            data.Add(new ExportElement("стены армирование деталей длина прутка, м", this[EValuesExportData.Wall_Arm_Detailed_Obram_Length_m], data,
          EValuesExportData.Wall_Arm_Detailed_Obram_Length_m));
            data.Add(new ExportElement("стены армирование деталей колво арматурин, штук", this[EValuesExportData.Wall_Arm_Detailed_Obram_Count], data,
          EValuesExportData.Wall_Arm_Detailed_Obram_Count));

            data.Add(new ExportElement("стены армирование деталей без вычета, пог.м", this[EValuesExportData.Wall_Arm_Detailes_Gross_m], data,
               EValuesExportData.Wall_Arm_Detailes_Gross_m, "Wall_Arm_Detailed_Obram_Length_m*Wall_Arm_Detailed_Obram_Count*Wall_Detailed_Hor_Count"));

            data.Add(new ExportElement("стены армирование у потолка арматурин, штук", this[EValuesExportData.Wall_Arm_DopTop_Count], data,
          EValuesExportData.Wall_Arm_DopTop_Count));
            data.Add(new ExportElement("стены армирование дополнительное у потолка, пог.м", this[EValuesExportData.Wall_Arm_Roof_m], data,
               EValuesExportData.Wall_Arm_Roof_m, "Wall_Arm_DopTop_Count*Wall_Length_m"));

            //вычитаем армирование дверей и окон
            this[EValuesExportData.Wall_Arm_MinesArmProem_m] = 0;

            this[EValuesExportData.Wall_Window_Count] = 0;
            this[EValuesExportData.Wall_Doors_Count] = 0;
            this[EValuesExportData.Wall_Vitrage_Count] = 0;

            foreach (var windAndDoor in ECWall.ECDoorOrWindows)
            {
                double l = windAndDoor.LengthM;
                double h = windAndDoor.HeightM ;
                double row = 0;
                if (!armIsStep)
                {
                    switch (windAndDoor.TypeInsidersWalls) 
                    {
                        case ETypeInsidersWalls.Window:
                            row = this[EValuesExportData.Wall_Arm_Row_Window];break;
                        case ETypeInsidersWalls.Door:
                            row = this[EValuesExportData.Wall_Arm_Row_Door]; break;
                        case ETypeInsidersWalls.Vitrage:
                            row = this[EValuesExportData.Wall_Arm_Row_Vitrage]; break;
                    }
                }
                else
                {
                    row = GetGridCount(h, this[EValuesExportData.Wall_Detailed_Row_Step_m]);
                    if (row > 0)
                    {
                        switch (windAndDoor.TypeInsidersWalls)
                        {
                            case ETypeInsidersWalls.Window:
                                this[EValuesExportData.Wall_Arm_Row_Window]=row; break;
                            case ETypeInsidersWalls.Door:
                                this[EValuesExportData.Wall_Arm_Row_Door] = row; break;
                            case ETypeInsidersWalls.Vitrage:
                                 this[EValuesExportData.Wall_Arm_Row_Vitrage] = row; break;
                        }
                    }
                }
                if (row > 0)
                {
                    //дистанция вычета
                    double dist = row * l;
                    this[EValuesExportData.Wall_Arm_MinesArmProem_m] += dist;
                }
            }

            if (this[EValuesExportData.Wall_Arm_MinesArmProem_m] > this[EValuesExportData.Wall_Arm_Rows_Gross_m])
            {
                this[EValuesExportData.Wall_Arm_MinesArmProem_m] = this[EValuesExportData.Wall_Arm_Rows_Gross_m];
            }
            data.Add(new ExportElement("стены армирование которое надо отнять из-за проемов, пог.м", this[EValuesExportData.Wall_Arm_MinesArmProem_m], data,
              EValuesExportData.Wall_Arm_MinesArmProem_m));

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
            data.Add(new ExportElement("стены армирование деталей которое надо отнять из-за проемов, пог.м", this[EValuesExportData.Wall_Arm_Detailed_Mines_m], data,
             EValuesExportData.Wall_Arm_Detailed_Mines_m));

            //подсчитываем итого
            
            //итого вилатерма
            this[EValuesExportData.Wall_Arm_Vilaterm_Itog_m] = ECWall.LengthM + ECWall.HeightM * this[EValuesExportData.Wall_OtherWallsKRCount];
            data.Add(new ExportElement("стены вилатерм итог, пог.м", this[EValuesExportData.Wall_Arm_Vilaterm_Itog_m], data,
             EValuesExportData.Wall_Arm_Vilaterm_Itog_m, "Wall_Length_m+Wall_Height_m*Wall_OtherWallsKRCount"));

            this[EValuesExportData.Wall_Arm_Vilaterm_Itog_m2] = this[EValuesExportData.Wall_Arm_Vilaterm_Itog_m] * ECWall.WidthM;
            //data.Add(new ExportElement("стены вилатерм итог, м2", this[EValuesExportData.Wall_Arm_Vilaterm_Itog_m2], data,
            //EValuesExportData.Wall_Arm_Vilaterm_Itog_m2, "Wall_Arm_Vilaterm_Itog_m*Wall_Width_m"));

            //итого деталей
            this[EValuesExportData.Wall_Arm_Itog_m] = this[EValuesExportData.Wall_Arm_Rows_Gross_m] - this[EValuesExportData.Wall_Arm_MinesArmProem_m]
            + this[EValuesExportData.Wall_Arm_Roof_m]
            + this[EValuesExportData.Wall_Arm_Detailes_Gross_m] - this[EValuesExportData.Wall_Arm_Detailed_Mines_m];

            data.Add(new ExportElement("стены армирование итог, пог.м", this[EValuesExportData.Wall_Arm_Itog_m], data,
            EValuesExportData.Wall_Arm_Itog_m, "Wall_Arm_Rows_Gross_m-Wall_Arm_MinesArmProem_m+Wall_Arm_Roof_m+Wall_Arm_Detailes_Gross_m-Wall_Arm_Detailed_Mines_m"));
            
            this[EValuesExportData.Wall_Arm_Itog_m2]= this[EValuesExportData.Wall_Arm_Itog_m]* ECWall.WidthM;
            data.Add(new ExportElement("стены армирование итог, м2", this[EValuesExportData.Wall_Arm_Itog_m2], data,
           EValuesExportData.Wall_Arm_Itog_m2, "Wall_Arm_Itog_m*Wall_Width_m"));


            _exportElements= data;
        }


        public static double GetGridCount(double height, double step)
        {
            //количество сеток по высоте стены
            if (step <= 0)
                return 0;

            return (int)Math.Floor(height / step);
        }

        public List<ExportElement> _exportElements { get; set; } = null;
        private bool isCalc=false;
        public List<ExportElement> GetCalcData()
        {
            //if (_exportElements != null) {  return _exportElements; } опасно так как могу для других дел брать..
            if (isCalc || !initial) { return _exportElements; }
            if (_exportElements == null||_exportElements.Count == 0)
            {
                CalculateArm();
            }

            var data = new List<ExportElement>()
            {
              
            };

            var wallsExportElement = ECWall.GetCalcData();

            EValuesGetter.AddExportElementInOurExportElements(data, wallsExportElement, new HashSet<EValuesExportData>()
            { 
                EValuesExportData.Wall_Id, EValuesExportData.Wall_Name, EValuesExportData.Wall_EWallTypes ,
                EValuesExportData.Wall_Length_m, EValuesExportData.Wall_Width_m, EValuesExportData.Wall_Height_m,
                
                EValuesExportData.Wall_OtherWallsKRIds, EValuesExportData.Wall_OtherWallsKRCount,

                 //EValuesExportData.Wall_Doors_Ids,
                EValuesExportData.Wall_Doors_Count,
                //EValuesExportData.Wall_Doors_L_Average_m,EValuesExportData.Wall_Doors_H_Average_m,

                // EValuesExportData.Wall_Window_Ids,
                EValuesExportData.Wall_Window_Count,
                 //EValuesExportData.Wall_Window_L_Average_m,
                //EValuesExportData.Wall_Window_H_Average_m,

            });
            if (_exportElements != null)
            {
                EValuesGetter.AddExportElementInOurExportElements(data, _exportElements);
            }

            _exportElements = data;
            isCalc=true;
            return data;
        }
        public string NameThisClass
        {
            get => nameof(ECArmWallElement);
        }
    }
}
