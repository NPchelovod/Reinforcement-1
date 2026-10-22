using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Updaters;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using static Autodesk.Revit.DB.SpecTypeId;

namespace Reinforcement
{
   
    public class AutoFillNoteUpdater 
    {
        

        // === Конфигурация ===
        public static bool regWriterAvtor = true;       // записывать автора
       
        
        public static bool longAvtors = true;           // хранить нескольких авторов через запятую


        public static bool correctGroup = true;
        //public static int CommandInt = 3;
        
        public static string NameAvtor = "ЕС_Автор";
        public static string NameSoAvtor = "ЕС_Посл Автор";
        //public static string NamePrimeh = "ADSK_Примечание";

        // === Временные данные ===
        private static DateTime _lastExecutionTime = DateTime.MinValue;
        //private static DateTime _lastExecutionTimeUpdate = DateTime.MinValue;
        private static readonly TimeSpan _minimumInterval = TimeSpan.FromMilliseconds(200);

        //private static readonly TimeSpan _minimumIntervaCommand = TimeSpan.FromMilliseconds(500);//если прошло более 5 секунд то обновляем автора из апдейтера

        // === Текущие значения (пересчитываются при каждом реальном выполнении) ===

        

        private static string _nowDate;                // dd.MM.yy.HH

        public const int MaxCoAuthorsLength = 170;     // лимит строки соавторов


        public static string UserName = "";
        /// <summary>
        /// Основной метод, вызываемый диспетчером для обработки добавленных/изменённых элементов.
        /// </summary>
        private static int _inUpdate;
        //private static int _inUpdateProcess;

        private static int _inUpdateLong;
        public static void AvtorUpdaterLong()
        {
            if (!regWriterAvtor) return;
            bool isUpdate = false;
            try
            {
                // Защита от рекурсивного вызова
                // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
                if (Interlocked.CompareExchange(ref _inUpdateLong, 1, 0) != 0)
                {
                    return;
                }
                isUpdate = true;
                //отложенное обновление авторов
                // Копируем элементы из очереди, чтобы безопасно перебирать и менять флаги
                var snapshots = AnyChange.SnapshotModificationElementsQueue.ToArray();
                DateTime now = DateTime.Now;

                foreach (var snapshot in snapshots)
                {
                    //если старая модификация или если известна команда
                    if (snapshot.IsAvtorUpdate) { continue; }
                    if (!snapshot.NotCommandAndChangedFind)//в иных случаях всегда обрабатываем
                    {
                        if (snapshot.SPastCommand == null)
                        {
                            //то обрабатываем только если по секундам уже старая
                            if ((now - snapshot.DateCreate) < AnyChange.ComandAndChangedMaxTime)
                            { continue; }
                        }

                    }

                    //теперь обработка
                    ProcessAvtorUpdate(now, snapshot);//обновляем отсюда
                }
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
            }
            finally
            {
                if (isUpdate)
                {
                    Interlocked.Exchange(ref _inUpdateLong, 0);
                }
            }


        }


        public static void AvtorUpdater(SnapshotModificationElements snapshotModificationElements)
        {
            if (!regWriterAvtor) return;
            bool isUpdate=false;
            try
            {
                // Защита от рекурсивного вызова
                // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
                if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0 || snapshotModificationElements.Doc==null)
                {
                    return;
                }
                isUpdate = true;

               // Проверка временного интервала сразу, до каких-либо действий
                DateTime now = DateTime.Now;
                if (now - _lastExecutionTime < _minimumInterval)
                {
                    return;
                }

                // Обновляем время последнего выполнения
                _lastExecutionTime = now;

                ProcessAvtorUpdate(now, snapshotModificationElements);//обновляем отсюда
                
            }
            catch(Exception ex) 
            {
                App_Apdater_1.AppErrors.LogError(ex);
            }
            finally
            {
                if (isUpdate)
                {
                    Interlocked.Exchange(ref _inUpdate, 0);
                }
            }
        }
       
        private static  int _commandSimvols = 3;
        private static string cmdName = null;
        public static void ProcessAvtorUpdate(DateTime now, SnapshotModificationElements snapshotModificationElements)
        {
            
            _nowDate = now.ToString("dd.MM.yy.HH");
                

            //имя команды
            cmdName = snapshotModificationElements.CommandName;

                
            cmdName = string.IsNullOrEmpty(cmdName)
                    ? null
                    : cmdName.Substring(0, Math.Min(_commandSimvols, cmdName.Length));

            snapshotModificationElements.IsAvtorUpdate = true;

            // Обрабатываем добавленные (автор при создании)

            ProcessElements(now, snapshotModificationElements, isNewElement: true);
                

            // Обрабатываем изменённые (автор и соавтор)
                
            ProcessElements(now, snapshotModificationElements, isNewElement: false);
                
        }

