#region Namespaces
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
    public class ElevationCommand : IExternalCommand
    {
        public Result Execute(
             ExternalCommandData commandData,
             ref string message,
             ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            var list_Name = new List<string>() { FamName, FamName2 };
            var ww = Utilit_1_2_Creater_FamilySymbol.GetResult(FamName, doc, uidoc);
            return Result.Succeeded;


        }

        public static  string FamName { get; set; } = "ЕС_Высотная отметка несколько_Вверх";
        public static string FamName2 { get; set; } = "ЕС_Высотная_Отметка_Несколько_Вверх";

    }
    }

