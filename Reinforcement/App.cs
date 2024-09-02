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

        public Result OnStartup(UIControlledApplication a)
        {
            string tabName = "�� ��", 
                   panelName = "�����������",
                   panel2Name = "����������",
                   panel3Name = "������������",
                   panel4Name = "�����";
            a.CreateRibbonTab(tabName);
            RibbonPanel panelReinforcement = a.CreateRibbonPanel(tabName, panelName);
            RibbonPanel panelDrawing = a.CreateRibbonPanel(tabName, panel2Name);
            RibbonPanel panelSchedules = a.CreateRibbonPanel(tabName, panel3Name);
            RibbonPanel panelSelection = a.CreateRibbonPanel(tabName, panel4Name);


            CreateButton("� �����", "� �����", "Reinforcement.RcEndCommand", Properties.Resources.ES_dot1,
                "���������� ����������� ������� � �����", $"��� ��������� ������ ���� {RcEndCommand.FamName}", panelReinforcement);

            CreateButton("������������", "������������", "Reinforcement.CreateSchedules", Properties.Resources.ES_Line1,
                "���������� ����������� ������� � �����", $"��� ��������� ������ ���� {RcEndCommand.FamName}", panelReinforcement);

            CreateButton("�����", "�����", "Reinforcement.RcLineCommand", Properties.Resources.ES_Line1,
                "���������� ����������� ������� �����", $"��� ��������� ������ ���� {RcLineCommand.FamName}",
                panelReinforcement);

            CreateButton("�������� �������", "��������\n�������", "Reinforcement.RcAddCommand",
                Properties.Resources.ES_dobor,
                "���������� �������� ���������� ��������", $"��� ��������� ������ ���� {RcAddCommand.FamName}",
                panelReinforcement);

            CreateButton("�����", "�����", "Reinforcement.RcHomutCommand", Properties.Resources.ES_homut,
                "���������� ������", $"��� ��������� ������ ���� {RcHomutCommand.FamName}",
                panelReinforcement);

            CreateButton("�������\n�����������", "������� �����������", "Reinforcement.RcFonCommand", Properties.Resources.ES_fon,
                "���������� �������� �����������", $"��� ��������� ������ ���� {RcFonCommand.FamName}",
                panelReinforcement);

            CreateButton("�������\n������������", "������� ������������", "Reinforcement.SelectParentElement", Properties.Resources.ES_Select,
             "������� ������������ ��������� �� ������������", "��������� ����� ������������ ��������� ������",
            panelReinforcement);

                //Create buttons for changing colors of elements on the active view
            RibbonItemData reinfColors = CreateButtonData("����� ��������", "����� ��������", "Reinforcement.ReinforcementColors", Properties.Resources.ES_RColors,
                "���������� �������� ��� ����� ��������", "������� �� ����������� ��� ��� ����������� �������� �������� �� ���", panelDrawing);
            RibbonItemData openColors = CreateButtonData("����� ���������", "����� ���������", "Reinforcement.OpeningsColors", Properties.Resources.ES_OpColors,
                "���������� �������� ��� ����� ���������", "������� �� ����������� ��� ��� ����������� �������� �������� �� ���", panelDrawing);
            IList<RibbonItem> stackedItems = panelDrawing.AddStackedItems(openColors, reinfColors);

            CreateButton("������������ �� ��", "������������ �� ��", "Reinforcement.SlabSchedulesCommand", Properties.Resources.ES_Slab,
                "����������� ������������ �� �����", "������� �������� ������ � ������� ��", panelSchedules);

            CreateButton("������������ �� ���", "������������ �� ���", "Reinforcement.WallSchedulesCommand", Properties.Resources.ES_Wall,
                "����������� ������������ �� ���", "������� �������� ������ � ������� ��", panelSchedules);

            CreateButton("����� � ��������", "����� � ��������", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
    "������� �������� �� �������� ��������� - ��� ��������", "��� ����� �� ������� ��������� ������ ���� � �� ��������", panelSelection); ;


            SplitButtonData breakLinesData = new SplitButtonData("����� ������", "����� ������");

            SplitButton breakLines = panelDrawing.AddItem(breakLinesData) as SplitButton;

            breakLines.AddPushButton(CreateButtonForSplit("����� ������", "����� ������", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_breakLine,
                "���������� ����� ������", $"��� ��������� ������ ���� {DrBreakLineCommand.FamName}"));
            breakLines.AddPushButton(CreateButtonForSplit("����� �������", "����� �������", "Reinforcement.DrBreakLinesCommand", Properties.Resources.ES_breakLines,
                "���������� ����� �������", $"��� ��������� ������ ���� {DrBreakLinesCommand.FamName}"));

            CreateButton("����������\n����", "���������� ����", "Reinforcement.DecorViewPlanStage1", Properties.Resources.ES_DecorViewPlanStage1,
            "���������� ���� ��� ������ �", "",
            panelDrawing);





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
