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
using System.Windows.Controls;
using System.Windows.Media;


#endregion

namespace Reinforcement
{
    internal class Utilit_1_2_Creater_FamilySymbol
    {
        public static Result GetResult(string FamName, Document doc, UIDocument uidoc)


        {
           
            // Retrieve elements from database

            FilteredElementCollector col = new FilteredElementCollector(doc);


            IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
            try
            {
                var symbol = Utilit_1_1_Depth_Seach_Name_FamilySymbol.GetResult(FamName, symbols);

                if (symbol == null)
                {
                    return Result.Failed;
                }

                //uidoc.PostRequestForElementTypePlacement(symbol);
                uidoc.PostRequestForElementTypePlacement(symbol);
                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }
    }
}
