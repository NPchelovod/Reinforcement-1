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
        /// <summary>
        /// Возвращает первую строку стека в виде "Type.Method at File.cs:line N".
        /// Если стека нет — null.
        /// </summary>
        private static string ExtractFirstFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "no stack";
            int nl = stack.IndexOf('\n');
            string first = (nl >= 0 ? stack.Substring(0, nl) : stack).Trim();
            // Убираем "at " в начале — так читабельнее
            if (first.StartsWith("at ")) first = first.Substring(3);
            return first;
        }
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
