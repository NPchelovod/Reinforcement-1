#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows.Documents;

#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Create : IExternalCommand
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



            // 1. get the active view
            List<XYZ> points = new List<XYZ>
            {
                new XYZ(0, 0, 0),
                new XYZ(10, 0, 2),
                new XYZ(10, 10, 3),
                new XYZ(0, 10, 1)
            };

            using (Transaction t = new Transaction(doc, "Create Toposolid"))
            {
                t.Start();
                Toposolid toposolid = Toposolid.Create(doc, points, )
                

                t.Commit();
            }


            return Result.Succeeded;
            }
            
        }

        

    }
    

