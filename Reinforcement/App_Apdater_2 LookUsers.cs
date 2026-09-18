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
            var past = ReadFileAndControlDate();// ReadFile();


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

            //if (closeRevit) из-за этого затирало ведь может перезаписать
            //{
            //    //только при закрытии ревита заполняем
            //    var cutoff = DateDay.AddDays(-dayMaxPast);
            //    foreach (var s in past.DateTimesCloseAndOpenRevit)
            //    {
            //        if (s.open >= cutoff)
            //            DateTimesCloseAndOpenRevit.Add(s);
            //    }

            //}
            DateTimesCloseAndOpenRevit.UnionWith(past.DateTimesCloseAndOpenRevit);
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

        private static int dayMaxPast = 120;
        private static double kSave = 0.5;
        private static int dayMaxSavePastHistory = (int)Math.Round(dayMaxPast* kSave);// сохраняем половину истории только
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
        private LookUsers ReadFileAndControlDate()
        {
            //если есть дата старше N дней то надо нам сохранить в архим данный класс
            LookUsers file = ReadFile();
            //ротационный архив по периодам
            try
            {
                if (!File.Exists(filePath))
                {
                    return file;
                }
                long sizeBytes = new FileInfo(filePath).Length;
                // Аварийный случай: не читаем, просто архивируем как есть и начинаем заново
                if (sizeBytes > maxFileSizeBytes)
                {
                    //ArchiveRawFile(sizeBytes, reason: "hard-size");
                    return new LookUsers();
                }

                //ищем хоть одну дату старую
                var oldDate = file.DocsDateUse.Keys
                    .Where(x => (PassDate - x).TotalDays > dayMaxPast)
                    .OrderBy(x => x)
                    .FirstOrDefault();

                if (oldDate == default(DateTime))
                {
                    return file;
                }

                //иначе сохраняем статистику в прошлое состояние
                if (!Directory.Exists(folderStatisticsHistory))
                    Directory.CreateDirectory(folderStatisticsHistory);

                string fileNameHistory = $"{Environment.UserName}_{Environment.MachineName}_" +
                                 $"{oldDate.Year}_{oldDate.Month}_{oldDate.Day}To" +
                                 $"{DateDay.Year}_{DateDay.Month}_{DateDay.Day}.json";
                //копируем 
                // Третий параметр (overwrite) = true — перезапишет файл, если он уже есть
                File.Copy(filePath, filePathHistory, overwrite: true);
                if (File.Exists(filePathHistory))
                {
                    // Здесь можно дополнительно очистить старые даты из активной статистики
                    var cutoffDate = PassDate.AddDays(-dayMaxSavePastHistory);
                    var keysToRemove = file.DocsDateUse.Keys.Where(k => k < cutoffDate).ToList();
                    foreach (var k in keysToRemove)
                    {
                        file.DocsDateUse.Remove(k);
                    }
                    file.DateTimesCloseAndOpenRevit = file.DateTimesCloseAndOpenRevit
                    .Where(x => x.open > cutoffDate)
                    .ToHashSet();

                    //также вычищаем старые клики так то или нет?
                    file.DictUse = DecayDict(file.DictUse, kSave);
                    //return new LookUsers();
                }
            }
            catch(Exception ex) 
            {
                LogError(ex);
            }

            return file;

        }

        // Мягкий порог — триггер архивации и обрезки
        private const long maxFileSizeBytes = 5L * 1024 * 1024;   // 5 МБ
                                                                  // Жёсткий порог — на такой файл лучше даже не замахиваться парсером
        private const long hardFileSizeBytes = 20L * 1024 * 1024; // 20 МБ

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
        //запись  архива
        public static string folderStatisticsHistory = Path.Combine(folderStatistics, "Архив");

        //запись ошибок
        public static string folderErrors = Path.Combine(folderStatistics, "Errors"); 

        public static string fileName => $"{Environment.UserName}_{Environment.MachineName}.json";

        public static string filePath => Path.Combine(folderStatistics, fileName);

        public static string fileNameHistory;
        public static string filePathHistory=> Path.Combine(folderStatisticsHistory, fileNameHistory);

        private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(2);

        /// <summary>
        /// Уменьшает счётчик в kSave раз. Если результат &lt; 1 — возвращает 0.
        /// </summary>
        private static int Decay(int count, double factor)
        {
            if (count <= 0) return 0;
            double result = count * factor;
            if (result < 1.0) return 0;                       // «клик = 0»
            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }
        /// <summary>
        /// Применяет Decay ко всему словарю, выкидывая обнулившиеся ключи.
        /// </summary>
        private static Dictionary<string, int> DecayDict(Dictionary<string, int> src, double factor)
        {
            var result = new Dictionary<string, int>(src.Count);
            foreach (var kv in src)
            {
                int v = Decay(kv.Value, factor);
                if (v > 0)
                    result[kv.Key] = v;
            }
            return result;
        }
    }
}
