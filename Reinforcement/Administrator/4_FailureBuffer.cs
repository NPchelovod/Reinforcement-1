using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Reinforcement
{
    public class FailureRecord
    {
        public DateTime Time { get; set; }
        public string UserName { get; set; }
        public string DocPath { get; set; }          // документ, где произошло
        public string Description { get; set; }
        public string Severity { get; set; }         // "Warning" / "Error" / ...
        public Guid DefinitionId { get; set; }
        public List<int> FailingElementIds { get; set; } = new List<int>();
    }

    public class FailureRecords : IJsonStorable<FailureRecords>
    {
        public  List<FailureRecord> Records = new List<FailureRecord>();
        public int Count()
        {
            return Records.Count;
        }
        public void Merge(FailureRecords other)
        {
            if (other?.Records == null || other.Records.Count == 0) return;
            Records.AddRange(other.Records);
        }
        public DateTime DateStart() => Records.Count > 0 ? Records.Min(r => r.Time) : DateTime.MinValue;
        public DateTime DateEnd() => Records.Count > 0 ? Records.Max(r => r.Time) : DateTime.MinValue;

    }

    public static class FailureBuffer
    {

        public static FailureRecords Records = new FailureRecords();
        private static readonly object _lock = new object();
        
        

        

        public static void Add(FailureRecord record)
        {
            if (record == null) return;
            lock (_lock)
            {
                Records.Records.Add(record);
                
            }
        }

        private static WriteAndRead<FailureRecords> writeAndRead = null;
        private static void EnsureCurrentFile(DateTime now)
        {
           
            if (writeAndRead != null) return;

            string name = $"{App_Folders.UserName_MachineName}_" +
                             $"{DateTime.Now:yyyyMMdd_HHmmss}";

            writeAndRead = new WriteAndRead<FailureRecords>(
                App_Folders.FolderStatisticsErrors,
                App_Folders.FolderStatisticsErrorsTemp,
                $"{name}.json");
        }
        public static DateTime pastExport = DateTime.MinValue;
        /// <summary>
        /// Сохраняет накопленное в файл сессии и очищает буфер.
        /// Возвращает true при успешной записи.
        /// </summary>
        public static bool Flush()
        {
            DateTime now = DateTime.Now;
            EnsureCurrentFile(now);
            if (writeAndRead == null) { AppErrors.LogError(new Exception("InitializationWriteAndRead()")); return false; }

            bool ok;
            lock (_lock)
            {
                pastExport = now;//пусть будет тут, чтобы повторно часто не вызываться при неудачи
                ok = writeAndRead.Flush(Records);
                if (ok)
                {
                    Records = new FailureRecords();

                }
            }
            if (!ok) { /* лог */ }

            return ok;
        }
    }
}

