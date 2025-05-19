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
    public class App_Panel_1_9_KR_to_OV
    {
        // 7. panelOV временная ничто так не временно как вечность
        public static void KR_to_OV(RibbonPanel panelOV, string tabName)
        {
            App_Helper_Button.CreateButton("Создание ОВ листов", "Создание\nОВ листов", "Reinforcement.OV_Construct_Command", Properties.Resources.ES_OV_for_KR,
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
        }
    }
}
