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

            // панели которые должны быть видны
            var list_panels_view = new List<string>()
            {
                "Конфигурация",
                "СПДС",
                "Схематичное армирование",
                "Детальное армирование",
                "Оформление",
                "Выбор",
                "САПР",
                "КР вставки",
                "Копировать задание"

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
