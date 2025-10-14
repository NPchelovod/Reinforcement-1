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
    public class Perforation_floor_Command : IExternalCommand
    {

        public static HashSet<string> list_Name = new HashSet<string>
        {
            "ЕС_Отверстие прямоугольное_В перекрытии",
            "Кубик_Перекрытие_Прямоугольный"
        };
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;

            

            string Type_seach = "Symbols";

            try
            {
                list_Name=Utilit_1_1_Depth_Seach.GetResult(doc, uidoc, list_Name, Type_seach).FamNames;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        
    }
}

