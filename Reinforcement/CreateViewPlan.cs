using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CreateViewPlan : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            try
            {
                using (Transaction t = new Transaction(doc, "Создание вида"))
                {
                    t.Start();
                    var viewTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .ToElements()
                    .OfType<ViewFamilyType>()
                    .ToList();
                    var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .ToElements()
                    .OfType<Level>()
                    .ToList();
                    foreach (ViewFamilyType viewType in viewTypes)
                    {
                        if (viewType.FamilyName == "План несущих конструкций") 
                        {
                            var viewTypeStructural = viewType.Id;
                            var newViewplan = ViewPlan.Create(doc, viewTypeStructural, levels.ElementAt(3).Id);
                        }
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }

    }
}
