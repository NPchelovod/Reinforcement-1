using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI;
using System.Threading;
namespace Reinforcement
{
    

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
                StartPeriodicWork(uiApp);
                //uiApp.Application.GroupEditModeChanged += OnGroupEditModeChanged;
            }
        }
        private void OnRevitClosing(object sender, ApplicationClosingEventArgs e)
        {
            //статистику записываем при закрытии 
            // Здесь сохраняем статистику
            //закрытие приложения
            //аналогично  Result OnShutdown
            //RevitAPI.LookUsers.ForceFlush(true);
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
            
        }

        private void OnDocumentSynchronizing(object sender, DocumentSynchronizingWithCentralEventArgs args)
        {
            // Срабатывает, когда Revit собирается синхронизировать документ с центральной моделью
            Document doc = args.Document;
            if (doc == null) return;

            string docPath = doc.PathName;
            // Запоминаем время начала синхронизации
            _syncStartTimes[doc] = DateTime.Now;

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
            RevitAPI.LookUsers.Update("OnDocumentSaved", EDocStatsOptions.Save);
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

            RevitAPI.LookUsers.Update("OnDocumentSynchronized", EDocStatsOptions.Sync);
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
       
        private ExternalEvent _exEvent;
        private Timer _timer;

        public void StartPeriodicWork(UIApplication uiapp)
        {
            try
            {

                var handler = new PeriodicHandler();
                _exEvent = ExternalEvent.Create(handler);

                // Таймер: первый вызов через 0 мс, затем каждые 1000 мс.
                // В callback только Raise() — никакой работы с Revit API!
                _timer = new Timer(_ => _exEvent.Raise(), null, 0, 1000);
            }
            catch (Exception ex)
            {
                try { RevitAPI.AppErrors?.LogError(ex); } catch { }
            }
        }

        public void StopPeriodicWork()
        {
            _timer?.Dispose();
            _timer = null;
            _exEvent?.Dispose(); // если версия Revit позволяет Dispose для ExternalEvent
            _exEvent = null;
        }
    }

    

}
