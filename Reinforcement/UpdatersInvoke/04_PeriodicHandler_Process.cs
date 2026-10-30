using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
namespace Reinforcement
{
    public enum ETypeChangeEl
    {
        None = 0,
        AddedElements,
        ModifiedElements,
        GeomModifiedElements,
        DeleteModifiedElements,
    }
    public static class UpdaterSubscriber
    {
        private static ExternalEvent _externalEvent;
        private static Timer _fallbackTimer;

        public static void Initialize(UIApplication app)
        {
            RegisterAutoFillUpdater.addInId = app.ActiveAddInId;

            RegisterAutoFillUpdater.Register();

            _externalEvent = ExternalEvent.Create(new PeriodicHandler());
            AnyChange.ExternalEventRef = _externalEvent;

            // Страховка: если Raise() из AnyChange был отклонён,
            // а новых изменений нет — таймер добьёт обработку.
            _fallbackTimer = new Timer(_ =>
            {
                if (!AnyChange.IsExistElementsChange) return;
                try { _externalEvent?.Raise(); } catch { }
            }, null, dueTime: 10000, period: 10000);//столько времени до следующей попытки 
        }

        public static void Shutdown()
        {
            _fallbackTimer?.Dispose(); _fallbackTimer = null;
            _externalEvent?.Dispose(); _externalEvent = null;
            AnyChange.ExternalEventRef = null;
            RegisterAutoFillUpdater.Unregister();
        }
    }
    public partial class PeriodicHandler : IExternalEventHandler
    {
        public string GetName() => "PeriodicHandler";
        public static readonly string TransactionName =
            RevitAPI.GetTranzactionName("Отложенное действие");

        public static volatile bool IsProcessingOwnChange = false;

        // Монотонная метка через Stopwatch — надёжнее DateTime
        private static long _lastExecuteTimestamp = 0;

        public static bool IsRecentlyExecuted(TimeSpan window)
        {
            long last = Interlocked.Read(ref _lastExecuteTimestamp);
            if (last == 0) return false;

            // Сколько тиков Stopwatch прошло с момента last
            long elapsedTicks = Stopwatch.GetTimestamp() - last;

            // Переводим тики в секунды: ticks / Frequency
            double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;

            return elapsedSeconds < window.TotalSeconds;
        }

        public void Execute(UIApplication app)
        {
            return;
            if (IsProcessingOwnChange)
            {
                return;
            }
            if (!AnyChange.IsExistElementsChange)
            {
                return;
            }

            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;

            Document doc = uidoc.Document;
            if (doc == null || doc.IsReadOnly) return;

            // Revit занят другой транзакцией — выйдем, таймер-страховка повторит Raise()
            if (doc.IsModifiable) return;

            SnapShotElements snap = AnyChange.GetSnapShotElements(doc);
            if (snap == null) return;

            IsProcessingOwnChange = true;
            try
            {
                using (var t = new Transaction(doc, TransactionName))
                {
                    t.Start();
                    try
                    {
                        ProcessSnapshot(snap.DateTimeCreate, snap, doc);
                        t.Commit();
                    }
                    catch
                    {
                        if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);
            }
            finally
            {
                IsProcessingOwnChange = false;
                // Фиксируем момент ЗАВЕРШЕНИЯ — от него отсчитывается cooldown в AnyChange
                Interlocked.Exchange(ref _lastExecuteTimestamp, Stopwatch.GetTimestamp());
            }
        }



        private static string _userName = Environment.UserName.ToString();
        private static string _nowDate; // dd.MM.yy.HH
        private static void ProcessSnapshot(DateTime dateTime, SnapShotElements snapshot, Document doc)
        {
            //прежде записи для автора нужно записать дату
            _nowDate = dateTime.ToString("dd.MM.yy.HH");


            // Вся разветвлённая логика здесь.
            // snapshot.AddedIds / GeomModificationIds / ModifiedIdsProp уже разделены.
            foreach (var addIds in snapshot.AddedIds)//тут добавленные элементы, включают изм геометрию и изм параметры
            {
                Element element = doc.GetElement(addIds);
                ProcessElement(dateTime, doc, element, ETypeChangeEl.AddedElements);
            }
            foreach(var geomIds in snapshot.GeomModificationIds)//тут измененная геометрия, но не добавленные элементы
            {
                Element element = doc.GetElement(geomIds);
                ProcessElement(dateTime, doc, element, ETypeChangeEl.GeomModifiedElements);
            }
            foreach(var propIds in snapshot.ModifiedIdsProp) // теоретически тут с измененным параметром, но не добавленные элементы
            {
                Element element = doc.GetElement(propIds);
                ProcessElement(dateTime, doc, element, ETypeChangeEl.ModifiedElements);
            }

        }
        private static void ProcessElement(DateTime dateTime, Document doc,Element element, ETypeChangeEl eTypeChangeEl )
        {
            if (element == null) return;
            ElementId groupId = element.GroupId; // ← NRE, если element == null
            if (groupId != ElementId.InvalidElementId)
            {
                if (!AnyChange.CorrectGroop) { return; }
                //пытаемся определить группу
                // Получаем экземпляр группы по GroupId элемента
                Group instanceGroup = doc.GetElement(groupId) as Group;
                if (instanceGroup == null)
                {
                    // Странная ситуация — GroupId есть, а Group нет. Пропускаем.
                    { return; }
                }
                ElementId groupTypeId = instanceGroup.GetTypeId();
                // Сравниваем по значению, а не по ссылке
                bool sameGroupType =
                    groupTypeId != null
                    && App.OnGroupCurrent.Id != null
                    && groupTypeId.Equals(App.OnGroupCurrent.Id);
                if (!sameGroupType)
                { return; }
            }

            //записываем автора
            try
            {
                AuthorWriter(dateTime, doc, element, eTypeChangeEl);
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }

            try
            {
                //сбрасываем кубики
                CubicWriter(dateTime, doc, element, eTypeChangeEl);
                
                //меняем длину выноски
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }
            try
            {
                CubicWriterLevel(dateTime, doc, element, eTypeChangeEl);
            }
            catch(Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }

            try
            {
                AnnotationWriter(dateTime, doc, element, eTypeChangeEl);
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }

        }


       

       
    }


}
    


