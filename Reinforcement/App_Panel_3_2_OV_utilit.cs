using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Reinforcement
{
    public class App_Panel_3_2_OV_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void OV_utilit(RibbonPanel panel, string tabName)
        {
            //App_Helper_Button.CreateButton("Расчет кладки", "Армирование\n кладки ", "Reinforcement.CalculateReinforcementArchitectureWallsCommand", Properties.Resources.rashet_walls2,
            //     "Позволяет создать отчет армирования кладки",
            //     "Для работы плагина нужно заполнить форму",
            //    panel);
            var data = new SplitButtonData(tabName, tabName);
            FillPullDown(panel, data);

        }


        public static void AddSplitButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }

        public static void AddPullDownButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new PulldownButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }
        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;
        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            Image OV1 = Properties.Resources.flight;
            Image OV2 = Properties.Resources.Properties;
            //предполагается тест и так далее
            App_Helper_Button.AddButtonToPullDownButton(item, "Аэро", assemblyPath, "Reinforcement.OV_Panel_GetPipes", "Позволяет считать аэродинамику", OV1);
            //App_Helper_Button.AddButtonToPullDownButton(item, "Отверстия\n в плите", assemblyPath, "Reinforcement.RoundDistanceForOpeningsInSlabs", "Дорабатывает отверстия в плите для корректного отображения в ведомости отверстий\n\nОкругляет привязки выбранных отверстий до 5 мм. Поворачивает отверстия, чтобы Ширина - был размер по Х. Поворачивает отверстия для корректного отображения знака проема", OV2);

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }


    }
}
