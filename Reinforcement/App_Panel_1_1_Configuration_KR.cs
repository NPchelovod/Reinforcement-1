using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using AW = Autodesk.Windows;
using RibbonPanel = Autodesk.Windows.RibbonPanel;
using RibbonButton = Autodesk.Windows.RibbonButton;
using static Reinforcement.App;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_1_1_Configuration_KR : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {

            var panelSpds = PanelVisibility.Panels["СПДС"];

            if (panelSpds != null)
            {
                panelSpds.Visible = true;
            }

            return Result.Succeeded;
        }
    }
}
