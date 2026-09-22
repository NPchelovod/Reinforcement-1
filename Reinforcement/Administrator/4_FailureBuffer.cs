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
    public static class FailureBuffer
    {
        private static readonly object _lock = new object();
        private static readonly List<FailureRecord> _records = new List<FailureRecord>();
        private static bool _dirty;

        public static string FolderPath = @"Y:\Revit\_ЕС BIM_Плагин\0_Разработчику\_statistics\Failures";

        public static void Add(FailureRecord record)
        {
            if (record == null) return;
            lock (_lock)
            {
                _records.Add(record);
                _dirty = true;
            }
        }

        public static bool HasData
        {
            get { lock (_lock) return _dirty && _records.Count > 0; }
        }

        /// <summary>
        /// Сохраняет накопленное в файл сессии и очищает буфер.
        /// Возвращает true при успешной записи.
        /// </summary>
        public static bool Flush()
        {
            List<FailureRecord> snapshot;
            lock (_lock)
            {
                if (!_dirty || _records.Count == 0) return true;
                snapshot = new List<FailureRecord>(_records);
            }

            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);

                string name = $"{Environment.UserName}_{Environment.MachineName}_" +
                              $"{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string path = Path.Combine(FolderPath, name);

                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(snapshot, options);

                // Атомарная запись через tmp
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Replace(tmp, path, null, true);
                else
                    File.Move(tmp, path);

                // Успех — очищаем буфер
                lock (_lock)
                {
                    _records.Clear();
                    _dirty = false;
                }
                return true;
            }
            catch
            {
                // Не удалось — оставляем данные в памяти,
                // попробуем при следующем Flush (или при закрытии Revit)
                return false;
            }
        }
    }
}

