using System;
using System.Collections.Generic;
using System.Linq;
namespace Reinforcement
{
    public sealed class BatchReport
    {
        private readonly List<long> completed = new List<long>();
        private readonly List<string> skipped = new List<string>();
        public IReadOnlyList<long> Completed => completed.AsReadOnly();
        public IReadOnlyList<string> Skipped => skipped.AsReadOnly();
        public void AddCompleted(long id) { completed.Add(id); }
        public void AddSkipped(long id, string reason) { skipped.Add($"ID {id}: {reason}"); }
        public string FormatSummary() => $"Создано марок: {completed.Count}\nПропущено: {skipped.Count}";
        public string FormatDetails() => string.Join(Environment.NewLine, skipped);
    }
}
