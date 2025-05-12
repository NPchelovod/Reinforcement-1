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
            string tabName = "�� ��";
            app.CreateRibbonTab(tabName);

            //Create panels
            RibbonPanel panelSpds = app.CreateRibbonPanel(tabName, "����");
            RibbonPanel panelSketchReinf = app.CreateRibbonPanel(tabName, "����������� �����������");
            RibbonPanel panelDetailReinf = app.CreateRibbonPanel(tabName, "��������� �����������");
            RibbonPanel panelDrawing = app.CreateRibbonPanel(tabName, "����������");
            RibbonPanel panelSelection = app.CreateRibbonPanel(tabName, "�����");
            RibbonPanel panelSAPR = app.CreateRibbonPanel(tabName, "����");
            RibbonPanel panelOV = app.CreateRibbonPanel(tabName, "��_�����");

            //1. PanelSpds
            RibbonItemData breakLine = CreateButtonData("����� ������", "����� ������", "Reinforcement.DrBreakLineCommand", Properties.Resources.ES_BreakLine,
                "���������� ����� ������", $"��� ��������� ������ ���� {DrBreakLineCommand.FamName}", panelSpds);
             RibbonItemData noteLine35mm = CreateButtonData("�������", "�������", "Reinforcement.NoteLineCommand35mm", Properties.Resources.ES_NoteLine,
                 "���������� ����������� �������", $"��� ��������� ������ ���� {NoteLineCommand35mm.FamName}", panelSpds);
            /*
            RibbonItemData noteLine25mm = CreateButtonData("�������", "�������", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine25,
                "���������� ����������� �������", $"��� ��������� ������ ���� {NoteLineCommand25mm.FamName}", panelSpds);
            /*
            CreateButton("������� 2,5", "������� 2,5", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine,
              "���������� ����������� �������", $"��� ��������� ������ ���� {NoteLineCommand25mm.FamName}", panelSpds);
            */

            IList<RibbonItem> stackedItemsLines =
                 CreateStackedItems(panelSpds, breakLine, noteLine35mm, "����� ������", "�������", tabName);
            /*
            CreateButton("������� 2,5", "������� 2,5", "Reinforcement.NoteLineCommand25mm", Properties.Resources.ES_NoteLine25,
             "���������� ����������� �������",
             $"��� ��������� ������ ���� {NoteLineCommand25mm.FamName}", panelSpds);
            */

            RibbonItemData concreteJoint = CreateButtonData("��� �������������", "��� �������������", "Reinforcement.ConcreteJointCommand", Properties.Resources.ConcreteJoint,
                 "���������� ��� ������������� � �������� �50", $"��� ��������� ������ ���� {ConcreteJointCommand.FamName}", panelSpds);
             RibbonItemData axisDirection = CreateButtonData("������������ ���", "������������ ���", "Reinforcement.AxisDirectionCommand", Properties.Resources.Axes_orient,
                 "���������� ������������ ��� � ������������ �������������� � ��������� ���������� ���", $"��� ��������� ������ ���� {AxisDirectionCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedItemsAxis =
                 CreateStackedItems(panelSpds, concreteJoint, axisDirection, "��� �������������", "������������ ���", tabName);

            RibbonItemData soilBorder = CreateButtonData("������� ������", "������� ������", "Reinforcement.SoilBorderCommand", Properties.Resources.SoilBorder,
                 "���������� ������� ������ � �������� �50", $"��� ��������� ������ ���� {SoilBorderCommand.FamName}", panelSpds);
            RibbonItemData waterProof = CreateButtonData("�������������", "�������������", "Reinforcement.WaterProofCommand", Properties.Resources.WaterProof,
                "���������� ������������� � �������� �50", $"��� ��������� ������ ���� {WaterProofCommand.FamName}", panelSpds);

            IList<RibbonItem> stackedItemsSequence =
                CreateStackedItems(panelSpds, soilBorder, waterProof, "������� ������", "�������������", tabName);

            RibbonItemData section = CreateButtonData("������", "������", "Reinforcement.SectionCommand", Properties.Resources.Section,
                 "���������� ��������� �������", $"��� ��������� ������ ���� {SectionCommand.FamName}", panelSpds);
             RibbonItemData elevation = CreateButtonData("�������� �������", "�������� �������", "Reinforcement.ElevationCommand", Properties.Resources.Elevation,
                 "���������� �������� �������", $"��� ��������� ������ ���� {ElevationCommand.FamName}", panelSpds);

             IList<RibbonItem> stackedSectionElevation =
                 CreateStackedItems(panelSpds, section, elevation, "������", "�������� �������", tabName);

            RibbonItemData serif = CreateButtonData("�������", "�������", "Reinforcement.SerifCommand", Properties.Resources.Serif,
                 "���������� �������", $"��� ��������� ������ ���� {SerifCommand.FamName}", panelSpds);
            RibbonItemData arrowView = CreateButtonData("������� ����", "������� ����", "Reinforcement.ArrowViewCommand", Properties.Resources.Arrow_of_view,
                "���������� ������� ����", $"��� ��������� ������ ���� {ArrowViewCommand.FamName}", panelSpds);

            IList<RibbonItem> stackedSerifArrow =
                CreateStackedItems(panelSpds, serif, arrowView, "�������", "������� ����", tabName);






            //2. PanelSketchReinf
            CreateButton("�������� �������", "��������\n�������", "Reinforcement.RcAddCommand",
                Properties.Resources.ES_Additional_rebars,
                "���������� �������� ���������� ��������", $"��� ��������� ������ ���� {RcAddCommand.FamName}",
                panelSketchReinf);

            CreateButton("������� �����������", "�������\n�����������", "Reinforcement.RcFonCommand", Properties.Resources.ES_Background_rebars,
                "���������� �������� �����������", $"��� ��������� ������ ���� {RcFonCommand.FamName}",
                panelSketchReinf);

            RibbonItemData distrPRebar = CreateButtonData("������������� � � �-��������", "������������� � � �-��������", "Reinforcement.PRebarDistribCommand", Properties.Resources.PRebarDistrib,
                 "���������� ������������� � � �-��������", $"��� ��������� ������ ���� {PRebarDistribCommand.FamName}", panelSketchReinf);
            RibbonItemData distrHomut = CreateButtonData("������������� �������", "������������� �������", "Reinforcement.HomutDistribCommand", Properties.Resources.HomutDistrib,
                "���������� ������������� �������", $"��� ��������� ������ ���� {HomutDistribCommand.FamName}", panelSketchReinf);

            IList<RibbonItem> stackedDistrRebars =
                CreateStackedItems(panelSketchReinf, distrPRebar, distrHomut, "������������� � � �-��������", "������������� �������", tabName);


            //3. PanelDetailReinf
            CreateButton("�����", "�����", "Reinforcement.RcEndCommand", Properties.Resources.ES_RebarInFront,
                "���������� ����������� ������� � �����", $"��� ��������� ������ ���� {RcEndCommand.FamName}", panelDetailReinf);

            CreateButton("�����", "�����", "Reinforcement.RcLineCommand", Properties.Resources.ES_RebarFromSide,
                "���������� ����������� ������� �����", $"��� ��������� ������ ���� {RcLineCommand.FamName}",
                panelDetailReinf);

            CreateButton("�����", "�����", "Reinforcement.RcHomutCommand", Properties.Resources.ES_RebarBracket,
                "���������� ������", $"��� ��������� ������ ���� {RcHomutCommand.FamName}",
                panelDetailReinf);

            RibbonItemData pRebarEqual = CreateButtonData("�-�������� �������������", "�-�������� �������������", "Reinforcement.PRebarEqualCommand", Properties.Resources.PRebarEqual,
                 "���������� �������������� �-�������", $"��� ��������� ������ ���� {PRebarEqualCommand.FamName}", panelDetailReinf);
            RibbonItemData pRebarNotEqual = CreateButtonData("�-�������� ���������������", "�-�������� ���������������", "Reinforcement.PRebarNotEqualCommand", Properties.Resources.PRebarNotEqual,
                 "���������� ���������������� �-�������", $"��� ��������� ������ ���� {PRebarNotEqualCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedPRebars =
                 CreateStackedItems(panelDetailReinf, pRebarEqual, pRebarNotEqual, "�-�������� �������������", "�-�������� ���������������", tabName);

            RibbonItemData gRebar = CreateButtonData("�-��������", "�-��������", "Reinforcement.RcGRebarCommand", Properties.Resources.GRebar,
                 "���������� �-�������", $"��� ��������� ������ ���� {RcGRebarCommand.FamName}", panelDetailReinf);
            RibbonItemData shpilka = CreateButtonData("�������", "�������", "Reinforcement.RcShpilkaCommand", Properties.Resources.Shpilka,
                "���������� �������", 
                $"��� ���������� ����� �������� ��� ���������� ������� � ������� �� ������� ���������� �������\n" +
                $"��� ��������� ������ ���� {RcShpilkaCommand.FamName}", panelDetailReinf);

            IList<RibbonItem> stackedGRebarShpilka =
                CreateStackedItems(panelDetailReinf, gRebar, shpilka, "�-��������", "�������", tabName);


            //4. PanelDrawing
            //Create buttons for changing colors of elements on the active view
            RibbonItemData reinfColors = CreateButtonData("����� ��������", "����� ��������", "Reinforcement.ReinforcementColors", Properties.Resources.ES_RColors,
                "���������� �������� ��� ����� ��������", "������� �� ����������� ��� ��� ����������� �������� �������� �� ���", panelDrawing);
            RibbonItemData openColors = CreateButtonData("����� ���������", "����� ���������", "Reinforcement.OpeningsColors", Properties.Resources.ES_OpColors,
                "���������� �������� ��� ����� ���������", "������� �� ����������� ��� ��� ����������� �������� �������� �� ���", panelDrawing);
            IList<RibbonItem> stackedItems = panelDrawing.AddStackedItems(openColors, reinfColors);

            var btnReinfColors = GetButton(tabName, panelDrawing.Name, "����� ��������");
            var btnOpenColors = GetButton(tabName, panelDrawing.Name, "����� ���������");

            btnReinfColors.Size = AW.RibbonItemSize.Large;
            btnReinfColors.ShowText = false;

            btnOpenColors.Size = AW.RibbonItemSize.Large;
            btnOpenColors.ShowText = false;

            CreateButton("��������\n����", "��������\n����", "Reinforcement.DecorViewPlan", Properties.Resources.Auto_plan,
            "������� ����������� ���, ������� ������� ����� ���� � ������������� ��",
            "�� ����� ������ ���� ����� �� � ����� ����.\n" +
            "� ������� ����������� �������� ������ ���������������� ��� ������������������� ��������� ��������",
            panelDrawing);

            //CreateButton("��������\n������", "��������\n������", "Reinforcement.DecorWallReinfViewSection", Properties.Resources.Auto_razrez,
            //"������� ������������� �����, ��������� ���, ������� ����� ������", "� ������� ����������� �������� ������ ���������������� ��� ������������������� ��������� ��������",
            //panelDrawing);


            //5. PanelSelection
            CreateButton("����� ������", "�����\n������", "Reinforcement.SelectParentElement", Properties.Resources.��_�����,
             "��������� �������� ������������ ��������� ������ �� ������������", 
             "1. �������� ������ ������ � ������� ������������\n" +
             "2. ������ �� ������\n" +
             "3. ������ �� ��� ������ ������������� ���������\n" +
             "4. ������� �� ����� ������ ���.\n������ ����� �������� �������� ������",
            panelSelection);

    //        CreateButton("����� � ��������", "�����\n� ��������", "Reinforcement.CommandPickWithFilter", Properties.Resources.ES_SelectWithFilter,
    //"������� �������� �� �������� ��������� - ��� ��������", "��� ����� �� ������� ��������� ������ ���� � �� ��������", panelSelection);


            //6. PanelSAPR

            CreateButton("����������� ������������", "�����������\n������������", "Reinforcement.CopySelectedSchedules.CommandCopySelectedSchedules", Properties.Resources.ES_Specification,
             "��������� ����������� ������������ � ������� ����� �����������", 
             "��� ������ ������� ����� ������� �������� ������������ ��� �����������, � ����� ������ �� ������",
            panelSAPR);
            CreateButton("���������� ���� �� DWG", "����������\n���� �� DWG", "Reinforcement.SetPilesByDWG", Properties.Resources.ES_PilesFromDwg,
             "��������� ���������� ���������� ���� �� ������������ DWG ��������", "������� ��������� ���������� ���������� ��������� � �����. ����� �� �������� ����� ���� ��������� ��� ����� ������� ����������� (��� �������������)",
            panelSAPR);
            CreateButton("�������� ��� ����", "��������\n��� ����", "Reinforcement.CommandCreateViewPlan", Properties.Resources.ES_ViewsForSlab,
             "��������� ������� �������� ��� ����� � ������� �� �� ����� ����", "��������� 3 ����, ��������� ����. � ����� ����������� ��� ���� � ��������� �� �����\n" +
             "� ����� � ������� �����������, ��������, (��3 �� ���. +3,560) - ��� ����� ������ ��� ������������ ��������\n " +
             "� ������� ����������� 21, 22 � �.�, ��� ����������� �����\n �� ���������� � ������ ������ ��������� ��������", panelSAPR);
            CreateButton("����� ���� ���������������", "����� ����\n���������������", "Reinforcement.GetLengthElectricalWiring", Properties.Resources.ElectricalWiring,
             "��������� ���������� ����� ����, ������� �� ����, ��������������� �� ���������", "�������� ������ � ������� ���������������:\n1. ���������������� �������� � DWG;\n2. ������ ����, ����������",
            panelSAPR);



            // 7. panelOV ��������� 
            CreateButton("�������� �� ������", "��������\n�� ������", "Reinforcement.OV_Constuct_Command", Properties.Resources.ES_OV_for_KR,
             "��������� �������",
             "��� ������ ������� ����� ",
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
