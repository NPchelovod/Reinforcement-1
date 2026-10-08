
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Reflection;

using System.Runtime.InteropServices;
namespace Reinforcement
{
    public static class App_Apdater_1
    {
        //Автообновление самого плагина из папки в Share

        public static DateTime TargetLatestTime = DateTime.MinValue;
        public static Version Version = null;
        public static string VersionString = null;

        public static string tempFolder;//временная папка куда можно всякое сохранять
        public static string userPKName;//имя пользователя ПК

        public static string updaterSourceDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\UpdaterENS";
        public static string updaterSourceDirAvtor = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\UpdaterENS_Admin";

        public static string targetPluginDir;// текущая папка плагина)

        public static string sourcePluginDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\ES_BIM_Плагин";

        public static DateTime InitialTimePlugin;
        public static string backupDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy";

        public static string sourcePluginDirAvtor = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\ES_BIM_Плагин_Admin";

        public static HashSet<string> Avtors = new HashSet<string> { "KVinogradov", "KBocharov", "KReimer", "KZubakhina" };

        // В App_Apdater_1
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string lpFileName);


        public static bool BAvtor = false;// не автор
        public static void CalcOtherProp()
        {
            InitialTimePlugin = DateTime.Now;
            // Вычисляем дату самого свежего файла в текущей папке плагина (необязательно)
            TargetLatestTime = GetLatestFileTime(targetPluginDir);

            // Версия сборки
            Version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionString = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;

            if(Avtors.Contains(Environment.UserName))
            {
                BAvtor = true;
            }
        }
        public static LookUsers LookUsers = new LookUsers();

        public static AppErrors AppErrors = new AppErrors();

