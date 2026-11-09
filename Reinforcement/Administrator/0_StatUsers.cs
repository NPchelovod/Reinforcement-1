using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

namespace Reinforcement
{
    public class StatElement: IJsonStorable<StatElement>
    {
        // ==== Данные ==== они идут в файл записи
        public string UserName = Environment.UserName;
        public string VersionName => SettingsWindow.GetDatePluginText;
        public DateTime DateDay { get; set; } = DateTime.Now;//дата текущего дня
        public Dictionary<DateTime, Dictionary<string, DocStats>> DictDateDocStats { get; set; }
            = new Dictionary<DateTime, Dictionary<string, DocStats>>();
        public WritePerfStats WriteStats { get; set; } = new WritePerfStats();

        public int Count()
        {
            return DictDateDocStats.Count;
        }

        public void Merge(StatElement other)
        {
            //прошлый словарь данных
            var pastDict = other.DictDateDocStats;// этот словарь маленький в сравнении с текущим тут только 2.5 часа памяти
            if (pastDict == null || !pastDict.Any()) {return; }
                       

            // 3. Складываем в текущее
            foreach (var dateEntry in pastDict)
            {
                var dateKey = dateEntry.Key;

                if (DictDateDocStats.TryGetValue(dateKey, out var targetDocs))
                {
                    foreach (var dk in dateEntry.Value)
                    {
                        if (targetDocs.TryGetValue(dk.Key, out var target))
                            target.Merge(dk.Value);
                        else
                            targetDocs[dk.Key] = dk.Value.Clone();
                    }
                }
                else
                {
                    // копируем через Clone, чтобы не тащить ссылки из other
                    var copy = new Dictionary<string, DocStats>(dateEntry.Value.Count);
                    foreach (var dk in dateEntry.Value)
                        copy[dk.Key] = dk.Value.Clone();
                    DictDateDocStats[dateKey] = copy;
                    
                }
            }
            // при слиянии past с текущим — после основного merge
            if (other.WriteStats != null)
                WriteStats.Merge(other.WriteStats);
        }

        public DateTime DateStart()
        {
            if(DictDateDocStats.Count>0)
            {
                return DictDateDocStats.Keys.Min();
            }
            return DateTime.Now;
        }
        public DateTime DateEnd()
        {
            if (DictDateDocStats.Count > 0)
            {
                return DictDateDocStats.Keys.Max();
            }
            return DateTime.Now;
        }
    }

    public  static class StatUsers
    {
        public static StatElement _statElement = new StatElement();
        private static WriteAndRead<StatElement> writeAndRead=null;

        private static void InitializationWriteAndRead()
        {
            if (writeAndRead != null) { return; }
            writeAndRead = new WriteAndRead<StatElement> (App_Folders.FolderStatistics, App_Folders.FolderStatisticsTemp, $"{Environment.UserName}_{Environment.MachineName}.json");
        }


        private static int _inUpdate;
        // Защита от гонок в рамках одной сессии
        private static readonly object _lock = new object();
        private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMilliseconds(300);
        public static void Update(string explicitCommandName = null, EDocStatsOptions commandType = 0)
        {
            // Пытаемся "занять" флаг: если уже 1 — значит кто-то внутри, выходим
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0)
            {
                return;
            }

            try
            {
                
                lock (_lock)
                {
                    Now = DateTime.Now;
                    if ((Now - _pastDateProcessStat) < ThrottleWindow)
                    {
                        //по времени слишком частые друг за другом идущие изменения - не реагируем
                        return;
                    }
                    ProcessStat(explicitCommandName, commandType);
                }
                //запись статистики
                if (Now - _pastDateFlush > _flushInterval)
                {
                    Flush();
                    
                }
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
            }
            finally
            {
               
                Interlocked.Exchange(ref _inUpdate, 0);
            }
        }

