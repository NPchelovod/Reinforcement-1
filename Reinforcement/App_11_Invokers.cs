using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI;
using System.Threading;
using System.IO;
namespace Reinforcement
{
    

    public partial class App
    {

        private void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            // Здесь sender — это Autodesk.Revit.ApplicationServices.Application
            var app = sender as Autodesk.Revit.ApplicationServices.Application;
            if (app == null) { return; }
            // Получаем UIApplication
            UIApplication uiApp = new UIApplication(app);
            if (uiApp == null) { return; }
            InitializedInvoker(uiApp);
          
        }


        public static void InitializedInvoker(UIApplication uiApp)
        {
            try
            {
                if (uiApp == null) { return; }
                // Теперь можно работать с uiApp
                // Например, сохранить в статическое свойство
                RevitAPI.Initialize(uiApp);

                UpdaterSubscriber.Initialize(uiApp);
                uiApp.Idling += OnIdling;
                //uiApp.Application.GroupEditModeChanged += OnGroupEditModeChanged;

                //RegisterAutoFillUpdater.addInId = uiApp.ActiveAddInId;
                //RegisterAutoFillUpdater.Register();


                //UpdaterSubscriber.Initialize(uiApp);
                //uiApp.Idling += OnIdling;
                //uiApp.Application.GroupEditModeChanged += OnGroupEditModeChanged;
            }
            catch (Exception ex)
            {
                ErrorException(ex, "OnApplicationInitialized");
            }

        }

        private static void OnIdling(object sender, IdlingEventArgs e)
        {
            // Здесь будет ваша логика
            //запуск после каждого освобожения ревита
            //В обработчике OnIdling вы проверяете, есть ли необработанные изменения, и если да — инициируете их обработку через ExternalEvent
            int c = 0;
            return;
            // 1. Если изменений нет, или уже идет обработка — ничего не делаем
            //if (!AnyChange.IsExistElementsChange) return;
            //if (PeriodicHandler.IsProcessingOwnChange) return;

            //// 2. (Опционально, но рекомендуется) Проверяем, не истек ли "кулдаун"
            ////    после нашей последней обработки, чтобы не ловить "хвосты" от самих себя
            //if (PeriodicHandler.IsRecentlyExecuted(CooldownAfterProcessing)) return;

            //// 3. Если все условия выполнены — инициируем внешнее событие
            //try
            //{
            //    // ExternalEventRef - это ваш статический ExternalEvent из AnyChange
            //    AnyChange.ExternalEventRef?.Raise();
            //}
            //catch
            //{
            //    // Revit может быть занят, но в контексте Idling это маловероятно.
            //    // Если Raise() не сработал, следующее событие Idling попробует снова.
            //}
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
       
        
    }

    

}
