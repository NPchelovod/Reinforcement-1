using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Reinforcement.App;

namespace Reinforcement
{
    internal class App_Panel_1_2_KR_SPDS 
    {
       
        public static void KR_SPDS(RibbonPanel panelSpds, string tabName)
        {
            //1. PanelSpds
            RibbonItemData breakLine = App_Helper_Button.CreateButtonData("Линия обрыва", "Линия обрыва", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_BreakLine,
                "Размещение линии обрыва", $"Имя семейства должно быть {DrBreakLineCommand.list_Name.FirstOrDefault()}", panelSpds);


            App_Helper_Button.CreateButton("Линия обрыва", "Линия обрыва", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_BreakLine,
                "Размещение линии обрыва", $"Имя семейства должно быть {DrBreakLineCommand.list_Name.FirstOrDefault()}",
                panelSpds);

            RibbonItemData noteLine25mm = App_Helper_Button.CreateButtonData("Выноска25", "Выноска25", "Reinforcement.NoteLineCommand25mm", Properties.Resources.NoteLine_2_5,
                "Размещение позиционной выноски 2,5 мм", $"Имя семейства должно быть {NoteLineCommand25mm.list_Name.FirstOrDefault()}", panelSpds);
            RibbonItemData noteLine35mm = App_Helper_Button.CreateButtonData("Выноска35", "Выноска35", "Reinforcement.NoteLineCommand35mm", Properties.Resources.NoteLine_3_5,
                 "Размещение позиционной выноски 3,5 мм", $"Имя семейства должно быть {NoteLineCommand35mm.list_Name.FirstOrDefault()}", panelSpds);
            IList<RibbonItem> stackedItemsLines =
                 App_Helper_Button.CreateStackedItems(panelSpds, noteLine25mm, noteLine35mm, "Выноска25", "Выноска35", tabName);

            RibbonItemData concreteJoint = App_Helper_Button.CreateButtonData("Шов бетонирования", "Шов бетонирования", "Reinforcement.ConcreteJointCommand", Properties.Resources.ConcreteJoint,
                 "Размещение шва бетонирования в масштабе М50", $"Имя семейства должно быть {ConcreteJointCommand.list_Name.FirstOrDefault()}", panelSpds);
            RibbonItemData axisDirection = App_Helper_Button.CreateButtonData("Строительная ось", "Строительная ось", "Reinforcement.AxisDirectionCommand", Properties.Resources.Axes_orient,
                 "Размещение строительной оси с возможностью преобразования в указатель ориентации оси", $"Имя семейства должно быть {AxisDirectionCommand.list_Name.FirstOrDefault()}", panelSpds);
            IList<RibbonItem> stackedItemsAxis =
                 App_Helper_Button.CreateStackedItems(panelSpds, concreteJoint, axisDirection, "Шов бетонирования", "Строительная ось", tabName);

            RibbonItemData soilBorder = App_Helper_Button.CreateButtonData("Граница грунта", "Граница грунта", "Reinforcement.SoilBorderCommand", Properties.Resources.SoilBorder,
                 "Размещение границы грунта в масштабе М50", $"Имя семейства должно быть {SoilBorderCommand.list_Name.FirstOrDefault()}", panelSpds);
            RibbonItemData waterProof = App_Helper_Button.CreateButtonData("Гидроизоляция", "Гидроизоляция", "Reinforcement.WaterProofCommand", Properties.Resources.WaterProof,
                "Размещение гидроизоляции в масштабе М50", $"Имя семейства должно быть {WaterProofCommand.list_Name.FirstOrDefault()}", panelSpds);
            IList<RibbonItem> stackedItemsSequence =
                App_Helper_Button.CreateStackedItems(panelSpds, soilBorder, waterProof, "Граница грунта", "Гидроизоляция", tabName);

            RibbonItemData section = App_Helper_Button.CreateButtonData("Разрез", "Разрез", "Reinforcement.SectionCommand", Properties.Resources.Section,
                 "Размещение условного разреза", $"Имя семейства должно быть {SectionCommand.list_Name.FirstOrDefault()}", panelSpds);
            RibbonItemData elevation = App_Helper_Button.CreateButtonData("Высотная отметка", "Высотная отметка", "Reinforcement.ElevationCommand", Properties.Resources.Elevation,
                 "Размещение высотной отметки", $"Имя семейства должно быть {ElevationCommand.list_Name.FirstOrDefault()}", panelSpds);
            IList<RibbonItem> stackedSectionElevation =
                 App_Helper_Button.CreateStackedItems(panelSpds, section, elevation, "Разрез", "Высотная отметка", tabName);

            RibbonItemData serif = App_Helper_Button.CreateButtonData("Засечка", "Засечка", "Reinforcement.SerifCommand", Properties.Resources.Serif,
                 "Размещение засечки", $"Имя семейства должно быть {SerifCommand.list_Name.FirstOrDefault()}", panelSpds);
            RibbonItemData arrowView = App_Helper_Button.CreateButtonData("Стрелка вида", "Стрелка вида", "Reinforcement.ArrowViewCommand", Properties.Resources.Arrow_of_view,
                "Размещение стрелки вида", $"Имя семейства должно быть {ArrowViewCommand.list_Name.FirstOrDefault()}", panelSpds);
            IList<RibbonItem> stackedSerifArrow =
                App_Helper_Button.CreateStackedItems(panelSpds, serif, arrowView, "Засечка", "Стрелка вида", tabName);




        }
    }
}
