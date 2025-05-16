using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AW = Autodesk.Windows;

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
            string panelToKeep = "Конфигурация"; 
            string pluginPrefix = "Конфигурация";

            var ribbon = AW.ComponentManager.Ribbon;
            var tab = ribbon.FindTab(tabName);
            if (tab == null)
            {
                return Result.Succeeded;
            }

            // Получаем все панели, которые нужно удалить
            var panelsToRemove = tab.Panels
                .Where(p => p.Source.Title != panelToKeep &&
                           ContainsPluginButtons(p, pluginPrefix))
                .ToList();

            foreach (var panel in panelsToRemove)
            {
                // Удаляем все кнопки плагина на панели
                var buttonsToRemove = panel.Source.Items
                    .Where(item => item.Id.StartsWith(pluginPrefix))
                    .ToList();

                foreach (var button in buttonsToRemove)
                {
                    panel.Source.Items.Remove(button);
                }

                // Если панель пустая, можно удалить её полностью
                if (panel.Source.Items.Count == 0)
                {
                    tab.Panels.Remove(panel);
                }
            }
            return Result.Succeeded;
        }

        private static bool ContainsPluginButtons(AW.RibbonPanel panel, string pluginPrefix)
        {
            return panel.Source.Items.Any(item => item.Id.StartsWith(pluginPrefix));
        }


    }
}
