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
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AW = Autodesk.Windows;
//using Autodesk.Windows;

//using System.Windows.Controls;

#endregion

namespace Reinforcement
{
    public enum EUserOtdel //все отделы для определения типа пользователя
    {
        None=0,
        KR=1,
        AR=2,
        Engineer=3, //ОВ, ВК, ЭЛ - все инженеры
    }
    
    public enum EPanelSelf //все конфигурации в порядке инициализации
    {
        KR=0,
        AR=1,
        OV,
        VK,
        EL,
        General,
        Test,
        Admin
    }
    public enum EPanels //все панели внутри которых кнопки, в порядке инициализации
    {
        Конфигурация=0,
        СПДС,
        СхематичноеАрмирование,
        ДетальноеАрмирование,
        Оформление,
        Выбор,
        САПР,
        КРвставки,
        CopyКубики,
        ИмпортЭкспорт,
        ОВплит,
        АРпанель,
        ОВпанель,
        ЭЛпанель,
        ОбщаяПанель,
        Разработчик,
        Опции,
        Сюрприз
    }
    public static class PanelVisibility
    {
        public static Dictionary<EPanels, RibbonPanel> Panels { get; } = new Dictionary<EPanels, RibbonPanel>();

    }

    public partial class App : IExternalApplication
    {
        public static readonly string TabName = "ЕС BIM"; // имя всей панели которая видна в ревите

        public static readonly Dictionary<EPanelSelf, (string Name, string Comand, string SurName, System.Drawing.Image image)> DConfigNames =
    new Dictionary<EPanelSelf, (string Name, string Comand, string SurName, System.Drawing.Image image)>
        {
            { EPanelSelf.KR,      ("КР",     "Reinforcement.App_Panel_1_1_Configuration_KR",  "Конструктив",            Properties.Resources.KR_config) },
            { EPanelSelf.AR,      ("АР",     "Reinforcement.App_Panel_2_1_Configuration_AR",  "Архитектура",            Properties.Resources.AR_config) },
            { EPanelSelf.OV,      ("ОВ",     "Reinforcement.App_Panel_3_1_Configuration_OV",  "Отопление и Вентиляция", Properties.Resources.OV_config) },
            { EPanelSelf.VK,      ("ВК",     "Reinforcement.App_Panel_4_1_Configuration_VK",  "Водснаб и Канализация",  Properties.Resources.VK_config) },
            { EPanelSelf.EL,      ("ЭЛ",     "Reinforcement.App_Panel_5_1_Configuration_EL",  "Электрика",              Properties.Resources.EL_config) },
            { EPanelSelf.General, ("Общее",  "Reinforcement.App_Panel_6_1_Configuration_General",           "Общее",                  Properties.Resources.KR_config) },
            { EPanelSelf.Test,    ("тест",   "Reinforcement.App_Panel_8_1_Configuration_Test","не трогать",             Properties.Resources.Test_config) },
            { EPanelSelf.Admin,   ("разраб", "Reinforcement.App_Panel_9_1_Configuration_Admin","не трогать",             Properties.Resources.Properties) },
        };

        //все панели в порядке пребывания
        public static List<EPanelSelf> EPanelSelfList { get; set; } = Enum.GetValues(typeof(EPanelSelf))
            .Cast<EPanelSelf>()
            .ToList();

        public static readonly Dictionary<EPanels, string> DPanels = new Dictionary<EPanels, string>
        {
            { EPanels.Конфигурация, "Конфигурация" },
            { EPanels.СПДС, "СПДС" },
            { EPanels.СхематичноеАрмирование, "Схематичное армирование" },
            { EPanels.ДетальноеАрмирование, "Детальное армирование" },
            { EPanels.Оформление, "Оформление" },
            { EPanels.Выбор, "Выбор" },
            { EPanels.САПР, "САПР" },
            { EPanels.КРвставки, "КР вставки" },
            { EPanels.CopyКубики, "Copy/Кубики" },
            { EPanels.ИмпортЭкспорт, "Импорт/Экспорт" },
            { EPanels.ОВплит, "ОВ плит" },
            { EPanels.АРпанель, "АР панель" },
            { EPanels.ОВпанель, "ОВ панель" },
            { EPanels.ЭЛпанель, "ЭЛ панель" },
            { EPanels.ОбщаяПанель, "Общая панель" },
            { EPanels.Разработчик, "Разработчик" },
            { EPanels.Опции, "Опции" },
            { EPanels.Сюрприз, "Сюрприз" }
        };
        //все кнопки в порядке пребывания
        public static List<EPanels> EPanelsList { get; set; } = Enum.GetValues(typeof(EPanels))
            .Cast<EPanels>()
            .ToList();

        public static EUserOtdel EUserOtdel { get; set; } = EUserOtdel.None; //отдел для определения КР или иной пользователь
        //можем менять панель если надо так то
        public static EPanelSelf MainEPanel { get; set; } = EPanelSelf.KR;//главная панель при запуске