        /// <summary>
        /// Разовая очистка целевой папки плагина от альтернативного потока Zone.Identifier.
        /// Вызывать из App.OnStartup ДО любой другой логики — лечит машины,
        /// на которых ранее остались «заражённые» файлы (HRESULT 0x80131515).
        /// </summary>
        /// 
        public static void CleanupTargetFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(targetPluginDir))
                    targetPluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                RemoveZoneIdentifiersRecursively(targetPluginDir);
            }
            catch (Exception ex)
            {
                try { AppErrors?.LogError(ex); } catch { }
            }
        }

        public static void StartUpdateENS()
        {
            try
            {
                targetPluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                tempFolder = Path.GetTempPath();
                userPKName = $"{Environment.UserName}_{Environment.MachineName}";

                CalcOtherProp();
                if (!PluginOptions.AutomaticUpdatesEnabled) return;
                // Разово вылечиваем текущую папку плагина от Zone.Identifier —
                // на случай, если предыдущее обновление оставило метки.
                CleanupTargetFolder();

                string tempUpdaterDir = Path.Combine(tempFolder, "ENS_Updater");
                Directory.CreateDirectory(tempUpdaterDir);

                if (IsUpdaterRunning())
                    return;

                // --- 1. Выбираем папку-источник САМОГО апдейтера ---
                // --- выбор апдейтера оставляем здесь: exe нужно СКОПИРОВАТЬ и ЗАПУСТИТЬ,
                //     а «пока работает плагин» свежесть апдейтера уже не проверить. ---
                // Не-автор: только основной источник.
                // Автор:   свежайший из (main, admin), при равенстве — admin.
                string updaterSource = ChooseSource(
                    mainDir: updaterSourceDir,
                    adminDir: updaterSourceDirAvtor,
                    isAuthor: BAvtor);

                if (updaterSource == null)
                {
                    TaskDialog.Show("Ошибка обновления", "Папка с апдейтером не найдена.");
                    return;
                }

                CopyDirectory(updaterSource, tempUpdaterDir);

                string tempUpdaterExe = Path.Combine(tempUpdaterDir, "UpdaterENS.exe");
                if (!File.Exists(tempUpdaterExe))
                {
                    TaskDialog.Show("Ошибка обновления", "Не найден исполняемый файл обновления.");
                    return;
                }

                RemoveZoneIdentifiersRecursively(tempUpdaterDir);

                // Валидация наличия источника плагина — но не выбор.
                // Источник выберет UpdaterENS уже после закрытия Revit.
                if (!Directory.Exists(sourcePluginDir) &&
                    !(BAvtor && Directory.Exists(sourcePluginDirAvtor)))
                {
                    TaskDialog.Show("Ошибка обновления", "Папка с обновлением плагина не найдена.");
                    return;
                }

                Directory.CreateDirectory(backupDir);

                int pid = Process.GetCurrentProcess().Id;
                string logFile = Path.Combine(backupDir, $"{userPKName}_log.txt");

                // pid, mainSource, adminSource, target, backup, logFile, isAuthor
                string arguments = string.Join(" ",
                    Quote(pid.ToString()),
                    Quote(sourcePluginDir),        // main
                    Quote(sourcePluginDirAvtor),   // admin (используется только если isAuthor=1)
                    Quote(targetPluginDir),
                    Quote(backupDir),
                    Quote(logFile),
                    Quote(BAvtor ? "1" : "0"));

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempUpdaterExe,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
                TaskDialog.Show("Ошибка запуска обновления", ex.Message);
            }
        }
        private static string Quote(string s) => "\"" + s + "\"";
        /// <summary>
        /// Выбирает папку-источник.
        /// Не-автор  → только mainDir.
        /// Автор     → более свежая из mainDir / adminDir; при равных датах приоритет adminDir.
        /// Возвращает null, если ни одна из доступных папок не существует.
        /// </summary>
        private static string ChooseSource(string mainDir, string adminDir, bool isAuthor)
        {
            bool mainOk = !string.IsNullOrWhiteSpace(mainDir) && Directory.Exists(mainDir);
            bool adminOk = isAuthor
                           && !string.IsNullOrWhiteSpace(adminDir)
                           && Directory.Exists(adminDir);

            if (!mainOk && !adminOk) return null;
            if (!adminOk) return mainDir;
            if (!mainOk) return adminDir;

            return GetDateTimeFolder(adminDir) > GetDateTimeFolder(mainDir)
                ? adminDir
                : mainDir;
        }
        private static bool IsUpdaterRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("UpdaterENS");
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
        private static DateTime GetDateTimeFolder(string sourceDir)
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

        // Рекурсивное копирование директории с учётом дат изменения
        // Рекурсивное копирование директории с учётом дат изменения
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                return;

            Directory.CreateDirectory(targetDir);

            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = GetRelativePath(sourceDir, filePath);
                string targetFilePath = Path.Combine(targetDir, relativePath);
                string targetFileDir = Path.GetDirectoryName(targetFilePath);
                Directory.CreateDirectory(targetFileDir);

                // Копируем, если файл отсутствует или источник новее
                if (!File.Exists(targetFilePath) ||
                    File.GetLastWriteTimeUtc(filePath) > File.GetLastWriteTimeUtc(targetFilePath))
                {
                    File.Copy(filePath, targetFilePath, overwrite: true);

                    // File.Copy переносит ADS вместе с файлом — снимаем метку сразу,
                    // чтобы не тащить «заражение» дальше по цепочке.
                    RemoveZoneIdentifier(targetFilePath);
                }
            }
        }
        // Вычисление относительного пути (для .NET Framework 4.8)
        private static string GetRelativePath(string basePath, string fullPath)
        {
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(basePath.Length);
            else
                throw new ArgumentException("fullPath is not inside basePath");
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
        private static void RemoveZoneIdentifier(string filePath)
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



        public static DateTime GetLatestFileTime(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return DateTime.MinValue;

            var extensions = new[] { ".dll", ".exe" };

            var paths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

            if (!paths.Any())
                return DateTime.MinValue;

            return paths.Max(p => File.GetLastWriteTimeUtc(p));
        }
    }


}

           


