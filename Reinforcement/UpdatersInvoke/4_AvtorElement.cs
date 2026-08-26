using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using Updaters;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class RegisterAutoFillUpdater
    {
        // Используем тот же AddInId, что и в вашем приложении
        public static AddInId addInId { get; set; }
        
        public static void Register(bool forceReregister = false)
        {
            var updater = new AutoFillNoteUpdater();

            if (!UpdaterRegistry.IsUpdaterRegistered(updater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(updater, true);
                var updaterId = updater.GetUpdaterId();

                //ElementClassFilter allElementsFilter = new ElementClassFilter(typeof(Element));

                var categoryIds = new HashSet<ElementId>
                {
                // === ВАШ ИСХОДНЫЙ СПИСОК (АРХИТЕКТУРА И КОНСТРУКЦИИ) ===
                new ElementId(BuiltInCategory.OST_Walls),                  // Стены
                new ElementId(BuiltInCategory.OST_Floors),                 // Перекрытия
                new ElementId(BuiltInCategory.OST_Ceilings),               // Потолки
                new ElementId(BuiltInCategory.OST_StructuralColumns),      // Несущие колонны
                new ElementId(BuiltInCategory.OST_StructuralFraming),      // Несущий каркас (балки)
                new ElementId(BuiltInCategory.OST_GenericModel),           // Обобщенные модели
                new ElementId(BuiltInCategory.OST_GenericAnnotation),      // Типовые аннотации
                new ElementId(BuiltInCategory.OST_Views),                  // Виды
                new ElementId(BuiltInCategory.OST_Grids),                  // Оси
                new ElementId(BuiltInCategory.OST_Doors),                  // Двери
                new ElementId(BuiltInCategory.OST_Windows),                // Окна
                new ElementId(BuiltInCategory.OST_Lines),                  // Линии
                new ElementId(BuiltInCategory.OST_TextNotes),              // Текстовые примечания
                new ElementId(BuiltInCategory.OST_Stairs),                 // Лестницы
                new ElementId(BuiltInCategory.OST_DetailComponents),// например узлы или область маскировки
                new ElementId(BuiltInCategory.OST_Dimensions),
                new ElementId(BuiltInCategory.OST_SpotCoordinates),//координаты
                // === ВАШИ НОВЫЕ КАТЕГОРИИ (ИНЖЕНЕРИЯ) ===
                new ElementId(BuiltInCategory.OST_PipeCurves),             // Трубы
                new ElementId(BuiltInCategory.OST_MechanicalEquipment),     // Оборудование (ОВ/ВК/Технология)
                new ElementId(BuiltInCategory.OST_DuctTerminal),           // Воздухораспределители (решетки)

                // === АНАЛОГИЧНЫЕ MEP-КАТЕГОРИИ (НЕОБХОДИМЫЕ ДЛЯ КОМПЛЕКТА) ===
                // 1. Вентиляция
                new ElementId(BuiltInCategory.OST_DuctCurves),             // Воздуховоды
                new ElementId(BuiltInCategory.OST_DuctFitting),            // Фитинги воздуховодов (отводы, тройники)
                new ElementId(BuiltInCategory.OST_DuctAccessory),          // Арматура воздуховодов (дроссели, клапаны)
                new ElementId(BuiltInCategory.OST_FlexDuctCurves),         // Гибкие воздуховоды

                // 2. Трубопроводы и сантехника
                new ElementId(BuiltInCategory.OST_PipeFitting),            // Фитинги трубопроводов
                new ElementId(BuiltInCategory.OST_PipeAccessory),          // Арматура трубопроводов (краны, вентили)
                new ElementId(BuiltInCategory.OST_FlexPipeCurves),         // Гибкие трубы
                new ElementId(BuiltInCategory.OST_PlumbingFixtures),       // Сантехнические приборы

                new ElementId(BuiltInCategory.OST_PipeTags),

                // 3. Электрика и слаботочка
                new ElementId(BuiltInCategory.OST_ElectricalEquipment),     // Электрооборудование (щиты)
                new ElementId(BuiltInCategory.OST_ElectricalFixtures),      // Электроустановочные изделия (розетки)
                new ElementId(BuiltInCategory.OST_LightingFixtures),        // Осветительные приборы (светильники)
                new ElementId(BuiltInCategory.OST_CableTray),               // Кабельные лотки
                new ElementId(BuiltInCategory.OST_Conduit),                 // Короба / Трубы для кабеля
                new ElementId(BuiltInCategory.OST_DataDevices),             // Сетевые устройства (слаботочка)
                // добавьте другие нужные категории

                new ElementId(BuiltInCategory.OST_IOSDetailGroups),
                new ElementId(BuiltInCategory.OST_TitleBlocks),

                new ElementId(BuiltInCategory.OST_Toposolid),
                new ElementId(BuiltInCategory.OST_StairsRailing),
                new ElementId(BuiltInCategory.OST_ShaftOpening),
                new ElementId(BuiltInCategory.OST_RoomSeparationLines),


                }
            ;

                //categoryIds = new List<ElementId>();
                //foreach (BuiltInCategory bic in Enum.GetValues(typeof(BuiltInCategory)))
                //{
                //    // Пропускаем заведомо недопустимые или внутренние категории
                //    if (bic == BuiltInCategory.INVALID || bic == BuiltInCategory.OST_IOSModelGroups)
                //        continue;

                //    try
                //    {
                //        categoryIds.Add(new ElementId(bic));
                //    }
                //    catch
                //    {
                //        // Некоторые категории могут быть недопустимы для ElementId — просто пропускаем
                //    }
                //}


                // Фильтр: все элементы (или только нужные категории)
                //ElementClassFilter classFilter = new ElementClassFilter(typeof(Element));

                ElementMulticategoryFilter classFilter = new ElementMulticategoryFilter(categoryIds.ToList());


                //ElementClassFilter classFilter = new ElementClassFilter(typeof(FamilyInstance));
                // При необходимости можно ограничить категориями, где есть параметр ADSK_Примечание

                // Триггер на добавление элементов

                UpdaterRegistry.AddTrigger(updaterId, classFilter, Element.GetChangeTypeAny()); //Element.GetChangeTypeElementAddition());
                //Нет, Element.GetChangeTypeAny() не реагирует на создание (и удаление) Это частая ловушка в Revit API.
                UpdaterRegistry.AddTrigger(updaterId, classFilter, Element.GetChangeTypeElementAddition());


            }
            else if (forceReregister)
            {
                UpdaterRegistry.RemoveAllTriggers(updater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId());
                Register(false);
            }
        }
        public static void RegisterUpdater()
        {
            AutoFillNoteUpdater updater = new AutoFillNoteUpdater();
            UpdaterRegistry.RegisterUpdater(updater);

            // Фильтр – все элементы (можно уточнить, если параметр есть только у определённых категорий)
            ElementClassFilter filter = new ElementClassFilter(typeof(Element));

            // Триггер на добавление элементов
            UpdaterRegistry.AddTrigger(
                updater.GetUpdaterId(),
                filter,
                Element.GetChangeTypeElementAddition());
        }
    }
    public class AutoFillNoteUpdater : IUpdater
    {
        public void Execute(UpdaterData data)
        {
            AnyChange.Execute(data); // передаём главному методу распределения
        }

        //автор элемента записывается при создании
        public static bool regWriterAvtor=true;
        public static bool regWriterAvtorPrim = false;

        public static bool regWriterSoAvtor = true;

        public static bool longAvtors=true;


        public static string NameAvtor = "ЕС_Автор";
        public static string NameSoAvtor = "ЕС_Посл Автор";
        public static string NamePrimeh = "ADSK_Примечание";
        public static string time;
        public static string username;
        public static string userDate;

        private static DateTime _lastExecutionTime = DateTime.MinValue;
        private static readonly TimeSpan _minimumInterval = TimeSpan.FromMilliseconds(200); // задержка 500 мс

        
       

        public static void AvtorUpdater(UpdaterData data)
        {
            if (!regWriterAvtor) { return; }

            Document doc = data.GetDocument();
            DateTime now = DateTime.Now;
            time = now.ToString("dd.MM.yy.HH");

            username = doc.Application.Username;
            userDate = username + "_" + time;

            var addedIds = data.GetAddedElementIds();// — элементы, которые были добавлены в модель;
            var modifiedIds = data.GetModifiedElementIds();
            //ExecuteNew(data, doc);// обработка добавленных

            now = DateTime.UtcNow;
            if (now - _lastExecutionTime < _minimumInterval)
            {
                return; // прошло слишком мало времени — игнорируем
            }


            ExecuteChange(data, doc, addedIds); // обработка изменённых
            if (regWriterSoAvtor)
            {
                ExecuteChange(data, doc, modifiedIds); // обработка изменённых
            }
        }
        


        public static bool SetParam(Parameter param, string value)
        {
            if(param == null) { return false; }
            string pastValue = param.AsString();

            if(!string.IsNullOrEmpty(pastValue) && pastValue == value) {  return false; }
            try
            {
                param.Set(value);
            }
            catch
            {
                return false;
            }
            return true;
        }

        //public HashSet<ElementId> pastIds=new HashSet<ElementId>();//от рекурсии
        private static void ExecuteChange(UpdaterData data, Document doc, ICollection<ElementId> modifiedIds)
        {

            
            if ( !modifiedIds.Any()) { return; }
            
            // Имя текущего пользователя Revit
            //string username = doc.Application.Username;

            //var pastId2= new HashSet<ElementId>();

            foreach (var id in modifiedIds)
            {
                Element element = doc.GetElement(id);
                if (element == null)// || pastIds.Contains(id))
                    continue;

                //pastId2.Add(id);
                // Ищем параметр по имени

                //записываем автора и соавтора
                Parameter noteParam = element.LookupParameter(NameAvtor);
                if (noteParam == null && regWriterAvtorPrim)
                {
                    noteParam = element.LookupParameter(NamePrimeh);
                }
                if (noteParam == null) { continue; }
                

                string value = noteParam.AsString();
                if (string.IsNullOrEmpty(value))
                {
                    //записываем автора
                    SetParam(noteParam, userDate);
                }
                //автора всегда записываем в соавторы

                Parameter noteParam2 = element.LookupParameter(NameSoAvtor);
                if (noteParam2 != null)
                {
                    string pastValue = noteParam2.AsString();
                            
                    //записываем соавтора
                    string newValue = nameNew(pastValue);

                    SetParam(noteParam2, newValue);
                    //иначе записываем в соавторы
                }
                else
                {
                    string newValue = nameNew(value);
                    //записываем в авторы в придачу
                    SetParam(noteParam, newValue);
                    //иначе записываем в соавторы

                }
                    
                
            }
            //pastIds = pastId2;

            DateTime now = DateTime.UtcNow;
            _lastExecutionTime = now;
        }

        private static string nameNew(string pastValue)
        {
            string newValue = "";

            

            if (!longAvtors|| string.IsNullOrEmpty(pastValue) || pastValue.Length > 160)
            {
                newValue = userDate;
            }
            else if (pastValue.Contains(username))
            {
                //newValue = pastValue;// userDate;
                // Разбиваем pastValue на отдельные записи, убираем лишние пробелы
                // Разбиваем строку на отдельные записи, убираем пустые и лишние пробелы
                var entries = pastValue
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                // Удаляем ВСЕ записи, начинающиеся с "username_"
                string prefix = username + "_";
                entries.RemoveAll(e => e.StartsWith(prefix));

                // Вставляем актуальную запись (userDate) в начало списка
                entries.Insert(0, userDate);

                // Собираем строку обратно
                newValue = string.Join(", ", entries);
            }
            else
            {
                newValue = userDate+ ", "+pastValue;
            }
            return newValue;
        }





        private static AddInId _addInId => RegisterAutoFillUpdater.addInId;
       
        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457271E"));
        public string GetAdditionalInformation() => string.Empty;
        public ChangePriority GetChangePriority() => ChangePriority.Annotations;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "Updater для заполнения ADSK_Примечание";
    }
}
