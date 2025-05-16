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
            Image KR_config = Properties.Resources.KR_config;
            Image OV_config = Properties.Resources.OV_config;
            Image VK_config = Properties.Resources.VK_config;
            Image EL_config = Properties.Resources.EL_config;

            AddButtonToPullDownButton(item, "КР", assemblyPath, "Reinforcement.OV_Constuct_Command", "Конструктив", KR_config);
            AddButtonToPullDownButton(item, "ОВ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Отопление и Вентиляция", OV_config);

            AddButtonToPullDownButton(item, "ВК", assemblyPath, "Reinforcement.OV_Constuct_Command", "Водснаб и Канализация", VK_config);

            item.AddSeparator();
            AddButtonToPullDownButton(item, "ЭЛ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Электрика", EL_config);

            AddButtonToPullDownButton(item, "тест", assemblyPath, "Reinforcement.OV_Constuct_Command", "не трогать", EL_config);

            // Устанавливаем иконку для самой PulldownButton
            ImageSource imageSource = App_Helper_Button.Convert(KR_config);
            item.LargeImage = imageSource;
        }

        private static void AddButtonToPullDownButton(PulldownButton button, string name, string path, string linkToCommand, string toolTip, Image img)
        {
            var data = new PushButtonData(name, name, path, linkToCommand);
            var pushButton = button.AddPushButton(data) as PushButton;
            pushButton.ToolTip = toolTip;

            // Загружаем изображения
            var smallImage = App_Helper_Button.Convert(img);

            ImageSource imageSource = App_Helper_Button.Convert(img);
            var largeImage = imageSource;

            // Устанавливаем изображения с проверкой на null
            if (smallImage != null) pushButton.Image = smallImage;
            if (largeImage != null) pushButton.LargeImage = largeImage;
        }

       
    }
}