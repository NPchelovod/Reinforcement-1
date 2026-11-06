using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Text.Json;
namespace Reinforcement
{
    public class StatIdDeletes
    {
        public Dictionary<string, HashSet<int>> DeleteIds { get; set; }
            = new Dictionary<string, HashSet<int>>();


        public void Merge(StatIdDeletes other)
        {
            var pastDict = other.DeleteIds;
            if (pastDict == null || pastDict.Count == 0) { return; }

            foreach (var dateEntry in pastDict)
            {
                var nameDoc = dateEntry.Key;
                var delValue = dateEntry.Value;
                if (!DeleteIds.TryGetValue(nameDoc, out var dv))
                {
                    dv = new HashSet<int>(delValue.Count);   // пре-аллокация
                    DeleteIds[nameDoc] = dv;
                }

                foreach (var di in delValue)
                {
                    dv.Add(di);
                }
            }
        }
    }

    public partial class PeriodicHandler
    {
        private static readonly object _lockDelete = new object();
        private static StatIdDeletes _statIdDeletes = new StatIdDeletes();
        public static void ExecuteDelete(DateTime now, Document doc, ICollection<ElementId> deletes)
        {
            if (deletes == null || deletes.Count == 0) return;
            string name = doc.PathName;
            if (string.IsNullOrEmpty(name))
            {
                name = doc.Title;
            }
            lock (_lockDelete)
            {
                if (!_statIdDeletes.DeleteIds.TryGetValue(name, out var hd))
                {
                    hd = new HashSet<int>();
                    _statIdDeletes.DeleteIds[name] = hd;
                }
                foreach (ElementId id in deletes)
                {
                    hd.Add(id.IntegerValue);   // ← ElementId → int
                }
            }
            if (now-pastExportIdDeletes> _flushInterval)
            {
                FlushDeleteIds(now);
            }
            
        }
        private static readonly TimeSpan _flushInterval = TimeSpan.FromHours(3.5);
        private static int _maxCountDelElements = 2500;
        public static DateTime pastExportIdDeletes = DateTime.MinValue;

        private static int _inFlush;
        private static string folderStatisticsDelIds => App_Folders.FolderStatisticsDelIds;
        private static string fileName =>
    $"{Environment.UserName}_{pastExportIdDeletes.ToString("yyyyMM")}.json";
        public static void FlushDeleteIds(DateTime now)
        {
            pastExportIdDeletes = now;
            //при закрытии ревита эта команда вызывается напрямую
            if (Interlocked.CompareExchange(ref _inFlush, 1, 0) != 0)
            {
                return;
            }
            
            try
            {
                
                string fsd = folderStatisticsDelIds;
                //универсально - обрубаем здесь сохранение
                if (!Directory.Exists(fsd))
                {
                    Directory.CreateDirectory(fsd);
                    if (!Directory.Exists(fsd))
                    {
                        return;
                    }
                }
                // Быстрый отказ, если диск недоступен — не ждём таймауты SMB
                
                

                
                string path = Path.Combine(folderStatisticsDelIds, fileName);
                //пытаемся прочитать старый файл
                StatIdDeletes past = ReadPastFile(path);
                if(past==null)
                {
                    past = new StatIdDeletes();
                }
                lock (_lockDelete)
                {
                    past.Merge(_statIdDeletes);

                    if (WriteFile(path, past))
                    {

                        _statIdDeletes = new StatIdDeletes();
                    }
                }

            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
            }
            finally
            {
                
                Interlocked.Exchange(ref _inFlush, 0);
               
            }

        }
        private static StatIdDeletes ReadPastFile(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                //првоерка размера до парсига
                long sizeBytes = new FileInfo(path).Length;
                if (sizeBytes >StatUsers.hardFileSizeBytes)
                {
                    return null;
                }
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                { return null; }
                var data = JsonSerializer.Deserialize<StatIdDeletes>(json,  StatUsers.Options);
                if (data == null)
                    { return null; }
                return data;
            }

            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return null;
            }
            
        }

        private static bool WriteFile(string path, StatIdDeletes statIdDeletes)
        {
            try
            {
                string json = JsonSerializer.Serialize(statIdDeletes, StatUsers.Options);
                string tmp = path + ".tmp";

                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                {
                    // Атомарная замена существующего файла:
                    // в целевой путь попадает tmp, старый файл удаляется.
                    // Третий параметр — путь для бэкапа (null — бэкап не нужен).
                    File.Replace(tmp, path, destinationBackupFileName: null,
                                 ignoreMetadataErrors: true);

                }
                else
                {
                    // Файла ещё нет — просто переносим.
                    File.Move(tmp, path);
                }
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return false;
            }
            return true;
        }

    }
}