        public static EPanelSelf CurrentEPanel { get; set; } = EPanelSelf.KR;//текущая панель у пользователя
        public static UIControlledApplication Application { get; private set; }=null;
       
        public Result OnStartup(UIControlledApplication app)
    {
            // Апдейтер запускаем ДО основной инициализации — он только стартует
            // внешний процесс UpdaterENS.exe и сразу возвращается.
            // Лечим уже заражённую папку плагина до того, как Revit попытается
            // грузить её файлы.
            
           

            TryStartUpdate(app);//пытаемся скачать обновления и начальные данные загружаем об окружении

            Result result = Result.Succeeded;
            try
            {
                // ... твоя инициализация  - Запуск самого плагина...
                result= Startup(app);
            }
            catch (Exception ex)
            {
                ErrorException(ex, "OnStartup");

                TaskDialog.Show($"{TabName}",
                    "Плагин не запустился. Лог: %APPDATA%\\RevitAddinsLogs\\ENSPlugin_startup.log\n\n" +
                    ex.Message);
                return Result.Failed;
            }

            try
            {
                //запуск иных файлов exe.
                if (result == Result.Succeeded)
                {
                    App_OtherExe.Execute();
                }
            }
            catch (Exception ex)
            {
                try { RevitAPI.AppErrors?.LogError(ex); } catch { }
            }
            return result;

        }
        /// <summary>
        /// Безопасный запуск автообновления.
        /// Гарантированно не бросает исключений наружу и не ломает OnStartup.
        /// </summary>
        private static void TryStartUpdate(UIControlledApplication app)
        {
            Application = app; // Сохраняем app в статическое свойство
            //1. запускаем инициализацию путей и тд
            try
            {
                App_Folders.Initializator(); //начальные данные - директории все
            }
            catch (Exception ex)
            {
                ErrorException(ex, "AppInitializator");
            }
            //2. Запускаем сам апдейтер
            try
            {
                
                UpdaterPlagina.StartUpdateENS();
            }
            catch (Exception ex)
            {
                ErrorException(ex, "StartUpdateENS");
            }
        }
        public static void ErrorException(Exception ex, string method)
        {
            try
            {
                string nameFile = "ENSPlugin_startup";
                if (method != null)
                {
                    nameFile += method;
                }
                nameFile += ".log";
                //запись падения
                //C:\Users\KVinogradov\AppData\Roaming\RevitAddinsLogs
                string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RevitAddinsLogs", nameFile);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(logPath,
                $"\n=== {DateTime.Now} update-start failed ===\n{ex}\n");
            }
            catch { }
        }
        public Result Startup(UIControlledApplication app)
        {
            Application = app; // Сохраняем app в статическое свойство
            //самое главное - подписаться на инициализацию
            app.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;

            HelperSeach.ClearCache();
            //app.ControlledApplication.DocumentChanged += FamilyCacheEvents.DocumentChanged;
            //app.ControlledApplication.DocumentClosing += FamilyCacheEvents.DocumentClosing;
            
            
            //Create tab
            
            app.CreateRibbonTab(TabName);
            // Подписываемся на событие инициализации

            PanelVisibility.Panels.Clear();
            // команды которые создают кнопки на конкретных панелях// все кнопеи тут
            foreach (EPanels ePanelName in EPanelsList)
            {
                DPanels.TryGetValue(ePanelName, out string panelName);
                if (string.IsNullOrEmpty(panelName)) { continue; }

                var panel = app.CreateRibbonPanel(TabName, panelName);
                PanelVisibility.Panels[ePanelName] = panel;

                switch (ePanelName)
                {
                    case EPanels.Конфигурация:// управление всеми панелями
                        App_Panel_1_1_Configuration.AddSplitButton(panel, TabName);
                        break;

                    case EPanels.СПДС:
                        App_Panel_1_2_KR_SPDS.KR_SPDS(panel, TabName);
                        break;
                    case EPanels.СхематичноеАрмирование:
                        App_Panel_1_3_KR_SketchReinf.KR_SketchReinf(panel, TabName);
                        break;
                    case EPanels.ДетальноеАрмирование:
                        App_Panel_1_4_KR_DetailReinf.KR_DetailReinf(panel, TabName);
                        break;
                    case EPanels.Оформление:
                        App_Panel_1_5_KR_Drawing.KR_Drawing(panel, TabName);
                        break;
                    case EPanels.Выбор:
                        App_Panel_1_6_KR_Selection.KR_Selection(panel, TabName);
                        break;
                    case EPanels.САПР:
                        App_Panel_1_7_KR_SAPR.KR_SAPR(panel, TabName);
                        break;

                    case EPanels.КРвставки:
                        App_Panel_1_71_KR_vstavka.AddSplitButton(panel, TabName);
                        break;

                    case EPanels.CopyКубики:
                        App_Panel_1_8_KR_Task.AddSplitButton(panel, TabName);
                        break;

                    case EPanels.ИмпортЭкспорт:
                        App_Panel_1_81_KR_Export.AddSplitButton(panel, TabName);
                        break;


                    case EPanels.ОВплит:
                        App_Panel_1_9_KR_to_OV.AddSplitButton(panel, TabName);
                        break;
                    case EPanels.АРпанель:
                        App_Panel_2_2_AR_utilit.AR_utilit(panel, TabName);
                        break;

                    case EPanels.ОВпанель:
                        App_Panel_3_2_OV_utilit.OV_utilit(panel, TabName);
                        break;

                    case EPanels.ЭЛпанель:
                        App_Panel_5_2_EL_utilit.EL_utilit(panel, TabName);
                        break;
                    case EPanels.ОбщаяПанель:
                        App_Panel_6_2_General_panel.AddSplitButton(panel, TabName);
                        break;

                    case EPanels.Разработчик:
                        App_Panel_9_2_AdminPanel.Admin_utilit(panel, TabName);
                        break;

                    case EPanels.Опции:
                        App_Panel_1_92_Opcii.AddSplitButton(panel, TabName);
                        break;

                    case EPanels.Сюрприз:
                        App_Panel_1_91_Toska.AddSplitButton(panel, TabName);
                        break;

                }

            }

            //определяем пользователя
            string usName = Environment.UserName.ToLower();
            if(usName.StartsWith("k"))
            {
                EUserOtdel = EUserOtdel.KR;
            }
            else if(usName.StartsWith("a"))
            {
                EUserOtdel = EUserOtdel.AR;
            }
            else if(usName.StartsWith("i"))
            {
                EUserOtdel = EUserOtdel.Engineer;
            }
            


            //создаем начальную панель
            App_Helper_Panels.CreatePanelConfiguration(MainEPanel);


            //все подписки реализуем
            Podpiska(app);

            //секретный набор команд


            return Result.Succeeded;
        }

