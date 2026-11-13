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
            return;
            TimeStat();
        }

        public static void TimeStat()
        {

            if (App.EUserOtdel != EUserOtdel.KR)
            {
                //только для КР акция
                return;
            }
            try
            {
                string path = App_Folders.UserTimeSourceDir;
                string tempDir = Path.Combine(Path.GetTempPath(), "ENS_UserTime");

                // Кандидаты: сначала свежие exe с шары (если она доступна),
                // иначе — ранее скопированный в темп плагин из прошлых сессий.
                var files = new List<FileInfo>();

                // Сеть может быть недоступна — тихо пропускаем шару.
                if (!string.IsNullOrWhiteSpace(path) &&
                    FastDirectoryChecker.DirectoryExistsFast(path))
                {
                    try
                    {
                        files = Directory
                            .EnumerateFiles(path,
                                            App_Folders.UserTimeProcessPrefix + "*.exe",
                                            SearchOption.TopDirectoryOnly)
                            .Select(p => new FileInfo(p))
                            .Where(fi => fi.Exists)
                            .OrderByDescending(fi => fi.LastWriteTimeUtc)
                            .ToList();
                    }
                    catch { }
                }

                // Если на шаре ничего нет — смотрим в темп.
                if (files.Count == 0 && FastDirectoryChecker.DirectoryExistsFast(tempDir))
                {
                    try
                    {
                        files = Directory
                            .EnumerateFiles(tempDir,
                                            App_Folders.UserTimeProcessPrefix + "*.exe",
                                            SearchOption.TopDirectoryOnly)
                            .Select(p => new FileInfo(p))
                            .Where(fi => fi.Exists)
                            .OrderByDescending(fi => fi.LastWriteTimeUtc)
                            .ToList();
                    }
                    catch { }
                }

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

                Directory.CreateDirectory(tempDir);
                string targetExe = Path.Combine(tempDir, newest.Name);

                // Если файл уже лежит в темпе (взят оттуда) — копирование не нужно.
                if (!string.Equals(newest.FullName, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Copy(newest.FullName, targetExe, overwrite: true);
                        // File.Copy переносит ADS из сетевой шары — снимаем метку,
                        // иначе пользователь увидит «Этот файл получен из другой зоны».
                        App_Folders.RemoveZoneIdentifier(targetExe);
                    }
                    catch
                    {
                        // Не удалось скопировать (нет доступа / файл занят) —
                        // пробуем запустить прямо из шары.
                        targetExe = newest.FullName;
                    }
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
                try { AppErrors.LogError(ex); } catch { }
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
                        if (p.ProcessName.StartsWith(App_Folders.UserTimeProcessPrefix,
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
