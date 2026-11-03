using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public class StatElement
    {
        // ==== Данные ==== они идут в файл записи
        public string UserName = Environment.UserName;
        public string VersionName => SettingsWindow.GetDatePluginText;
        public DateTime DateDay { get; set; } = DateTime.Now.Date;//дата текущего дня
        public Dictionary<DateTime, Dictionary<string, DocStats>> DictDateDocStats { get; set; }
            = new Dictionary<DateTime, Dictionary<string, DocStats>>();
        public WritePerfStats WriteStats { get; set; } = new WritePerfStats();
    }

    public  static class StatUsers
    {
        public static StatElement statElement = new StatElement();
        private static int _inUpdate;
        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();
        private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(300);
        public static void Update(string explicitCommandName = null, EDocStatsOptions commandType = 0)
        {
            // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
            {
                return;
            }

            try
            {
                
                lock (_lock)
                {
                    Now = DateTime.Now;
                    if ((Now - _pastDateProcessStat) < ThrottleWindow)
                    {
                        //по времени слишком частые друг за другом идущие изменения - не реагируем
                        return;
                    }
                    ProcessStat(explicitCommandName, commandType);
                }
                //запись статистики
                if (Now - _pastDateFlush < _flushInterval)
                {
                    Flush();
                }
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);
            }
            finally
            {
               
                Interlocked.Exchange(ref _inUpdate, 0);
            }
        }

        public static DateTime Now = DateTime.Now;//для быстрого доступа к дате

        
        private static DateTime _pastDateProcessStat = DateTime.MinValue;
        private static int _hour = 0;
        private static void ProcessStat(string explicitCommandName, EDocStatsOptions commandType)
        {
            _pastDateProcessStat = Now;
            _hour = Now.Hour;
            //запись статистики
            string key = explicitCommandName ?? GetCallerName(commandType);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
        }

        private static DateTime _pastDateFlush = DateTime.MinValue;
        private static readonly TimeSpan _flushInterval = TimeSpan.FromHours(2.5);
        private static int _inFlush;
        public static void Flush()
        {
            if (Interlocked.CompareExchange(ref _inFlush, 1, 0) != 0)
            {
                return;
            }
            try
            {
                lock (_lock)
                {
                    _pastDateFlush = Now;
                    // 1. Читаем прошлый файл
                    StatElement past = ReadFileAndControlSize();
                    if(past==null)
                    {
                        past = new StatElement();

                    }
                }
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _inFlush, 0);
            }

        }

        public static string folderStat => App_Folders.FolderStatistics;
        //запись  архива
        public static string folderStatisticsHistory = Path.Combine(folderStat, "Архив");
        public static string fileName => $"{Environment.UserName}_{Environment.MachineName}.json";
        public static string filePath => Path.Combine(folderStat, fileName);
        private const long maxFileSizeBytes = 5L * 1024 * 1024;   // 5 МБ
        private const long hardFileSizeBytes = 20L * 1024 * 1024; // 20 МБ Жёсткий порог — на такой файл лучше даже не замахиваться парсером
        private static StatElement ReadFileAndControlSize()
        {
            try
            {
                
                if (!IsStorageAvailable())
                {
                   return null;
                }
                if (!File.Exists(filePath))
                {
                    return null;
                }

                //првоерка размера до парсига
                long sizeBytes = new FileInfo(filePath).Length;
                if (sizeBytes > hardFileSizeBytes)
                {
                    MoveToHistory(Now, Now, "hardSize");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var data = JsonSerializer.Deserialize<StatElement>(json, Options);
                if (data == null) return null;

                //если размер большой и надо в иторию вложить уже
                if (sizeBytes > maxFileSizeBytes)
                {
                    DateTime from = Now;
                    DateTime to = Now; 
                    if(data.DictDateDocStats!=null)
                    {
                        from = data.DictDateDocStats.Keys.Min();
                        to= data.DictDateDocStats.Keys.Max();
                    }
                    MoveToHistory(from, to, "maxSize");
                    return null;
                }

                //проверка данных
               
                return data;
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);
                return null;
            }
        }

        private static volatile bool _storageAvailable = true;
        private static readonly TimeSpan StorageRetryInterval = TimeSpan.FromMinutes(5);
        private static DateTime _lastStorageFailure = DateTime.MinValue;
        private static bool IsStorageAvailable()
        {
            if (!_storageAvailable && Now - _lastStorageFailure < StorageRetryInterval)
            {
                return _storageAvailable;
            }
            // Прошло достаточно времени — разрешаем одну пробную попытку

            if(!Directory.Exists(folderStat))
            {
                _storageAvailable = false;
            }
            _storageAvailable= true;
            return _storageAvailable;

        }
        private static string GetCallerName(EDocStatsOptions commandType)
        {
            // 0 — GetCallerName, 1 — Update, 2 — вызывающий команду
            var st = new StackTrace(false);
            var frames = st.GetFrames();
            if (frames == null || frames.Length < 5)
                return null;
            if (commandType == EDocStatsOptions.Invoker)
            {
                int cc = 0;
            }
            else
            {

            }
            var caller = frames[4].GetMethod();
            var typeName = caller?.DeclaringType?.Name;
            var methodName = caller?.Name;
            if (string.IsNullOrEmpty(typeName))
                return null;

            return $"{typeName}.{methodName}";
        }
        private static bool MoveToHistory(DateTime from, DateTime to, string rizon)
        {
            if (!IsStorageAvailable())
                return false;

            try
            {
                string historyName = $"{Environment.UserName}_{Environment.MachineName}_" +
                                     $"{from.Year}_{from.Month}_{from.Day}To" +
                                     $"{to.Year}_{to.Month}_{to.Day}_{rizon}.json";
                string baseName = Path.GetFileNameWithoutExtension(historyName);
                string historyPath = Path.Combine(folderStatisticsHistory, historyName);
                int suffix = 1;
                while (File.Exists(historyPath))
                {
                    historyPath = Path.Combine(folderStatisticsHistory,
                        $"{baseName}_{suffix++}.json");
                }

                File.Copy(filePath, historyPath, overwrite: true);
                try { File.Delete(filePath); }
                catch (Exception ex) { RevitAPI.AppErrors.LogError(ex); }

                return File.Exists(historyPath);
            }
            catch (Exception ex)
            {
                
                RevitAPI.AppErrors?.LogError(ex);
                return false;
            }
        }
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };


    }
}
