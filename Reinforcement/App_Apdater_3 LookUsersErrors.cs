using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reinforcement
{
    public partial class LookUsers
    {
        /// <summary>
        /// Ошибки, накопленные за сессию. Попадут в JSON при следующем WriteFile.
        /// </summary>
        private List<string> Errors { get; set; } = new List<string>();
        private const int MaxErrors = 100; // чтобы файл не разрастался
        private static int _inLogError;
        private static readonly object _errorsLock = new object();

        public static volatile bool LookErrorsAdmin = false; // можно будет отлаживать ошибку и ловить
        public void LogError2(Exception ex)//медленный он не находит строк номера
        {
            if (ex == null) return;
            if (Interlocked.CompareExchange(ref _inLogError, 1, 0) != 0) return;

            

            try
            {
                // === 1. Пытаемся получить "своё" место ошибки с номером строки ===
                string place = ExtractPlace(ex, out string fullStack);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {place} | {ex.GetType().Name}: {ex.Message}";

                // === 2. Пишем в накопительный список (уйдёт в .errors.log) ===
                lock (_errorsLock)
                {
                    Errors.Add(line);
                    if (Errors.Count > MaxErrors)
                        Errors.RemoveRange(0, Errors.Count - MaxErrors);
                }

                // === 3. Показываем окно, если включён админ-режим ===
                if (LookErrorsAdmin && ShouldShowDialog(line))
                {
                    bool disable = ShowErrorDialog(fullStack);
                    if (disable)
                        LookErrorsAdmin = false;
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _inLogError, 0);
            }
        }
        /// <summary>
        /// Возвращает компактное место ошибки: "Type.Method at File.cs:line N".
        /// Через fullStack отдаёт полный стек со всей информацией (для окна).
        /// Пробует несколько источников — от точного к грубому.
        /// </summary>
        private static string ExtractPlace(Exception ex, out string fullStack)
        {
           
            fullStack = null;

            // --- Попытка 1: StackTrace с fNeedFileInfo:true ---
            // Даёт номера строк, если PDB подгружен (запуск из-под VS,
            // загрузка сборки с диска). Для Assembly.Load(byte[]) вернёт null/0.
            try
            {
                var st = new StackTrace(ex, fNeedFileInfo: true);
                var frames = st.GetFrames();

                if (frames != null && frames.Length > 0)
                {
                    string firstUserWithLine = null;
                    string firstUser = null;
                    string firstAny = null;

                    var sb = new StringBuilder();

                    foreach (var frame in frames)
                    {
                        var method = frame.GetMethod();
                        if (method == null) continue;

                        string typeName = method.DeclaringType?.FullName ?? "?";
                        string methodName = method.Name;

                        string file = frame.GetFileName();
                        int lineNum = frame.GetFileLineNumber();

                        bool isUser = typeName.StartsWith("Reinforcement.", StringComparison.Ordinal);
                        bool hasLine = !string.IsNullOrEmpty(file) && lineNum > 0;

                        string lineEntry;
                        if (hasLine)
                        {
                            lineEntry = $"{typeName}.{methodName} at {Path.GetFileName(file)}:line {lineNum}";
                        }
                        else
                        {
                            lineEntry = $"{typeName}.{methodName}";
                        }

                        // Полный стек — для окна
                        sb.AppendLine("  " + lineEntry);

                        if (firstAny == null) firstAny = lineEntry;

                        if (isUser)
                        {
                            if (firstUser == null) firstUser = lineEntry;
                            if (hasLine && firstUserWithLine == null)
                                firstUserWithLine = lineEntry;
                        }
                    }

                    fullStack = sb.ToString();

                    // Приоритет: свой кадр с номером строки → свой кадр → любой кадр
                    string best = firstUserWithLine ?? firstUser ?? firstAny;
                    if (!string.IsNullOrEmpty(best))
                        return best;
                }
            }
            catch { /* StackTrace может упасть в экзотических случаях */ }

            // --- Попытка 2: парсим ex.StackTrace как текст ---
            // Именно этот путь работает, если первая попытка не дала номеров,
            // но ex.StackTrace уже содержит "at File.cs:line N".
            string parsed = ExtractPlaceFromString(ex.StackTrace, out string parsedStack);
            if (!string.IsNullOrEmpty(parsedStack))
                fullStack = parsedStack;

            return parsed ?? "no stack";
        }
        /// <summary>
        /// Разбирает сырой стек как строку. Поддерживает русский и английский форматы.
        /// Возвращает компактное "Type.Method at File.cs:line N" и полный стек через out.
        /// </summary>
        private static string ExtractPlaceFromString(string stack, out string fullStack)
        {
            fullStack = null;
            if (string.IsNullOrEmpty(stack)) return null;

            var rawLines = stack.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string firstUserWithLine = null;
            string firstUser = null;
            string firstAny = null;

            const string userPrefix = "Reinforcement.";
            var sb = new StringBuilder();

            foreach (var raw in rawLines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                // Отрезаем префикс: "at " (en) или "в " (ru)
                if (line.StartsWith("at ", StringComparison.Ordinal))
                    line = line.Substring(3);
                else if (line.StartsWith("в ", StringComparison.Ordinal))
                    line = line.Substring(2);
                else
                    continue;

                line = line.Trim();
                if (line.Length == 0) continue;

                // Ищем ":line N" (en) и ":строка N" (ru)
                bool hasLineInfo = false;
                string compact = line;

                int idx = line.IndexOf(":line ", StringComparison.Ordinal);
                if (idx < 0)
                    idx = line.IndexOf(":строка ", StringComparison.Ordinal);

                if (idx >= 0)
                {
                    // Извлекаем имя файла и номер строки
                    int inIdx = line.LastIndexOf(" in ", StringComparison.Ordinal);
                    if (inIdx < 0)
                        inIdx = line.LastIndexOf(" в ", StringComparison.Ordinal);

                    string beforeLine = line.Substring(0, idx);
                    string numStr = line.Substring(idx + (line[idx + 1] == 'l' ? 6 : 8)).Trim();

                    string method = inIdx >= 0 ? beforeLine.Substring(0, inIdx).Trim() : beforeLine.Trim();

                    int fileStart = inIdx >= 0
                        ? inIdx + (line.Substring(inIdx).StartsWith(" in ") ? 4 : 3)
                        : 0;

                    string filePath = inIdx >= 0
                        ? line.Substring(fileStart, idx - fileStart).Trim()
                        : "";

                    string fileName = "";
                    try { fileName = Path.GetFileName(filePath); }
                    catch { fileName = filePath; }

                    if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(numStr))
                    {
                        compact = $"{method} at {fileName}:line {numStr}";
                        hasLineInfo = true;
                    }
                }

                sb.AppendLine("  " + compact);

                if (firstAny == null) firstAny = compact;

                if (line.StartsWith(userPrefix, StringComparison.Ordinal) ||
                    compact.StartsWith(userPrefix, StringComparison.Ordinal))
                {
                    if (firstUser == null) firstUser = compact;
                    if (hasLineInfo && firstUserWithLine == null)
                        firstUserWithLine = compact;
                }
            }

            fullStack = sb.ToString();
            return firstUserWithLine ?? firstUser ?? firstAny;
        }
        public void LogError(Exception ex)
        {
            if (ex == null) return;
            if (Interlocked.CompareExchange(ref _inLogError, 1, 0) != 0) return;

            try
            {
                string stack = ex.StackTrace;
                string place = ExtractFirstFrame(stack);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {place} | {ex.GetType().Name}: {ex.Message}";

                lock (_errorsLock)
                {
                    Errors.Add(line);
                    if (Errors.Count > MaxErrors)
                        Errors.RemoveRange(0, Errors.Count - MaxErrors);
                }

                // Если включён админ-режим — показываем окно.
                // _inLogError уже == 1, поэтому повторный LogError из ShowErrorDialog
                // не зациклится: CompareExchange вернёт 1 и просто выйдет.
                if (LookErrorsAdmin && ShouldShowDialog(line))
                {
                    bool disable = ShowErrorDialog(line);
                    if (disable)
                    {
                        LookErrorsAdmin = false;
                    }
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _inLogError, 0);
            }
        }
        
        private static string ExtractFirstFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "no stack";

            var lines = stack.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string firstUserWithLine = null;
            string firstUser = null;
            string firstAny = null;

            const string userPrefix = "Reinforcement.";

            foreach (var raw in lines)
            {
                string line = raw.Trim();

                // В русской локали префикс "в ", в английской — "at ".
                // Учитываем оба, иначе весь стек теряется.
                if (line.StartsWith("at "))
                    line = line.Substring(3);
                else if (line.StartsWith("в "))
                    line = line.Substring(2);
                else
                    continue;

                if (firstAny == null) firstAny = line;

                if (!line.StartsWith(userPrefix, StringComparison.Ordinal))
                    continue;

                bool hasLineInfo =
                    line.IndexOf(":line ", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf(" in ", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf(" в ", StringComparison.Ordinal) >= 0; // русский вариант

                if (hasLineInfo && firstUserWithLine == null)
                    firstUserWithLine = line;
                if (firstUser == null)
                    firstUser = line;
            }

            return firstUserWithLine ?? firstUser ?? firstAny ?? "no stack";
        }
        /// <summary>
        /// Возвращает первую строку стека в виде "Type.Method at File.cs:line N".
        /// Если стека нет — null.
        /// </summary>
        //private static string ExtractFirstFrame(string stack)
        //{
        //    if (string.IsNullOrEmpty(stack)) return "no stack";
        //    int nl = stack.IndexOf('\n');
        //    string first = (nl >= 0 ? stack.Substring(0, nl) : stack).Trim();
        //    // Убираем "at " в начале — так читабельнее
        //    if (first.StartsWith("at ")) first = first.Substring(3);
        //    return first;
        //}
        private static string GetCommandName(Exception ex)
        {
            if (ex == null) return null;

            var st = new StackTrace(ex, fNeedFileInfo: false);
            var frames = st.GetFrames();
            if (frames == null) return null;

            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var type = method?.DeclaringType;
                if (type == null) continue;

                if (typeof(Autodesk.Revit.UI.IExternalCommand).IsAssignableFrom(type))
                    return type.Name;

                if (typeof(Autodesk.Revit.UI.IExternalApplication).IsAssignableFrom(type))
                    return type.Name + " (app)";
            }
            return null;
        }
        public static string errorLogPath =>
        Path.Combine(folderErrors, $"{Environment.UserName}_{Environment.MachineName}.errors.log");
        
        private const int MaxLogLines = 200;
        /// <summary>
        /// Дописывает Errors в .errors.log с fallback на %TEMP%, если основная папка недоступна.
        /// После успешной записи очищает Errors.
        /// </summary>
        private void FlushErrorsToLog()
        {
            List<string> toWrite;

            lock (_errorsLock)
            {
                if (Errors.Count == 0) return;
                toWrite = new List<string>(Errors);
            }

            bool written = TryAppendLines(errorLogPath, toWrite);

            if (!written)
            {
                // Fallback — только если основная запись не удалась
                written = TryAppendLines(
                    Path.Combine(Path.GetTempPath(), "LookUsers.errors.log"),
                    toWrite);
            }

            if (written)
            {
                // Очищаем только если действительно записали куда-то
                lock (_errorsLock)
                {
                    Errors.Clear();
                }
            }
            // если оба провалились — оставляем Errors, попробуем при следующем WriteFile
        }
        /// <summary>
        /// Дописывает строки в файл, обрезая его до последних MaxLogLines.
        /// Возвращает true при успехе.
        /// </summary>
        private static bool TryAppendLines(string path, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return true;

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Просто дописываем — быстро, не читаем файл целиком
                File.AppendAllLines(path, lines);

                // Обрезаем только если файл стал заметно большим.
                // Проверка через размер файла — одна операция, дешёвая.
                var fi = new FileInfo(path);
                if (fi.Length > 200_000)   // ~200 КБ — это примерно 2000+ строк
                {
                    TrimLogFile(path);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TrimLogFile(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length > MaxLogLines)
                {
                    var last = lines.Skip(lines.Length - MaxLogLines).ToArray();
                    File.WriteAllLines(path, last);
                }
            }
            catch { }
        }
    }
}
