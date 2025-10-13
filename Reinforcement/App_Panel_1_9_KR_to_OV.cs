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
    public class App_Panel_1_9_KR_to_OV
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
        //private static RibbonPanel targetPanel;
        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            Image OV1 = Properties.Resources.ES_OV_for_KR;
            Image OV2 = Properties.Resources.ES_OV_for_KR;
            Image OV3 = Properties.Resources.ES_OV_for_KR;
            Image OV4 = Properties.Resources.ES_OV_for_KR;



            

            App_Helper_Button.AddButtonToPullDownButton(item, "11_ОВ шахт размеры", assemblyPath, "Reinforcement.GetAllSizeOV", "Все типоразмеры шахты", OV1);




            App_Helper_Button.AddButtonToPullDownButton(item, "1_ОВ шахт размеры", assemblyPath, "Reinforcement.OV_Construct_Command_1before_List_Size_OV", "Все типоразмеры шахты", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item, "2_ОВ этажи уник", assemblyPath, "Reinforcement.OV_Construct_Command_2before_Povtor_flour", "Поиск несовпадающих ОВ уровней", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item, "3_ОВ планы", assemblyPath, "Reinforcement.OV_Construct_Command_3before_Create_plans", "Создание ОВ планов", OV1);


            App_Helper_Button.AddButtonToPullDownButton(item, "4_ОВ листы", assemblyPath, "Reinforcement.OV_Construct_Command", "Создание ОВ листов", OV1);
            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }
        /*
        public static void KR_to_OV(RibbonPanel panelOV, string tabName)
        {
            App_Helper_Button.CreateButton("Создание ОВ листов", "Создание\nОВ листов", "Reinforcement.OV_Construct_Command", Properties.Resources.ES_OV_for_KR,
                 "Позволяет создать",
                 "Для работы плагина нужно ",
                panelOV);

            App_Helper_Button.CreateButton("Создание ОВ планов", "Создание\nОВ планов", "Reinforcement.OV_Construct_Command_3before_Create_plans", Properties.Resources.ES_OV_for_KR,
                 "Позволяет создать",
                 "Для работы плагина нужно ",
                panelOV);

            App_Helper_Button.CreateButton("Список типоразмеров шахт ОВ", " типоразмеры шахт", "Reinforcement.OV_Construct_Command_1before_List_Size_OV", Properties.Resources.ES_OV_for_KR,
                "Позволяет создать",
                "Для работы плагина нужно ",
               panelOV);

            App_Helper_Button.CreateButton("Список совпадающих этажей одинаковых ОВ", " уровни совпадения", "Reinforcement.OV_Construct_Command_2before_Povtor_flour", Properties.Resources.ES_OV_for_KR,
               "Позволяет создать",
               "Для работы плагина нужно ",
            panelOV);

            var item = ribbonPanel.AddItem(data) as PulldownButton;
            App_Helper_Button.AddButtonToPullDownButton(item, "КР", assemblyPath, "Reinforcement.App_Panel_1_1_Configuration_KR", "Конструктив", KR_config);


        }
        */
    }
}
