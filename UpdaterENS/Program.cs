using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;

namespace UpdaterENS
{
    class Program
    {
        // Флаг, определяющий, нужно ли создавать резервные копии файлов.
        public static bool rezervCopy = false;

        // Максимальное число строк, которое хранится в лог-файле.
        private const int MaxLogLines = 100;


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string lpFileName);

        // Аргументы: pid, sourceDir, targetDir, backupDir, [logFile]
        static void Main(string[] args)
        {
            // Single-instance lock.
            // Имя без префикса — мьютекс в рамках текущей сессии пользователя,
            // чего достаточно для нескольких окон Revit под одним логином.
            const string MutexName = "UpdaterENS_SingleInstance_7E1B4F6A";

            bool createdNew;
            using (var mutex = new Mutex(initiallyOwned: false, name: MutexName, createdNew: out createdNew))
            {
                bool acquired;
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
                }
                catch (AbandonedMutexException)
                {
                    // Предыдущий экземпляр упал, не освободив мьютекс.
                    acquired = true;
                }

                if (!acquired)
                {
                    Console.WriteLine("UpdaterENS is already running. Exiting.");
                    return;
                }

                try
                {
                    Run(args);
                }
                finally
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
            }
        }

        private static void Run(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage (new): UpdaterENS.exe <pid> <mainSource> <adminSource> " +
                                  "<targetDir> <backupDir> <logFile> [isAuthor]");
                Console.WriteLine("Usage (old): UpdaterENS.exe <pid> <sourceDir> <targetDir> " +
                                  "<backupDir> [logFile]");
                return;
            }

            if (!int.TryParse(args[0], out int pid))
            {
                Console.WriteLine("Invalid PID");
                return;
            }

            string mainSourceDir;
            string adminSourceDir;
            string targetDir;
            string backupDir;
            string logFile;
            bool isAuthor;
            bool legacyFormat;

            if (args.Length >= 6)
            {
                // --- Новый формат ---
                legacyFormat = false;
                mainSourceDir = args[1];
                adminSourceDir = args[2];
                targetDir = args[3];
                backupDir = args[4];
                logFile = args[5];
                isAuthor = args.Length > 6 && args[6] == "1";
            }
            else
            {
                // --- Legacy ---
                // Старый плагин уже сам выбрал папку и передал её как args[1].
                // admin-папку и авторство он не знает — считаем, что не автор.
                legacyFormat = true;
                mainSourceDir = args[1];
                adminSourceDir = null;
                targetDir = args[2];
                backupDir = args[3];
                logFile = args.Length > 4 ? args[4] : null;
                isAuthor = false;
            }

            const string certSourceDir = @"Y:\Revit\_ЕС BIM_Плагин\3_Установка\Certs";
            EnsureCertificate(certSourceDir, logFile);

            TrimLogIfNeeded(logFile);

            Log(logFile, $"Update started at {DateTime.Now}. Waiting for process {pid} to exit...");
            Log(logFile, $"Format: {(legacyFormat ? "LEGACY" : "NEW")}");
            Log(logFile, $"Mode  : {(isAuthor ? "AUTHOR" : "USER")}");
            Log(logFile, $"Main source : {mainSourceDir}");
            if (!string.IsNullOrEmpty(adminSourceDir))
                Log(logFile, $"Admin source: {adminSourceDir}");
            Log(logFile, $"Target      : {targetDir}");
            Log(logFile, $"Backup      : {backupDir} (enabled={rezervCopy})");

            try
            {
                WaitForProcessExit(pid, logFile);
                Thread.Sleep(3000);

                if (PathsEqual(mainSourceDir, targetDir) ||
                    (!string.IsNullOrEmpty(adminSourceDir) && PathsEqual(adminSourceDir, targetDir)))
                {
                    Log(logFile, "Source equals target. Aborting.");
                    return;
                }

                int[] delays = new[]
                {
            10000, 10000, 10000, 10000, 10000,
            30000, 30000, 30000, 30000, 30000,
            30000, 30000, 30000, 30000, 30000
        };

                bool allFree = WaitForOtherRevitProcesses(pid, logFile, delays);
                if (!allFree)
                {
                    Log(logFile, "Other Revit processes are still running. " +
                                 "Update will be retried on next Revit launch.");
                    return;
                }

                // «Последний момент»: если за время работы Revit автор что-то
                // дописал в admin — здесь это увидим и учтём.
                // В legacy-режиме adminSourceDir == null → вернётся mainSourceDir.
                string sourceDir = ChoosePluginSource(mainSourceDir, adminSourceDir, isAuthor);

                if (sourceDir == null)
                {
                    Log(logFile, "No valid plugin source directory. Nothing to update.");
                    return;
                }

                Log(logFile, $"Resolved plugin source: {sourceDir}");

                int copied = CopyFilesAtomically(sourceDir, targetDir, backupDir, logFile);
                Log(logFile, $"Update completed. Files updated: {copied}");
            }
            catch (Exception ex)
            {
                Log(logFile, $"Fatal error: {ex.Message}");
            }
        }

        private static string ChoosePluginSource(string mainDir, string adminDir, bool isAuthor)
        {
            bool mainOk = !string.IsNullOrWhiteSpace(mainDir) && Directory.Exists(mainDir);
            bool adminOk = isAuthor
                           && !string.IsNullOrWhiteSpace(adminDir)
                           && Directory.Exists(adminDir);

            if (!mainOk && !adminOk) return null;
            if (!adminOk) return mainDir;
            if (!mainOk) return adminDir;

            DateTime mainTime = GetDateTimeFolder(mainDir);
            DateTime adminTime = GetDateTimeFolder(adminDir);

            return adminTime > mainTime ? adminDir : mainDir;
        }
        // -------------------------------------------------------------------
        // Ожидания
        // -------------------------------------------------------------------

        static void WaitForProcessExit(int pid, string logFile)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    Log(logFile, $"Waiting for process {pid} to exit...");
                    p.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                Log(logFile, $"Process {pid} not found. Assuming it's already closed.");
            }
        }

        /// <summary>
        /// Ждёт закрытия чужих процессов Revit. Интервалы между проверками
        /// задаются массивом pollDelaysMs.
        /// Возвращает true, если все Revit закрылись; false — если попытки
        /// исчерпаны, а процессы ещё живы.
        /// </summary>
        static bool WaitForOtherRevitProcesses(int ownPid, string logFile, int[] pollDelaysMs)
        {
            bool logged = false;

            for (int i = 0; i <= pollDelaysMs.Length; i++)
            {
                Process[] others;
                try
                {
                    others = Process.GetProcessesByName("Revit")
                                    .Where(p => p.Id != ownPid)
                                    .ToArray();
                }
                catch (Exception ex)
                {
                    Log(logFile, $"Enum Revit processes failed: {ex.Message}");
                    return true; // не смогли проверить — считаем, что свободно
                }

                if (others.Length == 0)
                {
                    if (logged)
                        Log(logFile, "All Revit processes have exited.");
                    return true;
                }

                if (!logged)
                {
                    Log(logFile, $"Waiting for {others.Length} other Revit process(es): " +
                                 string.Join(", ", others.Select(p => p.Id)));
                    logged = true;
                }

                foreach (var p in others) p.Dispose();

                if (i == pollDelaysMs.Length)
                    break; // попытки исчерпаны

                int delayMs = pollDelaysMs[i];
                Log(logFile, $"Attempt {i + 1}/{pollDelaysMs.Length}: " +
                             $"other Revit still running, retry in {delayMs / 1000} s...");
                Thread.Sleep(delayMs);
            }

            return false;
        }

        // -------------------------------------------------------------------
        // Копирование
        // -------------------------------------------------------------------

        static int CopyFilesAtomically(string sourceDir, string targetDir, string backupDir, string logFile = null)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            sourceDir = Path.GetFullPath(sourceDir);
            targetDir = Path.GetFullPath(targetDir);
            backupDir = Path.GetFullPath(backupDir);

            CleanupOldTempFolders(targetDir, logFile);

            // 1. Триггер обновления — только DLL.
            //    Если хоть одна .dll в source новее (или её нет в target) — обновляем ВСЁ.
            //    Если все .dll одинаковые/старые — не делаем ничего.
            bool shouldUpdate = false;
            string triggerReason = null;

            foreach (var sourceDll in Directory.GetFiles(sourceDir, "*.dll", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(sourceDir, sourceDll);
                string targetDll = Path.Combine(targetDir, relative);

                if (!File.Exists(targetDll))
                {
                    shouldUpdate = true;
                    triggerReason = $"new dll: {relative}";
                    break;
                }

                if (File.GetLastWriteTimeUtc(sourceDll) > File.GetLastWriteTimeUtc(targetDll))
                {
                    shouldUpdate = true;
                    triggerReason = $"newer dll: {relative}";
                    break;
                }
            }

            if (!shouldUpdate)
            {
                Log(logFile, "No dll is newer than in target. Nothing to update.");
                return 0;
            }
            // Сразу после проверки shouldUpdate = true, до стейджинга.
            // Снимаем зону с источника, если есть права. Не критично, если не получится —
            // staging всё равно вычистится по ходу.
            try
            {
                int srcCleaned = 0, srcFailed = 0;
                foreach (var f in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    if (RemoveZoneIdentifierVerified(f, null)) srcCleaned++;
                    else srcFailed++;
                }
                Log(logFile, $"Source zone cleanup: cleaned={srcCleaned}, failed={srcFailed}");
            }
            catch (Exception ex)
            {
                Log(logFile, $"Source zone cleanup skipped: {ex.Message}");
            }


            Log(logFile, $"Update triggered by {triggerReason}. Copying all files.");

            // 2. Раз обновляемся — берём ВСЕ файлы, но в список замены попадут
            //    только те, у которых дата модификации отличается от целевой
            //    (в любую сторону — свежее или старее), либо которых нет в target.
            var filesToUpdate = new List<string>();

            foreach (var sourceFilePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(sourceDir, sourceFilePath);
                string targetFilePath = Path.Combine(targetDir, relative);

                if (!File.Exists(targetFilePath))
                {
                    filesToUpdate.Add(relative);
                    continue;
                }

                DateTime srcTime = File.GetLastWriteTimeUtc(sourceFilePath);
                DateTime dstTime = File.GetLastWriteTimeUtc(targetFilePath);

                // Даты совпадают — считаем файл идентичным, пропускаем.
                if (srcTime == dstTime)
                    continue;

                filesToUpdate.Add(relative);
            }

            if (filesToUpdate.Count == 0)
            {
                Log(logFile, "Trigger fired, but all files have matching timestamps. Nothing to copy.");
                return 0;
            }

            Log(logFile, $"Files to update: {filesToUpdate.Count}");

            // 3. Staging внутри целевого диска — чтобы File.Replace был атомарным.
            string tempSubdir = Path.Combine(targetDir, $".update_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempSubdir);

            foreach (var relative in filesToUpdate)
            {
                string sourceFilePath = Path.Combine(sourceDir, relative);
                string tempFilePath = Path.Combine(tempSubdir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));
                File.Copy(sourceFilePath, tempFilePath, overwrite: true);
                // Ключевая точка: снимаем зону ЗДЕСЬ, а не после замены.
                // После этого любые File.Replace/File.Move понесут в target
                // уже «чистый» файл.
                RemoveZoneIdentifierVerified(tempFilePath, logFile);
                Log(logFile, $"Staged: {relative}");
            }
            // 4. Замена. Сначала DLL — они критичны.
            //    Если хоть одна DLL не заменилась, остальные файлы не трогаем:
            //    лучше остаться на старой версии целиком, чем получить рассинхрон
            //    "старая dll + новый xml/config".
            var dllFiles = new List<string>();
            var otherFiles = new List<string>();

            foreach (var relative in filesToUpdate)
            {
                if (relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    dllFiles.Add(relative);
                else
                    otherFiles.Add(relative);
            }

            // Главная сборка плагина — первой. Остальные — по алфавиту,
            // чтобы порядок замены был стабильным и предсказуемым.
            const string mainDllName = "Reinforcement.dll";
            dllFiles = dllFiles
            .OrderBy(f => string.Equals(Path.GetFileName(f), mainDllName,
                                        StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

            otherFiles = otherFiles
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
            // 4. Замена.
            int updated = 0;
            int dllFailed = 0;
            string failedDll = null;
            // 4a. DLL
            foreach (var relative in dllFiles)
            {
                string tempFilePath = Path.Combine(tempSubdir, relative);
                string targetFilePath = Path.Combine(targetDir, relative);
                string backupFilePath = Path.Combine(backupDir, relative);

                if (TrySwapFile(tempFilePath, targetFilePath, backupFilePath, relative, logFile))
                {
                    updated++;
                }
                else
                {
                    dllFailed++; //хотя бы не расширяем «полу-состояние» дальше.
                    failedDll = relative;
                    Log(logFile,
                        $"Stopping dll replacement: '{relative}' failed. " +
                        $"Remaining {dllFiles.Count - updated - dllFailed} dll(s) will not be touched.");
                    
                    break;
                }
            }
            // 4b. Остальные файлы — только если все DLL заменились успешно.
            if (dllFailed == 0)
            {
                foreach (var relative in otherFiles)
                {
                    string tempFilePath = Path.Combine(tempSubdir, relative);
                    string targetFilePath = Path.Combine(targetDir, relative);
                    string backupFilePath = Path.Combine(backupDir, relative);

                    if (TrySwapFile(tempFilePath, targetFilePath, backupFilePath, relative, logFile))
                        updated++;
                }
            }
            else
            {
                Log(logFile,
               $"Skipped {otherFiles.Count} non-dll file(s): " +
               $"dll '{failedDll}' could not be replaced. " +
               "Plugin left on the previous version to avoid version mismatch.");
            }
            // 4c. Финальная страховка по всему target.
            int zoneFailed = 0;
            try
            {
                foreach (var file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    // Не трогаем staging-подпапку — её сейчас удалим.
                    if (file.StartsWith(tempSubdir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!RemoveZoneIdentifierVerified(file, logFile))
                        zoneFailed++;
                }

                Log(logFile, zoneFailed == 0
                    ? "Zone.Identifier stripped from target folder."
                    : $"Zone.Identifier cleanup: {zoneFailed} file(s) still blocked!");
            }
            catch (Exception ex)
            {
                Log(logFile, $"Zone.Identifier cleanup error: {ex.Message}");
            }
            // 5. Уборка.
            try
            {
                if (Directory.Exists(tempSubdir))
                {
                    Directory.Delete(tempSubdir, recursive: true);
                    Log(logFile, "Temporary folder deleted.");
                }
            }
            catch (Exception ex)
            {
                Log(logFile, $"Failed to delete temp folder: {ex.Message} " +
                             "Left for manual cleanup.");
            }

            return updated;
        }

        static bool TrySwapFile(string tempFilePath, string targetFilePath,
                                string backupFilePath, string relativePath, string logFile)
        {
            const int maxAttempts = 10;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                attempt++;

                if (!File.Exists(targetFilePath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                        File.Move(tempFilePath, targetFilePath);
                        RemoveZoneIdentifierVerified(targetFilePath, logFile); ;
                        Log(logFile, $"Added: {relativePath}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log(logFile, $"[{relativePath}] move (new file) attempt {attempt}: {ex.Message}");
                        SleepBackoff(attempt, logFile);
                        continue;
                    }
                }

                if (!IsFileWritable(targetFilePath))
                {
                    Log(logFile, $"[{relativePath}] still locked by another process (attempt {attempt}).");
                    SleepBackoff(attempt, logFile);
                    continue;
                }

                try
                {
                    string backupForReplace = null;
                    if (rezervCopy)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath));
                        if (File.Exists(backupFilePath))
                            File.Delete(backupFilePath);
                        backupForReplace = backupFilePath;
                    }

                    File.Replace(tempFilePath, targetFilePath,
                                 destinationBackupFileName: backupForReplace,
                                 ignoreMetadataErrors: true);
                    RemoveZoneIdentifierVerified(targetFilePath, logFile); ;   // ← добавить
                    Log(logFile, $"Replaced: {relativePath} (attempt {attempt})");
                    return true;
                }
                catch (Exception exReplace)
                {
                    Log(logFile, $"[{relativePath}] File.Replace attempt {attempt} failed: {exReplace.Message}");
                }

                string sideBak = targetFilePath + ".old_" + Guid.NewGuid().ToString("N");
                bool originalRenamed = false;
                try
                {
                    File.Move(targetFilePath, sideBak);
                    originalRenamed = true;

                    File.Move(tempFilePath, targetFilePath);
                    RemoveZoneIdentifierVerified(targetFilePath, logFile); ;   // ← добавить
                    try { File.Delete(sideBak); } catch { }

                    Log(logFile, $"Renamed-swap: {relativePath} (attempt {attempt})");
                    return true;
                }
                catch (Exception exMove)
                {
                    Log(logFile, $"[{relativePath}] rename-swap attempt {attempt} failed: {exMove.Message}");

                    try
                    {
                        if (originalRenamed && !File.Exists(targetFilePath) && File.Exists(sideBak))
                        {
                            File.Move(sideBak, targetFilePath);
                            Log(logFile, $"[{relativePath}] original restored.");
                        }
                        else if (File.Exists(sideBak))
                        {
                            File.Delete(sideBak);
                        }
                    }
                    catch (Exception exRestore)
                    {
                        Log(logFile, $"[{relativePath}] !!! FAILED TO RESTORE: {exRestore.Message}");
                    }

                    SleepBackoff(attempt, logFile);
                }
            }

            Log(logFile, $"Giving up on '{relativePath}' after {maxAttempts} attempts. " +
                         "Original file kept in place.");
            return false;
        }
        /// <summary>
        /// Удаляет альтернативный поток Zone.Identifier у файла.
        /// В .NET Framework File.Delete НЕ поддерживает ADS-пути ("file:stream")
        /// — он бросает NotSupportedException. Поэтому используем Win32 DeleteFile,
        /// который с этим форматом работает.
        /// Возвращает true, если поток отсутствует или успешно удалён.
        /// </summary>
        static bool RemoveZoneIdentifierVerified(string filePath, string logFile = null)
        {
            if (string.IsNullOrEmpty(filePath))
                return true;

            string zonePath = filePath + ":Zone.Identifier";

            // ERROR_FILE_NOT_FOUND = 2, ERROR_PATH_NOT_FOUND = 3 — потока нет, это норма.
            const int ERROR_FILE_NOT_FOUND = 2;
            const int ERROR_PATH_NOT_FOUND = 3;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (DeleteFile(zonePath))
                    return true; // поток был и удалён

                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_FILE_NOT_FOUND || err == ERROR_PATH_NOT_FOUND)
                    return true; // потока не было — тоже успех

                if (attempt == 3)
                {
                    try
                    {
                        Log(logFile,
                            $"RemoveZoneIdentifier FAILED for '{filePath}': Win32 error {err}");
                    }
                    catch { }
                    return false;
                }

                Thread.Sleep(150);
            }
            return false;
        }

        static void RemoveZoneIdentifier(string filePath)
        {
            RemoveZoneIdentifierVerified(filePath, null);
        }
        static bool IsFileWritable(string path)
        {
            if (!File.Exists(path)) return true;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open,
                                               FileAccess.ReadWrite, FileShare.None))
                {
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        static void SleepBackoff(int attempt, string logFile)
        {
            int delayMs = Math.Min(3000 * attempt, 15000);
            Log(logFile, $"Retrying in {delayMs / 1000} s...");
            Thread.Sleep(delayMs);
        }

        // -------------------------------------------------------------------
        // Служебное
        // -------------------------------------------------------------------

        static void CleanupOldTempFolders(string targetDir, string logFile = null)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(targetDir, ".update_tmp_*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        Log(logFile, $"Deleted old temp folder: {dir}");
                    }
                    catch (Exception ex)
                    {
                        Log(logFile, $"Failed to delete '{dir}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(logFile, $"Error scanning for old temp folders: {ex.Message}");
            }
        }

        static string GetRelativePath(string basePath, string fullPath)
        {
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Path '{fullPath}' is not inside '{basePath}'.");

            return fullPath.Substring(basePath.Length);
        }

        static bool PathsEqual(string p1, string p2)
        {
            string f1 = Path.GetFullPath(p1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string f2 = Path.GetFullPath(p2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(f1, f2, StringComparison.OrdinalIgnoreCase);
        }

        static void Log(string logFile, string message)
        {
            Console.WriteLine(message);
            if (!string.IsNullOrEmpty(logFile))
            {
                try
                {
                    File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
                }
                catch { }
            }
        }

        static void TrimLogIfNeeded(string logFile)
        {
            if (string.IsNullOrEmpty(logFile) || !File.Exists(logFile))
                return;

            try
            {
                var lines = File.ReadAllLines(logFile);
                if (lines.Length > MaxLogLines)
                {
                    var lastLines = lines.Skip(lines.Length - MaxLogLines);
                    File.WriteAllLines(logFile, lastLines);
                    Console.WriteLine($"Log trimmed to last {MaxLogLines} lines.");
                }
            }
            catch { }
        }

        // -------------------------------------------------------------------
        // Сертификат
        // -------------------------------------------------------------------

        private static bool IsCertTrusted(string subjectCn)
        {
            try
            {
                using (var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    foreach (var cert in store.Certificates)
                    {
                        if (cert.Subject.IndexOf($"CN={subjectCn}", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool CopyCertFolder(string sourceDir, string targetDir, string logFile)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    Log(logFile, $"Cert source dir not found: {sourceDir}");
                    return false;
                }

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    Log(logFile, $"Created: {targetDir}");
                }

                foreach (var srcFile in Directory.GetFiles(sourceDir))
                {
                    string fileName = Path.GetFileName(srcFile);
                    string destFile = Path.Combine(targetDir, fileName);

                    if (fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                    {
                        Log(logFile, $"Skipped (private key): {fileName}");
                        continue;
                    }

                    if (!File.Exists(destFile))
                    {
                        File.Copy(srcFile, destFile, overwrite: false);
                        Log(logFile, $"Copied: {fileName}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log(logFile, $"Copy error: {ex.Message}");
                return false;
            }
        }

        private static int RunCertutil(string arguments, string logFile)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "certutil.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(866);
                psi.StandardErrorEncoding = System.Text.Encoding.GetEncoding(866);

                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();

                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(); } catch { }
                        Log(logFile, $"certutil timed out: {arguments}");
                        return -1;
                    }

                    Log(logFile, $"certutil (exit={p.ExitCode}): {arguments}");
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Log(logFile, $"  out: {stdout.Trim()}");
                    if (!string.IsNullOrWhiteSpace(stderr))
                        Log(logFile, $"  err: {stderr.Trim()}");

                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Log(logFile, $"certutil launch error: {ex.Message}");
                return -1;
            }
        }

        private static void EnsureCertificate(string certSourceDir, string logFile)
        {
            const string targetDir = @"C:\Certs";
            const string certSubjectCn = "MyRevitPlugin";
            const string cerFileName = "MyRevitPlugin.cer";

            if (IsCertTrusted(certSubjectCn))
            {
                Log(logFile, $"Certificate '{certSubjectCn}' already trusted. Skipping.");
                return;
            }

            Log(logFile, $"Certificate '{certSubjectCn}' not found. Installing...");

            if (!CopyCertFolder(certSourceDir, targetDir, logFile))
            {
                Log(logFile, "Certificate installation aborted: copy failed.");
                return;
            }

            string cerPath = Path.Combine(targetDir, cerFileName);
            if (!File.Exists(cerPath))
            {
                Log(logFile, $"CER file not found: {cerPath}");
                return;
            }

            int rc1 = RunCertutil($"-user -addstore -f \"Root\" \"{cerPath}\"", logFile);
            int rc2 = RunCertutil($"-user -addstore -f \"TrustedPublisher\" \"{cerPath}\"", logFile);

            if (rc1 != 0 || rc2 != 0)
            {
                Log(logFile, "WARNING: certutil reported errors. Revit may still prompt.");
            }

            if (IsCertTrusted(certSubjectCn))
                Log(logFile, "Certificate installed and verified.");
            else
                Log(logFile, "Certificate installation could not be verified.");
        }

        /// <summary>
        /// Возвращает максимальную дату модификации файлов .dll или .exe в директории.
        /// Если файлов нет или директория не существует, возвращает DateTime.MinValue.
        /// </summary>
        private static DateTime GetDateTimeFolder(string sourceDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                return DateTime.MinValue;

            var max = new[] { "*.dll", "*.exe" }
                .SelectMany(mask => Directory.EnumerateFiles(sourceDir, mask, SearchOption.AllDirectories))
                .Select(File.GetLastWriteTime)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            return max;
        }

        
    }
}