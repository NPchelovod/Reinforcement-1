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
        public static string FamTypeName { get; set; }

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
            if (FamTypeName == "stop")
            {
                return Result.Cancelled;
            }

            ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));
            var collection = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Where(x => x.Name == FamTypeName)
                .ToList();
            if (collection.Count == 0)
            {
                return Result.Failed;
            }
            sel.SetElementIds(collection.FirstOrDefault().GetDependentElements(filter));
            DialogResult dialogResult = MessageBox.Show("Показать элементы на виде?", "Перебор элементов", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {

                int n = 0;
                while (n < collection.FirstOrDefault().GetDependentElements(filter).Count)
                {
                    var elementId = collection.FirstOrDefault().GetDependentElements(filter).ElementAt(n);
                    var element = doc.GetElement(elementId);
                    var viewId = element.OwnerViewId;
                    uidoc.ActiveView = doc.GetElement(viewId) as Autodesk.Revit.DB.View;
                    // uidoc.ShowElements(element);
                    dialogResult = MessageBox.Show("Показать далее?", "Перебор элементов", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.No)
                    {
                        return Result.Succeeded;
                    }
                    n++;
                }
            }
            else if (dialogResult == DialogResult.No)

            {
                return Result.Succeeded;
            }
            return Result.Succeeded;
        }
    }
}
