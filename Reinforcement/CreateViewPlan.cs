using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CreateViewPlan : IExternalCommand
    {
        public List<Level> Levels { get; set; } = new List<Level>();
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
                     Levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .ToElements()
                    .OfType<Level>()
                    .ToList();
                    var dialogueView = new MainViewCreateViewPlan(Levels);
                    dialogueView.ShowDialog();
                    foreach (ViewFamilyType viewType in viewTypes)
                    {
                        if (viewType.Name == "План несущих конструкций") 
                        {
                            var viewTypeStructural = viewType.Id;
                            var newViewplan = ViewPlan.Create(doc, viewTypeStructural, Levels.ElementAt(1).Id);
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
