
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

using FirstRevitPlugin;

namespace Revit_test
{
    internal class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location,
                iconsDirectoryPath = Path.GetDirectoryName(assemblyLocation) + @"\icons\",
                tabName = "База2";

            // создание вкладки 
            
            application.CreateRibbonTab(tabName);
            
            //создание панели

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Первый плагин");



            PushButtonData buttomData = new PushButtonData(nameof(FirstRevitCommand), "приветствие", assemblyLocation,
                typeof(FirstRevitCommand).FullName)
            {
                LargeImage = new BitmapImage(new Uri(iconsDirectoryPath + "green.png"))
            };

            panel.AddItem(buttomData);

            return Result.Succeeded;

        }

        
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        
    }
}
