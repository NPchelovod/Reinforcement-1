using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

        public static readonly HashSet<BuiltInCategory> BuiltInCategorys = new HashSet<BuiltInCategory>()
        { BuiltInCategory.OST_Walls              ,    // Стены
                BuiltInCategory.OST_Floors       ,          // Перекрытия
                BuiltInCategory.OST_Ceilings     ,          // Потолки
                BuiltInCategory.OST_StructuralColumns ,     // Несущие колонны
                BuiltInCategory.OST_StructuralFraming ,     // Несущий каркас (балки)
                BuiltInCategory.OST_GenericModel      ,     // Обобщенные модели
                BuiltInCategory.OST_GenericAnnotation ,     // Типовые аннотации
                BuiltInCategory.OST_Views             ,     // Виды
                BuiltInCategory.OST_Grids               ,   // Оси
                BuiltInCategory.OST_Doors                ,  // Двери
                BuiltInCategory.OST_Windows               , // Окна
                BuiltInCategory.OST_Lines                 , // Линии
                BuiltInCategory.OST_TextNotes              ,// Текстовые примечания
                BuiltInCategory.OST_Stairs                 ,// Лестницы
                BuiltInCategory.OST_DetailComponents,// например узлы или область маскировки
                BuiltInCategory.OST_Dimensions,
                BuiltInCategory.OST_SpotCoordinates,//координаты
                // === ВАШИ НОВЫЕ КАТЕГОРИИ (ИНЖЕНЕРИЯ) ===
                BuiltInCategory.OST_PipeCurves     ,        // Трубы
                BuiltInCategory.OST_MechanicalEquipment,     // Оборудование (ОВ/ВК/Технология)
                BuiltInCategory.OST_DuctTerminal       ,    // Воздухораспределители (решетки)

                // === АНАЛОГИЧНЫЕ MEP-КАТЕГОРИИ (НЕОБХОДИМЫЕ ДЛЯ КОМПЛЕКТА) ===
                // 1. Вентиляция
                BuiltInCategory.OST_DuctCurves ,            // Воздуховоды
                BuiltInCategory.OST_DuctFitting  ,          // Фитинги воздуховодов (отводы, тройники)
                BuiltInCategory.OST_DuctAccessory  ,        // Арматура воздуховодов (дроссели, клапаны)
                BuiltInCategory.OST_FlexDuctCurves  ,       // Гибкие воздуховоды

                // 2. Трубопроводы и сантехника
                BuiltInCategory.OST_PipeFitting     ,       // Фитинги трубопроводов
                BuiltInCategory.OST_PipeAccessory    ,      // Арматура трубопроводов (краны, вентили)
                BuiltInCategory.OST_FlexPipeCurves  ,       // Гибкие трубы
                BuiltInCategory.OST_PlumbingFixtures  ,     // Сантехнические приборы

                BuiltInCategory.OST_PipeTags,

                // 3. Электрика и слаботочка
                BuiltInCategory.OST_ElectricalEquipment  ,   // Электрооборудование (щиты)
                BuiltInCategory.OST_ElectricalFixtures  ,    // Электроустановочные изделия (розетки)
                BuiltInCategory.OST_LightingFixtures    ,    // Осветительные приборы (светильники)
                BuiltInCategory.OST_CableTray         ,      // Кабельные лотки
                BuiltInCategory.OST_Conduit           ,      // Короба / Трубы для кабеля
                BuiltInCategory.OST_DataDevices        ,     // Сетевые устройства (слаботочка)
                // добавьте другие нужные категории

                BuiltInCategory.OST_IOSDetailGroups,
                BuiltInCategory.OST_TitleBlocks,

                BuiltInCategory.OST_Toposolid,
                BuiltInCategory.OST_StairsRailing,
                BuiltInCategory.OST_ShaftOpening,
                BuiltInCategory.OST_RoomSeparationLines,

                BuiltInCategory.OST_StructConnections, //гидрошпонка
                BuiltInCategory.OST_RvtLinks, //связанные файлоы
                BuiltInCategory.OST_StructuralFoundation, //сваи и тп
                BuiltInCategory.OST_Sheets,
                BuiltInCategory.OST_Cameras,
                BuiltInCategory.OST_Materials,
                BuiltInCategory.OST_ProjectBasePoint,
                BuiltInCategory.OST_SharedBasePoint,
                 

        };
        public static void Register(bool forceReregister = false)
        {
            var updater = new AutoFillNoteUpdater();

            if (!UpdaterRegistry.IsUpdaterRegistered(updater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(updater, true);
                var updaterId = updater.GetUpdaterId();

                //ElementClassFilter allElementsFilter = new ElementClassFilter(typeof(Element));

                var categoryIds = BuiltInCategorys.Select(x=> new ElementId(x)).ToList();

                //метод второй для всех категорий

                var allCategories = Enum.GetValues(typeof(BuiltInCategory)).Cast<BuiltInCategory>();
                if(allCategories.Count()> categoryIds.Count)
                {
                    //очень много категорий так нельзя
                   // categoryIds = allCategories.Select(x => new ElementId(x)).ToList();
                }
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

                ElementMulticategoryFilter classFilter = new ElementMulticategoryFilter(categoryIds);


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


        public static bool correctGroup = false;

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
            //try
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
            //finally
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
        private static bool IsGroupCurrentlyEdited(Document doc, Group group)
        {
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return false;
            Document activeDoc = uidoc.Document;
            // В режиме редактирования группы активный документ – временный, типа GroupDocument

            return activeDoc.GetType() == typeof(Document) && activeDoc.IsModifiable; // э
        }
        public static bool IsInGroupEditMode(Document doc)
        {
         
            View activeView = doc.ActiveView;
            // Если у активного вида есть GroupId, значит мы находимся в режиме редактирования группы
            return activeView.GroupId != ElementId.InvalidElementId;
        }
        /// <summary>
        /// Обработка списка элементов: запись автора (при необходимости) и соавтора.
        /// </summary>
        /// 
        private static void ProcessElements(Document doc, ICollection<ElementId> ids, bool isNewElement)
        {
            
            //View activeView = doc.ActiveView;
            //при открывании проекта активного вида может не быть
            //bool isGroupEditMode = false;
            //if (activeView != null)
            //{
            //    ElementId editingGroupId = activeView.GroupId;
            //    // Режим редактирования группы активен, если у активного вида есть GroupId
            //    isGroupEditMode = activeView.GroupId != ElementId.InvalidElementId;
            //}

            foreach (var id in ids)
            {
                Element element = doc.GetElement(id);
                if (element == null) continue;
                // Проверка: если элемент входит в группу и группа не в режиме редактирования – пропускаем

                ElementId groupId = element.GroupId;
                if (groupId != ElementId.InvalidElementId)
                {
                    // Проверяем, связан ли активный вид со сборкой
                    //ElementId assemblyId = activeView.AssemblyInstanceId;
                    //if (assemblyId != ElementId.InvalidElementId) не работает
                    //{
                    //    // Мы, вероятно, находимся в режиме редактирования сборки assemblyId
                    //    // Дополнительно можно проверить, что изменяемые элементы принадлежат этой сборке
                   
                   
                    //    if (element.AssemblyInstanceId == assemblyId)
                    //    {
                    //        // Элемент из редактируемой сборки

                    //        int cd = 0;
                    //    }
                    //    int c = 0;
                    //}
                    
                
                    ////не знаю костыль работат только галка correctGroup)
                    //var ui = RevitAPI.UiApplication.ActiveUIDocument;//Возможно, в активном документе есть свойство, указывающее на активную сборку.
                    if (!correctGroup)
                    {
                        continue;
                    }
                }

                // 1. Параметр для автора (или запасной)
                Parameter authorParam = element.LookupParameter(NameAvtor);
                //if (authorParam == null && regWriterAvtorPrim)
                //    authorParam = element.LookupParameter(NamePrimeh);

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
