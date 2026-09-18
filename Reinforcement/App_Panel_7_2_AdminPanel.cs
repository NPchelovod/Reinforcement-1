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
    public class App_Panel_7_2_AdminPanel
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void Admin_utilit(RibbonPanel panel, string tabName)
        {
            Image OV1 = Properties.Resources.Properties;
            App_Helper_Button.CreateButton("Статистика", "Статистика\n пользователей", "Reinforcement.Statist", OV1,
                 "нннн",
                 "аааа",
                panel);
        }


    }
}
