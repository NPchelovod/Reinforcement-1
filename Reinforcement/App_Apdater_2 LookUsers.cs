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
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    public partial class LookUsers
    {

        public string UserName { get; set; }
        //просмотр характеристик пользователя и их обновление и сохранение 
        // ==== Данные ====
        public Dictionary<string, int> DictUse { get; set; } = new Dictionary<string, int>();
        public DateTime PassDate { get; set; } = DateTime.Now;

        public DateTime DateDay { get; set; } = DateTime.Now.Date;//дата текущего дня
        public DateTime DateOpenRevit { get; set; } = DateTime.Now;//дата открытия ревита шмевита
        //public Dictionary<string, int> DocsUse { get; set; } = new Dictionary<string, int>();


        //статистика по датам использования 
        public Dictionary<DateTime, Dictionary<string, int>> DocsDateUse { get; set; } = new Dictionary<DateTime, Dictionary<string, int>>();

        public HashSet<(DateTime open, DateTime close)>  DateTimesCloseAndOpenRevit { get; set; } = new HashSet<(DateTime open, DateTime close)>();

        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();

        //private static bool first=false;
        private static int _inUpdate;
        public void Update( string explicitCommandName = null)
        {
            // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
                return;
            try
            {
                lock (_lock)
                {
                    ProcessWriter(explicitCommandName); 
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


        private void ProcessWriter(string explicitCommandName)
        {
            

            string key = explicitCommandName ?? GetCallerName();
            if (string.IsNullOrEmpty(key))
                return;
            DateDay = DateTime.Now.Date;   // ← добавить

            DictUse.TryGetValue(key, out var value);
            DictUse[key] = value + 1;

            UIDocument uiDoc = RevitAPI.UiDocument;
            if (uiDoc == null) { return; }
              
                Document doc = uiDoc.Document;
            if (doc == null) { return; }

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
            if (string.IsNullOrEmpty(nameDoc))
            {
                return;
            }
            
            //DocsUse.TryGetValue(nameDoc, out value);
            //DocsUse[nameDoc] = value + 1;//можно сделать по времени чтобы было или как иначе??
            
            //теперь статистика по дням

            if(!DocsDateUse.TryGetValue(DateDay, out var dictUse))
            {
                dictUse = new Dictionary<string, int>();
                DocsDateUse[DateDay] = dictUse;
            }
            dictUse.TryGetValue(nameDoc, out value);
            dictUse[nameDoc]= value + 1;

        }

        /// <summary>
        /// Принудительно сохранить (например, при закрытии Revit).
        /// </summary>
        public void ForceFlush(bool closeRevit)
        {
            try
            {
                lock (_lock)
                {
                    if (closeRevit)
                    { //при закрытии окна статистика 
                        DateTimesCloseAndOpenRevit.Add((
                         new DateTime(DateOpenRevit.Year, DateOpenRevit.Month, DateOpenRevit.Day,
                                      DateOpenRevit.Hour, DateOpenRevit.Minute, DateOpenRevit.Second),
                         new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                      DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second)
                        ));
                    }

                    FlushInternal(closeRevit);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                Debug.WriteLine($"ForceFlush failed: {ex.Message}");

            }
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

        public void FlushInternal(bool closeRevit=false)
        {
            PassDate = DateTime.Now;

            // обновляем текущий день если сессия многодневная
            //DateDay = DateTime.Now.Date;
            UserName = Environment.UserName;

            //записываем часовые результааты, после глобальных


            // 1. Читаем прошлый файл
            var past = ReadFile();


            // Создаём НОВЫЕ словари, не трогая this.DictUse/DocsDateUse
            var mergedDictUse = new Dictionary<string, int>(DictUse);
            var mergedDocsDateUse = new Dictionary<DateTime, Dictionary<string, int>>();
            foreach (var kv in DocsDateUse)
            {
                mergedDocsDateUse[kv.Key] = new Dictionary<string, int>(kv.Value);
            }


            // 2. Складываем счётчики
            foreach (var kv in past.DictUse)
            {
                DictUse.TryGetValue(kv.Key, out var value);
                DictUse[kv.Key] = value + kv.Value;
            }
            //foreach (var kv in past.DocsUse)
            //{
            //    DocsUse.TryGetValue(kv.Key, out var value);
            //    DocsUse[kv.Key] = value + kv.Value;
            //}

            foreach (var kv in past.DocsDateUse)
            {
                var dateKey = kv.Key;
                var innerSource = kv.Value; // Dictionary<string, int> из прошлого

                if(dayMaxPast<(DateDay-kv.Key).TotalDays)
                {
                    continue;
                }

                if (!DocsDateUse.TryGetValue(dateKey, out var innerTarget))
                {
                    // Ключа нет — копируем весь внутренний словарь (чтобы не менять оригинал при будущих модификациях)
                    DocsDateUse[dateKey] = new Dictionary<string, int>(innerSource);
                }
                else
                {
                    // Ключ есть — суммируем значения по внутренним ключам
                    foreach (var innerKv in innerSource)
                    {
                        if (innerTarget.ContainsKey(innerKv.Key))
                            innerTarget[innerKv.Key] += innerKv.Value;
                        else
                            innerTarget[innerKv.Key] = innerKv.Value;
                    }
                }
            }

            if (closeRevit)
            {
                //только при закрытии ревита заполняем
                var cutoff = DateDay.AddDays(-dayMaxPast);
                foreach (var s in past.DateTimesCloseAndOpenRevit)
                {
                    if (s.open >= cutoff)
                        DateTimesCloseAndOpenRevit.Add(s);
                }

            }

            // 3. Пишем результат
            if (WriteFile())
            {
                // Обнуляем накопленное, чтобы не записать повторно
                //DictUse.Clear();
                DictUse.Clear();
                DocsDateUse.Clear();
                //DocsUse.Clear();
                DateTimesCloseAndOpenRevit.Clear();   // ← добавить, чтобы повторный флаш не дал дубликатов
            }
            else
            {
                //поидее надо откатить прошлые файлы!!!! Так как 
                DictUse = mergedDictUse;
                DocsDateUse = mergedDocsDateUse;
                LogError(new Exception("WriteFile failed, state rolled back"));
            }
        }

        private static int dayMaxPast = 100;
        private static int maxInt = 100000;
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
            catch (Exception ex)
            {
                LogError(ex);
                return new LookUsers();
            }
        }
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };
        private bool WriteFile()
        {
            try
            {
                if (!Directory.Exists(folderStatistics))
                    Directory.CreateDirectory(folderStatistics);

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

                // --- 2. Сбрасываем накопленные ошибки в .errors.log ---
                FlushErrorsToLog();

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex);
                // --- 2. Сбрасываем накопленные ошибки в .errors.log ---
                FlushErrorsToLog();
                return false;
            }
        }

        public static string folderStatistics = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics";
        //запись ошибок
        public static string folderErrors = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics\Errors";

        public static string fileName => $"{Environment.UserName}_{Environment.MachineName}.json";

        public static string filePath => Path.Combine(folderStatistics, fileName);
        private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(2);

        
    }
}
