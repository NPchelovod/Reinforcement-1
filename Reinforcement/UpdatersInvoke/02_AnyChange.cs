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
    
    public static class AnyChange 
    {
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;

        private static readonly object _lock = new object();


        public static void Execute(UpdaterData data)
        {
            //сюда приходят от всех изменений элементы
            //бывает приходят пустные
            
            
            if (PeriodicHandler.IsProcessingOwnChange) return;   // ⬅️ рвёт рекурсию
            Document doc = data.GetDocument();
            if (doc == null) { return; }

            var added = data.GetAddedElementIds().ToList();
            var modified = data.GetModifiedElementIds().ToList();
            

            if (!added.Any() && !modified.Any()) { return; }
            //секретная команда
            SecretComand(doc, modified);//секретная команда внутри группы действующая
            if (!AllUpdater) { return; }

            DateTime now = DateTime.Now;
            // Собираем как HashSet, чтобы дедуп и O(1) проверки
            HashSet<ElementId> addedSet = new HashSet<ElementId>(added);
            // modified = то, что изменено, НО НЕ добавлено
            HashSet<ElementId> modifiedSet = modified.Where(id => !addedSet.Contains(id)).ToHashSet();
            HashSet<ElementId> geomModificationEl = new HashSet<ElementId>();
            foreach (var id in modifiedSet)
            {
                if (data.IsChangeTriggered(id, Element.GetChangeTypeGeometry()))
                {
                    geomModificationEl.Add(id);
                }
            }
            lock (_lock)
            {
                //могут сразу несколько раз заполняться
                if(Doc!=doc)
                {
                    Doc = doc; ClearDataElements();
                }
                DateTimeChange = now;
                AddedIds.UnionWith(addedSet);
                ModifiedIds.UnionWith(modifiedSet);
                GeomModificationEl.UnionWith(modifiedSet);
            }

        }

        public static void ClearDataElements()
        {
            lock (_lock)
            {
                AddedIds.Clear(); ModifiedIds.Clear(); GeomModificationEl.Clear();
            }
        }

        
        public static DateTime DateTimeChange=DateTime.MinValue;
        public static Document Doc = null;
        public static HashSet<ElementId> AddedIds = new HashSet<ElementId>();
        public static HashSet<ElementId> ModifiedIds = new HashSet<ElementId>();
        public static HashSet<ElementId> GeomModificationEl = new HashSet<ElementId>();


        
        public static readonly TimeSpan ComandAndChangedMaxTime = TimeSpan.FromSeconds(2);
        //автопоиск соответствия команд
        

        private static int _inSecretComand;
        public static void SecretComand(Document doc, List<ElementId> elementIds)
        {
            //команда срабатывает внутри группы
            if (!App.InPendingSecret)
            { return; }
            
            bool isChange=false;
            try
            {
                if (Interlocked.CompareExchange(ref _inSecretComand, 1, 0) != 0)
                {
                    return;
                }
                isChange=true;
                App.InPendingSecret = false;
                if (string.IsNullOrEmpty(App.OnGroupCurrent.Name))
                {
                    return;
                }
                Element element = doc.GetElement(elementIds[0]);

                if (element == null) { return; }
                ElementId groupId = element.GroupId;
                if (groupId == ElementId.InvalidElementId) { return; }

                //лучше только выделенные, иначе если есть несколько групп на виде то ошибка будет
                List<Element> elements = elementIds.Select(x => Doc.GetElement(x)).ToList();// ArmLengthEquels.SelectOrAllElements(false, false);
                elements = elements.Where(el => el.GroupId == groupId).ToList();
                //надо найти имеющие туже группу

                //надо собрать все элементы которые принадлежат данной группе и находятся на виде
                DeleteDublicateFamily.ReplacedProcess(doc, elements, false, new HashSet<ElementId>(), false);
                
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }
            finally
            {
                App.InPendingSecret = false;
                if (isChange)
                {
                    Interlocked.Exchange(ref _inSecretComand, 0);
                }
            }

        }

        

        
    }
}
