#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
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
using Updaters;
//using System.Windows.Controls;

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

        //test app








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

       



        public Result OnStartup(UIControlledApplication app)
        {
            //Create tab
            string tabName = "ЕС КР";
            app.CreateRibbonTab(tabName);

            //Create panels
            RibbonPanel panelSpds = app.CreateRibbonPanel(tabName, "СПДС");
            RibbonPanel panelSketchReinf = app.CreateRibbonPanel(tabName, "Схематичное армирование");
            RibbonPanel panelDetailReinf = app.CreateRibbonPanel(tabName, "Детальное армирование");
            RibbonPanel panelDrawing = app.CreateRibbonPanel(tabName, "Оформление");
            RibbonPanel panelSelection = app.CreateRibbonPanel(tabName, "Выбор");
            RibbonPanel panelSAPR = app.CreateRibbonPanel(tabName, "САПР");
            RibbonPanel panelOV = app.CreateRibbonPanel(tabName, "ОВ_сырой");

            //1. PanelSpds
            RibbonItemData breakLine = CreateButtonData("Линия обрыва", "Линия обрыва", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_BreakLine,
                "Размещение линии обрыва", $"Имя семейства должно быть {DrBreakLineCommand.FamName}", panelSpds);
             RibbonItemData noteLine35mm = CreateButtonData("Выноска", "Выноска", "Reinforcement.NoteLineCommand35mm", Properties.Resources.ES_NoteLine,
                 "Размещение позиционной выноски", $"Имя семейства должно быть {NoteLineCommand35mm.FamName}", panelSpds);
            /*
            RibbonItemData noteLine25mm = CreateButtonData("Выноска", "Выноска", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine25,
                "Размещение позиционной выноски", $"Имя семейства должно быть {NoteLineCommand25mm.FamName}", panelSpds);
            /*
            CreateButton("Выноска 2,5", "Выноска 2,5", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine,
              "Размещение позиционной выноски", $"Имя семейства должно быть {NoteLineCommand25mm.FamName}", panelSpds);
            */

            IList<RibbonItem> stackedItemsLines =
                 CreateStackedItems(panelSpds, breakLine, noteLine35mm, "Линия обрыва", "Выноска", tabName);
            /*
            CreateButton("Выноска 2,5", "Выноска 2,5", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine25,
             "Размещение позиционной выноски",
             $"Имя семейства должно быть {NoteLineCommand25mm.FamName}", panelSpds);
            */

            RibbonItemData concreteJoint = CreateButtonData("Шов бетонирования", "Шов бетонирования", "Reinforcement.ConcreteJointCommand", Properties.Resources.ConcreteJoint,
                 "Размещение шва бетонирования в масштабе М50", $"Имя семейства должно быть {ConcreteJointCommand.FamName}", panelSpds);
             RibbonItemData axisDirection = CreateButtonData("Строительная ось", "Строительная ось", "Reinforcement.AxisDirectionCommand", Properties.Resources.Axes_orient,
                 "Размещение строительной оси с возможностью преобразования в указатель ориентации оси", $"Имя семейства должно быть {AxisDirectionCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedItemsAxis =
                 CreateStackedItems(panelSpds, concreteJoint, axisDirection, "Шов бетонирования", "Строительная ось", tabName);

            RibbonItemData soilBorder = CreateButtonData("Граница грунта", "Граница грунта", "Reinforcement.SoilBorderCommand", Properties.Resources.SoilBorder,
                 "Размещение границы грунта в масштабе М50", $"Имя семейства должно быть {SoilBorderCommand.FamName}", panelSpds);
            RibbonItemData waterProof = CreateButtonData("Гидроизоляция", "Гидроизоляция", "Reinforcement.WaterProofCommand", Properties.Resources.WaterProof,
                "Размещение гидроизоляции в масштабе М50", $"Имя семейства должно быть {WaterProofCommand.FamName}", panelSpds);

            IList<RibbonItem> stackedItemsSequence =
                CreateStackedItems(panelSpds, soilBorder, waterProof, "Граница грунта", "Гидроизоляция", tabName);

            RibbonItemData section = CreateButtonData("Разрез", "Разрез", "Reinforcement.SectionCommand", Properties.Resources.Section,
                 "Размещение условного разреза", $"Имя семейства должно быть {SectionCommand.FamName}", panelSpds);
             RibbonItemData elevation = CreateButtonData("Высотная отметка", "Высотная отметка", "Reinforcement.ElevationCommand", Properties.Resources.Elevation,
                 "Размещение высотной отметки", $"Имя семейства должно быть {ElevationCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedSectionElevation =
                 CreateStackedItems(panelSpds, section, elevation, "Разрез", "Высотная отметка", tabName);

            RibbonItemData serif = CreateButtonData("Засечка", "Засечка", "Reinforcement.SerifCommand", Properties.Resources.Serif,
                 "Размещение засечки", $"Имя семейства должно быть {SerifCommand.FamName}", panelSpds);
            RibbonItemData arrowView = CreateButtonData("Стрелка вида", "Стрелка вида", "Reinforcement.ArrowViewCommand", Properties.Resources.Arrow_of_view,
                "Размещение стрелки вида", $"Имя семейства должно быть {ArrowViewCommand.FamName}", panelSpds);

            IList<RibbonItem> stackedSerifArrow =
                CreateStackedItems(panelSpds, serif, arrowView, "Засечка", "Стрелка вида", tabName);






            //2. PanelSketchReinf
            CreateButton("Доборные стержни", "Доборные\nстержни", "Reinforcement.RcAddCommand",
                Properties.Resources.ES_Additional_rebars,
                "Размещение доборных арматурных стержней", $"Имя семейства должно быть {RcAddCommand.FamName}",
                panelSketchReinf);

            CreateButton("Фоновое армирование", "Фоновое\nармирование", "Reinforcement.RcFonCommand", Properties.Resources.ES_Background_rebars,
                "Размещение фонового армирования", $"Имя семейства должно быть {RcFonCommand.FamName}",
                panelSketchReinf);

            RibbonItemData distrPRebar = CreateButtonData("Распределение П и Г-стержней", "Распределение П и Г-стержней", "Reinforcement.PRebarDistribCommand", Properties.Resources.PRebarDistrib,
                 "Размещение распределения П и Г-стержней", $"Имя семейства должно быть {PRebarDistribCommand.FamName}", panelSketchReinf);
            RibbonItemData distrHomut = CreateButtonData("Распределение хомутов", "Распределение хомутов", "Reinforcement.HomutDistribCommand", Properties.Resources.HomutDistrib,
                "Размещение распределения хомутов", $"Имя семейства должно быть {HomutDistribCommand.FamName}", panelSketchReinf);

            IList<RibbonItem> stackedDistrRebars =
                CreateStackedItems(panelSketchReinf, distrPRebar, distrHomut, "Распределение П и Г-стержней", "Распределение хомутов", tabName);


            //3. PanelDetailReinf
            CreateButton("Точка", "Точка", "Reinforcement.RcEndCommand", Properties.Resources.ES_RebarInFront,
                "Размещение арматурного стержня с торца", $"Имя семейства должно быть {RcEndCommand.FamName}", panelDetailReinf);

            CreateButton("Сбоку", "Сбоку", "Reinforcement.RcLineCommand", Properties.Resources.ES_RebarFromSide,
                "Размещение арматурного стержня сбоку", $"Имя семейства должно быть {RcLineCommand.FamName}",
                panelDetailReinf);

            CreateButton("Хомут", "Хомут", "Reinforcement.RcHomutCommand", Properties.Resources.ES_RebarBracket,
                "Размещение хомута", $"Имя семейства должно быть {RcHomutCommand.FamName}",
                panelDetailReinf);

            RibbonItemData pRebarEqual = CreateButtonData("П-стержень равнополочный", "П-стержень равнополочный", "Reinforcement.PRebarEqualCommand", Properties.Resources.PRebarEqual,
                 "Размещение равнополочного п-стержня", $"Имя семейства должно быть {PRebarEqualCommand.FamName}", panelDetailReinf);
            RibbonItemData pRebarNotEqual = CreateButtonData("П-стержень неравнополочный", "П-стержень неравнополочный", "Reinforcement.PRebarNotEqualCommand", Properties.Resources.PRebarNotEqual,
                 "Размещение неравнополочного п-стержня", $"Имя семейства должно быть {PRebarNotEqualCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedPRebars =
                 CreateStackedItems(panelDetailReinf, pRebarEqual, pRebarNotEqual, "П-стержень равнополочный", "П-стержень неравнополочный", tabName);

            RibbonItemData gRebar = CreateButtonData("Г-стержень", "Г-стержень", "Reinforcement.RcGRebarCommand", Properties.Resources.GRebar,
                 "Размещение Г-стержня", $"Имя семейства должно быть {RcGRebarCommand.FamName}", panelDetailReinf);
            RibbonItemData shpilka = CreateButtonData("Шпилька", "Шпилька", "Reinforcement.RcShpilkaCommand", Properties.Resources.Shpilka,
                "Размещение шпильки", 
                $"Для размещения нужно выделить два арматурных стержня в сечении по которым построится шпилька\n" +
                $"Имя семейства должно быть {RcShpilkaCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedGRebarShpilka =
                CreateStackedItems(panelDetailReinf, gRebar, shpilka, "Г-стержень", "Шпилька", tabName);


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

            CreateButton("Оформить\nплан", "Оформить\nплан", "Reinforcement.DecorViewPlan", Properties.Resources.Auto_plan,
            "Команда передвигает оси, наносит размеры между ними и образмеривает Дж",
            "На плане должны быть стены Дж и плита пола.\n" +
            "В будущем планируется добавить больше функциональности для полуавтоматического получения чертежей",
            panelDrawing);

            //CreateButton("Оформить\nразрез", "Оформить\nразрез", "Reinforcement.DecorWallReinfViewSection", Properties.Resources.Auto_razrez,
            //"Команда образмеривает стены, подрезает вид, наносит линии обрыва", "В будущем планируется добавить больше функциональности для полуавтоматического получения чертежей",
            //panelDrawing);


            //5. PanelSelection
            CreateButton("Найти деталь", "Найти\nдеталь", "Reinforcement.SelectParentElement", Properties.Resources.ЕС_Выбор,
             "Позволяет выделить родительское семейство детали из спецификации", 
             "1. Выделить нужную деталь в строчке спецификации\n" +
             "2. Нажать на кнопку\n" +
             "3. Нажать Да для выбора родительского семейства\n" +
             "4. Перейти на любой другой вид.\nТеперь можно поменять свойства детали",
            panelSelection);

    //        CreateButton("Выбор с фильтром", "Выбор\nс фильтром", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
    //"Выбрать элементы по значению параметра - Тип элемента", "тут какая то большая подсказка должна быть я не придумал", panelSelection);


            //6. PanelSAPR

            CreateButton("Копирование спецификаций", "Копирование\nспецификаций", "Reinforcement.CopySelectedSchedules.CommandCopySelectedSchedules", Properties.Resources.ES_Specification,
             "Позволяет скопировать спецификации с заменой марки конструкции", 
             "Для работы плагина нужно сначала выделить спецификации для копирования, а потом нажать на кнопку",
            panelSAPR);
            CreateButton("Расставить сваи по DWG", "Расставить\nсваи по DWG", "Reinforcement.SetPilesByDWG", Properties.Resources.ES_PilesFromDwg,
             "Позволяет расставить экземпляры свай по подгруженной DWG подложке", "Команда позволяет расставить экземпляры семейства в плане. Нужно не забывать кусты свай подвинуть под центр тяжести конструкций (при необходимости)",
            panelSAPR);
            CreateButton("Подложки для плит", "Подложки\nдля плит", "Reinforcement.CommandCreateViewPlan", Properties.Resources.ES_ViewsForSlab,
             "Позволяет создать подложки для плиты и вынести их на новый лист", "Создается 3 вида, создается лист. В видах формируется имя вида и заголовок на листе\n" +
             "В Марка и отметки вписывается, например, (Пм3 на отм. +3,560) - это нужно только для формирования названий\n " +
             "В префикс вписывается 21, 22 и т.д, для организации видов\n По выбранному в списке уровню создаются подложки", panelSAPR);
            CreateButton("Длина труб электроразводки", "Длина труб\nэлектроразводки", "Reinforcement.GetLengthElectricalWiring", Properties.Resources.ElectricalWiring,
             "Позволяет рассчитать длину труб, видимых на виде, сгруппированную по диаметрам", "Алгоритм работы с планами электроразводки:\n1. Подготавливается подложка в DWG;\n2. Импорт САПР, расчленить",
            panelSAPR);



            // 7. panelOV временная 
            CreateButton("Создание ОВ листов", "Создание\nОВ листов", "Reinforcement.OV_Constuct_Command", Properties.Resources.ES_OV_for_KR,
             "Позволяет создать",
             "Для работы плагина нужно ",
            panelOV);

            //8. Updater
            RegisterUpdater.addInId = app.ActiveAddInId;
            RegisterUpdater.Register();
            
            return Result.Succeeded;
        }

       /* private void ControlledApp_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {

        }*/

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
