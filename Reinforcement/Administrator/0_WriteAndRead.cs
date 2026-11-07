using Autodesk.Revit.DB;
using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace Reinforcement
{
    public interface IJsonStorable<T>
    {
        void Merge(T other);
        DateTime DateStart();
        DateTime DateEnd();
    }

    public class WriteAndRead<T> where T : class, IJsonStorable<T>, new()
    {
        public string FileName;
        public string FileHistoryName;

        public string FolderPath;
        public string FolderPathTemp;
        public string RelativeFolderHistory = "_history";
        public string FolderPathHistory(string path) => Path.Combine(path, RelativeFolderHistory);
        public string historyName(DateTime from, DateTime to, string rizon, int i)
        {
            string hn=$"{Environment.UserName}_{Environment.MachineName}_" +
                                     $"{from.Year}_{from.Month}_{from.Day}To" +
                                     $"{to.Year}_{to.Month}_{to.Day}_{rizon}.json";
            if(i>0)
            {
                hn += $"{i}";
            }
            hn += ".json";
            return hn;
        }

        public DateTime Now=DateTime.MinValue;

        public string GetFileName(string path)=> Path.Combine(path, FileName);
        //public string GetPathHistory(string path) => Path.Combine(path, FileName);

        private static int _inFlush;
        public bool Flush(T sc)
        {
            //при закрытии ревита эта команда вызывается напрямую
            if (Interlocked.CompareExchange(ref _inFlush, 1, 0) != 0)
            {
                return false;
            }
            try
            {
                Now = DateTime.Now.Date;
                T pastT = null;
                bool existF1=false;
                string filepath = GetFileName(FolderPath);
                if (FastDirectoryChecker.DirectoryExistsFast(FolderPath))
                {
                    existF1 = true;
                    pastT = ReadFileAndControlSize(filepath);
                    
                }
                if (pastT == null)
                {
                    pastT = new T();    
                }

                T pastT2 = null;
                bool existF2 = false;
                string filepath2 = GetFileName(FolderPathTemp);
                if (FolderPath!= FolderPathTemp && FastDirectoryChecker.DirectoryExistsFast(FolderPathTemp))
                {
                    existF2= true;
                    pastT2 = ReadFileAndControlSize(filepath2);

                    if (pastT2 != null)
                    {
                        bool isDel = false;
                        try
                        {
                            File.Delete(filepath2);//удаляем временный файл
                            isDel = true;
                        }
                        catch { }
                        if (isDel)
                        {
                            pastT.Merge(pastT2);
                        }
                    }
                }
                

                pastT.Merge(sc);
                string folder = FolderPath;
                if (!existF1)
                {
                    filepath = filepath2;
                    folder = FolderPathTemp;
                }
                //теперь запись файла
                if(!Writer(pastT, filepath, folder))
                {
                    return false;
                }
                

            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex); return false; 
            }
            finally
            {
                Interlocked.Exchange(ref _inFlush, 0);
            }
            return true;
        }
        private bool Writer(T sc, string filePath, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                if (!Directory.Exists(folder)) { return false; }
                string json = JsonSerializer.Serialize(sc, Options);

                // Пишем во временный файл рядом с целевым (в той же папке —
                // это важно: File.Replace/File.Move работают атомарно только
                // в пределах одного тома)
                string tmp = filePath + ".tmp";
                // --- 2. Замер I/O ---
                var swIo = Stopwatch.StartNew();

                // 2. Потом пишем новый
                File.WriteAllText(tmp, json);
                if (File.Exists(filePath))
                {
                    // Атомарная замена существующего файла:
                    // в целевой путь попадает tmp, старый файл удаляется.
                    // Третий параметр — путь для бэкапа (null — бэкап не нужен).
                    File.Replace(tmp, filePath, destinationBackupFileName: null,
                                 ignoreMetadataErrors: true);

                }
                else
                {
                    // Файла ещё нет — просто переносим.
                    File.Move(tmp, filePath);
                }
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return false;
            }
            return true;
            
        }

        private const long maxFileSizeBytes = 5L * 1024 * 1024;   // 5 МБ
        public const long hardFileSizeBytes = 20L * 1024 * 1024; // 20 МБ Жёсткий порог — на такой файл лучше даже не замахиваться парсером
        private T ReadFileAndControlSize(string path)
        {
            try
            {

                if (!File.Exists(path))
                {
                    return null;
                }

                //првоерка размера до парсига
                long sizeBytes = new FileInfo(path).Length;
                if (sizeBytes > hardFileSizeBytes)
                {
                    
                    MoveToHistory(path, Now, Now, "hardSize");
                    
                   
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    MoveToHistory(path, Now, Now, "jsonError");
                    return null;
                }

                var data = JsonSerializer.Deserialize<T>(json, Options);
                if (data == null)
                {
                    MoveToHistory(path, Now, Now, "dataError");
                    return null;
                }

                //если размер большой и надо в иторию вложить уже
                if (sizeBytes > maxFileSizeBytes)
                {
                    DateTime from = data.DateStart();
                    DateTime to = data.DateEnd();
                    if(from==DateTime.MinValue)
                    {
                        from = Now;
                    }
                    if (to == DateTime.MinValue)
                    {
                        to = Now;
                    }
                    
                    MoveToHistory(path, from, to, "maxSize");
                    return null;
                }

                //проверка данных

                //и возврат данных
                return data;
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return null;
            }
        }
        private bool MoveToHistory(string sourcePath, DateTime from, DateTime to, string rizon)
        {
            string folderStatisticsHistory = FolderPathHistory(sourcePath);

            try
            {
                // Создаём папку «Архив», если её нет
                if (!Directory.Exists(folderStatisticsHistory))
                    Directory.CreateDirectory(folderStatisticsHistory);
                int suffix = 0;
                string historyPath;
                while (true)
                {

                   string hn= historyName(from,to,rizon,suffix);
                    historyPath = Path.Combine(folderStatisticsHistory, hn);
                   if(!File.Exists(historyPath))
                    {
                        break;
                    }
                    suffix++;
                }
                File.Copy(sourcePath, historyPath, overwrite: true);
                try { File.Delete(sourcePath); } catch (Exception ex) { AppErrors.LogError(ex); }

                return File.Exists(historyPath);
            }
            catch (Exception ex)
            {

                AppErrors.LogError(ex);
                return false;
            }
        }
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            IncludeFields = true,
            WriteIndented = true
        };
    }
    public static class FastDirectoryChecker
    {
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileAttributes(string lpFileName);

        public static bool DirectoryExistsFast(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            uint attrs = GetFileAttributes(path);
            if (attrs == INVALID_FILE_ATTRIBUTES)
            {
                // GetLastError можно проверить, если нужно различать «нет папки» и «диск недоступен»
                return false;
            }

            return (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0;
        }
    }
}
