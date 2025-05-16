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
    public class App_Panel_1_6_KR_Selection
    {
        public static void KR_Selection(RibbonPanel panelSelection, string tabName)
        {
            App_Helper_Button.CreateButton("Найти деталь", "Найти\nдеталь", "Reinforcement.SelectParentElement", Properties.Resources.ЕС_Выбор,
               "Позволяет выделить родительское семейство детали из спецификации",
               "1. Выделить нужную деталь в строчке спецификации\n" +
               "2. Нажать на кнопку\n" +
               "3. Нажать Да для выбора родительского семейства\n" +
              "4. Перейти на любой другой вид.\nТеперь можно поменять свойства детали",
              panelSelection);

            App_Helper_Button.CreateButton("Выбор с фильтром", "Выбор\nс фильтром", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
     "Выбрать элементы по значению параметра - Тип элемента", "тут какая то большая подсказка должна быть я не придумал", panelSelection);
        }
    }
}
