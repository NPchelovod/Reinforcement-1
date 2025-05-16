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
    public class App_Panel_1_7_KR_SAPR
    {
        public static void KR_SAPR(RibbonPanel panelSAPR, string tabName)
        {
            App_Helper_Button.CreateButton("Копирование спецификаций", "Копирование\nспецификаций", "Reinforcement.CopySelectedSchedules.CommandCopySelectedSchedules", Properties.Resources.ES_Specification,
                    "Позволяет скопировать спецификации с заменой марки конструкции",
                    "Для работы плагина нужно сначала выделить спецификации для копирования, а потом нажать на кнопку",
                   panelSAPR);
            App_Helper_Button.CreateButton("Расставить сваи по DWG", "Расставить\nсваи по DWG", "Reinforcement.SetPilesByDWG", Properties.Resources.ES_PilesFromDwg,
             "Позволяет расставить экземпляры свай по подгруженной DWG подложке", "Команда позволяет расставить экземпляры семейства в плане. Нужно не забывать кусты свай подвинуть под центр тяжести конструкций (при необходимости)",
             panelSAPR);
            App_Helper_Button.CreateButton("Подложки для плит", "Подложки\nдля плит", "Reinforcement.CommandCreateViewPlan", Properties.Resources.ES_ViewsForSlab,
             "Позволяет создать подложки для плиты и вынести их на новый лист", "Создается 3 вида, создается лист. В видах формируется имя вида и заголовок на листе\n" +
             "В Марка и отметки вписывается, например, (Пм3 на отм. +3,560) - это нужно только для формирования названий\n " +
             "В префикс вписывается 21, 22 и т.д, для организации видов\n По выбранному в списке уровню создаются подложки", panelSAPR);
            App_Helper_Button.CreateButton("Длина труб электроразводки", "Длина труб\nэлектроразводки", "Reinforcement.GetLengthElectricalWiring", Properties.Resources.ElectricalWiring,
             "Позволяет рассчитать длину труб, видимых на виде, сгруппированную по диаметрам", "Алгоритм работы с планами электроразводки:\n1. Подготавливается подложка в DWG;\n2. Импорт САПР, расчленить",
            panelSAPR);
        }
    }

}
