using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class SelectAllElementByFamilyName : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            Selection sel = uidoc.Selection;

            string famname = "ADSK_ЭУ_Узел_Линии разрыва";
  
            ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));         
            var collection = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Where(x => x.Name == famname)
                .ToList();
            sel.SetElementIds(collection.FirstOrDefault().GetDependentElements(filter));

            return Result.Succeeded;
        }
    }
}
