using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public readonly struct SPastCommand
    {
        public readonly string Name;//имя команды
        public readonly DateTime DateTime;//дата команды
        public SPastCommand(string name, DateTime dateTime)
        {
            Name = name; DateTime = dateTime;
        }
    }

    public partial class App
    {
        private void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            // Здесь sender — это Autodesk.Revit.ApplicationServices.Application
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            if (app != null)
            {
                // Получаем UIApplication
                UIApplication uiApp = new UIApplication(app);
                // Теперь можно работать с uiApp
                // Например, сохранить в статическое свойство
                RevitAPI.Initialize(uiApp);
                //uiApp.Application.GroupEditModeChanged += OnGroupEditModeChanged;
            }
        }
        private void OnRevitClosing(object sender, ApplicationClosingEventArgs e)
        {
            //статистику записываем при закрытии 
            // Здесь сохраняем статистику
            //закрытие приложения
            //аналогично  Result OnShutdown
            //App_Apdater_1.LookUsers.ForceFlush(true);
            //LookUsers.Instance.ForceFlush();
        }



        private void OnDocumentSaving(object sender, DocumentSavingEventArgs args)
        {
            // Срабатывает, когда Revit собирается сохранить существующий документ
            Document doc = args.Document;
            if (doc == null) return;
            string docPath = doc.PathName;
            // Запоминаем время начала синхронизации
            _syncStartTimes[doc] = DateTime.Now;
            // Здесь вы можете добавить свою логику статистики
            // Например, вызвать метод для записи информации о сохранении
            // UpdateStatistics(docPath, "Save");
        }

        private void OnDocumentSynchronizing(object sender, DocumentSynchronizingWithCentralEventArgs args)
        {
            // Срабатывает, когда Revit собирается синхронизировать документ с центральной моделью
            Document doc = args.Document;
            if (doc == null) return;

            string docPath = doc.PathName;
            // Запоминаем время начала синхронизации
            _syncStartTimes[doc] = DateTime.Now;
            // Здесь ваша логика статистики
            // UpdateStatistics(docPath, "Synchronize");
        }
        private void OnDocumentSaved(object sender, DocumentSavedEventArgs e)
        {
            Document doc = e.Document;
            if (doc == null) return;
            // Этот код выполнится после сохранения.
            // Пытаемся получить время старта
            if (_syncStartTimes.TryRemove(doc, out DateTime startTime))
            {
                TimeSpan duration = DateTime.Now - startTime;
                secondSaveModel = duration.TotalSeconds;
            }
            else
            {
                secondSaveModel = 0;
            }
            App_Apdater_1.LookUsers.Update("OnDocumentSaved", EDocStatsOptions.Save);
        }
        private void OnDocumentSynchronized(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            // Этот код выполнится после синхронизации.
            Document doc = e.Document;
            if (doc == null) return;

            // Пытаемся получить время старта
            if (_syncStartTimes.TryRemove(doc, out DateTime startTime))
            {
                TimeSpan duration = DateTime.Now - startTime;
                secondSaveModel = duration.TotalSeconds;
            }
            else
            {
                secondSaveModel = 0;
            }

            App_Apdater_1.LookUsers.Update("OnDocumentSynchronized", EDocStatsOptions.Sync);
        }

        public static (string Name, ElementId Id) OnGroupCurrent = ("", null);
        // Имена транзакций входа/выхода из группы — в EN и RU локалях
        private static readonly string[] EnterGroupTxNames =
        {
            "Edit Group",                    // EN
            "Редактировать группу"           // RU
        };

        private static readonly string[] ExitGroupTxNames =
        {
            "Exit Group Edit Mode",          // EN (пример)
            "Finish Group Edit",             // EN (альтернативный вариант, встречается в некоторых версиях)
            "Завершить работу редактирования группы" // RU
        };
        //а можно как в машине, когда там 3 раза ключ в зажигания и тд... сделать 3 раза подряд на какую-то из э
        public static SPastCommand PastCommand =new SPastCommand();
        public static Queue<SPastCommand> PastCommands = new Queue<SPastCommand>();
        private static readonly string[] PatternToDetect =
        {
            "Закрепить",
            "Открепить",
            "Закрепить",
            "Открепить"
        };

        private const int PatternTimeWindowMs = 12000; // 6 секунд - мало бывает виснет
        private static bool _pendingSecret;
        public static bool InPendingSecret = false;
        
        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {

            IList<string> txNames = e.GetTransactionNames();

            string comand = txNames.FirstOrDefault();
            if (string.IsNullOrEmpty(comand))
            {
                return;
            }
            App_Apdater_1.LookUsers.Update(comand, EDocStatsOptions.Invoker);

            Document doc = e.GetDocument();

            // Отсекаем фоновые документы/семейства, если они не нужны
            if (doc == null) { return; }

            if (PastCommands.Count >= 20)
            {
                PastCommands.Dequeue();//убираем самый старый элемент
            }
            PastCommand = new SPastCommand(comand, DateTime.Now);
            PastCommands.Enqueue(PastCommand);
            // === Магия: проверяем секретный паттерн ===
            if (MatchesSecretPattern())
            {
                if(!string.IsNullOrEmpty(OnGroupCurrent.Name))
                {
                    //мы в группе выполняем команду
                    PastCommands.Clear();
                    InPendingSecret=true;
                    //_pendingSecret = true;
                    //// Подписка в OnStartup:
                    //Application.Idling += (s, a) =>
                    //{
                    //    if (_pendingSecret)
                    //    {
                    //        _pendingSecret = false;
                    //        _secretEvent?.Raise();
                    //    }
                    //};
                }

            }
            
            // --- Вход в режим редактирования группы ---
            if (txNames.Any(n => EnterGroupTxNames.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                var modifiedIds = e.GetModifiedElementIds();


                foreach (var id in modifiedIds)
                {
                    Element elem = e.GetDocument().GetElement(id);
                    if (elem == null) continue;

                    // Вариант 2: это тип группы (GroupType) — тоже валидный результат
                    if (elem is GroupType groupType)
                    {
                        OnGroupCurrent = (groupType.Name, groupType.Id);
                        return;
                    }
                    // Вариант 1: это экземпляр группы (модельной или детальной)
                    if (elem is Group group)
                    {
                        OnGroupCurrent = (group.Name, group.Id);
                        return;
                    }

                    // Вариант 3: это какой-то элемент, у которого есть GroupId — возможно, мы внутри группы
                    if (elem.GroupId != ElementId.InvalidElementId)
                    {
                        Element parent = e.GetDocument().GetElement(elem.GroupId);
                        if (parent is Group parentGroup)
                        {
                            OnGroupCurrent = (parentGroup.Name, parentGroup.Id);
                            return;
                        }
                    }

                }

                // Если список пустой — фолбэк: берём группу из выделения
                TryCaptureGroupFromSelection(e.GetDocument());
                return;
            }

            // --- Выход из режима редактирования группы ---
            if (txNames.Any(n => ExitGroupTxNames.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                OnGroupCurrent = ("", null);
            }

            // Остальные транзакции — ничего не делают, состояние сохраняется
        }
        private static bool MatchesSecretPattern()
        {
            if (PastCommands.Count < 4) return false;

            // Берём последние 4 записи в хронологическом порядке
            // (Queue выдаёт в порядке добавления — от старых к новым)
            var last4 = PastCommands.Reverse().Take(4).Reverse().ToArray();
            // last4[0] — старейшая из четвёрки, last4[3] — самая свежая

            // 1. Проверяем имена
            for (int i = 0; i < 4; i++)
            {
                if (!string.Equals(last4[i].Name, PatternToDetect[i],
                                   StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. Проверяем временной интервал
            var span = last4[3].DateTime - last4[0].DateTime;
            return span.TotalMilliseconds <= PatternTimeWindowMs;
        }
        private static void TryCaptureGroupFromSelection(Document doc)
        {
            try
            {
                UIDocument uidoc = new UIDocument(doc);
                var selIds = uidoc.Selection.GetElementIds();
                foreach (var id in selIds)
                {
                    if (doc.GetElement(id) is Group g)
                    {
                        string name = g.GroupType?.Name ?? g.Name;
                        OnGroupCurrent = (name, g.Id);
                        return;
                    }
                }
            }
            catch { /* selection может быть недоступен — игнорируем */ }
        }
        private void OnFailuresProcessing(object sender, FailuresProcessingEventArgs e)
        {
            FailuresAccessor fa = e.GetFailuresAccessor();
            IList<FailureMessageAccessor> failList = fa.GetFailureMessages();

            if (failList.Count == 0)
            {
                e.SetProcessingResult(FailureProcessingResult.Continue);
                return;
            }

            // Путь документа — один раз на событие
            string docPath = null;
            try
            {
                Document doc = fa.GetDocument();
                if (doc != null)
                {
                    var central = doc.GetWorksharingCentralModelPath();
                    docPath = central != null
                        ? ModelPathUtils.ConvertModelPathToUserVisiblePath(central)
                        : doc.PathName;
                }
            }
            catch { /* не критично */ }

            string user = Environment.UserName;
            DateTime now = DateTime.Now;

            foreach (FailureMessageAccessor failure in failList)
            {
                try
                {
                    var rec = new FailureRecord
                    {
                        Time = now,
                        UserName = user,
                        DocPath = docPath,
                        Description = failure.GetDescriptionText(),
                        Severity = failure.GetSeverity().ToString(),
                        DefinitionId = failure.GetFailureDefinitionId()?.Guid ?? Guid.Empty
                    };

                    ICollection<ElementId> ids = failure.GetFailingElementIds();
                    if (ids != null)
                    {
                        foreach (var id in ids)
                            rec.FailingElementIds.Add(id.IntegerValue);
                    }

                    FailureBuffer.Add(rec);
                }
                catch
                {
                    // ошибка статистики не должна ломать транзакцию
                }
            }

            e.SetProcessingResult(FailureProcessingResult.Continue);
        }
        //private void OnGroupEditModeChanged(object sender, GroupEditModeChangedEventArgs e)
        //{
        //    // e.Active указывает, вошли (true) или вышли (false) из режима
        //    IsGroupEditModeActive = e.Active;
        //}
    }

    public class SecretReplacementHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            try
            {
                //UIDocument uidoc = app.ActiveUIDocument;
                //Document doc = uidoc?.Document;
                //if (doc == null) return;

                //// Здесь уже безопасно: транзакция закрыта, UI свободен
                //var elems = ArmLengthEquels.SelectOrAllElements();
                //DeleteDublicateFamily.ReplacedProcess(doc, elems, true);
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
            }
            finally
            {

            }
        }

        public string GetName() => "SecretReplacementHandler";


        

    }
}
