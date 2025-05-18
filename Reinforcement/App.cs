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
using System.Diagnostics;
//using Autodesk.Windows;

//using System.Windows.Controls;

#endregion

namespace Reinforcement
{
    internal class App : IExternalApplication
    {


        public static UIControlledApplication Application { get; private set; }

        public static class PanelVisibility
        {
            /*
            public static RibbonPanel Panel_1_1_Configuration { get; set; }
            public static RibbonPanel panelSpds { get; set; }
            */
            public static Dictionary<string, RibbonPanel> Panels { get; } = new Dictionary<string, RibbonPanel>();
        }


        public Result OnStartup(UIControlledApplication app)
        {
            Application = app; // ��������� app � ����������� ��������
            //Create tab
            string tabName = "�� BIM";
            app.CreateRibbonTab(tabName);

            //Create panels
            // �������� � ���������� �������
            /*
            RibbonPanel Panel_1_1_Configuration = app.CreateRibbonPanel(tabName, "������������");

            RibbonPanel panelSpds = app.CreateRibbonPanel(tabName, "����");
            PanelVisibility.panelSpds = panelSpds; // �������� ������!

            RibbonPanel panelSketchReinf = app.CreateRibbonPanel(tabName, "����������� �����������");

            RibbonPanel panelDetailReinf = app.CreateRibbonPanel(tabName, "��������� �����������");
            RibbonPanel panelDrawing = app.CreateRibbonPanel(tabName, "����������");
            RibbonPanel panelSelection = app.CreateRibbonPanel(tabName, "�����");
            RibbonPanel panelSAPR = app.CreateRibbonPanel(tabName, "����");
            RibbonPanel panelOV = app.CreateRibbonPanel(tabName, "��_�����");
            */

            var panelNames = new List<string>
            {
                "������������",
                "����",
                "����������� �����������",
                "��������� �����������",
                "����������",
                "�����",
                "����",
                "��_�����"
            };

            foreach (var panelName in panelNames)
            {
                var panel = app.CreateRibbonPanel(tabName, panelName);
                PanelVisibility.Panels.Add(panelName, panel);

                switch (panelName)
                {
                    case "������������":// ���������� ����� ��������
                        App_Panel_1_1_Configuration.AddSplitButton(panel, tabName);
                        break;

                    case "����":
                        App_Panel_1_2_KR_SPDS.KR_SPDS(panel, tabName);
                        break;
                    case "����������� �����������":
                        App_Panel_1_3_KR_SketchReinf.KR_SketchReinf(panel, tabName);
                        break;
                    case "��������� �����������":
                        App_Panel_1_4_KR_DetailReinf.KR_DetailReinf(panel, tabName);
                        break;
                    case "����������":
                        App_Panel_1_5_KR_Drawing.KR_Drawing(panel, tabName);
                        break;
                    case "�����":
                        App_Panel_1_6_KR_Selection.KR_Selection(panel, tabName);
                        break;
                    case "����":
                        App_Panel_1_7_KR_SAPR.KR_SAPR(panel, tabName);
                        break;
                    case "��_�����":
                        App_Panel_1_8_KR_to_OV.KR_to_OV(panel, tabName);
                        break;

                }

            }
           

            //8. Updater
            RegisterUpdater.addInId = app.ActiveAddInId;
            RegisterUpdater.Register();

            return Result.Succeeded;
        }

        /* private void ControlledApp_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
         {

         }*/

        

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
