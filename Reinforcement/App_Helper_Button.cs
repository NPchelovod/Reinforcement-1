using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public static IList<RibbonItem> CreateStackedItems(RibbonPanel panel, RibbonItemData firstItem,
            RibbonItemData secondItem, string firstButtonName, string secondButtonName, string tabName)
        {
            IList<RibbonItem> stackedItems = panel.AddStackedItems(firstItem, secondItem);
            var firstRibbonItem = App_Helper_Button.GetButton(tabName, panel.Name, firstButtonName);
            var secondRibbonItem = App_Helper_Button.GetButton(tabName, panel.Name, secondButtonName);
            firstRibbonItem.Size = AW.RibbonItemSize.Large;
            firstRibbonItem.ShowText = false;
            secondRibbonItem.Size = AW.RibbonItemSize.Large;
            secondRibbonItem.ShowText = false;

            return stackedItems;
        }

        public static void CreateButton(string name, string text, string className, Image img, string toolTip,
           string longDescription, RibbonPanel panel)
        {
            PushButtonData buttonData =
                new PushButtonData(name, text, Assembly.GetExecutingAssembly().Location, className);
            PushButton button = panel.AddItem(buttonData) as PushButton;
            ImageSource imageSource = Convert(img);
            button.LargeImage = imageSource;
            button.Image = imageSource;
            button.ToolTip = toolTip;
            button.LongDescription = longDescription;
        }



        public static PushButtonData CreateButtonForSplit(string name, string text, string className, Image img, string toolTip,
            string longDescription)
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
            if (img == null)
            {
                throw new ArgumentNullException(nameof(img), "Изображение не может быть null.");
            }

            try
            {
                // Создаём новое изображение 32x32 с DPI = 96
                var resizedImage = new Bitmap(32, 32);
                resizedImage.SetResolution(96, 96); // Устанавливаем DPI

                // Используем Graphics для качественного изменения размера
                using (var graphics = Graphics.FromImage(resizedImage))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    graphics.DrawImage(img, 0, 0, 32, 32);
                }

                // Конвертируем в BitmapImage
                using (var memory = new MemoryStream())
                {
                    resizedImage.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    resizedImage.Dispose(); // Освобождаем ресурсы
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при создании BitmapImage: {ex.Message}");
                return null;
            }
        }




    }
}
