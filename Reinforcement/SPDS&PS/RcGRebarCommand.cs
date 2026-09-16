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
    public class RcGRebarCommand : IExternalCommand
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
                Utilit_1_1_Depth_Seach.GetResult(list_Name, Type_seach, Type_Name);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        public static HashSet<string> list_Name = new HashSet<string>
        {
            "ЕС_А-11_Г-стержень"
        };
        public static HashSet<string> Type_Name = new HashSet<string>
        {
            "А500С"
        };

    }
    }