        public static DateTime Now = DateTime.Now;//для быстрого доступа к дате

        
        private static DateTime _pastDateProcessStat = DateTime.MinValue;
        private static int _hour = 0;
        private static DocStats _pastDocStat = null;
        private static ViewStat _pastViewStat = null;
        private static void ProcessStat(string explicitCommandName, EDocStatsOptions commandType)
        {
            _pastDateProcessStat = Now;
            _hour = Now.Hour;
            //запись статистики
            string key = explicitCommandName ?? GetCallerName(commandType);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (commandType == EDocStatsOptions.CloseRevit)
            {
                //так как документ к тому времени бывает == null;
                if (_pastDocStat != null)
                {
                    _pastDocStat.CloseRevit = true;
                    _pastDocStat.LastSeen = DateTime.Now;
                }
            }
            Document doc = RevitAPI.Document;
            if (doc == null) { return ; }

            var dateDay = Now.Date;   // ← добавить


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
                    return;
                }
            }

            //команды кликов
            if (!_statElement.DictDateDocStats.TryGetValue(dateDay, out var docStats))
            {
                docStats = new Dictionary<string, DocStats>();
                _statElement.DictDateDocStats[dateDay] = docStats;
            }
            if (!docStats.TryGetValue(nameDoc, out var docStat))
            {
                docStat = new DocStats();
                docStats[nameDoc] = docStat;

                //заполняем даты открытия данной модели
                docStat.FirstSeen = Now;

                docStat.Name = System.IO.Path.GetFileNameWithoutExtension(nameDoc);
                ProjectInfo info = doc.ProjectInformation;

                if (info != null)
                {
                    string documentGuid = info.UniqueId; // Уникальный GUID проекта совпадает если шаблон один

                    if (!string.IsNullOrEmpty(documentGuid))
                    {
                        docStat.Guid = documentGuid;
                    }
                }
                // Получаем 
                string creationGUID = doc.CreationGUID.ToString();
                if (!string.IsNullOrEmpty(creationGUID))
                {
                    docStat.CreationGUID = creationGUID;
                }

                int warnings = doc.GetWarnings().Count();//ошибок в документе
                docStat.Warnings = warnings;
            }

            ProcessWriterDocStat(docStat, key, commandType);//статистика всего документа для пользователя
            ProcessWriterViewStat(docStat, doc);//статистика по виду

        }
        private const int _secondsRazbrosTimeCommandMax = 20;
        private const double _kratProstoiTime = 3.0;//во сколько раз больше по времени между командами чтобы считать за простой

        private static int pastComDoc = 0;//для расчета среднего времени 
        private static void ProcessWriterDocStat(DocStats docStat, string key, EDocStatsOptions commandType)
        {
            //запись характеристик документа

            double stepTime = 0;//прибавка времени
            if (_pastDocStat == docStat)
            {
                stepTime = (Now - docStat.LastSeen).TotalSeconds;
                docStat.TotalWorkSeconds += stepTime;
                var asp = docStat.AverSecondsBetweenCommands;
                //вообще надо чтобы от среднего квадратичного с 90% вероятностью квантилью не отличалось время
                if(Math.Abs(stepTime-asp)< _secondsRazbrosTimeCommandMax)
                {
                    if (asp == 0)
                    {
                        docStat.AverSecondsBetweenCommands = stepTime; 
                        pastComDoc = 0;
                    }
                    else
                    {
                        docStat.AverSecondsBetweenCommands = (stepTime + asp* pastComDoc) / (pastComDoc+1);
                        pastComDoc++;
                    }
                }
                else if(stepTime> _kratProstoiTime * docStat.AverSecondsBetweenCommands)
                {
                    //вообще правильнее простой учитывать если и вид один и тот же, но не факт, после обеда можем сначала вид сменить затем команду сделать
                    docStat.TotalProstoiSeconds += stepTime;
                }
            }
            else
            {
                _pastDocStat = docStat;
                docStat.CountActiveDoc++;
                pastComDoc = 0;
            }

            //всегда может оказаться последним сеансом
            docStat.LastSeen = Now;
            docStat.TotalOps++;                                   // ← общий счётчик действий

            docStat.CommandHits.TryGetValue(key, out var cmdHits);
            docStat.CommandHits[key] = cmdHits + 1;

            docStat.CommandHitsHours.TryGetValue(_hour, out var cmdHitsH);
            docStat.CommandHitsHours[_hour] = (cmdHitsH.cliks + 1, cmdHitsH.time + stepTime);//почасовая характеристика для оценки


            if (commandType != 0)
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
                        docStat.MaxSyncSeconds = Math.Max(docStat.MaxSyncSeconds, App.secondSaveModel);
                        break;
                    case (EDocStatsOptions.CloseRevit):
                        docStat.CloseRevit = true;
                        break;
                    default:
                        break;
                }
            }
        }
        private const string None = "None";
        private static void ProcessWriterViewStat(DocStats docStat, Document doc)
        {
            //запись характеристик вида

            UIDocument uiDoc = RevitAPI.UiDocument;
            if (uiDoc == null) { return; }
            Autodesk.Revit.DB.View activeView = uiDoc.ActiveView;

            string viewName = None;

            if (activeView != null && !string.IsNullOrEmpty(activeView.Name))
            {
                viewName = activeView.Name;
            }
            else
            {
                return;
            }

            //добавка активного вида для статистики оч полезно знать сколько времени надо для создания вида и тд
            if (!docStat.ActiveViews.TryGetValue(viewName, out var viewData) || viewData == null)
            {
                ElementId viewId = activeView.Id;
                int ViewId = (int)viewId.Value;
                viewData = new ViewStat
                {
                    NameView = viewName,
                    ViewId = ViewId,
                    FirstSeen = Now,
                    LastSeen = Now,

                };
                docStat.ActiveViews[viewName] = viewData;
            }

            viewData.TotalOps++;
            double stepTime = 0;
            if (_pastViewStat == viewData)
            {
                stepTime = (Now - _pastViewStat.LastSeen).TotalSeconds;
                viewData.TotalWorkSeconds += stepTime;

            }
            else
            {
                viewData.CountActiveView++;//переход в активный вид
            }
            viewData.CommandHitsHours.TryGetValue(_hour, out var cmdHitsH);
            viewData.CommandHitsHours[_hour] = (cmdHitsH.cliks + 1, cmdHitsH.time + stepTime);//почасовая характеристика для оценки

            viewData.LastSeen = Now;
            _pastViewStat = viewData;

            //попытка найти активный лист
            if (string.IsNullOrEmpty(viewData.NameSheet) || viewData.NameSheet == None)
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



        

        private static DateTime _pastDateFlush = DateTime.MinValue;
        private static readonly TimeSpan _flushInterval = TimeSpan.FromHours(3.5);
        private static int _inFlush;
        public static void Flush()
        {
            //при закрытии ревита эта команда вызывается напрямую
            
            
            InitializationWriteAndRead();
            if (writeAndRead == null) { AppErrors.LogError(new Exception("InitializationWriteAndRead()")); return; }
            bool ok;
            lock (_lock)
            {
                _pastDateFlush = DateTime.Now;//пусть будет тут, чтобы повторно часто не вызываться при неудачи
                ok = writeAndRead.Flush(_statElement);
                if (ok)
                {
                    _statElement = new StatElement();
                    _pastDocStat = null;
                    _pastViewStat = null;
                }
            }
            if (!ok) { /* лог */ }

            //так же ошибки выгружаем
            AppErrors.FlushErrorsToLog();
        }

        
       
       

        private static string GetCallerName(EDocStatsOptions commandType)
        {
            // 0 — GetCallerName, 1 — Update, 2 — вызывающий команду
            var st = new StackTrace(false);
            var frames = st.GetFrames();
            if (frames == null || frames.Length < 5)
                return null;
           
            var caller = frames[4].GetMethod();
            var typeName = caller?.DeclaringType?.Name;
            var methodName = caller?.Name;
            if (string.IsNullOrEmpty(typeName))
                return null;

            return $"{typeName}.{methodName}";
        }
       


    }
}
