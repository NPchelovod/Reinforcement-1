using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;



namespace Reinforcement
{
    public class App_Panel_1_7_KR_SAPR
    {
        public static void KR_SAPR(RibbonPanel panelSAPR, string tabName)
        {
            //App_Helper_Button.CreateButton("Копирование спецификаций", "Копирование\nспецификаций", "Reinforcement.CopySelectedSchedules.CommandCopySelectedSchedules", Properties.Resources.ES_Specification,
            //        "Позволяет скопировать спецификации с заменой марки конструкции",
            //        "Для работы плагина нужно сначала выделить спецификации для копирования, а потом нажать на кнопку",
            //       panelSAPR);
            var data = new SplitButtonData("СпецКор", "СпецКор");

            FillPullDown2(panelSAPR, data);

            App_Helper_Button.CreateButton("Подложки для плит", "Подложки\nдля плит", "Reinforcement.CommandCreateViewPlan", Properties.Resources.ES_ViewsForSlab,
             "Позволяет создать подложки для плиты и вынести их на новый лист", "Создается 3 вида, создается лист. В видах формируется имя вида и заголовок на листе\n" +
             "В Марка и отметки вписывается, например, (Пм3 на отм. +3,560) - это нужно только для формирования названий\n " +
             "В префикс вписывается 21, 22 и т.д, для организации видов\n По выбранному в списке уровню создаются подложки", panelSAPR);
            App_Helper_Button.CreateButton("Длина линий (электроразводки)", "Длина труб\nэлектроразводки", "Reinforcement.GetLengthElectricalWiring", Properties.Resources.ElectricalWiring,
             "Позволяет рассчитать длину труб или просто линий, видимых на виде, сгруппированную по диаметрам", "Алгоритм работы с планами электроразводки:\n Открыть вид с аннотационными линиями, для электрики линии должны быть вида <ЭЛ_d25x2, d40>\n распознает диаметры 25 и 40 и кол-во труб d25 - 2 шт., d40 - 1шт.",
            panelSAPR);

            data = new SplitButtonData("СвайКор", "СвайКор");
            FillPullDown(panelSAPR, data);

            //App_Helper_Button.CreateButton("Расставить сваи по DWG", "Расставить\nсваи по DWG", "Reinforcement.SetPilesByDWG", Properties.Resources.ES_PilesFromDwg,
            //"Позволяет расставить экземпляры свай по подгруженной DWG подложке", "Команда позволяет расставить экземпляры семейства в плане. Нужно не забывать кусты свай подвинуть под центр тяжести конструкций (при необходимости)",
            //panelSAPR);

        }
        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;
        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            Image OV1 = Properties.Resources.ES_PilesFromDwg;
            Image OV2 = Properties.Resources.ES_PilesFromDwg;
            

            App_Helper_Button.AddButtonToPullDownButton(item, "Сваи по DWG", assemblyPath, "Reinforcement.SetPilesByDWG", "Сваи из DWG подложки \n\n на виде должны быть расчетная подложка DWG свай в виде точек", OV1);

            App_Helper_Button.AddButtonToPullDownButton(item, "Свай номера", assemblyPath, "Reinforcement.NumPiles", "На виде должны быть сваи,\n\n позволяет нумеромать сваи и выставлять УГО", OV2);


            // Устанавливаем иконку для самой PulldownButton
            System.Windows.Media.ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }
        private static void FillPullDown2(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            Image OV1 = Properties.Resources.ES_Specification;
            Image OV2 = Properties.Resources.ES_Specification;


            App_Helper_Button.AddButtonToPullDownButton(item, "Копирование\nспецификаций", assemblyPath, "Reinforcement.CopySelectedSchedules.CommandCopySelectedSchedules", "Позволяет скопировать спецификации с заменой марки конструкции \n Для работы плагина нужно сначала выделить спецификации для копирования, а потом нажать на кнопку", OV1);


            App_Helper_Button.AddButtonToPullDownButton(item, "Групповая\nспецификация", assemblyPath, "Reinforcement.GroupSpecification", "Редактор групповой спецификации ДЖ, ПМ", OV2);

            // Устанавливаем иконку для самой PulldownButton
            System.Windows.Media.ImageSource imageSource = App_Helper_Button.Convert(OV1);
            item.LargeImage = imageSource;
        }


    }

}