        /// <summary>
        /// Безопасная установка значения параметра (только если оно изменилось).
        /// </summary>
        public static bool SetParam(Parameter param, string value)
        {
            if(param == null) { return false; }
            string pastValue = param.AsString();

            if(!string.IsNullOrEmpty(pastValue) && pastValue == value) {  return false; }
            try
            {
                param.Set(value);
            }
            catch (Exception ex)
            {
               App_Apdater_1.AppErrors.LogError(ex);
                return false;
            }
            return true;
        }
        private static bool IsGroupCurrentlyEdited(Document doc, Group group)
        {
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return false;
            Document activeDoc = uidoc.Document;
            // В режиме редактирования группы активный документ – временный, типа GroupDocument

            return activeDoc.GetType() == typeof(Document) && activeDoc.IsModifiable; // э
        }
        public static bool IsInGroupEditMode(Document doc)
        {
         
            View activeView = doc.ActiveView;
            // Если у активного вида есть GroupId, значит мы находимся в режиме редактирования группы
            return activeView.GroupId != ElementId.InvalidElementId;
        }
        /// <summary>
        /// Обработка списка элементов: запись автора (при необходимости) и соавтора.
        /// </summary>
        /// 
        private static void ProcessElements(DateTime now, SnapshotModificationElements snapshotModificationElements, bool isNewElement)
        {

            var elements = isNewElement? snapshotModificationElements.AddedElements : snapshotModificationElements.ModifiedElements;
            Document doc = snapshotModificationElements.Doc;

            foreach (Element element in elements)
            {
               
                if (element == null) continue;
                // Проверка: если элемент входит в группу и группа не в режиме редактирования – пропускаем

                ElementId groupId = element.GroupId;
                if (groupId != ElementId.InvalidElementId)
                {
                    if (!correctGroup)
                    {
                        continue;
                    }
                    // Получаем экземпляр группы по GroupId элемента
                    Group instanceGroup = doc.GetElement(groupId) as Group;
                    if (instanceGroup == null)
                    {
                        // Странная ситуация — GroupId есть, а Group нет. Пропускаем.
                        continue;
                    }
                    // Id типа группы (то, что лежит в App.OnGroupCurrent.Id)
                    ElementId groupTypeId = instanceGroup.GetTypeId();
                    // Сравниваем по значению, а не по ссылке
                    bool sameGroupType =
                        groupTypeId != null
                        && App.OnGroupCurrent.Id != null
                        && groupTypeId.Equals(App.OnGroupCurrent.Id);


                    if (!sameGroupType)
                    {  continue; }
                        
                }

                // 1. Параметр для автора (или запасной)
                Parameter authorParam = element.LookupParameter(NameAvtor);
               

                if (authorParam == null || authorParam.IsReadOnly) continue; // нет нужного параметра – пропускаем
               
                string authorValue = authorParam.AsString();

               
                // Для нового элемента записываем автора, если поле пустое
                if (string.IsNullOrEmpty(authorValue))//isNewElement
                {
                    // Автор — это "кто создал", тип смены тут не нужен
                    authorValue = new AuthorEntry(UserName, _nowDate, cmdName).ToString();
                    SetParam(authorParam, authorValue);
                  
                }

                // 2. Параметр соавтора
                Parameter coAuthorParam = element.LookupParameter(NameSoAvtor);
                if (coAuthorParam != null)
                {
                    // Если параметр соавтора существует, обновляем его
                    string pastCoAuthors = coAuthorParam.AsString();
                    string newCoAuthors = BuildNewCoAuthors(pastCoAuthors);
                    SetParam(coAuthorParam, newCoAuthors);
                }
                else
                {
                    // Если параметра соавтора нет, записываем в тот же параметр автора (по вашей логике)
                    // Убедитесь, что это действительно нужно! Возможно, лучше просто пропустить.
                    string newValue = BuildNewCoAuthors(authorValue);
                    SetParam(authorParam, newValue);
                }
            }
        }


        /// <summary>
        /// Формирует новую строку соавторов на основе предыдущей.
        /// </summary>
        private static string BuildNewCoAuthors(string pastValue)
        {
            var newEntry = new AuthorEntry(UserName, _nowDate, cmdName);

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

            // 2) Убираем дубликат "имя+дата" (правка в тот же час)
            //entries.RemoveAll(e => e.Name == newEntry.Name && e.Date == newEntry.Date);

            //в тот же день дубли убираем
            entries.RemoveAll(e => e.Name == newEntry.Name &&  e.IsSameDayAs(newEntry));

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
        public AuthorEntry(string name, string date, string changeType = null)
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
