using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class SovpadFloor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            if (!ExecuteLogic(commandData))
            {
                TaskDialog.Show("Все уникальные ОВ этажи", "Список пуст");
                return Result.Failed;
            }

            // Объединяем все строки через перенос

            

            // Объединяем все строки через перенос
            string outputText = string.Join("\n", UnicFloors);

            // Выводим в Revit
            TaskDialog.Show("Все уникальные ОВ этажи", outputText);

            return Result.Succeeded;

        }

        //public Dictionary <int, List<ElemntDa>>
        public static int pogresNesovpad = 900;// 
        public static int pogresCenter = 5;


        public static List<int> UnicFloors = new List<int>();
        public static bool ExecuteLogic(ExternalCommandData commandData)
        {
            UnicFloors.Clear();
            GetDataAllOV.GetOVData(commandData);//заполненный список с данными

            var DataOVLevel = GetDataAllOV.DataOVLevel;
            if (DataOVLevel.Count == 0) { return false; }


            var Z_floors = new List<int>(DataOVLevel.Keys);
            Z_floors.Sort();

            var pastSize = new HashSet<(int width, int height)>();

            for(int i=0; i+1<Z_floors.Count; i++) 
            { 
                int Z1 = Z_floors[i];
                int Z2 = Z_floors[i+1];
                //нам надо найти схожие равных сестер
                var elements1 = DataOVLevel[Z1];
                var elements2 = DataOVLevel[Z2];

                for (int j=0; j < elements1.Count; j++)
                {
                    var element1 = elements1[j];
                    int distanceMin = -1;
                    int distance = 0;
                    HelperElement nearlyElement = null;

                    for (int k = 0; k < elements2.Count; k++)
                    {
                        var element2 = elements1[k];
                        distance = (int) Math.Sqrt((element1.X - element2.X) * (element1.X - element2.X) + (element1.Y - element2.Y) * (element1.Y - element2.Y));
                        if (distance < distanceMin || distanceMin < 0)
                        {
                            nearlyElement = element2;
                            distanceMin = distance;
                            if(distance==0)
                            {
                                break;
                            }
                        }
                    }
                    if(pogresNesovpad> distanceMin)
                    { continue; }

                    element1.ChildElement.Add(nearlyElement);
                    nearlyElement.ParentElement.Add(element1);

                }
            }

            // теперь собираем совпадающие этажи

            UnicFloors.Add(Z_floors[0]);
            for (int i = 1; i < Z_floors.Count; i++)
            {
                int Z1 = Z_floors[i];
                var elements1 = DataOVLevel[Z1];
                bool unicFloor = false;

               

                for (int j = 0; j < elements1.Count; j++)
                {
                    var element1 = elements1[j];
                    if (element1.ParentElement.Count == 0 || element1.ParentElement.Count > 1)
                    {
                        unicFloor = true; break;
                    }

                    var Parent = element1.ParentElement.FirstOrDefault();
                    if (Parent == null)
                    {
                        unicFloor = true; break;
                    }

                    if(Math.Abs(Parent.X- element1.X)> pogresCenter|| Math.Abs(Parent.Y - element1.Y) > pogresCenter)
                    {
                        unicFloor = true; break;
                    }

                    double width1 = 0;
                    double width2 = 0;
                    if (element1.lookupParameterDouble.TryGetValue(GetAllSizeOV.swidth, out var dwidth))
                    {
                        width1 = (int)dwidth;

                    }
                    if (Parent.lookupParameterDouble.TryGetValue(GetAllSizeOV.swidth, out var dwidth2))
                    {
                        width2 = (int)dwidth2;
                    }
                    if(width1!= width2 || width1==0)
                    {
                        unicFloor = true; break;
                    }

                    double height1 = 0;
                    double height2 = 0;
                    if (element1.lookupParameterDouble.TryGetValue(GetAllSizeOV.sheight, out var dheight1))
                    {
                        height1 = (int)dheight1;

                    }
                    if (Parent.lookupParameterDouble.TryGetValue(GetAllSizeOV.sheight, out var dheight2))
                    {
                        height2 = (int)dheight2;
                    }
                    if (height1 != height2 || height1 == 0)
                    {
                        unicFloor = true; break;
                    }


                }
                if(unicFloor)
                {
                    UnicFloors.Add(Z1);
                }
                    
            }

            if(UnicFloors.Count > 0)
            {return true;}
            return false;


        }
                    
    

    }
}
