#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AW = Autodesk.Windows;
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

        public PushButtonData CreateButtonData(string name, string text, string className, Image img, string toolTip,
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

        public AW.RibbonItem GetButton(string tabName, string panelName, string itemName)
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

        public IList<RibbonItem> CreateStackedItems(RibbonPanel panel, RibbonItemData firstItem,
            RibbonItemData secondItem, string firstButtonName, string secondButtonName, string tabName)
        {
            IList<RibbonItem> stackedItems = panel.AddStackedItems(firstItem, secondItem);
            var firstRibbonItem = GetButton(tabName, panel.Name, firstButtonName);
            var secondRibbonItem = GetButton(tabName, panel.Name, secondButtonName);
            firstRibbonItem.Size = AW.RibbonItemSize.Large;
            firstRibbonItem.ShowText = false;
            secondRibbonItem.Size = AW.RibbonItemSize.Large;
            secondRibbonItem.ShowText = false;

            return stackedItems;
        }
        
        public Result OnStartup(UIControlledApplication a)
        {
            //Create tab
            string tabName = "ЕС КР";
            a.CreateRibbonTab(tabName);

            //Create panels
            RibbonPanel panelSpds = a.CreateRibbonPanel(tabName, "СПДС");
            RibbonPanel panelSketchReinf = a.CreateRibbonPanel(tabName, "Схематичное армирование");
            RibbonPanel panelDetailReinf = a.CreateRibbonPanel(tabName, "Детальное армирование");
            RibbonPanel panelDrawing = a.CreateRibbonPanel(tabName, "Оформление");
            RibbonPanel panelSelection = a.CreateRibbonPanel(tabName, "Выбор");
            RibbonPanel panelSAPR = a.CreateRibbonPanel(tabName, "САПР");




            //1. PanelSpds
             RibbonItemData breakLine = CreateButtonData("Линия обрыва", "Линия обрыва", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_BreakLine,
                "Размещение линии обрыва", $"Имя семейства должно быть {DrBreakLineCommand.FamName}", panelSpds);
             RibbonItemData noteLine = CreateButtonData("Выноска", "Выноска", "Reinforcement.NoteLineCommand", Properties.Resources.ES_NoteLine,
                 "Размещение позиционной выноски", $"Имя семейства должно быть {NoteLineCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedItemsLines =
                 CreateStackedItems(panelSpds, breakLine, noteLine, "Линия обрыва", "Выноска", tabName);

             RibbonItemData axis = CreateButtonData("Строительная ось", "Строительная ось", "Reinforcement.AxisCommand", Properties.Resources.Axes,
                 "Размещение строительной оси", $"Имя семейства должно быть {AxisCommand.FamName}", panelSpds);
             RibbonItemData axisDirection = CreateButtonData("Ориентация оси", "Ориентация оси", "Reinforcement.AxisDirectionCommand", Properties.Resources.Axes_orient,
                 "Размещение указателя ориентация оси", $"Имя семейства должно быть {AxisDirectionCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedItemsAxis =
                 CreateStackedItems(panelSpds, axis, axisDirection, "Строительная ось", "Ориентация оси", tabName);

             RibbonItemData section = CreateButtonData("Разрез", "Разрез", "Reinforcement.SectionCommand", Properties.Resources.Section,
                 "Размещение условного разреза", $"Имя семейства должно быть {SectionCommand.FamName}", panelSpds);
             RibbonItemData elevation = CreateButtonData("Высотная отметка", "Высотная отметка", "Reinforcement.ElevationCommand", Properties.Resources.Elevation,
                 "Размещение высотной отметки", $"Имя семейства должно быть {ElevationCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedSectionElevation =
                 CreateStackedItems(panelSpds, section, elevation, "Разрез", "Высотная отметка", tabName);


            //2. PanelSketchReinf
            CreateButton("Доборные стержни", "Доборные\nстержни", "Reinforcement.RcAddCommand",
                Properties.Resources.ES_Additional_rebars,
                "Размещение доборных арматурных стержней", $"Имя семейства должно быть {RcAddCommand.FamName}",
                panelSketchReinf);

            CreateButton("Фоновое армирование", "Фоновое\nармирование", "Reinforcement.RcFonCommand", Properties.Resources.ES_Background_rebars,
                "Размещение фонового армирования", $"Имя семейства должно быть {RcFonCommand.FamName}",
                panelSketchReinf);

            //3. PanelDetailReinf
            CreateButton("Точка", "Точка", "Reinforcement.RcEndCommand", Properties.Resources.ES_RebarInFront,
                "Размещение арматурного стержня с торца", $"Имя семейства должно быть {RcEndCommand.FamName}", panelDetailReinf);

            CreateButton("Сбоку", "Сбоку", "Reinforcement.RcLineCommand", Properties.Resources.ES_RebarFromSide,
                "Размещение арматурного стержня сбоку", $"Имя семейства должно быть {RcLineCommand.FamName}",
                panelDetailReinf);

            CreateButton("Хомут", "Хомут", "Reinforcement.RcHomutCommand", Properties.Resources.ES_RebarBracket,
                "Размещение хомута", $"Имя семейства должно быть {RcHomutCommand.FamName}",
                panelDetailReinf);

            //4. PanelDrawing
            //Create buttons for changing colors of elements on the active view
            RibbonItemData reinfColors = CreateButtonData("Цвета арматуры", "Цвета арматуры", "Reinforcement.ReinforcementColors", Properties.Resources.ES_RColors,
                "Применение фильтров для цвета арматуры", "Команда не срабатывает при уже назначенных цветовых фильтров на вид", panelDrawing);
            RibbonItemData openColors = CreateButtonData("Цвета отверстий", "Цвета отверстий", "Reinforcement.OpeningsColors", Properties.Resources.ES_OpColors,
                "Применение фильтров для цвета отверстий", "Команда не срабатывает при уже назначенных цветовых фильтров на вид", panelDrawing);
            IList<RibbonItem> stackedItems = panelDrawing.AddStackedItems(openColors, reinfColors);

            var btnReinfColors = GetButton(tabName, panelDrawing.Name, "Цвета арматуры");
            var btnOpenColors = GetButton(tabName, panelDrawing.Name, "Цвета отверстий");

            btnReinfColors.Size = AW.RibbonItemSize.Large;
            btnReinfColors.ShowText = false;

            btnOpenColors.Size = AW.RibbonItemSize.Large;
            btnOpenColors.ShowText = false;



            CreateButton("Оформление вида", "Оформление\nвида", "Reinforcement.DecorViewPlanStage1", Properties.Resources.ES_DecorViewPlanStage1,
            "Оформление вида для стадии П", "",
            panelDrawing);


            //5. PanelSelection
            CreateButton("Выбрать родительское", "Выбрать\nродительское", "Reinforcement.SelectParentElement", Properties.Resources.ES_Select,
             "Выбрать родительское семейство из спецификации", "Позволяет найти родительское семейство детали",
            panelSelection);

            CreateButton("Выбор с фильтром", "Выбор\nс фильтром", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
    "Выбрать элементы по значению параметра - Тип элемента", "тут какая то большая подсказка должна быть я не придумал", panelSelection);

            //6. PanelSAPR

            CreateButton("Копирование спецификаций", "Копирование\nспецификаций", "Reinforcement.CreateSchedules", Properties.Resources.ES_Specification,
             "Позволяет скопировать спецификации с заменой марки конструкции", "Выбираются исходные спецификации и программой создаются аналогичные, но с изменением марки конструкции",
            panelSAPR);
            CreateButton("Расставить сваи по DWG", "Расставить\nсваи по DWG", "Reinforcement.SetPilesByDWG", Properties.Resources.ES_PilesFromDwg,
             "Позволяет расставить экземпляры свай по подгруженной DWG подложке", "Команда позволяет расставить экземпляры семейства в плане",
            panelSAPR);
            CreateButton("Подложки для плит", "Подложки\nдля плит", "Reinforcement.CommandCreateViewPlan", Properties.Resources.ES_ViewsForSlab,
             "Позволяет создать подложки для плиты и вынести их на новый лист", "Создается 3 вида, создается лист. В видах формируется имя вида и заголовок на листе",
            panelSAPR);




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
