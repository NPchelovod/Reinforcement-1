using Reinforcement;
using System;
using System.IO;
using System.Text;
using System.Threading;

public static class ErrorAnalyse
{
    private static readonly object _lock = new object();
    private static readonly StringBuilder _errors = new StringBuilder();
    private static int _inLogError;

    public static void Error(Exception ex = null, string message = "", string cod = "")
    {
        if (ex != null)
            AppErrors.LogError(ex);

        if (string.IsNullOrEmpty(message)) return;

        // Потокобезопасное добавление без потерь
        lock (_lock)
        {
            _errors.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}" +
                               (string.IsNullOrEmpty(cod) ? "" : $" (code: {cod})"));
        }
    }

    public static void GetMessageErrors(string filePath = null)
    {
        string snapshot;
        lock (_lock)
        {
            if (_errors.Length == 0) return;
            snapshot = _errors.ToString();
            _errors.Clear();
        }

        if (Interlocked.CompareExchange(ref _inLogError, 1, 0) != 0) return;
        try
        {
            string path = filePath ?? Path.Combine(
                Path.GetTempPath(),
                "Reinforcement_Errors.log");

            string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{snapshot}\n{new string('-', 60)}\n";
            File.AppendAllText(path, text, Encoding.UTF8);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true   // ← обязательно для открытия ассоциированной программой
            });
        }
        catch
        {
            // Здесь нельзя вызывать Error — уйдём в рекурсию.
            // Молча игнорируем: запись в лог-файл не должна ломать приложение.
        }
        finally
        {
            Interlocked.Exchange(ref _inLogError, 0);
        }
    }
}