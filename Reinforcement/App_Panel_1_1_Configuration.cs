using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Reinforcement
{
    internal class App_Panel_1_1_Configuration
    {
        public static void AddSplitButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new SplitButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }

        public static void AddPullDownButton(RibbonPanel ribbonPanel, string name)
        {
            var data = new PulldownButtonData(name, name);
            FillPullDown(ribbonPanel, data);
        }

        private static readonly string assemblyPath = Assembly.GetExecutingAssembly().Location;

        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;

            // Добавляем кнопки с иконками
            AddButtonToPullDownButton(item, "КР", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 1 — подсказка");
            AddButtonToPullDownButton(item, "ОВ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 2 — подсказка");
            AddButtonToPullDownButton(item, "ЭЛ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 3 — подсказка");

            item.AddSeparator();
            AddButtonToPullDownButton(item, "вр", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 4 — подсказка");

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = Convert(Properties.Resources.ES_BreakLine);
            item.LargeImage = imageSource;
        }

        private static void AddButtonToPullDownButton(PulldownButton button, string name, string path, string linkToCommand, string toolTip)
        {
            var data = new PushButtonData(name, name, path, linkToCommand);
            var pushButton = button.AddPushButton(data) as PushButton;
            pushButton.ToolTip = toolTip;

            // Загружаем изображения
            var smallImage =  Convert(Properties.Resources.ES_BreakLine);

            ImageSource imageSource = Convert(Properties.Resources.ES_BreakLine);
            var largeImage = imageSource;

            // Устанавливаем изображения с проверкой на null
            if (smallImage != null) pushButton.Image = smallImage;
            if (largeImage != null) pushButton.LargeImage = largeImage;
        }

        public static BitmapImage Convert(Image img)
        {
            if (img == null)
            {
                {
                    throw new ArgumentNullException(nameof(img), "Изображение не может быть null.");
                }
            }

            try
            {
                using (var memory = new MemoryStream())
                {

                    img.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();

                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    return bitmapImage;
                }
            }

            catch (Exception ex)
            {
                // Логирование ошибки, если нужно
                Debug.WriteLine($"Ошибка при создании BitmapImage: {ex.Message}");
                return null; // или вернуть заглушку
            }


        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}