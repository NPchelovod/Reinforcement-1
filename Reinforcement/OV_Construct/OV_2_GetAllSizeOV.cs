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

            var lines = SizeOV.Select((size, index) => $"{index + 1}. ({size.width}, {size.height})").ToList();

            // Объединяем все строки через перенос
            string outputText = string.Join("\n", lines);

            // Выводим в Revit
            TaskDialog.Show("Все типоразмеры шахт ОВ", outputText);

            return Result.Succeeded;
        }

        public static string swidth = "Ширина";
        public static string sheight = "Длина";
        public static List<(int width, int height)> SizeOV = new List<(int width, int height)>();

        public static bool ExecuteLogic(ExternalCommandData commandData)
        {
            SizeOV.Clear();
            
            GetDataAllOV.GetOVData(commandData);//заполненный список с данными

            if(GetDataAllOV.DataOV.Count==0) {return false;}

            int width = 0;
            int height = 0; 

            var pastSize = new HashSet<(int width, int height)> ();

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
                        pastSize.Add((width, height));

                        //if(Dict_Size_OV.ContainsKey(width))
                        //{
                        //    Dict_Size_OV[width].Add(height);
                        //}
                        //else
                        //{
                        //    Dict_Size_OV[width] = new List<double>{ height };
                        //}

                    }
                }
                
            }
            SizeOV = pastSize
            .OrderBy(size => size.width)
            .ThenBy(size => size.height)
            .ToList();

            if (SizeOV.Count > 0)
            {
                return true;
            }
            else
                { return false; }


        }

    }

    



}
