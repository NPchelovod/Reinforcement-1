using System.IO;
using System;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reinforcement
{

    public partial class LookUsers
    {
        private static int dayMaxPast = 120;
        // Мягкий порог — триггер архивации и обрезки
        private const long maxFileSizeBytes = 5L * 1024 * 1024;   // 5 МБ
                                                                  // Жёсткий порог — на такой файл лучше даже не замахиваться парсером
        private const long hardFileSizeBytes = 20L * 1024 * 1024; // 20 МБ
        public static string folderStatistics = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics";
        //запись  архива
        public static string folderStatisticsHistory = Path.Combine(folderStatistics, "Архив");

        

        public static string fileName => $"{Environment.UserName}_{Environment.MachineName}.json";

        public static string filePath => Path.Combine(folderStatistics, fileName);
        //2 часа — разумное значение. Некоторые системы берут 15 минут, некоторые — 4 часа. Но полностью убирать периодический флаш почти никто не делает.
        private static readonly TimeSpan FlushInterval = TimeSpan.FromHours(2);

        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };

        private static int _inFlush;

        /// <summary>
        /// Принудительно сохранить (например, при закрытии Revit).
        /// </summary>

        public void ForceFlush(bool closeRevit)
        {
            try
            {
                
                FlushInternalLock();
                
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
                Debug.WriteLine($"ForceFlush failed: {ex.Message}");

            }

        }
        private void FlushInternalLock()
        {
            bool acquired = false;
            try
            {
                // Диск недоступен — не блокируем Revit, данные копятся в памяти
                if (!IsStorageAvailable())
                    return;

                if (Interlocked.CompareExchange(ref _inFlush, 1, 0) != 0)
                    return;
                acquired = true;
                lock (_lock)
                {
                    FlushInternal();
                }
            }
            catch (Exception ex)
            {
                MarkStorageUnavailable();
                App_Apdater_1.AppErrors.LogError(ex);
                Debug.WriteLine($"FlushInternalLock failed: {ex.Message}");
            }
            finally
            {
                if (acquired)
                {
                    Interlocked.Exchange(ref _inFlush, 0);
                }
            }
        }


        private void FlushInternal()
        {
            PassDate = DateTime.Now;

            // обновляем текущий день если сессия многодневная
            //DateDay = DateTime.Now.Date;
            UserName = Environment.UserName;
            if (WriteStats == null) WriteStats = new WritePerfStats();
            //записываем часовые результааты, после глобальных


            // 1. Читаем прошлый файл
            var past = ReadFileAndControlSize();// ReadFile();

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
                        copy[dk.Key] = dk.Value;//.Clone();
                    DictDateDocStats[dateKey] = copy;
                }
                else
                {
                    foreach (var dk in dateEntry.Value)
                    {
                        if (!targetDocs.TryGetValue(dk.Key, out var target))
                            targetDocs[dk.Key] = dk.Value;//.Clone();
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
                App_Apdater_1.AppErrors.LogError(new Exception("WriteFile failed, state rolled back"));
            }
        }


        private static volatile bool _storageAvailable = true;
        private static DateTime _lastStorageCheck = DateTime.MinValue;
        private static readonly TimeSpan StorageCheckInterval = TimeSpan.FromMinutes(5);
        /// <summary>
        /// Помечаем хранилище недоступным и сбрасываем кэш,
        /// чтобы следующая проверка сработала сразу.
        /// </summary>
        /// /// <summary>
        /// Единственное место, где мы узнаём, что диск мёртв — после реальной IO-ошибки.
        /// </summary>
        private static void MarkStorageUnavailable()
        {
            _storageAvailable = false;
            _lastStorageCheck = DateTime.MinValue;
        }
        private static DateTime _lastStorageFailure = DateTime.MinValue;
        private static readonly TimeSpan StorageRetryInterval = TimeSpan.FromMinutes(5);
        /// <summary>
        /// Быстрая проверка без блокировки. Если флаг false — 
        /// раз в 5 минут даём «один шанс» (вернём true, чтобы следующая запись попробовала).
        /// Если запись снова упадёт — MarkStorageUnavailable отодвинет окно.
        /// </summary>
        public static bool IsStorageAvailable()
        {
            if (_storageAvailable)
                return true;

            // Прошло достаточно времени — разрешаем одну пробную попытку
            if (DateTime.Now - _lastStorageFailure > StorageRetryInterval)
            {
                _storageAvailable = true;   // оптимистично: пусть следующая запись попробует
                return true;
            }

            return false;
        }

        private LookUsers ReadFileAndControlSize()
        {
            try
            {
                if (!IsStorageAvailable())
                {
                    return new LookUsers();
                }
                if (!File.Exists(filePath))
                    return new LookUsers();

                //првоерка размера до парсига
                long sizeBytes = new FileInfo(filePath).Length;
                if (sizeBytes > hardFileSizeBytes)
                {
                    //выполняем перемещение
                    MoveToHistory(DateDay, DateDay, "hardSize");
                    return new LookUsers();
                }

                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new LookUsers();

                var data = JsonSerializer.Deserialize<LookUsers>(json, Options);
                if (data == null) return new LookUsers();

                //проверка данных
                if (TryMoveToHistoryFile(data))
                {
                    return new LookUsers();
                }

                // 👇 вот здесь миграция старых версий LookUsers();
                data.MigrateLegacyData();
                return data;
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
                return new LookUsers();
            }
        }


        /// <summary>
        /// Проверяет активный файл перед записью.
        /// true — нужен "чистый старт" (жёсткий перебор или старая дата).
        /// </summary>
        private bool TryMoveToHistoryFile(LookUsers past)
        {

            long sizeBytes;
            sizeBytes = new FileInfo(filePath).Length;

            bool move = false;
            string rizon = "";
            // 1. Жёсткий порог — файл нечитаем, начинаем с чистого листа
            if (sizeBytes > hardFileSizeBytes)
            {
                move = true;
                rizon = "size";
            }

            // 2. Мягкий порог — архивируем по размеру
            if (sizeBytes > maxFileSizeBytes)
            {
                move = true;
                rizon = "size";

            }

            // 3. Порог по возрасту — ищем хоть одну дату старше dayMaxPast
            if (past?.DictDateDocStats != null && past.DictDateDocStats.Count > 0)
            {
                var oldest = past.DictDateDocStats.Keys.Min();
                if ((DateDay - oldest).TotalDays > dayMaxPast)
                {
                    move = true;
                    rizon = "date";
                }
            }
            if (!move) { return false; }
            if (!File.Exists(filePath)) { return false; }
            if (!Directory.Exists(folderStatisticsHistory))
                Directory.CreateDirectory(folderStatisticsHistory);
            var dates = past?.DictDateDocStats?.Keys.ToList();

            if (dates == null || dates.Count == 0)
            { return false; }

            var from = dates.Min();
            var to = dates.Max();

            return MoveToHistory(from, to, rizon);
        }

        private bool MoveToHistory(DateTime from, DateTime to, string rizon)
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
                catch (Exception ex) { App_Apdater_1.AppErrors.LogError(ex); }

                return File.Exists(historyPath);
            }
            catch (Exception ex)
            {
                MarkStorageUnavailable();
                App_Apdater_1.AppErrors.LogError(ex);
                return false;
            }
        }
        private bool WriteFile()
        {
            try
            {
                // Быстрый отказ, если диск недоступен — не ждём таймауты SMB
                if (!IsStorageAvailable())
                    return false;

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




                // 2. Потом пишем новый
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
                App_Apdater_1.AppErrors?.FlushErrorsToLog();

                return true;
            }
            catch (Exception ex)
            {
                // Любая IO-ошибка → диск считается недоступным до следующей проверки
                MarkStorageUnavailable();
                App_Apdater_1.AppErrors?.LogError(ex);
                App_Apdater_1.AppErrors?.FlushErrorsToLog();
                return false;
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
    }
}