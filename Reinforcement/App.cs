#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endregion

namespace Reinforcement
{
    internal class App : IExternalApplication
    {
        public void CreateButton(string name, string text, string className, Image img, string toolTip,
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
        public PushButtonData CreateButtonForSplit(string name, string text, string className, Image img, string toolTip,
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

        public Result OnStartup(UIControlledApplication a)
        {
            string tabName = "ЕС КР", 
                   panelName = "Армирование",
                   panel2Name = "Оформление",
                   panel3Name = "Спецификации";
            a.CreateRibbonTab(tabName);
            RibbonPanel panelReinforcement = a.CreateRibbonPanel(tabName, panelName);
            RibbonPanel panelDrawing = a.CreateRibbonPanel(tabName, panel2Name);
            RibbonPanel panelSchedules = a.CreateRibbonPanel(tabName, panel3Name);

            CreateButton("С торца", "С торца", "Reinforcement.RcEndCommand", Properties.Resources.ES_dot1,
                "Размещение арматурного стержня с торца", $"Имя семейства должно быть {RcEndCommand.FamName}", panelReinforcement);

            CreateButton("Спецификации", "Спецификации", "Reinforcement.CreateSchedules", Properties.Resources.ES_Line1,
                "Размещение арматурного стержня с торца", $"Имя семейства должно быть {RcEndCommand.FamName}", panelReinforcement);

            CreateButton("Сбоку", "Сбоку", "Reinforcement.RcLineCommand", Properties.Resources.ES_Line1,
                "Размещение арматурного стержня сбоку", $"Имя семейства должно быть {RcLineCommand.FamName}",
                panelReinforcement);

            CreateButton("Доборные стержни", "Доборные\nстержни", "Reinforcement.RcAddCommand",
                Properties.Resources.ES_dobor,
                "Размещение доборных арматурных стержней", $"Имя семейства должно быть {RcAddCommand.FamName}",
                panelReinforcement);

            CreateButton("Хомут", "Хомут", "Reinforcement.RcHomutCommand", Properties.Resources.ES_homut,
                "Размещение хомута", $"Имя семейства должно быть {RcHomutCommand.FamName}",
                panelReinforcement);

            CreateButton("Фоновое\nармирование", "Фоновое армирование", "Reinforcement.RcFonCommand", Properties.Resources.ES_fon,
                "Размещение фонового армирования", $"Имя семейства должно быть {RcFonCommand.FamName}",
                panelReinforcement);

            CreateButton("Выбрать\nродительское", "Выбрать родительское", "Reinforcement.SelectParentElement", Properties.Resources.ES_Select,
             "Выбрать родительское семейство из спецификации", "Позволяет найти родительское семейство детали",
            panelReinforcement);

            CreateButton("Цвета арматуры", "Цвета арматуры", "Reinforcement.ReinforcementColors", Properties.Resources.ES_RColors,
                "Применение фильтров для цвета арматуры", "Команда работает только в шаблоне ЕС", panelDrawing);

            CreateButton("Спецификации на Пм", "Спецификации на Пм", "Reinforcement.SlabSchedulesCommand", Properties.Resources.ES_Slab,
                "Копирование спецификаций на плиту", "Команда работает только в шаблоне ЕС", panelSchedules);

            CreateButton("Спецификации на Ядж", "Спецификации на Ядж", "Reinforcement.WallSchedulesCommand", Properties.Resources.ES_Wall,
                "Копирование спецификаций на Ядж", "Команда работает только в шаблоне ЕС", panelSchedules);

            SplitButtonData breakLinesData = new SplitButtonData("Линия обрыва", "Линия обрыва");

            SplitButton breakLines = panelDrawing.AddItem(breakLinesData) as SplitButton;

            breakLines.AddPushButton(CreateButtonForSplit("Линия обрыва", "Линия обрыва", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_breakLine,
                "Размещение линии обрыва", $"Имя семейства должно быть {DrBreakLineCommand.FamName}"));
            breakLines.AddPushButton(CreateButtonForSplit("Линия разрыва", "Линия разрыва", "Reinforcement.DrBreakLinesCommand", Properties.Resources.ES_breakLines,
                "Размещение линии разрыва", $"Имя семейства должно быть {DrBreakLinesCommand.FamName}"));





            return Result.Succeeded;
        }
        public BitmapImage Convert(Image img)
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
        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
