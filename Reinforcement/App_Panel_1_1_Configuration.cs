using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Resources;
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
        private static global::System.Globalization.CultureInfo resourceCulture;
        private static global::System.Resources.ResourceManager resourceMan;

        private static void FillPullDown(RibbonPanel ribbonPanel, PulldownButtonData data)
        {
            var item = ribbonPanel.AddItem(data) as PulldownButton;
            AddButtonToPullDownButton(item, "КР", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 1 — подсказка");
            AddButtonToPullDownButton(item, "ОВ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 2 — подсказка");
            AddButtonToPullDownButton(item, "ЭЛ", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 3 — подсказка");
            item.AddSeparator();
            AddButtonToPullDownButton(item, "вр", assemblyPath, "Reinforcement.OV_Constuct_Command", "Команда 4 — подсказка");
            //item.LargeImage = (ImageSource)new BitmapImage(new Uri(@"/Reinforcement;component/Resources/Panel_1_1_Configuration/KR3.png", UriKind.RelativeOrAbsolute));
            item.LargeImage = LoadImageFromResource(@"Reinforcement.Resources.Panel_1_1_Configuration.KR2.png");
        }
        private static void AddButtonToPullDownButton(PulldownButton button, string name, string path, string linkToCommand, string toolTip)
        {
            var data = new PushButtonData(name, name, path, linkToCommand);
            var pushButton = button.AddPushButton(data) as PushButton;
            pushButton.ToolTip = toolTip;

            // Загрузка изображений из ресурсов сборки
            pushButton.Image = LoadImageFromResource(@"Reinforcement.Resources.Panel_1_1_Configuration.KR2.png");
            pushButton.LargeImage = LoadImageFromResource("Reinforcement.Resources.Panel_1_1_Configuration.KR.png");

            //pushButton.Image = (ImageSource)new BitmapImage(new Uri(@"/Reinforcement;component/Resources/Panel_1_1_Configuration/KR2.png", UriKind.RelativeOrAbsolute));
            //pushButton.LargeImage = (ImageSource)new BitmapImage(new Uri(@"/Reinforcement;component/Resources/Panel_1_1_Configuration/KR.png", UriKind.RelativeOrAbsolute));

           
        }
        private static ImageSource LoadImageFromResource(string resourcePath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourcePath))
                {
                    


                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to load image: {ex.Message}");
                return null;
            }
        }


    }
}
