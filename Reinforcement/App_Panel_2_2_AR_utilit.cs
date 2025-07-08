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
    public class App_Panel_2_2_AR_utilit
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void AR_utilit(RibbonPanel panel, string tabName)
        {
            App_Helper_Button.CreateButton("Расчет кладки", "Армирование\n кладки ", "Reinforcement.CalculateReinforcementArchitectureWallsCommand", Properties.Resources.rashet_walls2,
                 "Позволяет создать отчет армирования кладки",
                 "Для работы плагина нужно заполнить форму",
                panel);

        }
    }
}
