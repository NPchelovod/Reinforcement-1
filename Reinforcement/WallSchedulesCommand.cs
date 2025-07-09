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
    public class WallSchedulesCommand : IExternalCommand
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

            

            var reference = uidoc.Document;
            var createSchedulesWall = new CreateSchedulesWall(reference);
            var view = new MainViewWall(createSchedulesWall);
            view.ShowDialog();

          

            // Retrieve elements from database

            //var col = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().ToList();
            //string viewName;

            //using (Transaction t = new Transaction(doc, "Copy view"))
            //{
            //    t.Start();
            //    foreach (var view in col)
            //    {
            //        if (view.Name == "Parking")
            //        {
            //            view.Duplicate(ViewDuplicateOption.Duplicate);
            //            TaskDialog.Show("SEEE", "SUCCESSFUL");
            //            break;
            //        }
            //    }

            //    t.Commit();

            //}

            return Result.Succeeded;
            }
            
        }

        

    }
    

