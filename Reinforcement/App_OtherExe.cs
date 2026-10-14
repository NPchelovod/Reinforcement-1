using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement
{
    public static class App_OtherExe
    {
        //запуск иных exe которые нам может быть надо

        public static void Execute()
        {
            TimeStat();
        }
        // --- Контроль рабочего времени ---
        public static string userTimeSourceDir =
            @"Y:\ГОСТЕВЫЕ\КОНСТРУКТОРЫ\Ерухаева Ирина\Индивидуальный табель\контроль рабочего времени";

        public static string userTimeProcessPrefix = "WpfControlUserTime";
        public static void TimeStat()
        {
            if (App.EUserOtdel != EUserOtdel.KR)
            {
                //только для КР акция
                return;
            }
            try
            {
                // Сеть может быть недоступна — тихо выходим.
                if (string.IsNullOrWhiteSpace(userTimeSourceDir) ||
                    !Directory.Exists(userTimeSourceDir))
                    return;

                // Все exe по маске WpfControlUserTime*.exe, отсортированы по дате (свежие сверху).
                var files = Directory
                    .EnumerateFiles(userTimeSourceDir,
                                    userTimeProcessPrefix + "*.exe",
                                    SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .Where(fi => fi.Exists)
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .ToList();

                if (files.Count == 0) return;

                // Уже что-то из этого семейства запущено? Ничего не делаем.
                var namesWithoutExt = files
                    .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var name in namesWithoutExt)
                {
                    Process[] procs = null;
                    try { procs = Process.GetProcessesByName(name); }
                    catch { }
                    if (procs != null && procs.Length > 0)
                    {
                        foreach (var p in procs) p.Dispose();
                        return;
                    }
                }

                FileInfo newest = files[0];

                string tempDir = Path.Combine(Path.GetTempPath(), "ENS_UserTime");
                Directory.CreateDirectory(tempDir);
                string targetExe = Path.Combine(tempDir, newest.Name);

                try
                {
                    File.Copy(newest.FullName, targetExe, overwrite: true);
                    // File.Copy переносит ADS из сетевой шары — снимаем метку,
                    // иначе пользователь увидит «Этот файл получен из другой зоны».
                    App_Apdater_1.RemoveZoneIdentifier(targetExe);
                }
                catch
                {
                    // Не удалось скопировать (нет доступа / файл занят) —
                    // пробуем запустить прямо из шары.
                    targetExe = newest.FullName;
                }

                if (!File.Exists(targetExe)) return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = Path.GetDirectoryName(targetExe),
                    UseShellExecute = true,     // чтобы работал UAC/шелл-ассоциации, если нужно
                    WindowStyle = ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex)
            {
                try { App_Apdater_1.AppErrors?.LogError(ex); } catch { }
            }

        }
        /// <summary>
        /// true, если запущен хотя бы один процесс, имя которого начинается с префикса.
        /// Идём от списка процессов, а не от файлов на шаре: так мы поймаем и те
        /// экземпляры, чьи exe-файлы уже удалены / недоступны / переименованы.
        /// </summary>
        private static bool IsUserTimeControlRunning()
        {
            Process[] all = null;
            try { all = Process.GetProcesses(); }
            catch { return false; }

            try
            {
                foreach (var p in all)
                {
                    if (p == null) continue;
                    try
                    {
                        if (p.ProcessName.StartsWith(userTimeProcessPrefix,
                                                    StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch
                    {
                        // У защищённых системных процессов доступ к ProcessName может
                        // бросить исключение — просто пропускаем такой процесс.
                    }
                }
                return false;
            }
            finally
            {
                foreach (var p in all)
                {
                    try { p?.Dispose(); } catch { }
                }
            }
        }
    }
}
