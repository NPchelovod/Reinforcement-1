using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using System.Text.Json;
using Autodesk.Revit.DB;
using System.Data;
namespace Reinforcement
{
    public partial class StatLoad
    {
        // Что именно строить
        public ReportKind Kind { get; set; } = ReportKind.UsersPerFilePerDay;
        public DataTable GetDataTable()
        {
            switch (Kind)
            {
                case ReportKind.UsersPerFilePerDay:
                    return BuildUsersPerFilePerDay();
                case ReportKind.ClicksPerFilePerDay:
                    return BuildClicksPerFilePerDay();
                case ReportKind.UsersPerDay:
                    return BuildUsersPerDay();
                case ReportKind.FilesPerUser:
                    return BuildFilesPerUser();
                default:
                    return new DataTable();
            }
           
        }

        // ==== Методы построения таблиц ====

        private DataTable BuildUsersPerFilePerDay()
        {
            var table = new DataTable();
            table.Columns.Add("Дата", typeof(DateTime));

            var files = FilterFiles().ToList();
            var columns = AddFileColumns(table, files);

            //var allDates = files
            //    .SelectMany(f => f.Value.DatesProjectCountUsers.Keys)
            //    .Distinct()
            //    .OrderBy(d => d);

            var allDates = files
        .SelectMany(f => f.Value.DatesProjectCountUsers.Keys)
        .Select(d => d.Date)          // ← на всякий случай
        .Distinct()
        .OrderBy(d => d);

            foreach (var date in allDates)
            {
                var row = table.NewRow();
                row["Дата"] = date;

                foreach (var kv in columns)
                {
                    kv.Value.DatesProjectCountUsers.TryGetValue(date, out int count);
                    row[kv.Key] = count;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private DataTable BuildClicksPerFilePerDay()
        {
            var table = new DataTable();
            table.Columns.Add("Дата", typeof(DateTime));

            var files = FilterFiles().ToList();
            var columns = AddFileColumns(table, files);

            //var allDates = files
            //    .SelectMany(f => f.Value.DatesProjectCountClick.Keys)
            //    .Distinct()
            //    .OrderBy(d => d);
            var allDates = files
            .SelectMany(f => f.Value.DatesProjectCountUsers.Keys)
            .Select(d => d.Date)          // ← на всякий случай
            .Distinct()
            .OrderBy(d => d);
            foreach (var date in allDates)
            {
                var row = table.NewRow();
                row["Дата"] = date;

                foreach (var kv in columns)
                {
                    kv.Value.DatesProjectCountClick.TryGetValue(date, out int count);
                    row[kv.Key] = count;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private DataTable BuildUsersPerDay()
        {
            var table = new DataTable();
            table.Columns.Add("Дата", typeof(DateTime));

            var files = FilterFiles().ToList();

            var allUsers = files
                .SelectMany(f => f.Value.UserNames)
                .Distinct()
                .OrderBy(u => u)
                .ToList();

            foreach (var user in allUsers)
                table.Columns.Add(user, typeof(int));

            var allDates = files
                .SelectMany(f => f.Value.DatesUsers.Keys)
                .Distinct()
                .OrderBy(d => d);

            foreach (var date in allDates)
            {
                var row = table.NewRow();
                row["Дата"] = date;

                foreach (var user in allUsers)
                {
                    bool worked = files.Any(f =>
                        f.Value.DatesUsers.TryGetValue(date, out var set) &&
                        set.Contains(user));
                    row[user] = worked ? 1 : 0;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private DataTable BuildFilesPerUser()
        {
            var table = new DataTable();
            table.Columns.Add("Пользователь", typeof(string));

            var files = FilterFiles().ToList();
            var columns = AddFileColumns(table, files);

            var allUsers = files
                .SelectMany(f => f.Value.UserNames)
                .Distinct()
                .OrderBy(u => u)
                .ToList();

            foreach (var user in allUsers)
            {
                var row = table.NewRow();
                row["Пользователь"] = user;

                foreach (var kv in columns)
                {
                    int sum = 0;
                    foreach (var dkv in kv.Value.DatesProjectCountClick)
                    {
                        if (kv.Value.DatesUsers.TryGetValue(dkv.Key, out var set) &&
                            set.Contains(user))
                        {
                            sum += dkv.Value / Math.Max(1, set.Count);
                        }
                    }
                    row[kv.Key] = sum;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        // ==== Вспомогательное ====

        private IEnumerable<KeyValuePair<string, FileDataLook>> FilterFiles()
        {
            foreach (var kv in DictFileDataLook)
            {
                if (ServerOnly && !kv.Key.StartsWith(PrefixServer))
                    continue;
                yield return kv;
            }
        }

        private static Dictionary<string, FileDataLook> AddFileColumns(
            DataTable table,
            List<KeyValuePair<string, FileDataLook>> files)
        {
            var map = new Dictionary<string, FileDataLook>();
            int idx = 0;

            foreach (var kv in files)
            {
                string shortName = Path.GetFileName(kv.Key);
                if (string.IsNullOrEmpty(shortName))
                    shortName = $"Файл{idx}";
                idx++;

                string columnName = shortName;
                int suffix = 1;
                while (table.Columns.Contains(columnName))
                    columnName = $"{shortName} ({suffix++})";

                table.Columns.Add(columnName, typeof(int));
                map[columnName] = kv.Value;
            }

            return map;
        }
    }
}
