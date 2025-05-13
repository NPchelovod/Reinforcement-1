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
// стрелка вида
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ArrowViewCommand : IExternalCommand
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

            var ww = Utilit_1_1_Depth_Seach.GetResult(list_Name, doc, uidoc);
            return Result.Succeeded;


        }


        public static string FamName { get; set; } = "ЕС_ОбозначениеВида";
        public static string FamName2 { get; set; } = "ЕС_Обозначение Вида";

    }
}

