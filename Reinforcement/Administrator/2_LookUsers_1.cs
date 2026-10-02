using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    public partial class LookUsers
    {

        public string UserName { get; set; }

        public string VersionName => SettingsWindow.GetDatePluginText;
        //просмотр характеристик пользователя и их обновление и сохранение 
        // ==== Данные ====
        // ВАЖНО: инициализируем, иначе первый Update упадёт на NRE
        public Dictionary<DateTime, Dictionary<string, DocStats>> DictDateDocStats { get; set; }
            = new Dictionary<DateTime, Dictionary<string, DocStats>>();
        /// <summary>Статистика по времени записи JSON (для оценки необходимости async).</summary>
        public WritePerfStats WriteStats { get; set; } = new WritePerfStats();

        public DateTime PassDateWrite { get; set; } = DateTime.Now;//прошлая дата записи
        public DateTime DateDay { get; set; } = DateTime.Now.Date;//дата текущего дня

       

        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();
        private static int _inUpdate;

        public DateTime Now = DateTime.Now;//для быстрого доступа к дате
        
        int _hour = 0;
        public void Update(string explicitCommandName = null, EDocStatsOptions commandType = 0)
        {
            bool acquired = false;
            
            try
            {
                // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
                if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
                    return;
                acquired = true;
                lock (_lock)
                {
                    Now = DateTime.Now;//от этой даты всё и считаем чтобы было надежно
                    //hourAligned = new DateTime(Now.Year, Now.Month, Now.Day, Now.Hour, 0, 0);//кратно часу дата
                    _hour = Now.Hour;
                    if (ProcessWriter(explicitCommandName, commandType))
                    {
                        if (Now - PassDateWrite > FlushInterval)
                        {
                            FlushInternalLock();
                        }
                    }
                }

            }
            catch
            {
                // Статистика не должна ломать основную команду
            }
            finally
            {
                if (acquired)
                {
                    Interlocked.Exchange(ref _inUpdate, 0);   // обязательно сбросить, даже при исключении
                }
            }
        }


        private bool ProcessWriter(string explicitCommandName, EDocStatsOptions commandType)
        {
            if(OnlyOneCommand())
            {
                return false;
            }
            string key = explicitCommandName ?? GetCallerName(commandType);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            // ==== THROTTLE ====
            //if (ShouldThrottle(key))//защита от дублей
            //    return false;
            // ==== /THROTTLE ====


            if (commandType == EDocStatsOptions.CloseRevit)
            {
                //так как документ к тому времени бывает == null;
                if(pastDocStat!=null)
                {
                    pastDocStat.CloseRevit = true;
                    pastDocStat.LastSeen = DateTime.Now;
                }
            }

            UIDocument uiDoc = RevitAPI.UiDocument;
            if (uiDoc == null) { return false; }
            Document doc = uiDoc.Document;
            if (doc == null) { return false; }

            DateDay = Now.Date;   // ← добавить


            string nameDoc = doc.PathName;

            ModelPath centralModelPath = doc.GetWorksharingCentralModelPath();
            if (centralModelPath != null)
            {
                string centralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                // centralPath — это и есть путь к центральной модели string centralPath = BasicFileInfo.Extract(doc.PathName).CentralPath;
                if (!string.IsNullOrEmpty(centralPath))
                {
                    nameDoc = centralPath;
                }
            }
            if (string.IsNullOrEmpty(nameDoc))
            {
                //значит документ не сохранен просто возвращаем его имя
                nameDoc = doc.Title;
                if (string.IsNullOrEmpty(nameDoc))
                {
                    return false;
                }
            }

            

            //команды кликов
            if (!DictDateDocStats.TryGetValue(DateDay, out var docStats))
            {
                docStats = new Dictionary<string, DocStats>();
                DictDateDocStats[DateDay] = docStats;
            }
            if (!docStats.TryGetValue(nameDoc, out var docStat))
            {
                docStat = new DocStats();
                docStats[nameDoc] = docStat;

                //заполняем даты открытия данной модели
                docStat.FirstSeen = Now;
                docStat.LastSeen = Now;
               
                docStat.Name = System.IO.Path.GetFileNameWithoutExtension(nameDoc);
                ProjectInfo info = doc.ProjectInformation;

                if (info != null)
                {
                    string documentGuid = info.UniqueId; // Уникальный GUID проекта совпадает если шаблон один

                    if(!string.IsNullOrEmpty(documentGuid))
                    {
                        docStat.Guid= documentGuid;
                    }
                }
                // Получаем 
                string creationGUID=doc.CreationGUID.ToString();
                if (!string.IsNullOrEmpty(creationGUID))
                {
                    docStat.CreationGUID= creationGUID;
                }

                int warnings = doc.GetWarnings().Count();//ошибок в документе
                docStat.Warnings= warnings;
            }

            ProcessWriterViewStat(docStat,doc);//статистика по виду
            ProcessWriterDocStat(docStat, key, commandType);//статистика всего документа для пользователя

            return true;
        }
       
        private static string _lastThrottleKey;
        private static DateTime _lastThrottleTime = DateTime.MinValue;
        private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(300);

        //по времени ограничение на действие
        private bool OnlyOneCommand()
        {

            bool isThrottled = ((Now - _lastThrottleTime) < ThrottleWindow);
            _lastThrottleTime = Now;
            return isThrottled;
        }
        /// <summary>
        /// true — повтор в пределах окна, обработку надо пропустить.
        /// Save/Sync/CloseRevit не глушим — они редкие и важные.
        /// </summary>
        private  bool ShouldThrottle(string key)
        {
           
            bool isThrottled = (key == _lastThrottleKey) && ((Now - _lastThrottleTime) < ThrottleWindow);
            _lastThrottleKey = key;
            _lastThrottleTime = Now;
            return isThrottled;
        }
       

        private ViewStat pastViewStat = null;
        private void ProcessWriterViewStat(DocStats docStat, Document doc)
        {
            //запись характеристик вида

            UIDocument uiDoc = RevitAPI.UiDocument;
            if (uiDoc == null) { return; }
            Autodesk.Revit.DB.View activeView = uiDoc.ActiveView;

            string viewName = None;

            bool existView = false;
            if (activeView != null && !string.IsNullOrEmpty(activeView.Name))
            {
                viewName = activeView.Name;
                existView = true;
            }

            //добавка активного вида для статистики оч полезно знать сколько времени надо для создания вида и тд
            if (!docStat.ActiveViews.TryGetValue(viewName, out var viewData))
            {
                ElementId viewId = activeView.Id;
                int ViewId = (int)viewId.Value;
                viewData = new ViewStat
                {
                    NameView = viewName,
                    ViewId=ViewId,
                    FirstSeen = Now,
                    LastSeen = Now,
                    
                };
                docStat.ActiveViews[viewName] = viewData;
            }
            
            viewData.TotalOps++;
            double stepTime = 0;
            if (pastViewStat == viewData)
            {
                stepTime = (Now - pastViewStat.LastSeen).TotalSeconds;
                viewData.TotalWorkSeconds += stepTime;
                
            }
            else
            {
                viewData.CountActiveView++;//переход в активный вид
            }
            viewData.CommandHitsHours.TryGetValue(_hour, out var cmdHitsH);
            viewData.CommandHitsHours[_hour] = (cmdHitsH.cliks + 1, cmdHitsH.time + stepTime);//почасовая характеристика для оценки

            viewData.LastSeen = Now;
            pastViewStat = viewData;

            //попытка найти активный лист
            if (existView && (string.IsNullOrEmpty(viewData.NameSheet) || viewData.NameSheet == None))
            {
                //попытка найти лист
                string sheetName = None;
                string sheetNum = None;
                int sheetId = 0;
                // 1. Активный вид — сам лист
                if (activeView is ViewSheet sheet)
                {
                    if (!string.IsNullOrEmpty(sheet.Name))
                    {
                        sheetName = sheet.Name;
                    }
                    if (!string.IsNullOrEmpty(sheet.SheetNumber))
                    {
                        sheetNum = sheet.SheetNumber;
                    }
                    //попытка id установить 
                    ElementId viewId = activeView.Id;
                    sheetId = (int)viewId.Value;
                    
                }
                else
                {
                    var param = activeView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NAME);
                    if (param != null && param.HasValue)
                    {
                        string n = param.AsString();
                        if (!string.IsNullOrEmpty(n))
                        {
                            sheetName = n;

                            // дополнительно достаём номер листа через VIEWPORT_SHEET_NUMBER
                            string num = null;
                            var numParam = activeView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER);

                            //как id sheet достать??
                            ElementId viewId = activeView.Id;
                            sheetId = (int)viewId.Value;

                            if (numParam != null && numParam.HasValue)
                                num = numParam.AsString();
                            if (!string.IsNullOrEmpty(num))
                            {
                                sheetNum = num;
                            }

                            // Пытаемся найти Viewport, через который вид размещён на листе
                            Viewport vp = new FilteredElementCollector(doc)
                                .OfClass(typeof(Viewport))
                                .Cast<Viewport>()
                                .FirstOrDefault(v => v.ViewId == activeView.Id);

                            if (vp != null)
                            {
                                ElementId sheetElemId = vp.SheetId;
                                sheetId = (int)sheetElemId.Value;

                                // Имя и номер листа можно взять прямо из листа, а не из параметров вида
                                ViewSheet Sheet = doc.GetElement(sheetElemId) as ViewSheet;
                                if (Sheet != null)
                                {
                                    if (!string.IsNullOrEmpty(Sheet.Name))
                                        sheetName = Sheet.Name;
                                    if (!string.IsNullOrEmpty(Sheet.SheetNumber))
                                        sheetNum = Sheet.SheetNumber;
                                }
                            }

                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(sheetName) && sheetName != None)
                {
                    viewData.DateInSheet = Now;//первое размещение на листе
                                               //записываем
                    viewData.NumSheet = sheetNum;
                    viewData.NameSheet = sheetName;
                    viewData.IdSheet = sheetId;
                }
            }

        }
        private const string None = "None";
        private DocStats pastDocStat = null;
        private void ProcessWriterDocStat(DocStats docStat, string key, EDocStatsOptions commandType)
        {
            //запись характеристик документа

            double stepTime = 0;//прибавка времени
            if (pastDocStat == docStat)
            {
                stepTime = (Now - docStat.LastSeen).TotalSeconds;
                docStat.TotalWorkSeconds += stepTime;
            }
            else
            {
                docStat.CountActiveDoc++;
            }

            pastDocStat = docStat;

            //всегда может оказаться последним сеансом
            docStat.LastSeen = Now;
            docStat.TotalOps++;                                   // ← общий счётчик действий

            docStat.CommandHits.TryGetValue(key, out var cmdHits);
            docStat.CommandHits[key] = cmdHits + 1;

            docStat.CommandHitsHours.TryGetValue(_hour, out var cmdHitsH);
            docStat.CommandHitsHours[_hour] = (cmdHitsH.cliks + 1, cmdHitsH.time+ stepTime);//почасовая характеристика для оценки
           

            if (commandType !=0)
            {
                switch (commandType)
                {
                    case (EDocStatsOptions.Save):
                        docStat.SaveCount++;
                        docStat.LastSaveSeconds = App.secondSaveModel;
                        docStat.LastSave = DateTime.Now;
                        break;
                    case (EDocStatsOptions.Sync):
                        docStat.SyncCount++;
                        docStat.LastSaveSeconds = App.secondSaveModel;
                        docStat.LastSync = DateTime.Now;
                        break;
                    case (EDocStatsOptions.CloseRevit):
                        docStat.CloseRevit = true;
                        break;
                    default:
                        break;
                }
            }
        }



        // ==== Внутренняя логика ====

        private static string GetCallerName(EDocStatsOptions commandType)
        {
            // 0 — GetCallerName, 1 — Update, 2 — вызывающий команду
            var st = new StackTrace(false);
            var frames = st.GetFrames();
            if (frames == null || frames.Length < 5)
                return null;
            if(commandType== EDocStatsOptions.Invoker)
            {
                int cc = 0;
            }
            else
            {

            }
            var caller = frames[4].GetMethod();
            var typeName = caller?.DeclaringType?.Name;
            var methodName = caller?.Name;
            if (string.IsNullOrEmpty(typeName))
                return null;

            return $"{typeName}.{methodName}";
        }



        public void Merge(LookUsers other)
        {
            //для соединения если у пользователя несколько файлов в истории есть тоже файлы
            //для сбора статистики
            //прошлый словарь данных
            var pastDict = other.DictDateDocStats
                       ?? new Dictionary<DateTime, Dictionary<string, DocStats>>();

            // 3. Складываем прошлое в текущее
            foreach (var dateEntry in pastDict)
            {
                var dateKey = dateEntry.Key;

                if (!DictDateDocStats.TryGetValue(dateKey, out var targetDocs))
                {
                    // копируем через Clone, чтобы не тащить ссылки из past
                    var copy = new Dictionary<string, DocStats>(dateEntry.Value.Count);
                    foreach (var dk in dateEntry.Value)
                        copy[dk.Key] = dk.Value.Clone();
                    DictDateDocStats[dateKey] = copy;
                }
                else
                {
                    foreach (var dk in dateEntry.Value)
                    {
                        if (!targetDocs.TryGetValue(dk.Key, out var target))
                            targetDocs[dk.Key] = dk.Value.Clone();
                        else
                            target.Merge(dk.Value);
                    }
                }
            }
            // при слиянии past с текущим — после основного merge
            if (other.WriteStats != null)
                WriteStats.Merge(other.WriteStats);
        }




        // ==== Legacy (только чтение) ==== временно до удаления у всех пользователей
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<DateTime, Dictionary<string, int>> DocsDateUse { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<DateTime, Dictionary<string, int>> DictDateUse { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> DictUse { get; set; }
        /// <summary>
        /// Однократная миграция старых JSON-файлов (DocsDateUse/DictDateUse/DictUse)
        /// в новый формат DictDateDocStats. Безопасно вызывать повторно.
        /// </summary>
        private void MigrateLegacyData()
        {
            if (DictDateDocStats == null)
                DictDateDocStats = new Dictionary<DateTime, Dictionary<string, DocStats>>();

            // --- 1. DocsDateUse: { день -> { путь -> int } } -> DictDateDocStats ---
            if (DocsDateUse != null && DocsDateUse.Count > 0)
            {
                foreach (var dateEntry in DocsDateUse)
                {
                    var date = dateEntry.Key.Date; // нормализуем до дня
                    if (!DictDateDocStats.TryGetValue(date, out var docStats))
                    {
                        docStats = new Dictionary<string, DocStats>();
                        DictDateDocStats[date] = docStats;
                    }

                    foreach (var docEntry in dateEntry.Value)
                    {
                        string path = docEntry.Key;
                        int count = docEntry.Value;
                        if (count <= 0) continue;

                        if (!docStats.TryGetValue(path, out var stat))
                        {
                            stat = new DocStats();
                            docStats[path] = stat;
                        }

                        stat.TotalOps += count;

                        // сохраняем распределение, но без разбивки по командам —
                        // кладём всё в один legacy-ключ
                        stat.CommandHits.TryGetValue("legacy", out var lh);
                        stat.CommandHits["legacy"] = lh + count;

                        // FirstSeen/LastSeen приближённо = дата дня
                        if (stat.FirstSeen == default || date < stat.FirstSeen)
                            stat.FirstSeen = date;
                        if (stat.LastSeen < date)
                            stat.LastSeen = date;
                    }
                }
            }

            // --- 2. DictDateUse: старый словарь по командам с разбивкой по дням ---
            if (DictDateUse != null && DictDateUse.Count > 0)
            {
                foreach (var dateEntry in DictDateUse)
                {
                    var date = dateEntry.Key.Date;
                    if (!DictDateDocStats.TryGetValue(date, out var docStats))
                    {
                        docStats = new Dictionary<string, DocStats>();
                        DictDateDocStats[date] = docStats;
                    }

                    // команды без привязки к документу кладём в спец-ключ
                    const string unknownDoc = "<no-document>";
                    if (!docStats.TryGetValue(unknownDoc, out var stat))
                    {
                        stat = new DocStats();
                        docStats[unknownDoc] = stat;
                    }

                    foreach (var cmdEntry in dateEntry.Value)
                    {
                        stat.CommandHits.TryGetValue(cmdEntry.Key, out var v);
                        stat.CommandHits[cmdEntry.Key] = v + cmdEntry.Value;
                        stat.TotalOps += cmdEntry.Value;
                    }
                }
            }

            // --- 3. DictUse: глобальный счётчик по командам без даты ---
            // Кладём в «сегодня» с пометкой legacy, чтобы не потерять.
            if (DictUse != null && DictUse.Count > 0)
            {
                var today = DateTime.Now.Date;
                if (!DictDateDocStats.TryGetValue(today, out var docStats))
                {
                    docStats = new Dictionary<string, DocStats>();
                    DictDateDocStats[today] = docStats;
                }

                const string unknownDoc = "<no-document>";
                if (!docStats.TryGetValue(unknownDoc, out var stat))
                {
                    stat = new DocStats { FirstSeen = DateTime.Now, LastSeen = DateTime.Now };
                    docStats[unknownDoc] = stat;
                }

                foreach (var cmdEntry in DictUse)
                {
                    stat.CommandHits.TryGetValue(cmdEntry.Key, out var v);
                    stat.CommandHits[cmdEntry.Key] = v + cmdEntry.Value;
                    stat.TotalOps += cmdEntry.Value;
                }
            }

            // После миграции старые поля не нужны — при записи они не попадут в JSON
            DocsDateUse = null;
            DictDateUse = null;
            DictUse = null;
        }
    }
}