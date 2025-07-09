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
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            var dialogueView = new MainViewSelectAllElementsByFamilyName();
            dialogueView.ShowDialog();
            if (FamTypeName == "stop")
            {
                return Result.Cancelled;
            }

            ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));
            ElementClassFilter filterTag = new ElementClassFilter(typeof(IndependentTag));

            IList <ElementId> listId = new List <ElementId>();
            var collection = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(x => x.Symbol.Family.Name == FamTypeName)
                .Select(x => x.Id)
                .ToList();
            listId = collection;
            if (collection.Count == 0)
            {
                var collectionTag = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(x => x.Name == FamTypeName)
                .FirstOrDefault()
                .GetDependentElements(filterTag);
                listId = collectionTag;
                if (collectionTag.Count == 0)
                {
                    return Result.Failed;
                }
            }
            sel.SetElementIds(listId.ToList());
            DialogResult dialogResult = MessageBox.Show("Показать элементы на виде?", "Перебор элементов", MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                int n = 0;
                while (n < listId.Count)
                {
                    var elementId = listId[n];
                    var element = doc.GetElement(elementId);
                    var viewId = element.OwnerViewId;
                    var view = doc.GetElement(viewId) as Autodesk.Revit.DB.View;

                    if (uidoc.ActiveView.Title != view.Title)
                    {
                        uidoc.ActiveView = view;

                        // uidoc.ShowElements(element);
                        if (listId.Count > 1)
                        {

                            dialogResult = MessageBox.Show("Показать далее?", "Перебор элементов", MessageBoxButtons.YesNo);
                        }
                        if (dialogResult == DialogResult.No)
                        {
                            return Result.Succeeded;
                        }
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
