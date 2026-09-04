using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.IO;

using System.Threading;

namespace UpdaterENS
{
   
    class Program
    {
        static void Main(string[] args)
        {
            // Ожидаемые аргументы: pid, sourceDir, targetDir, [logFile]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: Updater.exe <pid> <sourceDir> <targetDir> [logFile]");
                return;
            }

            if (!int.TryParse(args[0], out int pid))
            {
                Console.WriteLine("Invalid PID");
                return;
            }

            string sourceDir = args[1];
            string targetDir = args[2];
            string logFile = args.Length > 3 ? args[3] : null;

            try
            {
                // Ждём завершения процесса Revit
                using (var process = Process.GetProcessById(pid))
                {
                    process.WaitForExit();
                }

                // Небольшая задержка, чтобы файлы точно освободились
                Thread.Sleep(3000);

                // Копируем файлы
                CopyFiles(sourceDir, targetDir);

                if (!string.IsNullOrEmpty(logFile))
                    File.AppendAllText(logFile, $"{DateTime.Now}: Update completed successfully.\n");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(logFile))
                    File.AppendAllText(logFile, $"{DateTime.Now}: Error - {ex.Message}\n");
                // Можно попробовать записать ошибку в EventLog или куда-то ещё
            }
        }

        static void CopyFiles(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // Приводим к абсолютным путям
            sourceDir = Path.GetFullPath(sourceDir);
            targetDir = Path.GetFullPath(targetDir);

            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var sourceFilePath in sourceFiles)
            {
                // Определяем относительный путь с помощью Uri
                string relativePath = GetRelativePath(sourceDir, sourceFilePath);
                string targetFilePath = Path.Combine(targetDir, relativePath);

                // Создаём папку, если нужно
                string targetFileDir = Path.GetDirectoryName(targetFilePath);
                if (!Directory.Exists(targetFileDir))
                    Directory.CreateDirectory(targetFileDir);

                // Копируем, если файл новее или отсутствует
                if (!File.Exists(targetFilePath) ||
                    File.GetLastWriteTimeUtc(sourceFilePath) > File.GetLastWriteTimeUtc(targetFilePath))
                {
                    File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                }
            }
        }

        // Вспомогательный метод для получения относительного пути
        static string GetRelativePath(string basePath, string fullPath)
        {
            Uri baseUri = new Uri(AppendDirectorySeparator(basePath));
            Uri fullUri = new Uri(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            // Декодируем URL-символы (например, %20 обратно в пробелы)
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        // Добавляем завершающий слеш, чтобы Uri корректно считал базовый путь как папку
        static string AppendDirectorySeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                return path + Path.DirectorySeparatorChar;
            return path;
        }
    }
}
