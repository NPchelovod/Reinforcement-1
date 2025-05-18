using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AW = Autodesk.Windows;

using System.Linq;
using static Reinforcement.App;



namespace Reinforcement
{
    internal class App_Panel_1_1_Configuration 
    {
        
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
        private static RibbonPanel targetPanel;

        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;

            // Добавляем кнопки с иконками
            Image KR_config = Properties.Resources.KR_config;
            Image AR_config = Properties.Resources.AR_config;
            Image OV_config = Properties.Resources.OV_config;
            Image VK_config = Properties.Resources.VK_config;
            Image EL_config = Properties.Resources.EL_config;
            Image Test_config = Properties.Resources.Test_config;

            App_Helper_Button.AddButtonToPullDownButton(item, "КР", assemblyPath, "Reinforcement.App_Panel_1_1_Configuration_KR", "Конструктив", KR_config);
            App_Helper_Button.AddButtonToPullDownButton(item, "АР", assemblyPath, "Reinforcement.App_Panel_2_1_Configuration_AR", "Архитектура", AR_config);

            App_Helper_Button.AddButtonToPullDownButton(item, "ОВ", assemblyPath, "Reinforcement.App_Panel_3_1_Configuration_OV", "Отопление и Вентиляция", OV_config);

            App_Helper_Button.AddButtonToPullDownButton(item, "ВК", assemblyPath, "Reinforcement.App_Panel_4_1_Configuration_VK", "Водснаб и Канализация", VK_config);

            item.AddSeparator();
            App_Helper_Button.AddButtonToPullDownButton(item, "ЭЛ", assemblyPath, "Reinforcement.App_Panel_5_1_Configuration_EL", "Электрика", EL_config);

            App_Helper_Button.AddButtonToPullDownButton(item, "тест", assemblyPath, "Reinforcement.App_Panel_6_1_Configuration_Test", "не трогать", Test_config);

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(KR_config);
            item.LargeImage = imageSource;
        }

        private static void Configuration_KR(UIControlledApplication app)
        {
            // Удаляем всю панель
        }

    }
}





    