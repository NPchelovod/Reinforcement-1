﻿#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Perforation_walls_Command : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;

            var list_Name = new List<string>() { FamName, FamName2, FamName3 };

            string Type_seach = "Symbols";

            try
            {
                Utilit_1_1_Depth_Seach.GetResult(doc, uidoc, list_Name, Type_seach);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        public static string FamName { get; set; } = "ЕС_Отверстие_В стене_Перфорация";
        public static string FamName2 { get; set; } = "ЕС_Отверстие_Стена_Перфорация";
        public static string FamName3 { get; set; } = "ЕС_ОтверстиеПрямоугольное_ВСтене";

    }
}

