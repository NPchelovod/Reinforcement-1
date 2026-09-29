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
        Invoker=1,// от событий апдейтера
        Save,
        Sync,
        CloseRevit,
    }
    public class DocStats
    {
        
        public int TotalOps { get; set; }
        public int SaveCount { get; set; }
        public int SyncCount { get; set; }

        public int CountActiveDoc {  get; set; }//смена активного документа и снова возврат к нему

        public double LastSyncSeconds { get; set; }
        public double LastSaveSeconds { get; set; }

        public double TotalWorkSeconds { get; set; }
        public double MaxSyncSeconds { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public DateTime LastSave { get; set; }

        public DateTime LastSync { get; set; }

        public string Guid { get; set; }//гуид документа при первом создании но если шаблон то его дубль
        public string CreationGUID { get; set; }//гуид документа опять может совпадать но вдруг не совпадает
        public string Name { get; set; }//имя документа

        public int Warnings { get; set; }
        public Dictionary<string, int> CommandHits { get; set; } = new Dictionary<string, int>();
        public Dictionary<int, (int cliks, double time)> CommandHitsHours { get; set; } = new Dictionary<int, (int cliks, double time)>();// словарь кликов по часам: час и количество кликов

        //активные виды список действий, полезно чтобы знать сколько надо время потратить на то или иное
        public Dictionary<string, ViewStat> ActiveViews { get; set; } = new Dictionary<string, ViewStat>();


        public bool CloseRevit { get; set; } = false;//на этом документе ревит был закрыт
        /// <summary>Складывает метрики из другого DocStats в этот.</summary>
        public void Merge(DocStats other)
        {
            if (other == null) return;

            TotalOps += other.TotalOps;
            SaveCount += other.SaveCount;
            SyncCount += other.SyncCount;
            LastSyncSeconds += other.LastSyncSeconds;
            LastSaveSeconds += other.LastSaveSeconds;
            TotalWorkSeconds += other.TotalWorkSeconds;

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

            if(string.IsNullOrEmpty(Guid))
            {
                Guid = other.Guid;
            }

            if (string.IsNullOrEmpty(CreationGUID))
            {
                CreationGUID = other.CreationGUID;
            }

            CountActiveDoc += other.CountActiveDoc;
            foreach (var kv in other.CommandHits)
            {
                CommandHits.TryGetValue(kv.Key, out var v);
                CommandHits[kv.Key] = v + kv.Value;
            }

            foreach (var kv in other.CommandHitsHours)
            {
                CommandHitsHours.TryGetValue(kv.Key, out var v);
                CommandHitsHours[kv.Key] = (v.cliks + kv.Value.cliks, v.time + kv.Value.time);
            }
            foreach (var kv in other.ActiveViews)
            {
                if (ActiveViews.TryGetValue(kv.Key, out var existing))
                    existing.Merge(kv.Value);
                else
                    ActiveViews[kv.Key] = kv.Value.Clone();
            }
        }

        /// <summary>Полная копия (нужна для откатов).</summary>
        public DocStats Clone()
        {
            var viewsCopy = new Dictionary<string, ViewStat>(ActiveViews.Count);
            foreach (var kv in ActiveViews)
                viewsCopy[kv.Key] = kv.Value.Clone();

            return new DocStats
            {
                TotalOps = TotalOps,
                SaveCount = SaveCount,
                SyncCount = SyncCount,
                LastSyncSeconds = LastSyncSeconds,
                LastSaveSeconds = LastSaveSeconds,
                TotalWorkSeconds = TotalWorkSeconds,
                MaxSyncSeconds = MaxSyncSeconds,
                FirstSeen = FirstSeen,
                LastSeen = LastSeen,
                LastSave = LastSave,
                LastSync = LastSync,
                Guid= Guid,
                CreationGUID= CreationGUID,
                Name = Name,
                CommandHits = new Dictionary<string, int>(CommandHits),
                CommandHitsHours =new Dictionary<int, (int cliks, double time)> (CommandHitsHours),
                ActiveViews = viewsCopy,
                Warnings= Warnings,
                CountActiveDoc= CountActiveDoc,
            };
        }
    }

    public class ViewStat
    {
        //может тут тоже добавить почасовое?
        public string NameView { get; set; }
        public int ViewId { get; set; }
        public string NameSheet { get; set; }//имя листа
        public string NumSheet {  get; set; }
        public int IdSheet { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public DateTime DateInSheet { get; set; }//дата размещения на листе впервые

        public int TotalOps { get; set; }

        public int CountActiveView { get; set; }//смена активного документа и снова возврат к нему
        public double TotalWorkSeconds { get; set; }
        public Dictionary<int, (int cliks, double time)> CommandHitsHours { get; set; } = new Dictionary<int, (int cliks, double time)>();// словарь кликов по часам: час и количество кликов

        public void Merge(ViewStat other)
        {
            if (other == null) return;
            TotalOps += other.TotalOps;

            if (FirstSeen == default || (other.FirstSeen != default && other.FirstSeen < FirstSeen))
                FirstSeen = other.FirstSeen;
            if (other.LastSeen > LastSeen)
                LastSeen = other.LastSeen;

            if (DateInSheet != DateTime.MinValue && DateInSheet < other.DateInSheet)
            {

            }
            else { DateInSheet = other.DateInSheet; }

            TotalWorkSeconds += other.TotalWorkSeconds;
            // обновляем имя листа, если у нас его ещё нет, а там есть
            if (string.IsNullOrEmpty(NameSheet) || NameSheet == "None")
                NameSheet = other.NameSheet;
            if (string.IsNullOrEmpty(NumSheet) || NumSheet == "None")
            {
                NumSheet = other.NumSheet;
                IdSheet = other.IdSheet;
            }

            if (string.IsNullOrEmpty(NameView))
                NameView = other.NameView;

            if(ViewId<1)
            {
                ViewId = other.ViewId;
            }
            CountActiveView += other.CountActiveView;
            foreach (var kv in other.CommandHitsHours)
            {
                CommandHitsHours.TryGetValue(kv.Key, out var v);
                CommandHitsHours[kv.Key] = (v.cliks + kv.Value.cliks, v.time + kv.Value.time);
            }
        }

        public ViewStat Clone()
        {
            return new ViewStat
            {
                NameView = NameView,
                NameSheet = NameSheet,
                NumSheet = NumSheet,
                IdSheet = IdSheet,
                FirstSeen = FirstSeen,
                LastSeen = LastSeen,
                DateInSheet = DateInSheet,
                TotalOps = TotalOps,
                TotalWorkSeconds = TotalWorkSeconds,
                ViewId= ViewId  ,
                CountActiveView= CountActiveView,
                CommandHitsHours = new Dictionary<int, (int cliks, double time)>(CommandHitsHours),
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