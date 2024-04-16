using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public class ViewModelCreateViewPlan
    {
        public ViewModelCreateViewPlan(List<Level> levels)
        {
            Levels = levels;
        }
        public List<Level> Levels { get; set; } = new List<Level>();

        public Level SelectedLevel { get; set; }
        public void CreateViewPlan()
        {
            Document doc = RevitAPI.Document;
            using (Transaction t = new Transaction(doc, "EC: Создание вида"))
            {
                t.Start();
                var viewTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .ToElements()
                    .OfType<ViewFamilyType>()
                    .ToList();
                foreach (ViewFamilyType viewType in viewTypes)
                {
                    if (viewType.Name == "План несущих конструкций")
                    {
                        var viewTypeStructural = viewType.Id;
                        var newViewplan = ViewPlan.Create(doc, viewTypeStructural, SelectedLevel.Id);
                    }
                }
                t.Commit();
            }

        }
    }
}