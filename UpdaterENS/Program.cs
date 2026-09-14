
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace UpdaterENS
{
    class Program
    {
        // Флаг, определяющий, нужно ли создавать резервные копии файлов.
        public static bool rezervCopy = false;
        // Максимальное число строк, которое хранится в лог-файле
        private const int MaxLogLines = 100;

        // Аргументы: pid, sourceDir, targetDir, [backupDir], [logFile], [--backup]
        static void Main(string[] args)
        {
            // Single-instance lock.
            // Имя без префикса — мьютекс в рамках текущей сессии пользователя,
            // чего достаточно для нескольких окон Revit под одним логином.
            const string MutexName = "UpdaterENS_SingleInstance_7E1B4F6A";

            bool createdNew;
            using (var mutex = new Mutex(initiallyOwned: false, name: MutexName, createdNew: out createdNew))
            {
                // Если мьютекс уже кем-то захвачен — выходим без ожидания.
                bool acquired;
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
                }
                catch (AbandonedMutexException)
                {
                    // Предыдущий экземпляр упал, не освободив мьютекс.
                    // Считаем, что захватили его мы.
                    acquired = true;
                }

                if (!acquired)
                {
                    Console.WriteLine("UpdaterENS is already running. Exiting.");
                    return;
                }

                try
                {
                    // Вся текущая логика Main (разбор args, ожидания, CopyFilesAtomically и т.д.)
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
            // Проверяем наличие флага --backup
            //rezervCopy = args.Contains("--backup", StringComparer.OrdinalIgnoreCase);

            if (args.Length < 4)
            {
                Console.WriteLine("Usage: UpdaterENS.exe <pid> <sourceDir> <targetDir> <backupDir> [logFile] [--backup]");
                return;
            }

            if (!int.TryParse(args[0], out int pid))
            {
                Console.WriteLine("Invalid PID");

                return;
            }

            string sourceDir = args[1];
            string targetDir = args[2];
            string backupDir = args[3];
            // logFile — первый аргумент после 4-го, который не является флагом
            string logFile = args.Skip(4)
                                 .FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));


            // Один раз при старте — обрезаем старый лог, если он слишком большой
            TrimLogIfNeeded(logFile);

            Log(logFile, $"Update started at {DateTime.Now}. Waiting for process {pid} to exit...");
            Log(logFile, $"Backup enabled: {rezervCopy}");

            try
            {
                // 1. Ждём завершения процесса, который запустил нас.
                WaitForProcessExit(pid, logFile);

                // 2. Ждём ВСЕ остальные процессы Revit (они держат DLL).
                //    Если оставить только один PID — у пользователя может быть
                //    открыто несколько Revit, и Reinf.dll останется залочен.
                //WaitForAllRevitProcesses(pid, logFile);//опасно ведь revitaccelaration например не вырубляется

                Thread.Sleep(3000); // Revit дочищает handles после закрытия

                if (PathsEqual(sourceDir, targetDir))
                {
                    Log(logFile, "Source and target directories are the same. Aborting.");
                    return;
                }

                int copied = CopyFilesAtomically(sourceDir, targetDir, backupDir, logFile);
                Log(logFile, $"Update completed. Files updated: {copied}");
            }
            catch (Exception ex)
            {
                Log(logFile, $"Fatal error: {ex.Message}");
            }
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
        /// Ждём, пока закроются все процессы Revit, кроме нашего и кроме того,
        /// который нас запустил (он уже должен был закрыться).
        /// </summary>
        static void WaitForAllRevitProcesses(int ownPid, string logFile)
        {
            int waited = 0;
            const int pollMs = 2000;
            while (true)
            {
                var others = Process.GetProcessesByName("Revit")
                                    .Where(p => p.Id != ownPid)
                                    .ToArray();

                if (others.Length == 0)
                    return;

                if (waited == 0)
                    Log(logFile, $"Waiting for {others.Length} other Revit process(es) to exit...");

                foreach (var p in others)
                {
                    try { p.WaitForExit(5000); }
                    catch { /* процесс мог завершиться между вызовами */ }
                    finally { p.Dispose(); }
                }

                waited += pollMs;
                Thread.Sleep(pollMs);
            }
        }

        /// <summary>
        /// Копирует файлы с использованием временной подпапки.
        /// Если rezervCopy == true, используется File.Replace с созданием резервной копии.
        /// Если false, выполняется удаление целевого файла и перемещение нового.
        /// Возвращает количество обновлённых файлов.
        /// </summary>
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

            // 1. Что нужно обновить
            var filesToUpdate = new List<string>();
            foreach (var sourceFilePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = GetRelativePath(sourceDir, sourceFilePath);
                string targetFilePath = Path.Combine(targetDir, relative);

                if (!File.Exists(targetFilePath) ||
                    File.GetLastWriteTimeUtc(sourceFilePath) > File.GetLastWriteTimeUtc(targetFilePath))
                {
                    filesToUpdate.Add(relative);
                }
            }

            if (filesToUpdate.Count == 0)
            {
                Log(logFile, "No files need to be updated.");
                return 0;
            }

            // 2. Staging: копируем всё во временную подпапку ВНУТРИ целевого диска,
            //    чтобы File.Replace/File.Move были атомарными (один том).
            string tempSubdir = Path.Combine(targetDir, $".update_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempSubdir);

            foreach (var relative in filesToUpdate)
            {
                string sourceFilePath = Path.Combine(sourceDir, relative);
                string tempFilePath = Path.Combine(tempSubdir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));
                File.Copy(sourceFilePath, tempFilePath, overwrite: true);
                Log(logFile, $"Staged: {relative}");
            }

            // 3. Замена
            int updated = 0;
            foreach (var relative in filesToUpdate)
            {
                string tempFilePath = Path.Combine(tempSubdir, relative);
                string targetFilePath = Path.Combine(targetDir, relative);
                string backupFilePath = Path.Combine(backupDir, relative);

                if (TrySwapFile(tempFilePath, targetFilePath, backupFilePath, relative, logFile))
                    updated++;
            }

            // 4. Уборка
            try
            {
                if (Directory.Exists(tempSubdir) &&
                    !Directory.EnumerateFileSystemEntries(tempSubdir).Any())
                {
                    Directory.Delete(tempSubdir);
                    Log(logFile, "Temporary folder deleted.");
                }
                else
                {
                    Log(logFile, $"Temp folder '{tempSubdir}' left for manual cleanup.");
                }
            }
            catch (Exception ex)
            {
                Log(logFile, $"Failed to delete temp folder: {ex.Message}");
            }

            return updated;
        }
        /// <summary>
        /// Пытается заменить targetFilePath на tempFilePath.
        /// Гарантия: при любой ошибке оригинальный файл по targetFilePath остаётся рабочим.
        /// Возвращает true, если замена удалась.
        /// </summary>
        static bool TrySwapFile(string tempFilePath, string targetFilePath,
                                string backupFilePath, string relativePath, string logFile)
        {
            const int maxAttempts = 10; // больше попыток, т.к. чужие Revit'ы могут ещё дочищать handles
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                attempt++;

                // Если файла нет — просто переносим (нечего сохранять).
                if (!File.Exists(targetFilePath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                        File.Move(tempFilePath, targetFilePath);
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

                // Проверяем, не держит ли файл кто-то ещё.
                if (!IsFileWritable(targetFilePath))
                {
                    Log(logFile, $"[{relativePath}] still locked by another process (attempt {attempt}).");
                    SleepBackoff(attempt, logFile);
                    continue;
                }

                // --- Попытка №1: атомарный File.Replace ------------------
                // Если у тебя он раньше "не работал" — теперь мы точно знаем почему:
                // либо целевой файл был залочен (IsFileWritable отфильтровывает),
                // либо temp и target были на разных томах (мы это исключили staging'ом).
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
                    Log(logFile, $"Replaced: {relativePath} (attempt {attempt})");
                    return true;
                }
                catch (Exception exReplace)
                {
                    Log(logFile, $"[{relativePath}] File.Replace attempt {attempt} failed: {exReplace.Message}");
                }

                // --- Попытка №2: swap через переименование --------------
                // ВАЖНО: не удаляем target, а переименовываем его в .bak рядом.
                // Если следующий Move упадёт — вернём .bak на место.
                string sideBak = targetFilePath + ".old_" + Guid.NewGuid().ToString("N");
                bool originalRenamed = false;
                try
                {
                    File.Move(targetFilePath, sideBak); // это тоже требует отсутствия lock'а
                    originalRenamed = true;

                    File.Move(tempFilePath, targetFilePath);

                    // Успех — можно удалить боковой бэкап
                    try { File.Delete(sideBak); } catch { /* не критично */ }

                    // Если пользователь просил нормальный бэкап — положим копию в backupDir
                    if (rezervCopy)
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath));
                            // Читаем уже новый файл как копию старого? Нет — старого уже нет.
                            // Поэтому при rezervCopy полагаемся на ветку File.Replace выше.
                        }
                        catch { }
                    }

                    Log(logFile, $"Renamed-swap: {relativePath} (attempt {attempt})");
                    return true;
                }
                catch (Exception exMove)
                {
                    Log(logFile, $"[{relativePath}] rename-swap attempt {attempt} failed: {exMove.Message}");

                    // Восстанавливаем оригинал, если он куда-то делся.
                    try
                    {
                        if (originalRenamed && !File.Exists(targetFilePath) && File.Exists(sideBak))
                        {
                            File.Move(sideBak, targetFilePath);
                            Log(logFile, $"[{relativePath}] original restored.");
                        }
                        else if (File.Exists(sideBak))
                        {
                            // target уже на месте, sideBak — мусор
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
        /// Проверяет, что файл можно открыть на запись монопольно.
        /// Это единственный надёжный способ узнать, держит ли DLL чужой процесс.
        /// </summary>
        static bool IsFileWritable(string path)
        {
            if (!File.Exists(path)) return true;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open,
                                               FileAccess.ReadWrite, FileShare.None))
                {
                    // ok
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
        /// <summary>
        /// Удаляет все подпапки, начинающиеся с ".update_tmp_", в указанной директории.
        /// </summary>
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
                catch { /* игнорируем ошибки логирования */ }
            }
        }
        /// <summary>
        /// Обрезает лог-файл, оставляя только последние MaxLogLines строк.
        /// Вызывается один раз при старте программы.
        /// </summary>
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
            catch
            {
                /* игнорируем ошибки */
            }
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Threading;

//namespace UpdaterENS
//{
//    class Program
//    {
//        // Аргументы: pid, sourceDir, targetDir, backupDir, [logFile]
//        static void Main(string[] args)
//        {
//            if (args.Length < 4)
//            {
//                Console.WriteLine("Usage: UpdaterENS.exe <pid> <sourceDir> <targetDir> <backupDir> [logFile]");
//                return;
//            }

//            if (!int.TryParse(args[0], out int pid))
//            {
//                Console.WriteLine("Invalid PID");
//                return;
//            }

//            string sourceDir = args[1];
//            string targetDir = args[2];
//            string backupDir = args[3];
//            string logFile = args.Length > 4 ? args[4] : null;

//            Log(logFile, $"Update started at {DateTime.Now}. Waiting for process {pid} to exit...");

//            try
//            {
//                // Ждём завершения процесса
//                try
//                {
//                    using (var process = Process.GetProcessById(pid))
//                    {
//                        process.WaitForExit();
//                    }
//                }
//                catch (ArgumentException)
//                {
//                    Log(logFile, $"Process with PID {pid} not found. Assuming it's already closed.");
//                }

//                Thread.Sleep(3000); // дополнительная задержка

//                if (PathsEqual(sourceDir, targetDir))
//                {
//                    Log(logFile, "Source and target directories are the same. Aborting.");
//                    return;
//                }

//                int copiedFiles = CopyFilesAtomically(sourceDir, targetDir, backupDir, logFile);
//                Log(logFile, $"Update completed successfully. Files updated: {copiedFiles}");
//            }
//            catch (Exception ex)
//            {
//                Log(logFile, $"Fatal error: {ex.Message}");
//            }
//        }

//        /// <summary>
//        /// Копирует файлы с использованием временной подпапки и атомарной замены.
//        /// Возвращает количество обновлённых файлов.
//        /// </summary>
//        static int CopyFilesAtomically(string sourceDir, string targetDir, string backupDir, string logFile = null)
//        {
//            if (!Directory.Exists(sourceDir))
//                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

//            if (!Directory.Exists(targetDir))
//                Directory.CreateDirectory(targetDir);

//            sourceDir = Path.GetFullPath(sourceDir);
//            targetDir = Path.GetFullPath(targetDir);
//            backupDir = Path.GetFullPath(backupDir);

//            // 1. Определяем список файлов, которые нужно обновить
//            var filesToUpdate = new List<string>();
//            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
//            foreach (var sourceFilePath in sourceFiles)
//            {
//                string relativePath = GetRelativePath(sourceDir, sourceFilePath);
//                string targetFilePath = Path.Combine(targetDir, relativePath);

//                if (!File.Exists(targetFilePath) ||
//                    File.GetLastWriteTimeUtc(sourceFilePath) > File.GetLastWriteTimeUtc(targetFilePath))
//                {
//                    filesToUpdate.Add(relativePath);
//                }
//            }

//            if (filesToUpdate.Count == 0)
//            {
//                Log(logFile, "No files need to be updated.");
//                return 0;
//            }

//            // 2. Создаём временную подпапку внутри целевой директории
//            string tempSubdir = Path.Combine(targetDir, $".update_tmp_{Guid.NewGuid():N}");
//            Directory.CreateDirectory(tempSubdir);

//            // 3. Копируем все изменённые файлы во временную подпапку
//            foreach (var relativePath in filesToUpdate)
//            {
//                string sourceFilePath = Path.Combine(sourceDir, relativePath);
//                string tempFilePath = Path.Combine(tempSubdir, relativePath);
//                string tempFileDir = Path.GetDirectoryName(tempFilePath);
//                if (!Directory.Exists(tempFileDir))
//                    Directory.CreateDirectory(tempFileDir);

//                File.Copy(sourceFilePath, tempFilePath, overwrite: true);
//                Log(logFile, $"Staged: {relativePath}");
//            }

//            // 4. Выполняем атомарную замену файлов с повторными попытками
//            int updatedCount = 0;
//            foreach (var relativePath in filesToUpdate)
//            {
//                string tempFilePath = Path.Combine(tempSubdir, relativePath);
//                string targetFilePath = Path.Combine(targetDir, relativePath);
//                bool replaced = false;
//                const int maxAttempts = 5;
//                int attempt = 0;

//                while (!replaced && attempt < maxAttempts)
//                {
//                    attempt++;
//                    try
//                    {
//                        if (File.Exists(targetFilePath))
//                        {
//                            // Готовим путь для резервной копии
//                            string backupFilePath = Path.Combine(backupDir, relativePath);
//                            string backupFileDir = Path.GetDirectoryName(backupFilePath);
//                            if (!Directory.Exists(backupFileDir))
//                                Directory.CreateDirectory(backupFileDir);

//                            // Удаляем старую резервную копию, если она есть
//                            if (File.Exists(backupFilePath))
//                                File.Delete(backupFilePath);

//                            // Атомарная замена с одновременным созданием резервной копии
//                            File.Replace(tempFilePath, targetFilePath, backupFilePath, ignoreMetadataErrors: true);
//                            Log(logFile, $"Replaced: {relativePath} (attempt {attempt})");
//                        }
//                        else
//                        {
//                            // Файл отсутствует – просто перемещаем
//                            string targetFileDir = Path.GetDirectoryName(targetFilePath);
//                            if (!Directory.Exists(targetFileDir))
//                                Directory.CreateDirectory(targetFileDir);

//                            File.Move(tempFilePath, targetFilePath);
//                            Log(logFile, $"Added: {relativePath} (attempt {attempt})");
//                        }
//                        replaced = true;
//                    }
//                    catch (Exception ex)
//                    {
//                        Log(logFile, $"Error updating '{relativePath}' on attempt {attempt}: {ex.Message}");
//                        if (attempt < maxAttempts)
//                        {
//                            // Ждём перед следующей попыткой (можно увеличивать паузу)
//                            int delayMs = 3000 * attempt; // 3, 6, 9, 12 секунд
//                            Log(logFile, $"Retrying in {delayMs / 1000} seconds...");
//                            Thread.Sleep(delayMs);
//                        }
//                    }
//                }

//                if (replaced)
//                    updatedCount++;
//                else
//                    Log(logFile, $"Giving up on '{relativePath}' after {maxAttempts} attempts.");
//            }

//            // 5. Удаляем временную подпапку (если остались файлы из-за ошибок – оставляем для диагностики)
//            try
//            {
//                if (Directory.Exists(tempSubdir) && !Directory.EnumerateFileSystemEntries(tempSubdir).Any())
//                {
//                    Directory.Delete(tempSubdir);
//                }
//                else
//                {
//                    Log(logFile, $"Temporary folder '{tempSubdir}' left for manual cleanup (contains unprocessed files).");
//                }
//            }
//            catch (Exception ex)
//            {
//                Log(logFile, $"Failed to delete temporary folder: {ex.Message}");
//            }

//            return updatedCount;
//        }

//        static string GetRelativePath(string basePath, string fullPath)
//        {
//            basePath = Path.GetFullPath(basePath);
//            fullPath = Path.GetFullPath(fullPath);

//            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
//                basePath += Path.DirectorySeparatorChar;

//            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
//                throw new ArgumentException($"Path '{fullPath}' is not inside '{basePath}'.");

//            return fullPath.Substring(basePath.Length);
//        }

//        static bool PathsEqual(string path1, string path2)
//        {
//            string full1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
//            string full2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
//            return string.Equals(full1, full2, StringComparison.OrdinalIgnoreCase);
//        }

//        static void Log(string logFile, string message)
//        {
//            Console.WriteLine(message);
//            if (!string.IsNullOrEmpty(logFile))
//            {
//                try
//                {
//                    File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
//                }
//                catch { /* игнорируем ошибки логирования */ }
//            }
//        }
//    }
//}











//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Threading;

//namespace UpdaterENS
//{
//    class Program
//    {
//        // Ожидаемые аргументы: pid, sourceDir, targetDir, backupDir, [logFile]
//        static void Main(string[] args)
//        {
//            if (args.Length < 4)
//            {
//                Console.WriteLine("Usage: UpdaterENS.exe <pid> <sourceDir> <targetDir> <backupDir> [logFile]");
//                return;
//            }

//            if (!int.TryParse(args[0], out int pid))
//            {
//                Console.WriteLine("Invalid PID");
//                return;
//            }

//            string sourceDir = args[1];
//            string targetDir = args[2];
//            string backupDir = args[3];
//            string logFile = args.Length > 4 ? args[4] : null;

//            // Логирование начала
//            Log(logFile, $"Update started at {DateTime.Now}. Waiting for process {pid} to exit...");

//            try
//            {
//                // Ждём завершения процесса Revit
//                try
//                {
//                    using (var process = Process.GetProcessById(pid))
//                    {
//                        process.WaitForExit();
//                    }
//                }
//                catch (ArgumentException)
//                {
//                    Log(logFile, $"Process with PID {pid} not found. Assuming it's already closed.");
//                }

//                // Небольшая задержка, чтобы файлы точно освободились
//                Thread.Sleep(3000);

//                // Проверка на совпадение каталогов
//                if (PathsEqual(sourceDir, targetDir))
//                {
//                    Log(logFile, "Source and target directories are the same. Aborting.");
//                    return;
//                }

//                // Копируем файлы с резервным копированием
//                int copiedFiles = CopyFilesWithBackup(sourceDir, targetDir, backupDir, logFile);

//                Log(logFile, $"Update completed successfully. Files copied/replaced: {copiedFiles}");
//            }
//            catch (Exception ex)
//            {
//                Log(logFile, $"Fatal error: {ex.Message}");
//                // Можно дополнительно записать в EventLog
//            }
//        }

//        /// <summary>
//        /// Копирует файлы из sourceDir в targetDir, создавая резервные копии заменяемых файлов в backupDir.
//        /// Возвращает количество скопированных/перезаписанных файлов.
//        /// </summary>
//        static int CopyFilesWithBackup(string sourceDir, string targetDir, string backupDir, string logFile = null)
//        {
//            if (!Directory.Exists(sourceDir))
//                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

//            if (!Directory.Exists(targetDir))
//                Directory.CreateDirectory(targetDir);

//            // Приводим к абсолютным путям
//            sourceDir = Path.GetFullPath(sourceDir);
//            targetDir = Path.GetFullPath(targetDir);
//            backupDir = Path.GetFullPath(backupDir);

//            int copiedCount = 0;
//            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

//            foreach (var sourceFilePath in sourceFiles)
//            {
//                string relativePath = GetRelativePath(sourceDir, sourceFilePath);
//                string targetFilePath = Path.Combine(targetDir, relativePath);

//                // Создаём целевую папку при необходимости
//                string targetFileDir = Path.GetDirectoryName(targetFilePath);
//                if (!Directory.Exists(targetFileDir))
//                    Directory.CreateDirectory(targetFileDir);

//                try
//                {
//                    // Копируем, если файл отсутствует или источник новее
//                    if (!File.Exists(targetFilePath) ||
//                        File.GetLastWriteTimeUtc(sourceFilePath) > File.GetLastWriteTimeUtc(targetFilePath))
//                    {
//                        // Если файл существует и мы его перезаписываем — делаем резервную копию
//                        if (File.Exists(targetFilePath))
//                        {
//                            string backupFilePath = Path.Combine(backupDir, relativePath);
//                            string backupFileDir = Path.GetDirectoryName(backupFilePath);
//                            if (!Directory.Exists(backupFileDir))
//                                Directory.CreateDirectory(backupFileDir);

//                            File.Copy(targetFilePath, backupFilePath, overwrite: true);
//                            Log(logFile, $"Backup created: {relativePath}");
//                        }

//                        File.Copy(sourceFilePath, targetFilePath, overwrite: true);
//                        copiedCount++;
//                        Log(logFile, $"Copied: {relativePath}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    // Логируем ошибку для конкретного файла и продолжаем
//                    Log(logFile, $"Error copying '{relativePath}': {ex.Message}");
//                }
//            }

//            return copiedCount;
//        }

//        /// <summary>
//        /// Вычисляет относительный путь от basePath к fullPath.
//        /// Требует, чтобы fullPath находился внутри basePath.
//        /// </summary>
//        static string GetRelativePath(string basePath, string fullPath)
//        {
//            basePath = Path.GetFullPath(basePath);
//            fullPath = Path.GetFullPath(fullPath);

//            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
//                basePath += Path.DirectorySeparatorChar;

//            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
//                throw new ArgumentException($"Path '{fullPath}' is not inside '{basePath}'.");

//            return fullPath.Substring(basePath.Length);
//        }

//        /// <summary>
//        /// Сравнивает два пути без учёта регистра и завершающих слешей.
//        /// </summary>
//        static bool PathsEqual(string path1, string path2)
//        {
//            string full1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
//            string full2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
//            return string.Equals(full1, full2, StringComparison.OrdinalIgnoreCase);
//        }

//        /// <summary>
//        /// Записывает сообщение в лог-файл (если задан) и в консоль.
//        /// </summary>
//        static void Log(string logFile, string message)
//        {
//            Console.WriteLine(message);
//            if (!string.IsNullOrEmpty(logFile))
//            {
//                try
//                {
//                    File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
//                }
//                catch { /* игнорируем ошибки логирования */ }
//            }
//        }
//    }
//}