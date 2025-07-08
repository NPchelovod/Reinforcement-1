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
    public class App_Panel_1_71_KR_vstavka
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
            Image OV1 = Properties.Resources.hole_in_walls;
            

            App_Helper_Button.AddButtonToPullDownButton(item, "Перфорация\n стены", assemblyPath, "Reinforcement.Perforation_walls_Command", "перфорация стены", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item, "Перфорация\n перекрытия", assemblyPath, "Reinforcement.Perforation_floor_Command", "перфорация перекрытия", OV1);

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }
     
    }
}
