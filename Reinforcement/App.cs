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
            Application = app; // Сохраняем app в статическое свойство
            //Create tab
            string tabName = "ЕС BIM";
            app.CreateRibbonTab(tabName);

            
            // сюда вписываешь новую панель и вообще все панели здесь в списке, список это порядок панелей, отображение панелей на конкретной конфигурации задача конфигуратора, в него иди и там настраивай
            var panelNames = new List<string>
            {
                "Конфигурация",
                "СПДС",
                "Схематичное армирование",
                "Детальное армирование",
                "Оформление",
                "Выбор",
                "САПР",
                "КР вставки",
                "Копировать задание",
                "ОВ плит",
                "АР панель"
            };

            // команды которые создают кнопки на конкретных панелях
            foreach (var panelName in panelNames)
            {
                var panel = app.CreateRibbonPanel(tabName, panelName);
                PanelVisibility.Panels.Add(panelName, panel);

                switch (panelName)
                {
                    case "Конфигурация":// управление всеми панелями
                        App_Panel_1_1_Configuration.AddSplitButton(panel, tabName);
                        break;

                    case "СПДС":
                        App_Panel_1_2_KR_SPDS.KR_SPDS(panel, tabName);
                        break;
                    case "Схематичное армирование":
                        App_Panel_1_3_KR_SketchReinf.KR_SketchReinf(panel, tabName);
                        break;
                    case "Детальное армирование":
                        App_Panel_1_4_KR_DetailReinf.KR_DetailReinf(panel, tabName);
                        break;
                    case "Оформление":
                        App_Panel_1_5_KR_Drawing.KR_Drawing(panel, tabName);
                        break;
                    case "Выбор":
                        App_Panel_1_6_KR_Selection.KR_Selection(panel, tabName);
                        break;
                    case "САПР":
                        App_Panel_1_7_KR_SAPR.KR_SAPR(panel, tabName);
                        break;

                    case "КР вставки":
                        App_Panel_1_71_KR_vstavka.AddSplitButton(panel, tabName);
                        break;

                    case "Копировать задание":
                        App_Panel_1_8_KR_Task.KR_Task(panel, tabName);
                        break;
                    case "ОВ плит":
                        App_Panel_1_9_KR_to_OV.AddSplitButton(panel, tabName);
                        break;
                    case "АР панель":
                        App_Panel_2_2_AR_utilit.AR_utilit(panel, tabName);
                        break;

                }

            }

            // панели которые видны на начальном экране конфигурация КР

            var list_panels_view = new List<string>()
            {
                "Конфигурация",
                "СПДС",
                "Схематичное армирование",
                "Детальное армирование",
                "Оформление",
                "Выбор",
                "САПР",
                "КР вставки",
                "Копировать задание"

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

