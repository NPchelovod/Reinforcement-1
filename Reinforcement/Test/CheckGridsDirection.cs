using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CheckGridsDirection : IExternalCommand
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

            FilteredElementCollector collection = new FilteredElementCollector(doc);

            var gridIds = collection.OfClass(typeof(Grid))
                .Cast<Grid>()
                .Select(x => x.Id);
            if (gridIds.Count() == 0)
            {
                return Result.Failed;
            }

            string text = "";

            foreach (var gridId in gridIds)
            {
                Grid grid = doc.GetElement(gridId) as Grid;
                string name = grid.Name;
                Line curve = (Line)grid.Curve;
                string direction = curve.Direction.ToString();
                text = string.Concat(text, name," - ", direction, "\n");
            }


            MessageBox.Show(text);

            return Result.Succeeded;
        }
    }
}
;