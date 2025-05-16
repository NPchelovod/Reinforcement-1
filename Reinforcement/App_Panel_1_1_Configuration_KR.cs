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
            string tabName = "ЕС BIM";

            var panelToKeeps = new List<string>() // панели которые остаются
            {
                "Конфигурация", "СПДС"
                
            };

            string pluginPrefix = "Reinforcement";

            RecreatePluginPanels(tabName, panelToKeeps, pluginPrefix);

            TaskDialog.Show("Готово", "Панели успешно обновлены!");
            return Result.Succeeded;


        }
        public static void RecreatePluginPanels(string tabName, List <string> panelToKeeps, string pluginPrefix)
        {
            // Получаем доступ к ленте Revit через AW API
            var ribbon = AW.ComponentManager.Ribbon;
            if (ribbon == null) return;

            // Находим вкладку по имени
            var tab = ribbon.Tabs.FirstOrDefault(t => t.Title == tabName);
            if (tab == null) return;

            // Создаем временную коллекцию для панелей, которые нужно сохранить
            var panelsToKeep = new List<RibbonPanel>();

            // Перебираем все панели на вкладке

            var perebor = tab.Panels.ToList();


            foreach (var panel in tab.Panels.ToList())
            {

                if (panelToKeeps.Contains(panel.Source.Title)==false)

                { tab.Panels.Remove(panel); }
                continue;
                
            }

            // Принудительное обновление ленты
            ribbon.UpdateLayout();
        }
        private static bool ContainsPluginButtons(AW.RibbonPanel panel, string pluginPrefix)
        {
            return panel.Source.Items.Any(item => item.Id.StartsWith(pluginPrefix));
        }


    }
}
