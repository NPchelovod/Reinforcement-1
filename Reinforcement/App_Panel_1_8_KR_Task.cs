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
        public static void KR_Task(RibbonPanel panelOV, string tabName)
        {
            App_Helper_Button.CreateButton("Задание ЭЛ", "40_ЭЛ\n копия на вид", "Reinforcement.CopyTaskFromElectricV2_0", Properties.Resources.CopyTaskFromElectric,
                 "Позволяет создать копию задания электриков",
                 "Для работы плагина нужно открыть 3д вид, ЭЛ должна быть подгружена как связь, выбрать её, тогда произойдёт копирование ",
                panelOV);

            
        }
    }
}
