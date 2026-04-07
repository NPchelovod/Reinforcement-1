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
    public class DrBreakLineCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;



            ElementTypeOrSymbol Type_seach = ElementTypeOrSymbol.Symbol;

            try
            {
                Utilit_1_1_Depth_Seach.GetResult(list_Name, Type_seach, list_Type_Name);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        //имена семейсьва
        public static HashSet<string> list_Name = new HashSet<string>
        {
             
            "ЕС_О_Линии разрыва",
            "ЕС_О_Линия обр",
            
        };
        //имена типа
        public static HashSet<string> list_Type_Name = new HashSet<string>
        {
            "Линейный обрыв",
        };

    }
}


