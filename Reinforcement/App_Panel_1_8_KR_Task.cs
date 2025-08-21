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
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Reinforcement
{
    public class App_Panel_1_8_KR_Task
    {
        // 7. panelOV временная ничто так не временно как вечность
        /*
        public static void KR_Task(RibbonPanel panelOV, string tabName)
        {
            App_Helper_Button.CreateButton("Задание ЭЛ", "40_ЭЛ\n копия на вид", "Reinforcement.CopyTaskFromElectric", Properties.Resources.CopyTaskFromElectric,
                 "Позволяет создать копию задания электриков",
                 "Для работы плагина нужно открыть 3д вид, ЭЛ должна быть подгружена как связь, выбрать её, тогда произойдёт копирование ",
                panelOV);

            App_Helper_Button.CreateButton("Отверстия в плите", "Отверстия\n в плите", "Reinforcement.RoundDistanceForOpeningsInSlabs", Properties.Resources.Hole,
                 "Дорабатывает отверстия в плите для корректного отображения в ведомости отверстий",
                 "Округляет привязки выбранных отверстий до 5 мм. Поворачивает отверстия, чтобы Ширина - был размер по Х. Поворачивает отверстия для корректного отображения знака проема",
                panelOV);


        }
        */
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
            Image OV1 = Properties.Resources.CopyTaskFromElectric;
            Image OV2 = Properties.Resources.Hole;
            //предполагается тест и так далее
            App_Helper_Button.AddButtonToPullDownButton(item, "40_ЭЛ\n копия на вид", assemblyPath, "Reinforcement.CopyTaskFromElectric", "Позволяет создать копию задания электриков\n\nДля работы плагина нужно открыть 3д вид, ЭЛ должна быть подгружена как связь, выбрать её, тогда произойдёт копирование", OV1);
            App_Helper_Button.AddButtonToPullDownButton(item, "Отверстия\n в плите", assemblyPath, "Reinforcement.RoundDistanceForOpeningsInSlabs", "Дорабатывает отверстия в плите для корректного отображения в ведомости отверстий\n\nОкругляет привязки выбранных отверстий до 5 мм. Поворачивает отверстия, чтобы Ширина - был размер по Х. Поворачивает отверстия для корректного отображения знака проема", OV2);

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }


    }
}
