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
    internal class App_Panel_1_4_KR_DetailReinf
    {
        public static void KR_DetailReinf(RibbonPanel panelDetailReinf, string tabName)
        {
            //3. PanelDetailReinf
            App_Helper_Button.CreateButton("Точка", "Точка", "Reinforcement.RcEndCommand", Properties.Resources.ES_RebarInFront,
                "Размещение арматурного стержня с торца", $"Имя семейства должно быть {RcEndCommand.FamName}", panelDetailReinf);

            App_Helper_Button.CreateButton("Сбоку", "Сбоку", "Reinforcement.RcLineCommand", Properties.Resources.ES_RebarFromSide,
                "Размещение арматурного стержня сбоку", $"Имя семейства должно быть {RcLineCommand.FamName}",
                panelDetailReinf);

            App_Helper_Button.CreateButton("Хомут", "Хомут", "Reinforcement.RcHomutCommand", Properties.Resources.ES_RebarBracket,
               "Размещение хомута", $"Имя семейства должно быть {RcHomutCommand.FamName}",
               panelDetailReinf);

            RibbonItemData pRebarEqual = App_Helper_Button.CreateButtonData("П-стержень равнополочный", "П-стержень равнополочный", "Reinforcement.PRebarEqualCommand", Properties.Resources.PRebarEqual,
                "Размещение равнополочного п-стержня", $"Имя семейства должно быть {PRebarEqualCommand.FamName}", panelDetailReinf);
            RibbonItemData pRebarNotEqual = App_Helper_Button.CreateButtonData("П-стержень неравнополочный", "П-стержень неравнополочный", "Reinforcement.PRebarNotEqualCommand", Properties.Resources.PRebarNotEqual,
                  "Размещение неравнополочного п-стержня", $"Имя семейства должно быть {PRebarNotEqualCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedPRebars =
                 App_Helper_Button.CreateStackedItems(panelDetailReinf, pRebarEqual, pRebarNotEqual, "П-стержень равнополочный", "П-стержень неравнополочный", tabName);

            RibbonItemData gRebar = App_Helper_Button.CreateButtonData("Г-стержень", "Г-стержень", "Reinforcement.RcGRebarCommand", Properties.Resources.GRebar,
                 "Размещение Г-стержня", $"Имя семейства должно быть {RcGRebarCommand.FamName}", panelDetailReinf);
            RibbonItemData shpilka = App_Helper_Button.CreateButtonData("Шпилька", "Шпилька", "Reinforcement.RcShpilkaCommand", Properties.Resources.Shpilka,
                "Размещение шпильки",
               $"Для размещения нужно выделить два арматурных стержня в сечении по которым построится шпилька\n" +
                 $"Имя семейства должно быть {RcShpilkaCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedGRebarShpilka =
                App_Helper_Button.CreateStackedItems(panelDetailReinf, gRebar, shpilka, "Г-стержень", "Шпилька", tabName);
        }


    }
}
