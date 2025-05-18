using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using UIFramework;
using static Reinforcement.App;



namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_2_1_Configuration_AR : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {

            var panelSpds = PanelVisibility.Panels["СПДС"];
            if (panelSpds != null)
            {
                panelSpds.Visible = !panelSpds.Visible;
            }
            return Result.Succeeded;
        }
    }
}