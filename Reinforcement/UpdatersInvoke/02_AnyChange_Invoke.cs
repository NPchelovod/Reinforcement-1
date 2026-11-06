using Autodesk.Revit.Attributes;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement
{
    public class SnapShotElements
    {
        public HashSet<ElementId> AddedIds;// добавленный новый элемент
        public HashSet<ElementId> GeomModificationIds;//не добавленный, геом измененный элемент
        public HashSet<ElementId> ModifiedIdsProp;//не добавленные и не геометрически измененный
        public DateTime DateTimeCreate;
        public Document Doc;
        public SnapShotElements(DateTime dateTimeCreate, Document doc, HashSet<ElementId> addedIds, HashSet<ElementId> geomModificationIds, HashSet<ElementId> modifiedIdsProp)
        {
            Doc = doc;
            DateTimeCreate = dateTimeCreate;
            AddedIds = addedIds;
            GeomModificationIds = geomModificationIds;
            ModifiedIdsProp = modifiedIdsProp;
        }
    }
    public class AnyChangeClass : IUpdater
    {
        private static AddInId _addInId => RegisterAutoFillUpdater.addInId;

        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457271E"));
        public string GetAdditionalInformation() => string.Empty;
        public ChangePriority GetChangePriority() => ChangePriority.Annotations;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => App.TabName + "_Updater всех процессов";

        private readonly double TimeOutMilliseconds = 200;

        public void Execute(UpdaterData data)
        {
            if (!AllUpdater) { return; }
            //сюда приходят от всех изменений элементы
            //бывает приходят пустные
            if (IsProcessingOwnChange)
            {
                return;

            }// ⬅️ рвёт рекурсию мы итак в нашем изменении
            DateTime now = DateTime.Now;
            if ((now-DateLastExecute).TotalMilliseconds< TimeOutMilliseconds)
            {
                return;
            }
            //наверно надо еще и задержку по времени добавить в 0.1 - 0.2 секунды чтобы после нашего вмешательства не срабатывало повторно

            try
            {
                IsProcessingOwnChange = true;
                //изменен я которые случились менее чем за 150 мс
                // проверка не защищает от рекурсии, а глушит пользователя:
                
                Document doc = data.GetDocument();
                if (doc == null) { return; }

                var added = data.GetAddedElementIds();
                var modified = data.GetModifiedElementIds();
                var deleted = data.GetDeletedElementIds();
                if (added.Count == 0 && modified.Count == 0 && deleted.Count == 0) { return; }


                // === Строим попарно непересекающиеся множества ===
                // Приоритет: added > deleted > modified/geom.
                // Если элемент и добавлен, и удалён — считаем «добавленным» (transient),
                // в ExecuteDelete не пускаем. Если после этого в doc его нет —
                // ProcessSnapshot отфильтрует по null.
                var addHash = new HashSet<ElementId>(added);
                var modifHash = new HashSet<ElementId>(modified);
                var delHash = new HashSet<ElementId>(deleted);

                delHash.ExceptWith(addHash);          // added «побеждает» deleted
                addHash.ExceptWith(delHash);          // уже disjoint, страховка
                modifHash.ExceptWith(addHash);
                modifHash.ExceptWith(delHash);

                // geom из оставшегося modif
                var geomHash = new HashSet<ElementId>();
                foreach (var id in modifHash.ToList())
                {
                    if (data.IsChangeTriggered(id, Element.GetChangeTypeGeometry()))
                    {
                        geomHash.Add(id);
                        modifHash.Remove(id);
                    }
                }

                bool anyProcessed = false;

                if (addHash.Count > 0 || geomHash.Count > 0 || modifHash.Count > 0)
                {
                    var snap = new SnapShotElements(now, doc, addHash, geomHash, modifHash);
                    if (PeriodicHandler.Execute(snap))
                    {
                        anyProcessed = true;
                    }
                }

                if (delHash.Count > 0)
                {
                    PeriodicHandler.ExecuteDelete(now, doc, delHash);
                    anyProcessed = true;
                }

                if (anyProcessed)
                {
                    DateLastExecute = now;
                }



            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
            }
            finally
            {
                IsProcessingOwnChange = false;
            }
        }

        private static void ClearInternal()
        {
            AddedIds.Clear();
            GeomModificationIds.Clear();
            ModifiedIdsProp.Clear();
            IsExistElementsChange = false;
        }
        public static SnapShotElements GetSnapShotElements(Document document)
        {
            lock (_lock)
            {
                if (!IsExistElementsChange)// || document != Doc) return null;
                {
                    return null;
                }
                // Стало (логическое сравнение, true только для разных файлов):
                if (!document.Equals(Doc))
                {
                    return null;
                }
                var snap = new SnapShotElements(
                    DateLastExecute, document,
                    AddedIds,
                    GeomModificationIds,
                    ModifiedIdsProp);

                ClearInternal();
                return snap;
            }
        

        }
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;
        public static bool CorrectGroop = false;

        private static readonly object _lock = new object();

        internal static ExternalEvent ExternalEventRef;
        //Обычный пользователь: совершает 3–6 кликов в секунду, что соответствует интервалу в 160–330 мс между кликами.
        private static readonly TimeSpan CooldownAfterProcessing = TimeSpan.FromSeconds(0.125);//столько секунд ждем после обработки собственного изменения

        public static DateTime DateLastExecute = DateTime.MinValue;
        public static volatile bool IsExistElementsChange = false;

        private static Document Doc = null;
        private static readonly HashSet<ElementId> AddedIds = new HashSet<ElementId>();
        private static readonly HashSet<ElementId> GeomModificationIds = new HashSet<ElementId>();
        private static readonly HashSet<ElementId> ModifiedIdsProp = new HashSet<ElementId>();

        public static volatile bool IsProcessingOwnChange = false;

    }
    

    
    
}
