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
    public class PRebarEqualCommand : IExternalCommand
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


            var ww = Utilit_1_2_Creater.GetResult(FamName, doc, uidoc);
            return Result.Succeeded;
        }

        public static  string FamName { get; set; } = "ЕС_А-21_П-равнопол";

    }
    }

