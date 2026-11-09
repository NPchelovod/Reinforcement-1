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
    public class StatIdDeletes : IJsonStorable<StatIdDeletes>
    {
        public DateTime DateTime { get; set; } = DateTime.MinValue;
        public Dictionary<string, HashSet<int>> DeleteIds { get; set; }
            = new Dictionary<string, HashSet<int>>();
        public int Count()
        {
            return DeleteIds.Count;
        }
        public StatIdDeletes() 
        {
            DateTime = DateTime.Now;
        }

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
        public DateTime DateStart()
        {
            return DateTime;
        }
        public DateTime DateEnd()
        {
            return DateTime;
        }
    }

    public partial class PeriodicHandler
    {
        private static readonly object _lockDelete = new object();
        private static StatIdDeletes _statIdDeletes = new StatIdDeletes();
        public static void ExecuteDelete(DateTime now, Document doc, ICollection<ElementId> deletes)
        {
            if (deletes == null || deletes.Count == 0) return;
            try
            {
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
                if (now - pastExportIdDeletes > _flushInterval)
                {
                    FlushDeleteIds(now);
                }
            }
            catch (Exception ex)
            {
                //AppErrors.LogError(ex);
            }
            
        }
        private static readonly TimeSpan _flushInterval = TimeSpan.FromHours(3.5);
        public static DateTime pastExportIdDeletes = DateTime.MinValue;

        

        private static string _currentFileMonth = null;
        public static void FlushDeleteIds(DateTime now)
        {

            //при закрытии ревита эта команда вызывается напрямую

            EnsureCurrentFile(now);
            if (writeAndRead == null) { AppErrors.LogError(new Exception("InitializationWriteAndRead()")); return; }
            bool ok;
            lock (_lockDelete)
            {
                pastExportIdDeletes = now;//пусть будет тут, чтобы повторно часто не вызываться при неудачи
                ok = writeAndRead.Flush(_statIdDeletes);
                if (ok)
                {
                    _statIdDeletes = new StatIdDeletes();
                    
                }
            }
            if (!ok) { /* лог */ }
            
        }
        private static WriteAndRead<StatIdDeletes> writeAndRead = null;

        private static void EnsureCurrentFile(DateTime now)
        {
            string month = now.ToString("yyyyMM");
            if (writeAndRead != null && _currentFileMonth == month) return;

            _currentFileMonth = month;
            writeAndRead = new WriteAndRead<StatIdDeletes>(
                App_Folders.FolderStatisticsDelIds,
                App_Folders.FolderStatisticsDelIdsTemp,
                $"{Environment.UserName}_{Environment.MachineName}_{month}.json");
        }

    }
}