using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Text.Encodings.Web;
namespace Reinforcement
{
    public static class App_Apdater
    {
        //Автообновление самого плагина из папки в Share

        public static DateTime TargetLatestTime = DateTime.MinValue;
        public static Version Version = null;
        public static string VersionString = null;

        public static string tempFolder;//временная папка куда можно всякое сохранять
        public static string userPKName;//имя пользователя ПК

        public static string updaterSourceDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\UpdaterENS";
        public static string targetPluginDir;// текущая папка плагина)

        public static DateTime InitialTimePlugin;
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
        }
        public static LookUsers LookUsers = new LookUsers();
        public static void StartUpdateENS()
        {
            try
            {
                // Папка, куда будет устанавливаться обновление (текущая папка плагина)
                targetPluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                tempFolder = Path.GetTempPath();
                userPKName = $"{Environment.UserName}_{Environment.MachineName}";

                CalcOtherProp();


                
                string tempUpdaterDir = Path.Combine(tempFolder, "ENS_Updater");
                Directory.CreateDirectory(tempUpdaterDir);

                
                if (IsUpdaterRunning())
                {
                    // Для диагностики можно оставить лог, но TaskDialog не показываем —
                    // пользователя это не касается.
                    return;
                }

                // Копируем UpdaterENS целиком во временную папку
                CopyDirectory(updaterSourceDir, tempUpdaterDir);
                string tempUpdaterExe = Path.Combine(tempUpdaterDir, "UpdaterENS.exe");
                if (!File.Exists(tempUpdaterExe))
                {
                    TaskDialog.Show("Ошибка обновления", "Не найден исполняемый файл обновления.");
                    return;
                }

                RemoveZoneIdentifiersRecursively(tempUpdaterDir);

                // Папка с новыми файлами плагина
                string sourcePluginDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\ES_BIM_Плагин";
                if (!Directory.Exists(sourcePluginDir))
                {
                    TaskDialog.Show("Ошибка обновления", "Папка с обновлением не найдена.");
                    return;
                }

                

                // Папка для резервных копий заменяемых файлов
                string backupDir = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\RezervCopy";
                Directory.CreateDirectory(backupDir); // на всякий случай

                int pid = Process.GetCurrentProcess().Id;

                // Формируем аргументы: pid, source, target, backup, logFile
                string logFile = Path.Combine(backupDir, $"{userPKName}_log.txt");
                string arguments = $"\"{pid}\" \"{sourcePluginDir}\" \"{targetPluginDir}\" \"{backupDir}\" \"{logFile}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempUpdaterExe,
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false // для надёжности
                });

                
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка запуска обновления", ex.Message);
            }
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
        private static void RemoveZoneIdentifiersRecursively(string directory)
        {
            foreach (var filePath in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    string zonePath = filePath + ":Zone.Identifier";
                    if (File.Exists(zonePath))
                        File.Delete(zonePath);
                }
                catch
                {
                    // Игнорируем ошибки удаления
                }
            }
        }

        private static void RemoveZoneIdentifier(string filePath)
        {
            string zoneIdentifierPath = filePath + ":Zone.Identifier";
            try
            {
                if (File.Exists(zoneIdentifierPath))
                    File.Delete(zoneIdentifierPath);
            }
            catch
            {
                // Игнорируем ошибки, файл может не иметь этого потока
            }
        }


        public static DateTime GetLatestFileTime(string directoryPath)
        {
            //получение даты создания
            if (!Directory.Exists(directoryPath))
                return DateTime.MinValue;

            //var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
            //                     .Select(f => new FileInfo(f))
            //                     .Where(f => f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            //                                 f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            //                     .ToList();
            var extensions = new[] { ".dll", ".exe" };
            //var paths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            //.Where(p => Path.GetExtension(p).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            //    Path.GetExtension(p).Equals(".exe", StringComparison.OrdinalIgnoreCase));
            //искать только в текущей директории (без вложенных папок),
            var paths = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(p => extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase));

            if (!paths.Any())
                return DateTime.MinValue;

            return paths.Max(p => File.GetLastWriteTimeUtc(p));

            //if (files.Count == 0)
            //    return DateTime.MinValue;

            //// Максимальная дата последнего изменения
            //return files.Max(f => f.LastWriteTimeUtc); //Если нужно получить дату создания, замените LastWriteTimeUtc на CreationTimeUtc
        }


    }

    public class LookUsers
    {
        //просмотр характеристик пользователя и их обновление и сохранение 
        // ==== Данные ====
        public Dictionary<string, int> DictUse { get; set; } = new Dictionary<string, int>();
        public DateTime PassDate { get; set; } = DateTime.Now;

        public DateTime DateOpenRevit { get; set; } = DateTime.Now;//дата открытия ревита шмевита
        public Dictionary<string, int> DocsUse { get; set; } = new Dictionary<string, int>();

        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();

        //private static bool first=false;
        private static int _inUpdate;
        public void Update(ExternalCommandData commandData = null, string explicitCommandName = null)
        {
            // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
                return;


            try
            {
                string key = explicitCommandName ?? GetCallerName();
                if (string.IsNullOrEmpty(key))
                    return;

                lock (_lock)
                {
                    DictUse.TryGetValue(key, out var value);
                    DictUse[key] = value + 1;

                    UIDocument uiDoc = RevitAPI.UiDocument;
                    if (uiDoc != null)
                    {
                        Document doc = uiDoc.Document;
                        if (doc != null)
                        {
                            string nameDoc = doc.PathName;

                            

                            ModelPath centralModelPath = doc.GetWorksharingCentralModelPath();
                            if (centralModelPath != null)
                            {
                                string centralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                                // centralPath — это и есть путь к центральной модели string centralPath = BasicFileInfo.Extract(doc.PathName).CentralPath;
                                if (!string.IsNullOrEmpty(centralPath))
                                {
                                    nameDoc = centralPath;
                                }    
                            }
                            if (!string.IsNullOrEmpty(nameDoc))
                            {
                                DocsUse.TryGetValue(nameDoc, out value);

                                DocsUse[nameDoc] = value + 1;//можно сделать по времени чтобы было или как иначе??
                            }

                           
                        }
                    }
                }

                TryFlush();
            }
            catch
            {
                // Статистика не должна ломать основную команду
            }
            finally
            {
                Interlocked.Exchange(ref _inUpdate, 0);   // обязательно сбросить, даже при исключении
            }
        }

        /// <summary>
        /// Принудительно сохранить (например, при закрытии Revit).
        /// </summary>
        public void ForceFlush()
        {
            try
            {
                lock (_lock)
                {
                    FlushInternal();
                }
            }
            catch { }
        }
        // ==== Внутренняя логика ====

        private static string GetCallerName()
        {
            // 0 — GetCallerName, 1 — Update, 2 — вызывающий команду
            var st = new StackTrace(false);
            var frames = st.GetFrames();
            if (frames == null || frames.Length < 4)
                return null;

            var caller = frames[3].GetMethod();
            var typeName = caller?.DeclaringType?.Name;
            var methodName = caller?.Name;
            if (string.IsNullOrEmpty(typeName))
                return null;

            return $"{typeName}.{methodName}";
        }

        private void TryFlush()
        {
            lock (_lock)
            {
                if (DateTime.Now - PassDate < FlushInterval)
                {
                    return;
                }

                FlushInternal();
            }
        }

        public void FlushInternal()
        {
            PassDate = DateTime.Now;


            //записываем часовые результааты, после глобальных


            // 1. Читаем прошлый файл
            var past = ReadFile();

            // 2. Складываем счётчики
            foreach (var kv in past.DictUse)
            {
                DictUse.TryGetValue(kv.Key, out var value);
                DictUse[kv.Key] = value + kv.Value;
            }
            foreach (var kv in past.DocsUse)
            {
                DocsUse.TryGetValue(kv.Key, out var value);
                DocsUse[kv.Key] = value + kv.Value;
            }

            // 3. Пишем результат
            if (WriteFile())
            {
                // Обнуляем накопленное, чтобы не записать повторно
                //DictUse.Clear();
                DictUse.Clear();
                DocsUse.Clear();
            }
        }



        private LookUsers ReadFile()
        {
            try
            {
                if (!File.Exists(filePath))
                    return new LookUsers();

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new LookUsers();

                var data = JsonSerializer.Deserialize<LookUsers>(json, Options);
                return data ?? new LookUsers();
            }
            catch
            {
                return new LookUsers();
            }
        }
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };
        private bool WriteFile()
        {
            try
            {
                if (!Directory.Exists(statistics))
                    Directory.CreateDirectory(statistics);

                string json = JsonSerializer.Serialize(this, Options);

                // Пишем во временный файл рядом с целевым (в той же папке —
                // это важно: File.Replace/File.Move работают атомарно только
                // в пределах одного тома)
                string tmp = filePath + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(filePath))
                {
                    // Атомарная замена существующего файла:
                    // в целевой путь попадает tmp, старый файл удаляется.
                    // Третий параметр — путь для бэкапа (null — бэкап не нужен).
                    File.Replace(tmp, filePath, destinationBackupFileName: null,
                                 ignoreMetadataErrors: true);
                }
                else
                {
                    // Файла ещё нет — просто переносим.
                    File.Move(tmp, filePath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string statistics = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics";
        public static string nameAdd => $"{Environment.UserName}_{Environment.MachineName}.json";

        public static string filePath => Path.Combine(statistics, nameAdd);
        private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(2);

        
    }
           

}
