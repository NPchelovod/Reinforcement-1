using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace Reinforcement
{
    public static class App_Folders
    {
        //тут все глобальные пути и параметры
        //Автообновление самого плагина из папки в Share

        public static DateTime InitialTimeCurrentSessia = DateTime.MinValue;//начало текущей секции открытия ревита
        public static DateTime DatePluginDll = DateTime.MinValue;//дата обновления плагина

        public static Version Version = null;
        public static string VersionString = null;

        public static string TempFolder = null;//временная папка куда можно всякое сохранять

        public static string TempUpdaterDir = null;//временная папка с апдейтером плагина
        public static string TempUpdaterExe=null;
        public static string UserName = null;
        public static string MachineName = null;
        public static string UserName_MachineName = null;//имя пользователя ПК

        public static string TargetPluginDir;// текущая папка плагина)

        public static string UpdaterSourceDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\UpdaterENS";
        public static string UpdaterSourceDirAvtor = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\UpdaterENS_Admin";

        public static string SourcePluginDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\ES_BIM_Плагин";
        public static string SourcePluginDirAvtor = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\ES_BIM_Плагин_Admin";

        public static string BackupDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy";
        public static string LogUpdaterFile;
        public static string UpdaterNameProcess = "UpdaterENS";

        public static string FolderStatistics = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics";

        // --- Контроль рабочего времени ---
        public static string UserTimeSourceDir =
            @"Y:\ГОСТЕВЫЕ\КОНСТРУКТОРЫ\Ерухаева Ирина\Индивидуальный табель\контроль рабочего времени";

        public static string UserTimeProcessPrefix = "WpfControlUserTime";



        public static string FolderErrors = Path.Combine(FolderStatistics, "Errors");

        public static HashSet<string> Avtors = new HashSet<string> { "KVinogradov", "KBocharov", "KReimer", "KZubakhina" };

        public static bool UserIsAvtor=false;


        public const string Password = "kerulen";
        public static bool UserIsAdmin=false;


        public static bool isInitialize =false;
        public static void Initializator()
        {
            InitialTimeCurrentSessia = DateTime.Now;
            // Вычисляем дату самого свежего файла в текущей папке плагина (необязательно)
            if (string.IsNullOrEmpty(TargetPluginDir))
            {
                TargetPluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            DatePluginDll = GetLatestFileTime(TargetPluginDir);
            TempFolder = Path.GetTempPath();
            TempUpdaterDir = Path.Combine(TempFolder, "ENS_Updater");
            TempUpdaterExe = Path.Combine(TempUpdaterDir, "UpdaterENS.exe");
            LogUpdaterFile = Path.Combine(BackupDir, $"{UserName_MachineName}_log.txt");
            // Версия сборки
            Version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionString = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;


            UserName = Environment.UserName;
            MachineName = Environment.MachineName;
            UserName_MachineName = $"{UserName}_{MachineName}";

            if (Avtors.Contains(UserName))
            {
                UserIsAvtor = true;
            }

            isInitialize=true;
        }



        public static DateTime GetLatestFileTime(string directoryPath)
        {
            //самая свежая дата
            if (!Directory.Exists(directoryPath))
                return DateTime.MinValue;

            var extensions = new[] { ".dll", ".exe" };

            var paths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

            if (!paths.Any())
                return DateTime.MinValue;

            return paths.Max(p => File.GetLastWriteTimeUtc(p));
        }

        //возврат рекомендуемого имени транзакции
       

        /// <summary>
        /// Разовая очистка целевой папки плагина от альтернативного потока Zone.Identifier.
        /// Вызывать из App.OnStartup ДО любой другой логики — лечит машины,
        /// на которых ранее остались «заражённые» файлы (HRESULT 0x80131515).
        /// </summary>
        /// 
        public static void CleanupTargetFolder(string folder)
        {
            try
            {

                RemoveZoneIdentifiersRecursively(folder);
            }
            catch (Exception ex)
            {
                try { RevitAPI.AppErrors?.LogError(ex); } catch { }
            }
        }
        public static bool IsExistProcessCurent(string updaterNameProcess)
        {
            //сейчас уже запущен данный процесс
            try
            {
                var procs = Process.GetProcessesByName(updaterNameProcess);
                bool running = procs.Length > 0;
                foreach (var p in procs) p.Dispose();
                return running;
            }
            catch
            {
                // Если не смогли узнать — считаем, что не запущен (лучше попробовать,
                // чем вообще не обновляться; второй уровень защиты — мьютекс в самом апдейтере).
                return false;
            }
        }
        /// <summary>
        /// Максимальная дата модификации .dll / .exe в директории (рекурсивно).
        /// DateTime.MinValue, если ничего не найдено / директории нет.
        /// </summary>
        public static DateTime GetDateTimeFolder(string sourceDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                return DateTime.MinValue;

            var max = new[] { "*.dll", "*.exe" }
                .SelectMany(mask => Directory.EnumerateFiles(sourceDir, mask, SearchOption.AllDirectories))
                .Select(File.GetLastWriteTime)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            return max;
        }

        // Рекурсивное удаление альтернативного потока Zone.Identifier чтобы не показывать предупреждение «Этот файл получен из другой зоны»;
        /// Не бросает исключений. Безопасно вызывать на любой существующей папке.
        /// </summary>
        private static void RemoveZoneIdentifiersRecursively(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            foreach (var filePath in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                RemoveZoneIdentifier(filePath);
        }


        /// <summary>
        /// Удаляет альтернативный поток Zone.Identifier у одного файла.
        /// Никогда не бросает исключений.
        /// </summary>
        public static void RemoveZoneIdentifier(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                DeleteFile(filePath + ":Zone.Identifier");
                // Возвращаемое значение и GetLastWin32Error нас здесь не интересуют —
                // нас устраивает и «удалено», и «потока не было».
            }
            catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFile(string lpFileName);
    }
}
