using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Collections.Generic;

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
            RevitAPI.Initialize(commandData);
            // панели которые должны быть видны
            

            var list_panels_view = new List<string>()
            {
                "Конфигурация",
                "СПДС",
                "Выбор",
                "Оформление",
                "АР панель",
                "Волшебная кнопка"

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