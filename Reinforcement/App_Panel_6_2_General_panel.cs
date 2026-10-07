using Autodesk.Revit.UI;
using System.Drawing;
using System.Reflection;
using System.Windows.Media;


namespace Reinforcement
{
    public class App_Panel_6_2_General_panel
    {
        public static void AddSplitButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }


        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;
        //private static RibbonPanel targetPanel;
        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            Image OV1 = Properties.Resources.Properties;
            //Image OV2 = Properties.Resources.toska2;
            Image OV3 = Properties.Resources.Properties;
            

            App_Helper_Button.AddButtonToPullDownButton(item, "СохрВид", assemblyPath, "Reinforcement.MemoryViewSave", $"Сохранить вид", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item, "ВосстанВид", assemblyPath, "Reinforcement.MemoryViewCorrect", $"Восстановить вид из записанного ранее состояния", OV1);
        }
        public static void Gen_utilit(RibbonPanel panel, string tabName)
        {
            //App_Helper_Button.CreateButton("Расчет кладки", "Армирование\n кладки ", "Reinforcement.CalculateReinforcementArchitectureWallsCommand", Properties.Resources.rashet_walls2,
            //     "Позволяет создать отчет армирования кладки",
            //     "Для работы плагина нужно заполнить форму",
            //    panel);
            //var data = new SplitButtonData(tabName, tabName);
            

        }
    }
}
