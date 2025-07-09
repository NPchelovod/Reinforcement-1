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

            
            // ���� ���������� ����� ������ � ������ ��� ������ ����� � ������, ������ ��� ������� �������, ����������� ������� �� ���������� ������������ ������ �������������, � ���� ��� � ��� ����������
            var panelNames = new List<string>
            {
                "������������",
                "����",
                "����������� �����������",
                "��������� �����������",
                "����������",
                "�����",
                "����",
                "�� �������",
                "���������� �������",
                "�� ����",
                "�� ������"
            };

            // ������� ������� ������� ������ �� ���������� �������
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

                    case "�� �������":
                        App_Panel_1_71_KR_vstavka.AddSplitButton(panel, tabName);
                        break;

                    case "���������� �������":
                        App_Panel_1_8_KR_Task.KR_Task(panel, tabName);
                        break;
                    case "�� ����":
                        App_Panel_1_9_KR_to_OV.AddSplitButton(panel, tabName);
                        break;
                    case "�� ������":
                        App_Panel_2_2_AR_utilit.AR_utilit(panel, tabName);
                        break;

                }

            }

            // ������ ������� ����� �� ��������� ������ ������������ ��

            var list_panels_view = new List<string>()
            {
                "������������",
                "����",
                "����������� �����������",
                "��������� �����������",
                "����������",
                "�����",
                "����",
                "�� �������",
                "���������� �������"

            };

            foreach (var panel in PanelVisibility.Panels)
            {
                if (list_panels_view.Contains(panel.Key))
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = true;
                    }
                }
                else
                {
                    if (panel.Value != null)
                    {
                        panel.Value.Visible = false;
                    }
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