        public static bool ExistPodpiskaOnChanged=false;// активна ли подписка
        public void Podpiska(UIControlledApplication app)
        {
            try
            {
                // подписка на все изменения элементов, возможно ненужна
               // PodpiskaAll(app);// подписка на все
                                 // AutoFillNoteUpdater.RegisterUpdater();

                RegisterAutoFillUpdater.addInId = app.ActiveAddInId;

                RegisterAutoFillUpdater.Register();

                

                //любое изменение внутри документа
                app.ControlledApplication.DocumentChanged += OnDocumentChanged;

                // Подписка на событие закрытия Revit
                app.ApplicationClosing += OnRevitClosing;
                var controlledApp = app.ControlledApplication;

                

                // Подписываемся на события сохранения моделей
                controlledApp.DocumentSaving += OnDocumentSaving;
                controlledApp.DocumentSynchronizingWithCentral += OnDocumentSynchronizing;//начало синхронизации

                controlledApp.DocumentSaved += OnDocumentSaved;
                controlledApp.DocumentSynchronizedWithCentral += OnDocumentSynchronized;//конец синхронизации

                // Подписываемся на событие ошибок через ControlledApplication
                app.ControlledApplication.FailuresProcessing +=
                    new EventHandler<FailuresProcessingEventArgs>(OnFailuresProcessing);

                ExistPodpiskaOnChanged= true;

            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
                ExistPodpiskaOnChanged= false;
                ErrorException(ex, "Podpiska");
            }
        }
        
        //public static void PodpiskaAll(UIControlledApplication app)
        //{
        //    //var app = App.Application;//RevitAPI.UiApplication;
            
        //    //RegisterUpdater.addInId = app.ActiveAddInId;
        //    //RegisterUpdater.Register();

        //    //RegisterZakladkaUpdater.addInId = app.ActiveAddInId;
        //    //RegisterZakladkaUpdater.Register();

        //    RegisterAutoFillUpdater.addInId = app.ActiveAddInId;

        //    RegisterAutoFillUpdater.Register();



            
        //}


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
                
                controlledApp.DocumentSaving -= OnDocumentSaving;
                controlledApp.DocumentSaved -= OnDocumentSaved;
                controlledApp.DocumentSynchronizingWithCentral -= OnDocumentSynchronizing;
                controlledApp.DocumentSynchronizedWithCentral -= OnDocumentSynchronized;
                // Отписываемся (хорошая практика)
                application.ControlledApplication.FailuresProcessing -= OnFailuresProcessing;
                application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
                //Но есть и практический смысл. OnShutdown вызывается, когда Revit закрывается штатно. Это последний момент, когда ваш код ещё может что-то сделать
                UpdaterSubscriber.Shutdown();
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }
            try
            {
                RevitAPI.LookUsers.Update("OnShutdownRevit", EDocStatsOptions.CloseRevit);
                RevitAPI.LookUsers.ForceFlush(closeRevit: true);
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


      

    }
}

