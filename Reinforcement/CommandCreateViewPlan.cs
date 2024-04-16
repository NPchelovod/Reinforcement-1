using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CommandCreateViewPlan : IExternalCommand
    {
        public List<Level> Levels { get; set; } = new List<Level>();

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            Document doc = RevitAPI.Document;
            try
            {
                     var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .ToElements()
                    .OfType<Level>()
                    .ToList();
                    var list = new ViewModelCreateViewPlan(levels);
                    var dialogueView = new MainViewCreateViewPlan(list);
                    dialogueView.ShowDialog();
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
