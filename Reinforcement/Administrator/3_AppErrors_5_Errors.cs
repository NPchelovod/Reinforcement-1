using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Reinforcement
{
    public static partial class AppErrors
    {
        // ================== Буфер ==================
        private static List<string> Errors = new List<string>(MaxErrors);
        private const int MaxErrors = 100;

        // ================== Троттлинг ==================
        private const int TimeOutMilliseconds = 200;

        // Ключ без аллокаций: тип + сообщение
        private struct ThrottleKey : IEquatable<ThrottleKey>
        {
            public readonly Type Type;
            public readonly string Message;
            public ThrottleKey(Type t, string m) { Type = t; Message = m; }
            public bool Equals(ThrottleKey o) => ReferenceEquals(Type, o.Type)
                                               && string.Equals(Message, o.Message);
            public override bool Equals(object o) => o is ThrottleKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Type?.GetHashCode() ?? 0;
                    return (h * 397) ^ (Message?.GetHashCode() ?? 0);
                }
            }
        }

        private struct ThrottleEntry
        {
            public int LastTick;    // Environment.TickCount
            public int Suppressed;  // сколько раз подавили
        }

        private static readonly Dictionary<ThrottleKey, ThrottleEntry> _throttle
            = new Dictionary<ThrottleKey, ThrottleEntry>(64);

        // ================== Синхронизация ==================
        private static int _inLogError;
        private static readonly object _errorsLock = new object();

        public static volatile bool LookErrorsAdmin = false;

        // ================== Основной вход ==================

        public static void LogError(Exception ex)
        {
            if (ex == null) return;
            if (Interlocked.CompareExchange(ref _inLogError, 1, 0) != 0) return;

            try
            {
                // ---- fast path: троттлинг без аллокаций ----
                var type = ex.GetType();
                var msg = ex.Message ?? string.Empty;   // Message обычно кэширован в exception
                var key = new ThrottleKey(type, msg);
                int nowTick = Environment.TickCount;

                int suppressed = 0;
                if (_throttle.TryGetValue(key, out var entry))
                {
                    int delta = unchecked(nowTick - entry.LastTick);
                    // delta >= 0 — защита от переполнения TickCount (~раз в 24.9 дней)
                    if (delta >= 0 && delta < TimeOutMilliseconds)
                    {
                        entry.Suppressed++;
                        _throttle[key] = entry;
                        return;                        // ← выходим без DateTime.Now, без форматирования
                    }
                    suppressed = entry.Suppressed;
                }

                // ---- slow path: реально пишем ----
                string place = ExtractPlace(ex);           // только для новой/прошедшей ошибки
                DateTime now = DateTime.Now;

                string line = $"{now:yyyy-MM-dd HH:mm:ss.fff} | {place} | {type.Name}: {msg}";
                if (suppressed > 0)
                    line += $" | (x{suppressed + 1})";     // показываем «всего было N+1»

                lock (_errorsLock)
                {
                    entry.LastTick = nowTick;
                    entry.Suppressed = 0;
                    _throttle[key] = entry;

                    Errors.Add(line);
                    if (Errors.Count > MaxErrors)
                        Errors.RemoveRange(0, Errors.Count - MaxErrors);
                }

                if (LookErrorsAdmin && ShouldShowDialog(line))
                {
                    if (ShowErrorDialog(line))
                        LookErrorsAdmin = false;
                }
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"LogError failed: {logEx}");
            }
            finally
            {
                Interlocked.Exchange(ref _inLogError, 0);
            }
        }

        // ================== Извлечение места ошибки ==================
        // Раньше был сплит ex.StackTrace по '\n' + StartsWith("at "/"в ") + IndexOf.
        // Теперь — TargetSite (O(1)) и, если не наш — StackTrace.Frames без сериализации в строку.

        private static string ExtractPlace(Exception ex)
        {
            try
            {
                // 1) TargetSite — метод, где выброшено (самое быстрое)
                var ts = ex.TargetSite;
                if (ts?.DeclaringType != null)
                {
                    string ns = ts.DeclaringType.FullName ?? string.Empty;
                    if (ns.StartsWith("Reinforcement.", StringComparison.Ordinal))
                        return $"{ts.DeclaringType.Name}.{ts.Name}";
                }

                // 2) Ищем первый фрейм нашего пространства имён
                var st = new StackTrace(ex, false);
                int n = st.FrameCount;
                for (int i = 0; i < n; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    var dt = m?.DeclaringType;
                    if (dt == null) continue;

                    string ns = dt.FullName ?? string.Empty;
                    if (ns.StartsWith("Reinforcement.", StringComparison.Ordinal))
                        return $"{dt.Name}.{m.Name}";
                }

                // 3) fallback — хоть что-то
                return ts != null ? $"{ts.DeclaringType?.Name}.{ts.Name}" : "no stack";
            }
            catch
            {
                return "stack error";
            }
        }

        // ================== Запись на диск ==================

        public static string NameFile  =>
            $"{App_Folders.UserName_MachineName}.errors.log";

        public static string errorLogPath
        {
            get
            {
                string folder = App_Folders.IsTempFolders
                    ? App_Folders.FolderErrorsLogTemp
                    : App_Folders.FolderErrorsLog;
                return Path.Combine(folder, NameFile);
            }
        }

        private const int MaxLogLines = 1000;
        private const long TrimThresholdBytes = 200_000;

        public static void FlushErrorsToLog()
        {
            // --- ownership transfer: I/O ВНЕ lock, LogError не блокируется ---
            List<string> toWrite;
            lock (_errorsLock)
            {
                if (Errors.Count == 0) return;
                toWrite = Errors;
                Errors = new List<string>(MaxErrors);
            }

            bool written = TryAppendLines(errorLogPath, toWrite);

            if (!written && !App_Folders.IsTempFolders)
                written = TryAppendLines(
                    Path.Combine(App_Folders.FolderErrorsLogTemp, NameFile), toWrite);

            if (!written && App_Folders.IsTempFolders)
                written = TryAppendLines(
                    Path.Combine(Path.GetTempPath(), NameFile), toWrite);

            if (!written)
            {
                // вернуть в буфер, порядок сохраняется
                lock (_errorsLock)
                {
                    Errors.InsertRange(0, toWrite);
                    if (Errors.Count > MaxErrors)
                        Errors.RemoveRange(0, Errors.Count - MaxErrors);
                }
                return;
            }

            TrimThrottle();   // флаш = хороший момент почистить словарь
        }

        private static bool TryAppendLines(string path, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return true;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) return false;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    if (!Directory.Exists(dir)) return false;
                }

                File.AppendAllLines(path, lines);

                // Trim — best effort: lines уже записаны, падение trim не должно ронять запись
                try
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > TrimThresholdBytes) TrimLogFile(path);
                }
                catch (Exception trimEx) { Debug.WriteLine(trimEx); }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        private static void TrimLogFile(string path)
        {
            try
            {
                var all = File.ReadAllLines(path);
                if (all.Length > MaxLogLines)
                    File.WriteAllLines(path, all.Skip(all.Length - MaxLogLines).ToArray());
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }

        private static void TrimThrottle()
        {
            if (_throttle.Count < 200) return;

            int nowTick = Environment.TickCount;
            lock (_errorsLock)
            {
                var stale = new List<ThrottleKey>();
                foreach (var kv in _throttle)
                {
                    int delta = unchecked(nowTick - kv.Value.LastTick);
                    if (delta < 0 || delta > 60_000)     // >60с или переполнение
                        stale.Add(kv.Key);
                }
                foreach (var k in stale) _throttle.Remove(k);
            }
        }
    }
}