using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Reinforcement
{
    public partial class PeriodicHandler
    {

        public static bool RegWriterAvtor=true;
        public static string NameAvtor = "ЕС_Автор";
        public static string NameSoAvtor = "ЕС_Посл Автор";
        public const int MaxCoAuthorsLength = 180;     // лимит строки соавторов
        public static bool longAvtors = true;           // хранить нескольких авторов через запятую
        private static void AuthorWriter(DateTime dateTime, Document doc, Element element, ETypeChangeEl eTypeChangeEl)
        {
            // 1. Параметр для автора (или запасной)
            Parameter authorParam = element.LookupParameter(NameAvtor);
            if (authorParam == null || authorParam.IsReadOnly) return; // нет нужного параметра – пропускаем

            Parameter coAuthorParam = element.LookupParameter(NameSoAvtor);

            string authorValue = authorParam.AsString();

            string commandName = null;
            if (eTypeChangeEl == ETypeChangeEl.AddedElements)
            {
                commandName = "нов";//то что новые элемент
            }
            else if (ETypeChangeEl.GeomModifiedElements == eTypeChangeEl)
            {
                commandName = "гео";//изменилась геометрия
            }
            else
            {
                commandName = "мод";//изменился скорее всего параметр
            }

            // Для нового элемента записываем автора, если поле пустое
            if (string.IsNullOrEmpty(authorValue) || eTypeChangeEl == ETypeChangeEl.AddedElements)//isNewElement
            {
               //добавочный ли автор
                // Автор — это "кто создал", тип смены тут не нужен
                string newAuthorValue = new AuthorEntry(_userName, _nowDate, commandName).ToString();
                SetParam(authorParam, newAuthorValue);

                if (coAuthorParam != null)
                {
                    SetParam(coAuthorParam, newAuthorValue);
                }
            }
            else if(coAuthorParam != null)
            {
                // Если параметр соавтора существует, обновляем его
                string pastCoAuthors = coAuthorParam.AsString();
                string newCoAuthors = BuildNewCoAuthors(pastCoAuthors, commandName);
                SetParam(coAuthorParam, newCoAuthors);
            }

        }
        /// <summary>
        /// Безопасная установка значения параметра (только если оно изменилось).
        /// </summary>
        public static bool SetParam(Parameter param, string value)
        {
            
            string pastValue = param.AsString();

            if (!string.IsNullOrEmpty(pastValue) && pastValue == value) { return false; }
            
            param.Set(value);
            
            return true;
        }
        /// <summary>
        /// Формирует новую строку соавторов на основе предыдущей.
        /// </summary>
        private static string BuildNewCoAuthors(string pastValue, string comand)
        {
            var newEntry = new AuthorEntry(_userName, _nowDate, comand);

            if (!longAvtors)
                return newEntry.ToString();

            // 1) Разбираем существующую строку
            var entries = new List<AuthorEntry>();
            if (!string.IsNullOrEmpty(pastValue))
            {
                foreach (var part in pastValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (AuthorEntry.TryParse(part, out var e))
                        entries.Add(e);
                }
            }
            // 2) ГЛАВНАЯ ЗАЩИТА ОТ РЕКУРСИИ:
            // Если в списке уже есть запись этого пользователя с точно такой же датой (вплоть до часа),
            // мы возвращаем строку без изменений. Команда (нов/мод/гео) при этом игнорируется.
            // Так как строка не меняется, SetParam вернет false и не вызовет param.Set().
            if (entries.Any(e => e.Name == newEntry.Name && e.Date == newEntry.Date))
            {
                return pastValue;
            }

            // 2) Убираем дубликат "имя+дата" (правка в тот же час)
            //entries.RemoveAll(e => e.Name == newEntry.Name && e.Date == newEntry.Date);

            //в тот же день дубли убираем
            entries.RemoveAll(e => e.Name == newEntry.Name && e.IsSameDayAs(newEntry));

            // 3) Точная дедупликация
            var seen = new HashSet<string>();
            entries = entries.Where(e => seen.Add(e.ToString())).ToList();

            // 4) Свежая запись — в начало
            entries.Insert(0, newEntry);

            // 5) Помещается — выходим сразу
            string result = SerializeEntries(entries);
            if (result.Length <= MaxCoAuthorsLength)
                return result;

            // 6) Слишком длинно — сначала схлопываем последовательных авторов
            entries = CollapseConsecutiveSameAuthor(entries);
            result = SerializeEntries(entries);

            // 7) Всё ещё длинно — удаляем с конца (самые старые)
            while (result.Length > MaxCoAuthorsLength && entries.Count > 1)
            {
                entries.RemoveAt(entries.Count - 1);
                result = SerializeEntries(entries);
            }

            return result;
        }
        /// <summary>
        /// Схлопывает подряд идущие записи одного автора (по порядку в строке = от новых к старым).
        /// Оставляет более новую запись. Если у оставляемой тип не задан, а у отбрасываемой — был,
        /// тип наследуется, чтобы не потерять информацию о характере правки.
        /// </summary>
        private static List<AuthorEntry> CollapseConsecutiveSameAuthor(List<AuthorEntry> entries)
        {
            if (entries.Count <= 1) return entries;

            var result = new List<AuthorEntry>(entries.Count) { entries[0] };
            for (int i = 1; i < entries.Count; i++)
            {
                var prev = result[result.Count - 1];
                var cur = entries[i];
                if (cur.Name == prev.Name)
                {
                    // Тот же автор подряд — старая запись отбрасывается.
                    if (string.IsNullOrEmpty(prev.ChangeType) && !string.IsNullOrEmpty(cur.ChangeType))
                        prev.ChangeType = cur.ChangeType;
                    continue;
                }
                result.Add(cur);
            }
            return result;
        }
        private static string SerializeEntries(List<AuthorEntry> entries)
        => string.Join(", ", entries.Select(e => e.ToString()));
    }
    /// <summary>
    /// Одна запись в поле «соавтор»:
    ///   Name_dd.MM.yy.HH          — без типа
    ///   Name_dd.MM.yy.HH_type     — с типом изменения (1–2 символа)
    /// </summary>
    public class AuthorEntry
    {
        public string Name { get; set; }
        public string Date { get; set; }   // dd.MM.yy.HH
        public string ChangeType { get; set; }   // может быть null/пусто

        public AuthorEntry() { }
        public AuthorEntry(string name, string date, string changeType)
        {
            Name = name;
            Date = date;
            ChangeType = string.IsNullOrWhiteSpace(changeType) ? null : changeType.Trim();
        }

        public override string ToString()
            => string.IsNullOrEmpty(ChangeType)
                ? $"{Name}_{Date}"
                : $"{Name}_{Date}_{ChangeType}";

        private static readonly System.Text.RegularExpressions.Regex DateRegex =
            new System.Text.RegularExpressions.Regex(@"^\d{2}\.\d{2}\.\d{2}\.\d{2}$");

        public static bool TryParse(string s, out AuthorEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            var parts = s.Split('_');
            if (parts.Length < 2) return false;

            int dateIdx;
            string changeType = null;

            // сначала считаем, что типа нет — тогда последняя часть должна быть датой
            if (DateRegex.IsMatch(parts[parts.Length - 1]))
            {
                dateIdx = parts.Length - 1;
            }
            else if (parts.Length >= 3 && DateRegex.IsMatch(parts[parts.Length - 2]))
            {
                // иначе — второй с конца должен быть датой, а последний кусок это тип
                dateIdx = parts.Length - 2;
                changeType = parts[parts.Length - 1];
            }
            else
            {
                return false;
            }

            string name = string.Join("_", parts.Take(dateIdx));
            if (string.IsNullOrEmpty(name)) return false;

            entry = new AuthorEntry(name, parts[dateIdx], changeType);
            return true;
        }

        /// <summary>
        /// Совпадает ли дата с другой записью в пределах одного календарного дня.
        /// Формат Date — "dd.MM.yy.HH", поэтому день — первые 8 символов.
        /// </summary>
        public bool IsSameDayAs(AuthorEntry other)
        {
            if (other == null) return false;
            if (string.IsNullOrEmpty(Date) || string.IsNullOrEmpty(other.Date)) return false;

            // dd.MM.yy.HH → dd.MM.yy = первые 8 символов
            if (Date.Length < 8 || other.Date.Length < 8) return false;

            return string.CompareOrdinal(Date, 0, other.Date, 0, 8) == 0;
        }
    }
}
