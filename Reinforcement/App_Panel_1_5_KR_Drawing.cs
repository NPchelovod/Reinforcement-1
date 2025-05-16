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
using AW = Autodesk.Windows;

namespace Reinforcement
{
    public class App_Panel_1_5_KR_Drawing
    {
        public static void KR_Drawing(RibbonPanel panelDrawing, string tabName)
        {
            //4. PanelDrawing
            //Create buttons for changing colors of elements on the active view
            RibbonItemData reinfColors = App_Helper_Button.CreateButtonData("Цвета арматуры", "Цвета арматуры", "Reinforcement.ReinforcementColors", Properties.Resources.ES_RColors,
               "Применение фильтров для цвета арматуры", "Команда не срабатывает при уже назначенных цветовых фильтров на вид", panelDrawing);
            RibbonItemData openColors = App_Helper_Button.CreateButtonData("Цвета отверстий", "Цвета отверстий", "Reinforcement.OpeningsColors", Properties.Resources.ES_OpColors,
               "Применение фильтров для цвета отверстий", "Команда не срабатывает при уже назначенных цветовых фильтров на вид", panelDrawing);
            IList<RibbonItem> stackedItems = panelDrawing.AddStackedItems(openColors, reinfColors);

            var btnReinfColors = App_Helper_Button.GetButton(tabName, panelDrawing.Name, "Цвета арматуры");
            var btnOpenColors = App_Helper_Button.GetButton(tabName, panelDrawing.Name, "Цвета отверстий");

            btnReinfColors.Size = AW.RibbonItemSize.Large;
            btnReinfColors.ShowText = false;

            btnOpenColors.Size = AW.RibbonItemSize.Large;
            btnOpenColors.ShowText = false;

            App_Helper_Button.CreateButton("Оформить\nплан", "Оформить\nплан", "Reinforcement.DecorViewPlan", Properties.Resources.Auto_plan,
            "Команда передвигает оси, наносит размеры между ними и образмеривает Дж",
            "На плане должны быть стены Дж и плита пола.\n" +
           "В будущем планируется добавить больше функциональности для полуавтоматического получения чертежей",
            panelDrawing);

            App_Helper_Button.CreateButton("Оформить\nразрез", "Оформить\nразрез", "Reinforcement.DecorWallReinfViewSection", Properties.Resources.Auto_razrez,
            "Команда образмеривает стены, подрезает вид, наносит линии обрыва", "В будущем планируется добавить больше функциональности для полуавтоматического получения чертежей",
            panelDrawing);
        }
    }
}
