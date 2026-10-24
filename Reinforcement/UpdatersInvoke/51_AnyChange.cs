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
    
    public static class AnyChange 
    {
        //тут запиши какие хочешь действия на любые действия

        public static bool AllUpdater = true;

        public static SnapshotModificationElements PastSnapshotModificationElements=null;
        public static Queue<SnapshotModificationElements> SnapshotModificationElementsQueue = new Queue<SnapshotModificationElements>();
        
        private static readonly TimeSpan _minimumInterval = TimeSpan.FromMilliseconds(200);
        private static readonly int MaxHistory = 6;// 
        public static void Execute(UpdaterData data)
        {
            //сюда приходят от всех изменений элементы
            //бывает приходят пустные
            
            try
            {
                Document doc = data.GetDocument();
                if (doc == null) { return; }
                var added = data.GetAddedElementIds().ToList();
                var modified = data.GetModifiedElementIds().ToList();

                //секретная команда
                SnapshotModificationElements snapshotModificationElements = CreateOrReturnSnapshot(doc, added, modified);

                if (snapshotModificationElements == null)
                {
                    return;
                }
                SecretComand(snapshotModificationElements);

                if (!AllUpdater) { return; }
                
                try
                {

                    //LookUsersUpdate(data);
                    //не заходим в удаленные элементы!!!

                    // if ( data.GetDeletedElementIds().Count > 0) { return; }
                    //меняем автора элемента
                    int c = 0;
                    //логика вынесена в транзакцию и подписку
                    //AutoFillNoteUpdater.AvtorUpdater(snapshotModificationElements);
                }
                catch (Exception ex)
                {
                    RevitAPI.AppErrors.LogError(ex);
                    // Debug.WriteLine($"Ошибка в AnyChange: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
                
            }
            
        }

        public static void ProcessAllUpdeters()
        {
            //процесс обновления всех элементов которых нам надо
        }



       


        public static readonly TimeSpan ComandAndChangedMaxTime = TimeSpan.FromSeconds(2);
        //автопоиск соответствия команд
        

        private static int _inSecretComand;
        public static void SecretComand(SnapshotModificationElements snapshotModificationElements)
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

                   
                Element elemGroup = snapshotModificationElements.AddAndModifiedsElements.FirstOrDefault();//элемент в группе
                if (elemGroup == null) { return; }
                ElementId groupId = elemGroup.GroupId;
                if (groupId == ElementId.InvalidElementId) { return; }

                //лучше только выделенные, иначе если есть несколько групп на виде то ошибка будет
                List<Element> elements = snapshotModificationElements.AddAndModifiedsElements;// ArmLengthEquels.SelectOrAllElements(false, false);
                elements = elements.Where(el => el.GroupId == groupId).ToList();
                //надо найти имеющие туже группу

                //надо собрать все элементы которые принадлежат данной группе и находятся на виде
                DeleteDublicateFamily.ReplacedProcess(snapshotModificationElements.Doc, elements, false, new HashSet<ElementId>(), false);
                

                
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
                    isChange=false;
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

        
    }
}
