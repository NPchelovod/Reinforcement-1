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


namespace Reinforcement
{
    public class App_Panel_1_1_Configuration 
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

            // Добавляем кнопки с иконками
            Image KR_config = null;

            foreach (EPanelSelf ePanelSelf in App.EPanelSelfList)
            {
                if(!App.DConfigNames.TryGetValue(ePanelSelf, out var dateConfig))
                {
                    continue;
                }
                if(KR_config==null)
                {
                    KR_config = dateConfig.image;// считай главная у нас будет та что главная
                }
                
                App_Helper_Button.AddButtonToPullDownButton(item, dateConfig.Name, assemblyPath, dateConfig.Comand, dateConfig.SurName, dateConfig.image);
            }
            if (KR_config == null)
            {
                KR_config = Properties.Resources.KR_config;
            }
           
            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(KR_config);
            item.LargeImage = imageSource;
        }

      
    }
}





    