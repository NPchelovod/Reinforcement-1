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

        int Count();
        
    }

    public class WriteAndRead<T> where T : class, IJsonStorable<T>, new()
    {
        
        public string FileName;//основное имя файла .json!
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FileName);
        public string FolderPath;//основная папка
        public string FolderPathTemp;//временная папка
        
        
        public string RelativeFolderHistory = "_history";

        public WriteAndRead(string folderPath,string folderPathTemp, string fileName)
        {
            FolderPath = folderPath;
            FolderPathTemp = folderPathTemp;
            FileName = fileName;

        }

       

        
        public string HistoryName(DateTime from, DateTime to, string rizon, int i)
        {
            //имя для записи в историю
            string hn=$"{FileNameWithoutExtension}_" +
                                     $"{from.Year}_{from.Month}_{from.Day}To" +
                                     $"{to.Year}_{to.Month}_{to.Day}_{rizon}";
            if(i>0)
            {
                hn += $"_{i}";
            }
            hn += ".json";
            return hn;
        }

        

        public string GetFileName(string path)=> Path.Combine(path, FileName);
        //public string GetPathHistory(string path) => Path.Combine(path, FileName);

        private  int _inFlush;
        private  readonly object _lock = new object();
        public bool Flush(T sc)
        {
            //при закрытии ревита эта команда вызывается напрямую
            if (Interlocked.CompareExchange(ref _inFlush, 1, 0) != 0)
            {
                return false;
            }
            try
            {
               
                lock (_lock)
                {
                    if (sc.Count() == 0) { return false; }//пустой значит

                    T pastT = null;
                    bool existF1 = false;
                    string filepath = GetFileName(FolderPath);
                    if (FastDirectoryChecker.DirectoryExistsFast(FolderPath))
                    {
                        //основная директория существует
                        existF1 = true;
                        //читаем прошлый файл
                        pastT = ReadFileAndControlSize(filepath, FolderPath);

                    }
                    if (pastT == null)
                    {
                        pastT = new T();
                    }

                    T pastT2 = null;
                    
                    string filepath2 = GetFileName(FolderPathTemp);
                    if (!string.Equals(Path.GetFullPath(FolderPath), Path.GetFullPath(FolderPathTemp),
                   StringComparison.OrdinalIgnoreCase) && FastDirectoryChecker.DirectoryExistsFast(FolderPathTemp))
                    {
                        //возможно в temp есть файл когда диск основной был недоступен, надо его прочитать, и удалить
                        
                        pastT2 = ReadFileAndControlSize(filepath2, FolderPathTemp);

                        if (pastT2 != null)
                        {
                            bool isDel = false;
                            if (existF1)
                            {
                                try
                                {
                                    //удаляем файл чтобы не складывать дубли в  статистику в следующий раз
                                    File.Delete(filepath2);//удаляем временный файл
                                    isDel = true;
                                }
                                catch { }

                                if (isDel)
                                {
                                    pastT.Merge(pastT2);
                                }
                            }
                            else
                            {
                                //значит temp - наша основная директория
                                pastT = pastT2;
                            }
                        }
                    }


                    pastT.Merge(sc);//соединяем с нашей статистикой за время t
                    string folder = FolderPath;
                    if (!existF1)
                    {
                        filepath = filepath2;
                        folder = FolderPathTemp;
                    }
                    //теперь запись файла
                    if (!Writer(pastT, filepath, folder))
                    {
                        return false;
                    }
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
                Directory.CreateDirectory(folder);//если создана - не создастся
                if (!Directory.Exists(folder)) { return false; }
                string json = JsonSerializer.Serialize(sc, RevitAPI.Options);

                // Пишем во временный файл рядом с целевым (в той же папке —
                // это важно: File.Replace/File.Move работают атомарно только
                // в пределах одного тома)
                string tmp = filePath + ".tmp";
                // --- 2. Замер I/O ---
               

                // 2. Потом пишем новый
                File.WriteAllText(tmp, json);
                try
                {
                    File.Replace(tmp, filePath, null, true);
                }
                catch (FileNotFoundException)
                {
                    File.Move(tmp, filePath);  // файла не было — просто переносим
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
        private T ReadFileAndControlSize(string path, string folder)
        {
            try
            {

                if (!File.Exists(path))
                {
                    return null;
                }
                DateTime NowDay = DateTime.Now.Date;//обрезанная дата
                                                           //првоерка размера до парсига
                long sizeBytes = new FileInfo(path).Length;
                if (sizeBytes > hardFileSizeBytes)
                {
                    
                    MoveToHistory(path, folder, NowDay, NowDay, "hardSize");
                    return null;

                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    MoveToHistory(path, folder, NowDay, NowDay, "jsonError");
                    return null;
                }

                var data = JsonSerializer.Deserialize<T>(json, RevitAPI.Options);
                if (data == null)
                {
                    MoveToHistory(path, folder, NowDay, NowDay, "dataError");
                    return null;
                }

                //если размер большой и надо в иторию вложить уже
                if (sizeBytes > maxFileSizeBytes)
                {
                    DateTime from = data.DateStart();
                    DateTime to = data.DateEnd();
                    if(from==DateTime.MinValue)
                    {
                        from = NowDay;
                    }
                    if (to == DateTime.MinValue)
                    {
                        to = NowDay;
                    }
                    
                    MoveToHistory(path, folder, from, to, "maxSize");
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
        private bool MoveToHistory(string filePath, string folder, DateTime from, DateTime to, string rizon)
        {
            
            string historyFolder = Path.Combine(folder, RelativeFolderHistory);
            try
            {
                Directory.CreateDirectory(historyFolder);
                int suffix = 0;
                string historyPath;
                while (true)
                {
                    string hn = HistoryName(from, to, rizon, suffix);
                    historyPath = Path.Combine(historyFolder, hn);
                    if (!File.Exists(historyPath)) break;
                    suffix++;
                }
                File.Copy(filePath, historyPath, overwrite: false);
                try { File.Delete(filePath); } catch (Exception ex) { AppErrors.LogError(ex); }
                return File.Exists(historyPath);
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return false;
            }
        }
        
    }
    public static class FastDirectoryChecker
    {
        //быстрый способ чтобы если нет диска Y/ то не зависала на 30+ секунд
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
