using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Reinforcement.PickFilter;
using Reinforcement.SelectAllElementsByFamilyName;
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

    public class CommandSelectAllElementsByFamilyName : IExternalCommand
    {
        public static string famTypeName { get; set; }

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
            var dialogueView = new MainViewSelectAllElementsByFamilyName();
            dialogueView.ShowDialog();
            if (famTypeName == "stop")
            {
                return Result.Cancelled;
            }
  
            ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));         
            var collection = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Where(x => x.Name == famTypeName)
                .ToList();
            if (collection.Count == 0)
            {
                return Result.Failed;
            }
                sel.SetElementIds(collection.FirstOrDefault().GetDependentElements(filter));

            return Result.Succeeded;
        }
    }
}
