using Autodesk.Revit.Attributes;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using static Autodesk.Revit.DB.SpecTypeId;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Reinforcement
{
    public  class AnyChangeClass : IUpdater
    {
        public void Execute(UpdaterData data)
        {
            // Делегируем главному диспетчеру (если так задумано)
            try
            {
                AnyChange.Execute(data); // передаём главному методу распределения
            }

            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
                // Debug.WriteLine($"Ошибка в AnyChange: {ex.Message}");
            }

        }
        private static AddInId _addInId => RegisterAutoFillUpdater.addInId;

        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457271E"));
        public string GetAdditionalInformation() => string.Empty;
        public ChangePriority GetChangePriority() => ChangePriority.Annotations;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => App.TabName+"_Updater всех процессов";

    }
    public class SnapShotElements
    {
        public HashSet<ElementId> AddedIds;// добавленный новый элемент
        public HashSet<ElementId> GeomModificationIds;//не добавленный, геом измененный элемент
        public HashSet<ElementId> ModifiedIdsProp;//не добавленные и не геометрически измененный
        public DateTime DateTimeCreate;
        public Document Doc;
        public SnapShotElements(DateTime dateTimeCreate, Document doc, ICollection<ElementId> addedIds, ICollection<ElementId> geomModificationIds, ICollection<ElementId> modifiedIdsProp)
        {
            Doc = doc;
            DateTimeCreate = dateTimeCreate;
            AddedIds = new HashSet<ElementId>(addedIds);
            GeomModificationIds = geomModificationIds.Where(x=> !addedIds.Contains(x)).ToHashSet();
            ModifiedIdsProp = modifiedIdsProp.Where(x => !addedIds.Contains(x) && !GeomModificationIds.Contains(x)).ToHashSet();
        }
    }

    public static partial class AnyChange 
    {
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;
        public static bool CorrectGroop =false;

        private static readonly object _lock = new object();

        internal static ExternalEvent ExternalEventRef;
        private static readonly TimeSpan CooldownAfterProcessing = TimeSpan.FromSeconds(0.15);//столько секунд ждем после обработки собственного изменения

        public static DateTime DateLastExecute = DateTime.MinValue;
        public static volatile bool IsExistElementsChange = false;

        private static Document Doc = null;
        private static readonly HashSet<ElementId> AddedIds = new HashSet<ElementId>();
        private static readonly HashSet<ElementId> GeomModificationIds = new HashSet<ElementId>();
        private static readonly HashSet<ElementId> ModifiedIdsProp = new HashSet<ElementId>();

        public static void Execute(UpdaterData data)
        {
            //сюда приходят от всех изменений элементы
            //бывает приходят пустные
            if (!AllUpdater) { return; }
            if (PeriodicHandler.IsProcessingOwnChange) return;   // ⬅️ рвёт рекурсию мы итак в нашем изменении
                                                                 //наверно надо еще и задержку по времени добавить в 0.1 - 0.2 секунды чтобы после нашего вмешательства не срабатывало повторно
                                                                 // 2. Наш PeriodicHandler отработал < 0.2 с назад — «хвосты» от него

            //изменен я которые случились менее чем за 150 мс
            // проверка не защищает от рекурсии, а глушит пользователя:
            //if (PeriodicHandler.IsRecentlyExecuted(CooldownAfterProcessing)) return;

            Document doc = data.GetDocument();
            if (doc == null) { return; }

            var added = data.GetAddedElementIds();
            var modified = data.GetModifiedElementIds();
            
            if (!added.Any() && !modified.Any()) { return; }
            //секретная команда
           
            

            DateTime now = DateTime.Now;
            // Геометрию вычисляем ЗДЕСЬ, пока UpdaterData валиден
            var geomSet = new HashSet<ElementId>();
            foreach (var id in modified)
            {
                if (data.IsChangeTriggered(id, Element.GetChangeTypeGeometry()))
                    geomSet.Add(id);
            }
            var modifiedIdsPropt = modified.Where(x => !geomSet.Contains(x)).ToList();
            lock (_lock)
            {
                if (Doc == null || !doc.Equals(Doc)) ClearInternal();//Doc != doc — та же ловушка, что и раньше
                
                Doc = doc;
                DateLastExecute = now;
                AddedIds.UnionWith(added);
                GeomModificationIds.UnionWith(geomSet);
                ModifiedIdsProp.UnionWith(modifiedIdsPropt);
                IsExistElementsChange = true;
            }
            PeriodicHandler.Execute(Doc);
        }
        

        internal static void RequestProcessing()
        {
            var ev = ExternalEventRef;
            if (ev == null) return;
            if (!IsExistElementsChange) return;

            try { ev.Raise(); } catch { /* Revit занят — таймер-страховка повторит */ }
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



        

        

        
    }
}
