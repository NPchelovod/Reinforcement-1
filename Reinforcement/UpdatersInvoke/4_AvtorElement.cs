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
            // Делегируем главному диспетчеру (если так задумано)
            AnyChange.Execute(data); // передаём главному методу распределения
        }

        // === Конфигурация ===
        public static bool regWriterAvtor = true;       // записывать автора
        public static bool regWriterAvtorPrim = false;  // использовать запасной параметр примечания
        public static bool regWriterSoAvtor = true;     // записывать соавтора
        public static bool longAvtors = true;           // хранить нескольких авторов через запятую

        public static string NameAvtor = "ЕС_Автор";
        public static string NameSoAvtor = "ЕС_Посл Автор";
        public static string NamePrimeh = "ADSK_Примечание";

        // === Временные данные ===
        private static DateTime _lastExecutionTime = DateTime.MinValue;
        private static readonly TimeSpan _minimumInterval = TimeSpan.FromMilliseconds(200);
        private static bool _isUpdating = false; // флаг защиты от рекурсии

        // === Текущие значения (пересчитываются при каждом реальном выполнении) ===
        private static string _username;
        private static string _userDate;




        /// <summary>
        /// Основной метод, вызываемый диспетчером для обработки добавленных/изменённых элементов.
        /// </summary>
        public static void AvtorUpdater(UpdaterData data)
        {
            if (!regWriterAvtor) return;

            // Проверка временного интервала сразу, до каких-либо действий
            DateTime now = DateTime.UtcNow;
            if (now - _lastExecutionTime < _minimumInterval)
                return;

            // Защита от рекурсивного вызова
            if (_isUpdating) return;

            Document doc = data.GetDocument();
            _username = doc.Application.Username;
            _userDate = $"{_username}_{DateTime.Now:dd.MM.yy.HH}";

            // Получаем списки элементов
            var addedIds = data.GetAddedElementIds();
            var modifiedIds = data.GetModifiedElementIds();

            // Устанавливаем флаг выполнения
            _isUpdating = true;
            try
            {
                // Обрабатываем добавленные (автор при создании)
                if (addedIds.Count > 0)
                {
                    ProcessElements(doc, addedIds, isNewElement: true);
                }

                // Обрабатываем изменённые (автор и соавтор)
                if (modifiedIds.Count > 0)
                {
                    ProcessElements(doc, modifiedIds, isNewElement: false);
                }

                // Обновляем время последнего выполнения
                _lastExecutionTime = DateTime.UtcNow;
            }
            finally
            {
                _isUpdating = false;
            }
        }


        /// <summary>
        /// Безопасная установка значения параметра (только если оно изменилось).
        /// </summary>
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

        /// <summary>
        /// Обработка списка элементов: запись автора (при необходимости) и соавтора.
        /// </summary>
        private static void ProcessElements(Document doc, ICollection<ElementId> ids, bool isNewElement)
        {
            foreach (var id in ids)
            {
                Element element = doc.GetElement(id);
                if (element == null) continue;

                // 1. Параметр для автора (или запасной)
                Parameter authorParam = element.LookupParameter(NameAvtor);
                if (authorParam == null && regWriterAvtorPrim)
                    authorParam = element.LookupParameter(NamePrimeh);

                if (authorParam == null) continue; // нет нужного параметра – пропускаем

                string authorValue = authorParam.AsString();

                // Для нового элемента записываем автора, если поле пустое
                if (string.IsNullOrEmpty(authorValue))//isNewElement
                {
                    SetParam(authorParam, _userDate);
                    authorValue = _userDate; // обновляем локальное значение для дальнейшего использования
                }

                // 2. Параметр соавтора
                Parameter coAuthorParam = element.LookupParameter(NameSoAvtor);
                if (coAuthorParam != null)
                {
                    // Если параметр соавтора существует, обновляем его
                    string pastCoAuthors = coAuthorParam.AsString();
                    string newCoAuthors = BuildNewCoAuthors(pastCoAuthors);
                    SetParam(coAuthorParam, newCoAuthors);
                }
                else
                {
                    // Если параметра соавтора нет, записываем в тот же параметр автора (по вашей логике)
                    // Убедитесь, что это действительно нужно! Возможно, лучше просто пропустить.
                    string newValue = BuildNewCoAuthors(authorValue);
                    SetParam(authorParam, newValue);
                }
            }
        }

        /// <summary>
        /// Формирует новую строку соавторов на основе предыдущей.
        /// </summary>
        private static string BuildNewCoAuthors(string pastValue)
        {
            // Если хранение нескольких авторов отключено, или строка пустая/слишком длинная – просто заменяем
            if (!longAvtors || string.IsNullOrEmpty(pastValue) || pastValue.Length > 170)
                return _userDate;

            // Разбиваем на отдельные записи
            var entries = pastValue
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // Удаляем все записи, начинающиеся с "username_"
            string prefix = _username + "_";
            entries.RemoveAll(e => e.StartsWith(prefix));

            // Вставляем актуальную запись в начало
            entries.Insert(0, _userDate);

            return string.Join(", ", entries);
        }






        private static AddInId _addInId => RegisterAutoFillUpdater.addInId;
       
        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457271E"));
        public string GetAdditionalInformation() => string.Empty;
        public ChangePriority GetChangePriority() => ChangePriority.Annotations;
        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "Updater для заполнения ADSK_Примечание";
    }
}
