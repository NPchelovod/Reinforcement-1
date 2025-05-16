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
    public class App_Panel_1_3_KR_SketchReinf
    {
        public static void KR_SketchReinf(RibbonPanel panelSketchReinf, string tabName)
        {
            App_Helper_Button.CreateButton("Доборные стержни", "Доборные\nстержни", "Reinforcement.RcAddCommand",
                      Properties.Resources.ES_Additional_rebars,
                      "Размещение доборных арматурных стержней", $"Имя семейства должно быть {RcAddCommand.FamName}",
                      panelSketchReinf);

            App_Helper_Button.CreateButton("Фоновое армирование", "Фоновое\nармирование", "Reinforcement.RcFonCommand", Properties.Resources.ES_Background_rebars,
                    "Размещение фонового армирования", $"Имя семейства должно быть {RcFonCommand.FamName}",
                   panelSketchReinf);

            RibbonItemData distrPRebar = App_Helper_Button.CreateButtonData("Распределение П и Г-стержней", "Распределение П и Г-стержней", "Reinforcement.PRebarDistribCommand", Properties.Resources.PRebarDistrib,
                     "Размещение распределения П и Г-стержней", $"Имя семейства должно быть {PRebarDistribCommand.FamName}", panelSketchReinf);
            RibbonItemData distrHomut = App_Helper_Button.CreateButtonData("Распределение хомутов", "Распределение хомутов", "Reinforcement.HomutDistribCommand", Properties.Resources.HomutDistrib,
               "Размещение распределения хомутов", $"Имя семейства должно быть {HomutDistribCommand.FamName}", panelSketchReinf);

            IList<RibbonItem> stackedDistrRebars =
                App_Helper_Button.CreateStackedItems(panelSketchReinf, distrPRebar, distrHomut, "Распределение П и Г-стержней", "Распределение хомутов", tabName);

        }
    }
}
