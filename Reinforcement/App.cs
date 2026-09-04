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
using System.Linq;
//using Autodesk.Windows;

//using System.Windows.Controls;

#endregion

namespace Reinforcement
{
    public enum Panels
    {
        Конфигурация,
        СПДС,
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
    internal class App : IExternalApplication
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
            Application = app; // Сохраняем app в статическое свойство
            app.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
            
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

            
            //для автообновления
            StartUpdateENS();

            return Result.Succeeded;
        }

        /* private void ControlledApp_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
         {

         }*/



        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
        private void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            // Здесь sender — это Autodesk.Revit.ApplicationServices.Application
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            if (app != null)
            {
                // Получаем UIApplication
                UIApplication uiApp = new UIApplication(app);
                // Теперь можно работать с uiApp
                // Например, сохранить в статическое свойство
                RevitAPI.Initialize(uiApp);
                //uiApp.Application.GroupEditModeChanged += OnGroupEditModeChanged;
            }
        }
        public static bool IsGroupEditModeActive { get; private set; }
        //private void OnGroupEditModeChanged(object sender, GroupEditModeChangedEventArgs e)
        //{
        //    // e.Active указывает, вошли (true) или вышли (false) из режима
        //    IsGroupEditModeActive = e.Active;
        //}


        public static void StartUpdateENS()
        {
            // Путь к Updater.exe (можно хранить в ресурсах или в папке плагина)
            //string updaterPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UpdaterENS.exe");
            string updaterPath = Path.Combine("Y:\\Revit\\_ЕС BIM_Плагин\\3_Автообновление\\UpdaterENS", "UpdaterENS.exe");
            // Аргументы
            string sourceDir = @"Y:\Revit\_ЕС BIM_Плагин\3_Автообновление\ENSPlagin"; // откуда копировать новые файлы
            string targetDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); // текущая папка плагина
            int pid = Process.GetCurrentProcess().Id;

            // Запускаем процесс
            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{pid}\" \"{sourceDir}\" \"{targetDir}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });


            // Получаем дату самого свежего файла в текущей папке плагина
            TargetLatestTime = GetLatestFileTime(targetDir);

            // Версия сборки (необязательно)
            Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        }

        public static DateTime TargetLatestTime = DateTime.MinValue;
        public static Version Version =null;

        public static DateTime GetLatestFileTime(string directoryPath)
        {
            //получение даты создания
            if (!Directory.Exists(directoryPath))
                return DateTime.MinValue;

            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                                 .Select(f => new FileInfo(f))
                                 .Where(f => f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                                             f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                                 .ToList();

            if (files.Count == 0)
                return DateTime.MinValue;

            // Максимальная дата последнего изменения
            return files.Max(f => f.CreationTimeUtc); //Если нужно получить дату создания, замените LastWriteTimeUtc на CreationTimeUtc
        }
    }
}

