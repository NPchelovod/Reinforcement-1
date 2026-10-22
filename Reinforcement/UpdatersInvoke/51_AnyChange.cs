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
            AnyChange.Execute(data); // передаём главному методу распределения
        }
        private static AddInId _addInId => RegisterAutoFillUpdater.addInId;

        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457271E"));
        public string GetAdditionalInformation() => string.Empty;
        public ChangePriority GetChangePriority() => ChangePriority.Annotations;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "ENS_Updater всех процессов";
    }
    public class SnapshotModificationElements
    {
        //Слепок измененных элементов
        public DateTime DateCreate;

        public SnapshotModificationElements(DateTime dateCreate, HashSet<ElementId> addedIds, HashSet<ElementId> modifiedIds, HashSet<ElementId> addAndModifiedsElementsId, List<Element> addedElements, List<Element> modifiedElements, List<Element> addAndModifiedsElements, Document doc, string commandName)
        {
            DateCreate = dateCreate;
            AddedIds = addedIds;
            ModifiedIds = modifiedIds;
            AddAndModifiedsElementsId = addAndModifiedsElementsId;
            AddedElements = addedElements;
            ModifiedElements = modifiedElements;
            AddAndModifiedsElements = addAndModifiedsElements;
            Doc = doc;
            CommandName = commandName;
        }

        public HashSet<ElementId> AddedIds = new HashSet<ElementId>();
        public HashSet<ElementId> ModifiedIds = new HashSet<ElementId>();
        public HashSet<ElementId> AddAndModifiedsElementsId = new HashSet<ElementId>();

        public List<Element> AddedElements = new List<Element>();
        public List<Element> ModifiedElements = new List<Element>();
        public List<Element> AddAndModifiedsElements = new List<Element>();

        public Document Doc = null;

        public string CommandName = null;
        public bool IsCommand => CommandName != null && (AddedIds.Count > 0 || ModifiedIds.Count > 0);


    }

    public static class AnyChange 
    {
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;

        public static SnapshotModificationElements PastSnapshotModificationElements=null;
        public static Queue<SnapshotModificationElements> SnapshotModificationElementsQueue = new Queue<SnapshotModificationElements>();
        private static readonly object _sync = new object();
        private static readonly int MaxHistory = 6;// 
        public static void Execute(UpdaterData data)
        {
            //сюда приходят от всех изменений элементы
            //бывает приходят пустные
            
            try
            {
                Document doc = data.GetDocument();
                if (doc == null) { return; }

                // Собираем как HashSet, чтобы дедуп и O(1) проверки
                var addedSet = new HashSet<ElementId>(data.GetAddedElementIds());
                // modified = то, что изменено, НО НЕ добавлено
                var modifiedSet = data.GetModifiedElementIds().Where(id => !addedSet.Contains(id)).ToHashSet();
                

                if (!addedSet.Any() && !modifiedSet.Any())
                {
                    return;
                }
                
                // Общий набор: added ∪ (modified \ added) = added ∪ modified, без дублей
                var allSet = new HashSet<ElementId>(addedSet.Concat(modifiedSet));

                
                List<Element> addedElements = new List<Element>(); 
                List<Element> modifElements = new List<Element>();
                List<Element> addAndModifiedsElements = new List<Element>();

                foreach (var id in addedSet)
                {
                    Element element = doc.GetElement(id);
                    if (element == null) continue;
                    addedElements.Add(element);
                    addAndModifiedsElements.Add(element);

                }
                foreach (var id in modifiedSet)
                {
                    Element element = doc.GetElement(id);
                    if (element == null) continue;
                    modifElements.Add(element);
                    addAndModifiedsElements.Add(element);
                }

                DateTime now = DateTime.Now;
                SnapshotModificationElements snapshotModificationElements = new SnapshotModificationElements(now, addedSet, modifiedSet, allSet, addedElements, modifElements, addAndModifiedsElements,doc, null  );
                lock (_sync)
                {
                    SnapshotModificationElementsQueue.Enqueue(snapshotModificationElements);
                    PastSnapshotModificationElements = snapshotModificationElements;
                    if (SnapshotModificationElementsQueue.Count >= MaxHistory)
                    {
                        SnapshotModificationElementsQueue.Dequeue();//убираем самый старый элемент
                    }
                }

                //секретная команда

                SecretComand(data);

                if (!AllUpdater) { return; }
                
                try
                {

                    //LookUsersUpdate(data);
                    //не заходим в удаленные элементы!!!

                    // if ( data.GetDeletedElementIds().Count > 0) { return; }
                    //меняем автора элемента
                    AutoFillNoteUpdater.AvtorUpdater(snapshotModificationElements);
                }
                catch (Exception ex)
                {
                    App_Apdater_1.AppErrors.LogError(ex);
                    // Debug.WriteLine($"Ошибка в AnyChange: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
                
            }
            
        }
        //переподписаться


        private static int _inSecretComand;
        private static void SecretComand(UpdaterData data)
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
                
                App.InPendingSecret = false;
                Document doc = data.GetDocument();
                if (doc == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(App.OnGroupCurrent.Name))
                {
                    Element elemGroup = GetAnyElement(data);//элемент в группе
                    if (elemGroup == null) { return; }
                    ElementId groupId = elemGroup.GroupId;
                    if (groupId == ElementId.InvalidElementId) { return; }

                    List<Element> elements = ArmLengthEquels.SelectOrAllElements(false, false);
                    elements = elements.Where(el => el.GroupId == groupId).ToList();
                    //надо найти имеющие туже группу

                    //надо собрать все элементы которые принадлежат данной группе и находятся на виде
                    DeleteDublicateFamily.ReplacedProcess(doc, elements, false, new HashSet<ElementId>(), false);
                }

                
            }
            catch (Exception ex)
            {
                App_Apdater_1.AppErrors.LogError(ex);
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

        public static Element GetAnyElement(UpdaterData data)
        {
            Document doc = data.GetDocument();
            if (doc == null)
            {
                return null;
            }
            // Получаем списки элементов
            var addedIds = data.GetAddedElementIds();
            var modifiedIds = data.GetModifiedElementIds();
            if (addedIds.Count > 0)
            {
                Element element = doc.GetElement(addedIds.First());
                return element;
            }
            if (modifiedIds.Count > 0)
            {
                Element element = doc.GetElement(modifiedIds.First());
                return element;
            }
            return null;
        }

        public static void PodpiskaAll()
        {
            var app = App.Application;//RevitAPI.UiApplication;
            if (app != null)
            {
                RegisterUpdater.addInId = app.ActiveAddInId;
                RegisterUpdater.Register();

                RegisterZakladkaUpdater.addInId = app.ActiveAddInId;
                RegisterZakladkaUpdater.Register();

                RegisterAutoFillUpdater.addInId = app.ActiveAddInId;

                RegisterAutoFillUpdater.Register();
            }
        }
    }
}
