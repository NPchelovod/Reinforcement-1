using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Reinforcement._2_Architecture.CalculateReinforcementArchitectureWalls.View;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CalculateReinforcementArchitectureWallsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            RevitAPI.Initialize(commandData);

            var viewModel = new CalculateReinforcementArchitectureWallsViewModel();
            var view = new CalculateReinforcementArchitectureWallsView { DataContext = viewModel };

            view.ShowDialog();
            return Result.Succeeded;

        }
    }
}
