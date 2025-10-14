using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class GetAllSizeOV : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ExecuteLogic(commandData))
            {
                TaskDialog.Show("Все типоразмеры шахт ОВ", "Список пуст");
                return Result.Failed; 
            }

            // Объединяем все строки через перенос

            var lines = SizeOV.Select((size, index) => $"{index + 1}. ({size.height}, {size.width}) {size.num} шт.").ToList();

            // Объединяем все строки через перенос
            string outputText = string.Join("\n", lines);

            // Выводим в Revit
            TaskDialog.Show("Все типоразмеры шахт ОВ", outputText);

            return Result.Succeeded;
        }

        public static string swidth = "Ширина";
        public static string sheight = "Длина";
        public static List<(int height,int width, int num)> SizeOV = new List<(int height, int width, int num)>();

        public static bool ExecuteLogic(ExternalCommandData commandData)
        {
            SizeOV.Clear();
            
            GetDataAllOV.GetOVData(commandData);//заполненный список с данными

            if(GetDataAllOV.DataOV.Count==0) {return false;}

            int width = 0;
            int height = 0; 

            
            var dictNum = new Dictionary<(int height, int width), int>();
            foreach(var Data in GetDataAllOV.DataOV)
            {
                if(Data.lookupParameterDouble.TryGetValue(swidth,out var dwidth))
                {
                    width = (int)dwidth;
                    if (Data.lookupParameterDouble.TryGetValue(sheight, out var dheight))
                    {
                        height = (int)dheight;
                        //if(pastSize.Contains((width, height)))
                        //{ continue; }
                       

                        if(dictNum.TryGetValue((height, width), out int num))
                        {
                            dictNum[(height, width)] = num + 1;
                        }
                        else
                        {
                            dictNum[(height, width)] = 1;
                        }

                        
                    }
                }
                
            }
            var sortSize = dictNum.Keys
            .OrderBy(size => size.height )
            .ThenBy(size => size.width)
            .ToList();

            foreach(var Data in sortSize)
            {
                SizeOV.Add((Data.height, Data.width, dictNum[Data]));
            }

            if (SizeOV.Count > 0)
            {
                return true;
            }
            else
                { return false; }


        }

    }

    



}
