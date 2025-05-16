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
//using System.Windows.Controls;

#endregion

namespace Reinforcement
{
    internal class App : IExternalApplication
    {
       

      



        public Result OnStartup(UIControlledApplication app)
        {
            //Create tab
            string tabName = "�� BIM";
            app.CreateRibbonTab(tabName);

            //Create panels
            RibbonPanel Panel_1_1_Configuration = app.CreateRibbonPanel(tabName, "������������");
            RibbonPanel panelSpds = app.CreateRibbonPanel(tabName, "����");
            RibbonPanel panelSketchReinf = app.CreateRibbonPanel(tabName, "����������� �����������");
            RibbonPanel panelDetailReinf = app.CreateRibbonPanel(tabName, "��������� �����������");
            RibbonPanel panelDrawing = app.CreateRibbonPanel(tabName, "����������");
            RibbonPanel panelSelection = app.CreateRibbonPanel(tabName, "�����");
            RibbonPanel panelSAPR = app.CreateRibbonPanel(tabName, "����");
            RibbonPanel panelOV = app.CreateRibbonPanel(tabName, "��_�����");

            //1.1. ������ ������������
            App_Panel_1_1_Configuration.AddSplitButton(Panel_1_1_Configuration, "������������"); // ���������� ����� ��������

            //1.2 PanelSpds

            App_Panel_1_2_KR_SPDS.KR_SPDS(panelSpds, tabName);



            //2. PanelSketchReinf

            App_Panel_1_3_KR_SketchReinf.KR_SketchReinf(panelSketchReinf, tabName);



            //3. PanelDetailReinf

            App_Panel_1_4_KR_DetailReinf.KR_DetailReinf(panelDetailReinf, tabName);

            //4. PanelDrawing

            App_Panel_1_5_KR_Drawing.KR_Drawing(panelDrawing, tabName);


            //5. PanelSelection

            App_Panel_1_6_KR_Selection.KR_Selection(panelSelection, tabName);




            //6. PanelSAPR

            App_Panel_1_7_KR_SAPR.KR_SAPR(panelSAPR, tabName);

           



            // 7. panelOV ��������� ����� ��� �� �������� ��� ��������

           

            App_Panel_1_8_KR_to_OV.KR_to_OV(panelOV, tabName);

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
