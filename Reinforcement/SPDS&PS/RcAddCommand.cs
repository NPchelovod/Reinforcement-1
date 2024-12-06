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
    public class RcAddCommand : IExternalCommand
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

            // Access current selection

            Selection sel = uidoc.Selection;

            // Retrieve elements from database

            FilteredElementCollector col = new FilteredElementCollector(doc);

            IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
            FamilySymbol symbol = null;

            try
            {
                foreach (var element in symbols)
                {   
                    ElementType elemType = element as ElementType;
                    if (elemType.FamilyName == FamName)
                    {
                        symbol = element as FamilySymbol;
                        break;
                    }
                }

                uidoc.PostRequestForElementTypePlacement(symbol);
                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }

        public static  string FamName { get; set; } = "ЕС_А-01_Дополнительная";

    }
    }

