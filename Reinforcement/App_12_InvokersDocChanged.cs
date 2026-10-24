using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public enum ESnapshotMod
    {
        None = 0,
        FromDocChanged=1,
        FromInvoker=2,
        IsPereodicalHandler, // что уже обработана в процессе
    }
    public class GroopCurrent
    {
        //эта текущая группа а может и не группа вовсе
        //если эта группа то бывает вложенной тогда FatherGroopCurrent её отец
        public DateTime InGroopTime =DateTime.MinValue;
        public string Name;
        public ElementId Id;
        public bool InGroop=false;
        public Document doc=null;
        public GroopCurrent(DateTime inGroopTime,Document doc=null,bool inGroop=false, string name=null, ElementId elementId=null) 
        {
            InGroopTime = inGroopTime; InGroop = inGroop; Name = name; Id = elementId;
        }
        public GroopCurrent FatherGroopCurrent=null;
        public void Clear()
        {
            // поидее все не так
            FatherGroopCurrent = null; InGroop = false;
        }
    }

    public class SnapshotModificationElements
    {
        //Слепок измененных элементов
        public DateTime DateCreate=DateTime.MinValue;
        public string CommandName=null;
        public ESnapshotMod Father = ESnapshotMod.None;//происхождение
        public SnapshotModificationElements(DateTime dateCreate, HashSet<ElementId> addedIds, HashSet<ElementId> modifiedIds, HashSet<ElementId> addAndModifiedsElementsId,  Document doc, ESnapshotMod father, string commandName)
        {
            DateCreate = dateCreate;
            AddedIds = addedIds;
            ModifiedIds = modifiedIds;
            AddAndModifiedsElementsId = addAndModifiedsElementsId;
            
            Doc = doc;
            Father = father;
            CommandName = commandName;
        }
        public Document Doc = null;
        public HashSet<ElementId> AddedIds = new HashSet<ElementId>();
        public HashSet<ElementId> ModifiedIds = new HashSet<ElementId>();
        public HashSet<ElementId> AddAndModifiedsElementsId = new HashSet<ElementId>();

        private List<Element> _addedElements = new List<Element>();
        private List<Element> _modifiedElements = new List<Element>();
        private List<Element> _addAndModifiedsElements = new List<Element>();
        private bool _isElementInitializ = false;

        public List<Element> AddedElements {  get { Initializaer(); return _addedElements; } }
        public List<Element> ModifiedElements {  get { Initializaer(); return _modifiedElements; } }
        public List<Element> AddAndModifiedsElements { get { Initializaer(); return _modifiedElements; } }
        private void Initializaer()
        {
            if (_isElementInitializ ||Doc==null) { return; }
            _isElementInitializ= true;
           
            foreach (var id in AddedIds)
            {
                Element element = Doc.GetElement(id);
                if (element == null) continue;
                _addedElements.Add(element);
                _addAndModifiedsElements.Add(element);
            }
            foreach (var id in ModifiedIds)
            {
                Element element = Doc.GetElement(id);
                if (element == null) continue;
                _modifiedElements.Add(element);
                _addAndModifiedsElements.Add(element);
            }
        }

        public HashSet<ESnapshotMod> ESnapshotMods = new HashSet<ESnapshotMod>();


    }

    public partial class App
    {
        //это от изменени ядокумента
        public static List<SnapshotModificationElements> SnapshotModificationElementsList = new List<SnapshotModificationElements>();
        public static SnapshotModificationElements CurrentSnapshotModificationElements =null;
        //это от подписки на события, может совпадать с изменением от документа элементами
        public static List<SnapshotModificationElements> SnapshotModificationElementsListFromInvoke = new List<SnapshotModificationElements>();
        public static SnapshotModificationElements CurrentSnapshotModificationElementsFromInvoke = null;


        private readonly static int _maxCountTranzSave = 6;//не более стольки транзаций храним
        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {

            IList<string> txNames = e.GetTransactionNames();
            // Пропускаем свою служебную транзакцию, иначе получим рекурсию:
            // SetParam -> Commit -> DocumentChanged -> снапшот -> SetParam -> ...
            bool hasMatch = txNames.Any(n =>
         n.StartsWith(TabName, StringComparison.OrdinalIgnoreCase));
            if (hasMatch)
            {
                //это наша собственная транзакция
                return;
                //txNames.Any(n => string.Equals(n, PeriodicHandler.TransactionName,
                //StringComparison.OrdinalIgnoreCase))) как проверить что не равен а начинается с PeriodicHandler.TransactionName
            }

            string comand = txNames.FirstOrDefault();
            if (string.IsNullOrEmpty(comand))
            {
                return;
            }

            Document doc = e.GetDocument();

            // Отсекаем фоновые документы/семейства, если они не нужны
            if (doc == null) { return; }

            var added = e.GetAddedElementIds().ToList();
            var modified = e.GetModifiedElementIds().ToList();

            if (!added.Any() && !modified.Any()) {  return; }

            //0. - записываем в апдейтер нашу команду для статистики
            RevitAPI.LookUsers.Update(comand, EDocStatsOptions.Invoker);

            DateTime now = DateTime.Now;
            //1. запись прошлых команд

            SnapshotModificationElements snapshotModificationElements = CreateSnapshot(now,doc, added, modified,ESnapshotMod.FromDocChanged, comand);

            //2. === Магия: проверяем секретный паттерн ===
            InOrOutGroopAndSecretCommandPossible(now,txNames, snapshotModificationElements);
        }


        private static readonly object _syncSnapshot = new object();
        public static SnapshotModificationElements CreateSnapshot(DateTime now, Document doc, List<ElementId> added, List<ElementId> modified, ESnapshotMod father, string commandName)
        {
            // Собираем как HashSet, чтобы дедуп и O(1) проверки
            var addedSet = new HashSet<ElementId>(added);
            // modified = то, что изменено, НО НЕ добавлено
            var modifiedSet = modified.Where(id => !addedSet.Contains(id)).ToHashSet();

            if (!addedSet.Any() && !modifiedSet.Any())
            {
                return null;
            }

            // Общий набор: added ∪ (modified \ added) = added ∪ modified, без дублей
            var allSet = new HashSet<ElementId>(addedSet.Concat(modifiedSet));

            SnapshotModificationElements snapshotModificationElements = new SnapshotModificationElements(now, addedSet, modifiedSet, allSet, doc, father, commandName);

            lock (_syncSnapshot)
            {
                if (father == ESnapshotMod.FromDocChanged)
                {
                    if (SnapshotModificationElementsList.Count >= _maxCountTranzSave)
                    {
                        SnapshotModificationElementsList.RemoveAt(0);//убираем самый старый элемент
                    }
                    CurrentSnapshotModificationElements = snapshotModificationElements;
                    SnapshotModificationElementsList.Add(snapshotModificationElements);
                    PeriodicHandler.ChangeModelFromDoc = true;//что модель изменилась за эту секунду
                }
                else if(father == ESnapshotMod.FromInvoker)
                {
                    if (SnapshotModificationElementsListFromInvoke.Count >= _maxCountTranzSave)
                    {
                        SnapshotModificationElementsListFromInvoke.RemoveAt(0);//убираем самый старый элемент
                    }
                    CurrentSnapshotModificationElementsFromInvoke = snapshotModificationElements;
                    SnapshotModificationElementsListFromInvoke.Add(snapshotModificationElements);

                    PeriodicHandler.ChangeModelFromInvoke = true;//что модель изменилась за эту секунду
                }

            }

            return snapshotModificationElements;
        }

        private void InOrOutGroopAndSecretCommandPossible(DateTime now, IList<string> txNames, SnapshotModificationElements snapshotModificationElements)
        {
            //проверка входа в группу и проверка на секретную команду

            //1. проверка на секретную команду
            if (!string.IsNullOrEmpty(OnGroupCurrent.Name) && MatchesSecretPattern())
            {
                InPendingSecret = true;//применение секрета
            }

            //3. --- распознаем Вход в режим редактирования группы ---
            if (txNames.Any(n => EnterGroupTxNames.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                
                Document doc = snapshotModificationElements.Doc;
                foreach (var id in snapshotModificationElements.AddAndModifiedsElementsId)
                {
                    Element elem = doc.GetElement(id);
                    if (elem == null) continue;

                    // Вариант 2: это тип группы (GroupType) — тоже валидный результат
                    if (elem is GroupType groupType)
                    {
                        CreateGroop(doc,now, groupType.Name, groupType.Id);
                        return;
                    }
                    // Вариант 1: это экземпляр группы (модельной или детальной)
                    if (elem is Group group)
                    {
                        CreateGroop(doc, now, group.Name, group.Id);
                        return;
                    }

                    // Вариант 3: это какой-то элемент, у которого есть GroupId — возможно, мы внутри группы
                    if (elem.GroupId != ElementId.InvalidElementId)
                    {
                        Element parent = doc.GetElement(elem.GroupId);
                        if (parent is Group parentGroup)
                        {
                            CreateGroop(doc, now, parentGroup.Name, parentGroup.Id);
                            return;
                        }
                    }

                }

                // Если список пустой — фолбэк: берём группу из выделения
                TryCaptureGroupFromSelection(doc,now);

            }
            else if (OnGroupCurrent.InGroop && txNames.Any(n => ExitGroupTxNames.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                // --- Выход из режима редактирования группы ---
                //вообще если мы во вложенной группе то выходя мы тоже в группе!!!!
                if (OnGroupCurrent.FatherGroopCurrent != null)
                {
                    //значит мы в отце теперь, вышли из вложенной группы
                    //боюсь что будут ситуации когда мы типо не вышли из группы поэтому может подстраховать как-то
                    OnGroupCurrent = OnGroupCurrent.FatherGroopCurrent;
                }
                else
                {
                    OnGroupCurrent.Clear();
                }
            }
        }
        private static void CreateGroop(Document doc,DateTime now, string name, ElementId elementId)
        {
            bool inGroop=false;
            if (!string.IsNullOrEmpty(name)) { inGroop = true; }
            GroopCurrent groopCurrent = new GroopCurrent(now, doc, inGroop,name,elementId);
            if (OnGroupCurrent.InGroop && doc== OnGroupCurrent.doc && elementId!= OnGroupCurrent.Id)
            {
                //значит мы во вложенной группе она становится основной группой
                var father = OnGroupCurrent;
                OnGroupCurrent = groopCurrent;
                OnGroupCurrent.FatherGroopCurrent = father;
            }
            else
            {
                OnGroupCurrent = groopCurrent;
            }
        }
        private static void TryCaptureGroupFromSelection(Document doc, DateTime now)
        {
            try // никогда не срабатывает
            {
                UIDocument uidoc = new UIDocument(doc);
                var selIds = uidoc.Selection.GetElementIds();
                foreach (var id in selIds)
                {
                    if (doc.GetElement(id) is Group g)
                    {
                        string name = g.GroupType?.Name ?? g.Name;
                        CreateGroop(doc,now,name, g.Id);
                        
                        return;
                    }
                }
            }
            catch { /* selection может быть недоступен — игнорируем */ }
        }

        


        public static bool InPendingSecret = false;
        private static bool MatchesSecretPattern()
        {
            int count = SnapshotModificationElementsList.Count;
            int take = 4;
            if (count < 4) return false;

            // Берём последние 4 записи в хронологическом порядке
            // (Queue выдаёт в порядке добавления — от старых к новым)
            var last4 = count <= take
            ? SnapshotModificationElementsList.ToArray()
            : SnapshotModificationElementsList.Skip(count - take).Take(take).ToArray();
            // last4[0] — старейшая из четвёрки, last4[3] — самая свежая

            // 1. Проверяем имена
            for (int i = 0; i < 4; i++)
            {
                if (!string.Equals(last4[i].CommandName, PatternToDetect[i],
                                   StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 2. Проверяем временной интервал
            var span = last4[3].DateCreate - last4[0].DateCreate;
            return span.TotalMilliseconds <= PatternTimeWindowMs;
        }

        //важная вещь показывает в группе ли мы находимся
        public static GroopCurrent OnGroupCurrent = new GroopCurrent(DateTime.MinValue);
        

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
      
        private const int PatternTimeWindowMs = 12000; // 6 секунд - мало бывает виснет - время на выполнения секретной команды
        private static readonly string[] PatternToDetect =
        {
            "Закрепить",
            "Открепить",
            "Закрепить",
            "Открепить"
        };

    }
}
