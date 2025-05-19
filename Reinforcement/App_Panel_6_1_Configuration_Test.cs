using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Collections.Generic;

using static Reinforcement.App;



namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Panel_6_1_Configuration_Test : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {

            // панели которые должны быть видны
            var list_panels_view = new List<string>()
            {
                "Конфигурация",
                
                "ОВ_сырой"

            };

            foreach (var panel in PanelVisibility.Panels)
            {
                if (list_panels_view.Contains(panel.Key))
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = true;
                    }
                }
                else
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = false;
                    }
                }
            }
            return Result.Succeeded;
        }
    }
}