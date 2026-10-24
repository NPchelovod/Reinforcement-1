
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Reflection;

using System.Runtime.InteropServices;
namespace Reinforcement
{
    public static class UpdaterPlagina
    {
        //Запуск автообновления
        public static void StartUpdateENS()
        {
            try
            {
                if (!App_Folders.isInitialize)
                {
                    App_Folders.Initializator();
                }
                if (!AutomaticUpdatesEnabled) return;
                // Разово вылечиваем текущую папку плагина от Zone.Identifier —
                // на случай, если предыдущее обновление оставило метки.
                App_Folders.CleanupTargetFolder(App_Folders.TargetPluginDir);

                Directory.CreateDirectory(App_Folders.TempUpdaterDir);

                if (App_Folders.IsExistProcessCurent(App_Folders.UpdaterNameProcess))
                {
                    return;
                }

                // --- 1. Выбираем папку-источник САМОГО апдейтера ---
                // --- выбор апдейтера оставляем здесь: exe нужно СКОПИРОВАТЬ и ЗАПУСТИТЬ,
                //     а «пока работает плагин» свежесть апдейтера уже не проверить. ---
                // Не-автор: только основной источник.
                // Автор:   свежайший из (main, admin), при равенстве — admin.
                string updaterSource = ChooseSource(
                    mainDir: App_Folders.UpdaterSourceDir,
                    adminDir: App_Folders.UpdaterSourceDirAvtor,
                    isAuthor: App_Folders.UserIsAvtor);

                if (updaterSource == null)
                {
                    TaskDialog.Show("Ошибка обновления", "Папка с апдейтером не найдена.");
                    return;
                }

                CopyDirectory(updaterSource, App_Folders.TempUpdaterDir);

               
                if (!File.Exists(App_Folders.TempUpdaterExe))
                {
                    TaskDialog.Show("Ошибка обновления", "Не найден исполняемый файл обновления.");
                    return;
                }

                App_Folders.CleanupTargetFolder(App_Folders.TempUpdaterDir);

                // Валидация наличия источника плагина — но не выбор.
                // Источник выберет UpdaterENS уже после закрытия Revit.
                if (!Directory.Exists(App_Folders.SourcePluginDir) &&
                    !(App_Folders.UserIsAvtor && Directory.Exists(App_Folders.SourcePluginDirAvtor)))
                {
                    TaskDialog.Show("Ошибка обновления", "Папка с обновлением плагина не найдена.");
                    return;
                }

                Directory.CreateDirectory(App_Folders.BackupDir);

                int pid = Process.GetCurrentProcess().Id;
                string logFile = Path.Combine(App_Folders.BackupDir, $"{App_Folders.UserName_MachineName}_log.txt");

                // pid, mainSource, adminSource, target, backup, logFile, isAuthor
                string arguments = string.Join(" ",
                    Quote(pid.ToString()),
                    Quote(App_Folders.SourcePluginDir),        // main
                    Quote(App_Folders.SourcePluginDirAvtor),   // admin (используется только если isAuthor=1)
                    Quote(App_Folders.TargetPluginDir),
                    Quote(App_Folders.BackupDir),
                    Quote(App_Folders.LogUpdaterFile),
                    Quote(App_Folders.UserIsAvtor ? "1" : "0"));

                Process.Start(new ProcessStartInfo
                {
                    FileName = App_Folders.TempUpdaterExe,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
                TaskDialog.Show("Ошибка запуска обновления", ex.Message);
            }
        }



        public static bool AutomaticUpdatesEnabled => true;
        public static bool AutomaticUpdatesEnabled2
        {
            get
            {
                string path = Assembly.GetExecutingAssembly().Location + ".config";
                if (!File.Exists(path)) return false;
                try
                {
                    var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
                    var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
                    return string.Equals(configuration.AppSettings.Settings["EnableAutomaticUpdates"]?.Value,
                        "true", StringComparison.OrdinalIgnoreCase);
                }
                catch (ConfigurationErrorsException) { return false; }
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

            return App_Folders.GetDateTimeFolder(adminDir) > App_Folders.GetDateTimeFolder(mainDir)
                ? adminDir
                : mainDir;
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
                    App_Folders.RemoveZoneIdentifier(targetFilePath);
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

        



        
    }


}

           


