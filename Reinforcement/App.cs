#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Updaters;
using AW = Autodesk.Windows;
//using Autodesk.Windows;

//using System.Windows.Controls;

#endregion

namespace Reinforcement
{
    public enum Panels
    {
        Конфигурация,
        СПДС,
        Разработчик,
        СхематичноеАрмирование,
        ДетальноеАрмирование,
        Оформление,
        Выбор,
        САПР,
        КРвставки,
        CopyКубики,
        Опции,
        ВолшебнаяKнопка
    }
    public partial class App : IExternalApplication
    {


        public static UIControlledApplication Application { get; private set; }=null;
       // public static UIApplication _uiApplication => RevitAPI.UiApp
        public static class PanelVisibility
        {
            /*
            public static RibbonPanel Panel_1_1_Configuration { get; set; }
            public static RibbonPanel panelSpds { get; set; }
            */
            public static Dictionary<string, RibbonPanel> Panels { get; } = new Dictionary<string, RibbonPanel>();

        }

        // !!! панели которые видны на начальном экране конфигурация КР
        public static List<string> list_panels_viewKR { get; set; } = new List<string>()
            {
                "Конфигурация",
                "СПДС",
                "Схематичное армирование",
                "Детальное армирование",
                "Оформление",
                "Выбор",
                "САПР",
                "КР вставки",
                "Copy/Кубики",
                "Импорт/Экспорт",
                "Опции",
                "Сюрприз",
                

            };

        //постоянные панели
        //public static List<string> list_panels_const { get; set; } = new List<string>
        //    {
        //        "Конфигурация",
        //        "СПДС",

        //    };

        

    public Result OnStartup(UIControlledApplication app)
        {
            HelperSeach.ClearCache();
            app.ControlledApplication.DocumentChanged += FamilyCacheEvents.DocumentChanged;
            app.ControlledApplication.DocumentClosing += FamilyCacheEvents.DocumentClosing;
            Application = app; // Сохраняем app в статическое свойство
            app.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
            
            //для автообновления
            App_Apdater_1.StartUpdateENS();

            //Create tab
            string tabName = "ЕС BIM";
            app.CreateRibbonTab(tabName);
            // Подписываемся на событие инициализации
           

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
                "Copy/Кубики",
                "Импорт/Экспорт",
                "ОВ плит",
                "АР панель",
                "ОВ панель",
                "ЭЛ панель",
                "Разработчик",
                "Опции",
                "Сюрприз"
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

                    case "Copy/Кубики":
                        App_Panel_1_8_KR_Task.AddSplitButton(panel, tabName);
                        break;

                    case "Импорт/Экспорт":
                        App_Panel_1_81_KR_Export.AddSplitButton(panel, tabName);
                        break;


                    case "ОВ плит":
                        App_Panel_1_9_KR_to_OV.AddSplitButton(panel, tabName);
                        break;
                    case "АР панель":
                        App_Panel_2_2_AR_utilit.AR_utilit(panel, tabName);
                        break;

                    case "ОВ панель":
                        App_Panel_3_2_OV_utilit.OV_utilit(panel, tabName);
                        break;

                    case "ЭЛ панель":
                        App_Panel_5_2_EL_utilit.EL_utilit(panel, tabName);
                        break;

                    case "Разработчик":
                        App_Panel_7_2_AdminPanel.Admin_utilit(panel, tabName);
                        break;

                    case "Опции":
                        App_Panel_1_92_Opcii.AddSplitButton(panel, tabName);
                        break;

                    case "Сюрприз":
                        App_Panel_1_91_Toska.AddSplitButton(panel, tabName);
                        break;

                }

            }

            

            
             

            foreach (var panel in PanelVisibility.Panels)
            {
                if (list_panels_viewKR.Contains(panel.Key))
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



            AnyChange.PodpiskaAll();// подписка на все
                                    // AutoFillNoteUpdater.RegisterUpdater();


            
            // Подписка на событие закрытия Revit
            app.ApplicationClosing += OnRevitClosing;
            //app.ControlledApplication.DocumentClosed
            //app.ControlledApplication.ApplicationClosing += (sender, args) =>
            //{
            //    lookUsers.ForceFlush();
            //};

            // Подписываемся на события сохранения моделей
            var controlledApp = app.ControlledApplication;

            controlledApp.DocumentSaving += OnDocumentSaving;
            controlledApp.DocumentSynchronizingWithCentral += OnDocumentSynchronizing;

            controlledApp.DocumentSaved += OnDocumentSaved;
            controlledApp.DocumentSynchronizedWithCentral += OnDocumentSynchronized;

            app.ControlledApplication.DocumentChanged += OnDocumentChanged;

            // Подписываемся на событие через ControlledApplication
            app.ControlledApplication.FailuresProcessing +=
                new EventHandler<FailuresProcessingEventArgs>(OnFailuresProcessing);

            //секретный набор команд
            _secretHandler = new SecretReplacementHandler();
            _secretEvent = ExternalEvent.Create(_secretHandler);
            return Result.Succeeded;
        }

        /* private void ControlledApp_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
         {

         }*/



        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Отписываемся от событий, чтобы избежать утечек памяти
                var controlledApp = application.ControlledApplication;
                controlledApp.ApplicationInitialized -= OnApplicationInitialized;
                application.ApplicationClosing -= OnRevitClosing;
                controlledApp.DocumentChanged -= FamilyCacheEvents.DocumentChanged;
                controlledApp.DocumentClosing -= FamilyCacheEvents.DocumentClosing;
                HelperSeach.ClearCache();
                _syncStartTimes.Clear();
                _secretEvent?.Dispose();
                _secretEvent = null;
                controlledApp.DocumentSaving -= OnDocumentSaving;
                controlledApp.DocumentSaved -= OnDocumentSaved;
                controlledApp.DocumentSynchronizingWithCentral -= OnDocumentSynchronizing;
                controlledApp.DocumentSynchronizedWithCentral -= OnDocumentSynchronized;
                // Отписываемся (хорошая практика)
                application.ControlledApplication.FailuresProcessing -= OnFailuresProcessing;
                application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
                //Но есть и практический смысл. OnShutdown вызывается, когда Revit закрывается штатно. Это последний момент, когда ваш код ещё может что-то сделать
                
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
            }
            try
            {
                App_Apdater_1.LookUsers.Update("OnShutdownRevit", EDocStatsOptions.CloseRevit);
                App_Apdater_1.LookUsers.ForceFlush(closeRevit: true);
            }
            catch { /* уже нечего терять */ }
            // Пытаемся сохранить накопленные ошибки самого revit
            try
            {
                if (FailureBuffer.HasData)
                    FailureBuffer.Flush();
            }
            catch { /* уже нечего терять */ }

            IsShuttingDown = true;//закрылся
            return Result.Succeeded;
        }
        
        public static volatile bool IsShuttingDown = false;
        // Словарь: документ -> время начала синхронизации
        private static readonly ConcurrentDictionary<Document, DateTime> _syncStartTimes = new ConcurrentDictionary<Document, DateTime>();
        public static double secondSaveModel = 0;


        private static ExternalEvent _secretEvent;
        private static SecretReplacementHandler _secretHandler;

    }
}

