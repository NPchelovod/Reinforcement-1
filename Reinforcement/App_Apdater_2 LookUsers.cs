using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        // ВАЖНО: инициализируем, иначе первый Update упадёт на NRE
        public Dictionary<DateTime, Dictionary<string, DocStats>> DictDateDocStats { get; set; }
            = new Dictionary<DateTime, Dictionary<string, DocStats>>();
        /// <summary>Статистика по времени записи JSON (для оценки необходимости async).</summary>
        public WritePerfStats WriteStats { get; set; } = new WritePerfStats();

        public DateTime PassDate { get; set; } = DateTime.Now;
        public DateTime DateDay { get; set; } = DateTime.Now.Date;//дата текущего дня

        // ==== Legacy (только чтение) ==== временно до удаления у всех пользователей
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<DateTime, Dictionary<string, int>> DocsDateUse { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<DateTime, Dictionary<string, int>> DictDateUse { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> DictUse { get; set; }

        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();
        private static int _inUpdate;
        public void Update( string explicitCommandName = null, EDocStatsOptions commandType = 0)
        {
            // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
                return;
            try
            {
                lock (_lock)
                {
                    ProcessWriter(explicitCommandName, commandType); 
                }

                TryFlush(commandType);
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


        private void ProcessWriter(string explicitCommandName, EDocStatsOptions commandType)
        {
            
            string key = explicitCommandName ?? GetCallerName();
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            DateDay = DateTime.Now.Date;   // ← добавить

            UIDocument uiDoc = RevitAPI.UiDocument;
            if (uiDoc == null) { return; }
            Autodesk.Revit.DB.View activeView = uiDoc.ActiveView;
            string viewName = "None";
            if (activeView != null && !string.IsNullOrEmpty(activeView.Name))
            {
                viewName = activeView.Name;
            }

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


            //команды кликов
            if(!DictDateDocStats.TryGetValue(DateDay, out var docStats))
            {
                docStats = new Dictionary<string, DocStats>();
                DictDateDocStats[DateDay]= docStats;
            }
            if (!docStats.TryGetValue(nameDoc, out var docStat))
            {
                docStat = new DocStats();
                docStats[nameDoc]= docStat;

                //заполняем даты открытия данной модели
                docStat.FirstSeen= DateTime.Now;
            }
            //всегда может оказаться последним сеансом
            docStat.LastSeen = DateTime.Now;
            docStat.TotalOps += 1;                                   // ← общий счётчик действий
            
            docStat.CommandHits.TryGetValue(key, out var cmdHits);
            docStat.CommandHits[key]= cmdHits+1;

            //добавка активного вида для статистики оч полезно знать сколько времени надо для создания вида и тд
            if(!docStat.ActiveViews.TryGetValue(viewName, out var viewData))
            {
                docStat.ActiveViews[viewName] = (DateTime.Now, DateTime.Now, 1);
            }
            else
            {
                docStat.ActiveViews[viewName] = (viewData.FirstSeen, DateTime.Now, viewData.TotalOps + 1);
            }


            if (commandType != 0)
            {
                switch (commandType)
                {
                    case (EDocStatsOptions.Save):
                        docStat.SaveCount++;
                        docStat.TotalSaveSeconds = App.secondSaveModel;
                        docStat.LastSave = DateTime.Now;
                        break;
                    case (EDocStatsOptions.Sync):
                        docStat.SyncCount++;
                        docStat.TotalSaveSeconds = App.secondSaveModel;
                        docStat.LastSync = DateTime.Now;
                        break;
                    case (EDocStatsOptions.CloseRevit):
                        docStat.CloseRevit=true;
                        break;
                    default:
                        break;
                }
            }


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

                    FlushInternal();
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

        private void TryFlush(EDocStatsOptions commandType)
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

            // обновляем текущий день если сессия многодневная
            //DateDay = DateTime.Now.Date;
            UserName = Environment.UserName;

            //записываем часовые результааты, после глобальных


            // 1. Читаем прошлый файл
            var past = ReadFileAndControlDate();// ReadFile();

            //прошлый словарь данных
            var pastDict = past.DictDateDocStats
                       ?? new Dictionary<DateTime, Dictionary<string, DocStats>>();
            // 2. Снимок текущего состояния — на случай неудачной записи
            var backup = DeepCloneDict(DictDateDocStats);

            var backupPerf = WriteStats.Clone();
            //складываем с нашим словарем
            // 3. Складываем прошлое в текущее
            foreach (var dateEntry in pastDict)
            {
                var dateKey = dateEntry.Key;

                // отсекаем слишком старые даты
                if ((DateDay - dateKey).TotalDays > dayMaxPast)
                    continue;

                if (!DictDateDocStats.TryGetValue(dateKey, out var targetDocs))
                {
                    // копируем через Clone, чтобы не тащить ссылки из past
                    var copy = new Dictionary<string, DocStats>(dateEntry.Value.Count);
                    foreach (var dk in dateEntry.Value)
                        copy[dk.Key] = dk.Value.Clone();
                    DictDateDocStats[dateKey] = copy;
                }
                else
                {
                    foreach (var dk in dateEntry.Value)
                    {
                        if (!targetDocs.TryGetValue(dk.Key, out var target))
                            targetDocs[dk.Key] = dk.Value.Clone();
                        else
                            target.Merge(dk.Value);
                    }
                }
            }
            // при слиянии past с текущим — после основного merge
            if (past.WriteStats != null)
                WriteStats.Merge(past.WriteStats);
            // 3. Пишем результат
            if (WriteFile())
            {
                // Успешно — обнуляем накопленное, чтобы не записать повторно
                DictDateDocStats = new Dictionary<DateTime, Dictionary<string, DocStats>>();
            }
            else
            {
                //поидее надо откатить прошлые файлы!!!! Так как 
                //нет окатывать не надо у нас же независимые классы
                // Неудача — откатываемся к состоянию до слияния
                DictDateDocStats = backup;
                WriteStats = backupPerf;
                LogError(new Exception("WriteFile failed, state rolled back"));
            }
        }
        /// <summary>Глубокая копия словаря со всеми DocStats (для отката).</summary>
        private static Dictionary<DateTime, Dictionary<string, DocStats>> DeepCloneDict(
            Dictionary<DateTime, Dictionary<string, DocStats>> src)
        {
            var result = new Dictionary<DateTime, Dictionary<string, DocStats>>();
            if (src == null) return result;

            foreach (var dateEntry in src)
            {
                var inner = new Dictionary<string, DocStats>(dateEntry.Value.Count);
                foreach (var dk in dateEntry.Value)
                    inner[dk.Key] = dk.Value.Clone();
                result[dateEntry.Key] = inner;
            }
            return result;
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
                // 👇 вот здесь
                data.MigrateLegacyData();
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
                if (file.DictDateDocStats == null)
                    file.DictDateDocStats = new Dictionary<DateTime, Dictionary<string, DocStats>>();
                //ищем хоть одну дату старую
                var oldDate = file.DictDateDocStats.Keys
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

                string historyPath = Path.Combine(
                folderStatisticsHistory,
                $"{Environment.UserName}_{Environment.MachineName}_" +
                $"{oldDate.Year}_{oldDate.Month}_{oldDate.Day}To" +
                $"{DateDay.Year}_{DateDay.Month}_{DateDay.Day}.json");

                File.Copy(filePath, historyPath, overwrite: true);
                if (File.Exists(historyPath))
                {
                    // Обрезаем активный файл: удаляем даты старше dayMaxSavePastHistory
                    var cutoffDate = PassDate.AddDays(-dayMaxSavePastHistory);
                    var keysToRemove = file.DictDateDocStats.Keys
                        .Where(k => k < cutoffDate)
                        .ToList();

                    foreach (var k in keysToRemove)
                        file.DictDateDocStats.Remove(k);

                    // Decay для CommandHits в оставшихся днях
                    foreach (var dateEntry in file.DictDateDocStats)
                        foreach (var docStat in dateEntry.Value.Values)
                            DecayCommandHits(docStat.CommandHits, kSave);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

            return file;

        }
        // ==== Decay (адаптирован под CommandHits) ====
        private static int Decay(int count, double factor)
        {
            if (count <= 0) return 0;
            double result = count * factor;
            if (result < 1.0) return 0;
            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }

        private static void DecayCommandHits(Dictionary<string, int> src, double factor)
        {
            if (src == null) return;
            var keys = src.Keys.ToList();
            foreach (var k in keys)
            {
                int v = Decay(src[k], factor);
                if (v > 0) src[k] = v;
                else src.Remove(k);
            }
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
                // Убеждаемся, что поле есть (после десериализации старых файлов могло быть null)
                if (WriteStats == null) WriteStats = new WritePerfStats();
                // --- 1. Замер сериализации ---
                var swSerialize = Stopwatch.StartNew();

                string json = JsonSerializer.Serialize(this, Options);
                swSerialize.Stop();
                // Пишем во временный файл рядом с целевым (в той же папке —
                // это важно: File.Replace/File.Move работают атомарно только
                // в пределах одного тома)
                string tmp = filePath + ".tmp";
                // --- 2. Замер I/O ---
                var swIo = Stopwatch.StartNew();

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
                swIo.Stop();

                // --- 3. Записываем метрики в объект ---
                // ВАЖНО: эти цифры попадут в JSON только при СЛЕДУЮЩЕЙ записи,
                // потому что текущий json уже сериализован выше. Это нормально —
                // одна итерация задержки не критична.
                WriteStats.Add(swSerialize.Elapsed.TotalMilliseconds,
                               swIo.Elapsed.TotalMilliseconds);

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
        //2 часа — разумное значение. Некоторые системы берут 15 минут, некоторые — 4 часа. Но полностью убирать периодический флаш почти никто не делает.
        private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(2);
        //Единственная причина убрать флаш — если запись на сеть реально тормозит Revit. Но тогда правильнее сделать запись асинхронной, а не редкой.

        /// <summary>
        /// Однократная миграция старых JSON-файлов (DocsDateUse/DictDateUse/DictUse)
        /// в новый формат DictDateDocStats. Безопасно вызывать повторно.
        /// </summary>
        private void MigrateLegacyData()
        {
            if (DictDateDocStats == null)
                DictDateDocStats = new Dictionary<DateTime, Dictionary<string, DocStats>>();

            // --- 1. DocsDateUse: { день -> { путь -> int } } -> DictDateDocStats ---
            if (DocsDateUse != null && DocsDateUse.Count > 0)
            {
                foreach (var dateEntry in DocsDateUse)
                {
                    var date = dateEntry.Key.Date; // нормализуем до дня
                    if (!DictDateDocStats.TryGetValue(date, out var docStats))
                    {
                        docStats = new Dictionary<string, DocStats>();
                        DictDateDocStats[date] = docStats;
                    }

                    foreach (var docEntry in dateEntry.Value)
                    {
                        string path = docEntry.Key;
                        int count = docEntry.Value;
                        if (count <= 0) continue;

                        if (!docStats.TryGetValue(path, out var stat))
                        {
                            stat = new DocStats();
                            docStats[path] = stat;
                        }

                        stat.TotalOps += count;

                        // сохраняем распределение, но без разбивки по командам —
                        // кладём всё в один legacy-ключ
                        stat.CommandHits.TryGetValue("legacy", out var lh);
                        stat.CommandHits["legacy"] = lh + count;

                        // FirstSeen/LastSeen приближённо = дата дня
                        if (stat.FirstSeen == default || date < stat.FirstSeen)
                            stat.FirstSeen = date;
                        if (stat.LastSeen < date)
                            stat.LastSeen = date;
                    }
                }
            }

            // --- 2. DictDateUse: старый словарь по командам с разбивкой по дням ---
            if (DictDateUse != null && DictDateUse.Count > 0)
            {
                foreach (var dateEntry in DictDateUse)
                {
                    var date = dateEntry.Key.Date;
                    if (!DictDateDocStats.TryGetValue(date, out var docStats))
                    {
                        docStats = new Dictionary<string, DocStats>();
                        DictDateDocStats[date] = docStats;
                    }

                    // команды без привязки к документу кладём в спец-ключ
                    const string unknownDoc = "<no-document>";
                    if (!docStats.TryGetValue(unknownDoc, out var stat))
                    {
                        stat = new DocStats();
                        docStats[unknownDoc] = stat;
                    }

                    foreach (var cmdEntry in dateEntry.Value)
                    {
                        stat.CommandHits.TryGetValue(cmdEntry.Key, out var v);
                        stat.CommandHits[cmdEntry.Key] = v + cmdEntry.Value;
                        stat.TotalOps += cmdEntry.Value;
                    }
                }
            }

            // --- 3. DictUse: глобальный счётчик по командам без даты ---
            // Кладём в «сегодня» с пометкой legacy, чтобы не потерять.
            if (DictUse != null && DictUse.Count > 0)
            {
                var today = DateTime.Now.Date;
                if (!DictDateDocStats.TryGetValue(today, out var docStats))
                {
                    docStats = new Dictionary<string, DocStats>();
                    DictDateDocStats[today] = docStats;
                }

                const string unknownDoc = "<no-document>";
                if (!docStats.TryGetValue(unknownDoc, out var stat))
                {
                    stat = new DocStats { FirstSeen = DateTime.Now, LastSeen = DateTime.Now };
                    docStats[unknownDoc] = stat;
                }

                foreach (var cmdEntry in DictUse)
                {
                    stat.CommandHits.TryGetValue(cmdEntry.Key, out var v);
                    stat.CommandHits[cmdEntry.Key] = v + cmdEntry.Value;
                    stat.TotalOps += cmdEntry.Value;
                }
            }

            // После миграции старые поля не нужны — при записи они не попадут в JSON
            DocsDateUse = null;
            DictDateUse = null;
            DictUse = null;
        }
    }
}
