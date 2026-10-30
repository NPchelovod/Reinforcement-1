using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Reinforcement
{
   
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

    public  struct Command
    { 
        public string CommandName { get; set; }
        public DateTime DateCreate { get; set; }

    }


    public partial class App
    {
        
        public static Command CurentCommand = new Command();
        public static List <Command> Commands = new List <Command>();
        public static DateTime DateLastExecute = DateTime.MinValue;
        private static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                
                var txNames = e.GetTransactionNames();
                if (txNames == null || txNames.Count == 0) return;
                string comand = txNames.First();
                if(string.IsNullOrEmpty(comand)) {return; }

                
                // ⚠️ КЛЮЧЕВОЙ ФИЛЬТР: пропускаем свои транзакции.
                // Здесь и рвётся потенциальная рекурсия.
                bool ours = txNames.Any(n =>
                    !string.IsNullOrEmpty(n) &&
                    n.StartsWith(PeriodicHandler.TransactionName, StringComparison.OrdinalIgnoreCase));
                if (ours) return;
               

                Document doc = e.GetDocument();
                if (doc == null) return;
                //0. - записываем в апдейтер нашу команду для статистики
                RevitAPI.LookUsers.Update(comand, EDocStatsOptions.Invoker);

                DateTime now = DateTime.Now;
                CurentCommand = new Command() { CommandName = comand, DateCreate = now };
                Commands.Add(CurentCommand);

                //2. === Магия: проверяем секретный паттерн ===
                InOrOutGroopAndSecretCommandPossible(now, txNames, doc, e.GetModifiedElementIds());

            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);
            }
        }
        


        private readonly static int _maxCountTranzSave = 6;//не более стольки транзаций храним
        

        

        private static void InOrOutGroopAndSecretCommandPossible(DateTime now, IList<string> txNames, Document doc, ICollection<ElementId> elIds)
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
                
                
                foreach (var id in elIds)
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




        public static volatile bool InPendingSecret = false;
        private static bool MatchesSecretPattern()
        {
            int count = Commands.Count;
            int take = 4;
            if (count < 4) return false;

            // Берём последние 4 записи в хронологическом порядке
            // (Queue выдаёт в порядке добавления — от старых к новым)
            var last4 = count <= take
            ? Commands.ToArray()
            : Commands.Skip(count - take).Take(take).ToArray();
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
