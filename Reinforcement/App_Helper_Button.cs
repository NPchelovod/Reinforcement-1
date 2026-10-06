using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Drawing;

using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media;
using AW = Autodesk.Windows;

namespace Reinforcement


// служит для добавления кнопок 
{
    public class App_Helper_Button
    {
        // для конфигуратора
        public static void AddButtonToPullDownButton(PulldownButton button, string name, string path, string linkToCommand, string toolTip, Image img)
        {
            var data = new PushButtonData(name, name, path, linkToCommand);
            var pushButton = button.AddPushButton(data) as PushButton;
            pushButton.ToolTip = toolTip;

            // Загружаем изображения
            var smallImage = App_Helper_Button.Convert(img);

            ImageSource imageSource = smallImage;
            var largeImage = imageSource;

            // Устанавливаем изображения с проверкой на null
            if (smallImage != null) pushButton.Image = smallImage;
            if (largeImage != null) pushButton.LargeImage = largeImage;
        }










        public static IList<RibbonItem> CreateStackedItems(RibbonPanel panel, RibbonItemData firstItem,
            RibbonItemData secondItem, string firstButtonName, string secondButtonName, string tabName)
        {
            IList<RibbonItem> stackedItems = panel.AddStackedItems(firstItem, secondItem);
            var firstRibbonItem = App_Helper_Button.GetButton(tabName, panel.Name, firstButtonName);
            var secondRibbonItem = App_Helper_Button.GetButton(tabName, panel.Name, secondButtonName);
            if (firstRibbonItem != null)
            {
                firstRibbonItem.Size = AW.RibbonItemSize.Large;
                firstRibbonItem.ShowText = false;
            }
            if (secondRibbonItem != null)
            {
                secondRibbonItem.Size = AW.RibbonItemSize.Large;
                secondRibbonItem.ShowText = false;
            }

            return stackedItems;
        }

        public static void CreateButton(string name, string text, string className, Image img, string toolTip, string longDescription, RibbonPanel panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            panel.AddItem(CreateButtonData(name, text, className, img, toolTip, longDescription, panel));
        }



        public static PushButtonData CreateButtonForSplit(string name, string text, string className, Image img, string toolTip, string longDescription)
        { return CreateButtonData(name, text, className, img, toolTip, longDescription, null); }



        public static PushButtonData CreateButtonData(string name, string text, string className, Image img, string toolTip,
            string longDescription, RibbonPanel panel)
        {
            PushButtonData buttonData =
                new PushButtonData(name, text, Assembly.GetExecutingAssembly().Location, className);
            ImageSource imageSource = Convert(img);
            buttonData.LargeImage = imageSource;
            buttonData.Image = imageSource;
            buttonData.ToolTip = toolTip;
            buttonData.LongDescription = longDescription;
            return buttonData;
        }

        public static AW.RibbonItem GetButton(string tabName, string panelName, string itemName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;
            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    foreach (AW.RibbonPanel panel in tab.Panels)
                    {
                        if (panel.Source.Title == panelName)
                        {
                            return panel.FindItem("CustomCtrl_%CustomCtrl_%" + tabName + "%" + panelName + "%" + itemName, true) as
                                AW.RibbonItem;
                        }
                    }
                }
            }
            return null;
        }






        public static BitmapImage Convert(Image img)
        {
            if (img == null) throw new ArgumentNullException(nameof(img));
            try { return RibbonImageHelper.Convert(img); }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
                Debug.WriteLine(ex.Message); return null;
            }
        }




    }
}
