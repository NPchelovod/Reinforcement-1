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
        // Аргументы: pid, sourceDir, targetDir, backupDir, [logFile]
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: UpdaterENS.exe <pid> <sourceDir> <targetDir> <backupDir> [logFile]");
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
            string logFile = args.Length > 4 ? args[4] : null;

            Log(logFile, $"Update started at {DateTime.Now}. Waiting for process {pid} to exit...");

            try
            {
                // Ждём завершения процесса
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        process.WaitForExit();
                    }
                }
                catch (ArgumentException)
                {
                    Log(logFile, $"Process with PID {pid} not found. Assuming it's already closed.");
                }

                Thread.Sleep(3000); // дополнительная задержка

                if (PathsEqual(sourceDir, targetDir))
                {
                    Log(logFile, "Source and target directories are the same. Aborting.");
                    return;
                }

                int copiedFiles = CopyFilesAtomically(sourceDir, targetDir, backupDir, logFile);
                Log(logFile, $"Update completed successfully. Files updated: {copiedFiles}");
            }
            catch (Exception ex)
            {
                Log(logFile, $"Fatal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Копирует файлы с использованием временной подпапки и атомарной замены.
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

            // 1. Определяем список файлов, которые нужно обновить
            var filesToUpdate = new List<string>();
            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var sourceFilePath in sourceFiles)
            {
                string relativePath = GetRelativePath(sourceDir, sourceFilePath);
                string targetFilePath = Path.Combine(targetDir, relativePath);

                if (!File.Exists(targetFilePath) ||
                    File.GetLastWriteTimeUtc(sourceFilePath) > File.GetLastWriteTimeUtc(targetFilePath))
                {
                    filesToUpdate.Add(relativePath);
                }
            }

            if (filesToUpdate.Count == 0)
            {
                Log(logFile, "No files need to be updated.");
                return 0;
            }

            // 2. Создаём временную подпапку внутри целевой директории
            string tempSubdir = Path.Combine(targetDir, $".update_tmp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempSubdir);

            // 3. Копируем все изменённые файлы во временную подпапку
            foreach (var relativePath in filesToUpdate)
            {
                string sourceFilePath = Path.Combine(sourceDir, relativePath);
                string tempFilePath = Path.Combine(tempSubdir, relativePath);
                string tempFileDir = Path.GetDirectoryName(tempFilePath);
                if (!Directory.Exists(tempFileDir))
                    Directory.CreateDirectory(tempFileDir);

                File.Copy(sourceFilePath, tempFilePath, overwrite: true);
                Log(logFile, $"Staged: {relativePath}");
            }

            // 4. Выполняем атомарную замену файлов
            int updatedCount = 0;
            foreach (var relativePath in filesToUpdate)
            {
                string tempFilePath = Path.Combine(tempSubdir, relativePath);
                string targetFilePath = Path.Combine(targetDir, relativePath);

                try
                {
                    if (File.Exists(targetFilePath))
                    {
                        // Готовим путь для резервной копии
                        string backupFilePath = Path.Combine(backupDir, relativePath);
                        string backupFileDir = Path.GetDirectoryName(backupFilePath);
                        if (!Directory.Exists(backupFileDir))
                            Directory.CreateDirectory(backupFileDir);

                        // Удаляем старую резервную копию, если она есть
                        if (File.Exists(backupFilePath))
                            File.Delete(backupFilePath);

                        // Атомарная замена с одновременным созданием резервной копии
                        File.Replace(tempFilePath, targetFilePath, backupFilePath, ignoreMetadataErrors: true);
                        Log(logFile, $"Replaced: {relativePath}");
                    }
                    else
                    {
                        // Файл отсутствует – просто перемещаем
                        string targetFileDir = Path.GetDirectoryName(targetFilePath);
                        if (!Directory.Exists(targetFileDir))
                            Directory.CreateDirectory(targetFileDir);

                        File.Move(tempFilePath, targetFilePath);
                        Log(logFile, $"Added: {relativePath}");
                    }
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    Log(logFile, $"Error updating '{relativePath}': {ex.Message}");
                }
            }

            // 5. Удаляем временную подпапку (если остались файлы из-за ошибок – оставляем для диагностики)
            try
            {
                if (Directory.Exists(tempSubdir) && !Directory.EnumerateFileSystemEntries(tempSubdir).Any())
                {
                    Directory.Delete(tempSubdir);
                }
                else
                {
                    Log(logFile, $"Temporary folder '{tempSubdir}' left for manual cleanup (contains unprocessed files).");
                }
            }
            catch (Exception ex)
            {
                Log(logFile, $"Failed to delete temporary folder: {ex.Message}");
            }

            return updatedCount;
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

        static bool PathsEqual(string path1, string path2)
        {
            string full1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(full1, full2, StringComparison.OrdinalIgnoreCase);
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