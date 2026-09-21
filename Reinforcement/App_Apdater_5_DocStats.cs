using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public enum EDocStatsOptions
    {
        None = 0,
        Save,
        Sync,
        CloseRevit,
    }
    public class DocStats
    {
        public int TotalOps { get; set; }
        public int SaveCount { get; set; }
        public int SyncCount { get; set; }
        public double TotalSyncSeconds { get; set; }
        public double TotalSaveSeconds { get; set; }
        public double MaxSyncSeconds { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public DateTime LastSave { get; set; }

        public DateTime LastSync { get; set; }
        public Dictionary<string, int> CommandHits { get; set; } = new Dictionary<string, int>();

        //активные виды список действий, полезно чтобы знать сколько надо время потратить на то или иное
        public Dictionary<string, (DateTime FirstSeen, DateTime LastSeen, int TotalOps)> ActiveViews { get; set; } = new Dictionary<string, (DateTime FirstSeen, DateTime LastSeen, int TotalOps)>();


        public bool CloseRevit { get; set; } = false;//на этом документе ревит был закрыт
        /// <summary>Складывает метрики из другого DocStats в этот.</summary>
        public void Merge(DocStats other)
        {
            if (other == null) return;

            TotalOps += other.TotalOps;
            SaveCount += other.SaveCount;
            SyncCount += other.SyncCount;
            TotalSyncSeconds += other.TotalSyncSeconds;
            TotalSaveSeconds += other.TotalSaveSeconds;

            if (other.MaxSyncSeconds > MaxSyncSeconds)
                MaxSyncSeconds = other.MaxSyncSeconds;

            // min FirstSeen (учитываем default)
            if (FirstSeen == default || (other.FirstSeen != default && other.FirstSeen < FirstSeen))
                FirstSeen = other.FirstSeen;

            // max LastSeen
            if (other.LastSeen > LastSeen)
                LastSeen = other.LastSeen;

            if (other.LastSave > LastSave)
                LastSave = other.LastSave;
            if (other.LastSync > LastSync)
                LastSync = other.LastSync;

            foreach (var kv in other.CommandHits)
            {
                CommandHits.TryGetValue(kv.Key, out var v);
                CommandHits[kv.Key] = v + kv.Value;
            }
            foreach (var kv in other.ActiveViews)
            {
                ActiveViews[kv.Key] = kv.Value;
            }
        }

        /// <summary>Полная копия (нужна для откатов).</summary>
        public DocStats Clone()
        {
            return new DocStats
            {
                TotalOps = TotalOps,
                SaveCount = SaveCount,
                SyncCount = SyncCount,
                TotalSyncSeconds = TotalSyncSeconds,
                TotalSaveSeconds = TotalSaveSeconds,
                MaxSyncSeconds = MaxSyncSeconds,
                FirstSeen = FirstSeen,
                LastSeen = LastSeen,
                LastSave = LastSave,
                LastSync = LastSync,

                CommandHits = new Dictionary<string, int>(CommandHits),
                ActiveViews = new Dictionary<string, (DateTime FirstSeen, DateTime LastSeen, int TotalOps)>(ActiveViews)
            };
        }
    }
    public class WritePerfStats
    {
//        Медиана totalMs > 200 мс — стоит задуматься.

//P95 > 1000 мс — точно пора выносить запись в Task.Run.

//Max > 5000 мс — асинхронность критична, иначе пользователи будут замечать «залипания» Revit раз в 2 часа.

        public int Count { get; set; }
        public double LastMs { get; set; }
        public double MinMs { get; set; } = double.MaxValue;
        public double MaxMs { get; set; }
        public double TotalMs { get; set; }
        public DateTime LastWriteUtc { get; set; }

        // Раздельные метрики — где именно тормозит
        public double LastSerializeMs { get; set; }
        public double LastIoMs { get; set; }

        // Скользящее окно (для расчёта P95 при анализе)
        public List<double> RecentTotalMs { get; set; } = new List<double>();
        public const int WindowSize = 50;

        public void Add(double serializeMs, double ioMs)
        {
            double totalMs = serializeMs + ioMs;

            Count++;
            LastMs = totalMs;
            LastSerializeMs = serializeMs;
            LastIoMs = ioMs;
            TotalMs += totalMs;
            LastWriteUtc = DateTime.UtcNow;

            if (totalMs < MinMs) MinMs = totalMs;
            if (totalMs > MaxMs) MaxMs = totalMs;

            RecentTotalMs.Add(totalMs);
            if (RecentTotalMs.Count > WindowSize)
                RecentTotalMs.RemoveAt(0);
        }

        /// <summary>Медиана по скользящему окну (грубая оценка "типичного" времени).</summary>
        public double MedianRecentMs()
        {
            if (RecentTotalMs.Count == 0) return 0;
            var sorted = RecentTotalMs.OrderBy(x => x).ToList();
            return sorted[sorted.Count / 2];
        }

        /// <summary>P95 по скользящему окну.</summary>
        public double P95RecentMs()
        {
            if (RecentTotalMs.Count == 0) return 0;
            var sorted = RecentTotalMs.OrderBy(x => x).ToList();
            int idx = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
            if (idx < 0) idx = 0;
            if (idx >= sorted.Count) idx = sorted.Count - 1;
            return sorted[idx];
        }

        public void Merge(WritePerfStats other)
        {
            if (other == null) return;

            Count += other.Count;
            TotalMs += other.TotalMs;
            LastMs = other.LastMs > 0 ? other.LastMs : LastMs;
            LastSerializeMs = other.LastSerializeMs > 0 ? other.LastSerializeMs : LastSerializeMs;
            LastIoMs = other.LastIoMs > 0 ? other.LastIoMs : LastIoMs;
            if (other.LastWriteUtc > LastWriteUtc) LastWriteUtc = other.LastWriteUtc;

            if (other.MinMs < MinMs) MinMs = other.MinMs;
            if (other.MaxMs > MaxMs) MaxMs = other.MaxMs;

            // скользящее окно — просто берём последнее (свежайшее)
            if (other.RecentTotalMs != null && other.RecentTotalMs.Count > 0)
                RecentTotalMs = new List<double>(other.RecentTotalMs);
        }

        public WritePerfStats Clone()
        {
            return new WritePerfStats
            {
                Count = Count,
                LastMs = LastMs,
                MinMs = MinMs == double.MaxValue ? 0 : MinMs,
                MaxMs = MaxMs,
                TotalMs = TotalMs,
                LastWriteUtc = LastWriteUtc,
                LastSerializeMs = LastSerializeMs,
                LastIoMs = LastIoMs,
                RecentTotalMs = new List<double>(RecentTotalMs)
            };
        }
    }
}