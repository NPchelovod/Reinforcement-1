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

                BuiltInCategory.OST_Viewports, // видовые экраны
             BuiltInCategory.OST_Levels,
             BuiltInCategory.OST_RevisionClouds //чудо облачко
        };
        public static void Register(bool forceReregister = false)
        {
            var updater = new AnyChangeClass();

            if (!UpdaterRegistry.IsUpdaterRegistered(updater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(updater, true);
                var updaterId = updater.GetUpdaterId();

                //ElementClassFilter allElementsFilter = new ElementClassFilter(typeof(Element));

                var categoryIds = BuiltInCategorys.Select(x => new ElementId(x)).ToList();

                //метод второй для всех категорий

                var allCategories = Enum.GetValues(typeof(BuiltInCategory)).Cast<BuiltInCategory>();
                if (allCategories.Count() > categoryIds.Count)
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
                UpdaterRegistry.AddTrigger(updaterId, classFilter, Element.GetChangeTypeElementDeletion());

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
            AnyChangeClass updater = new AnyChangeClass();
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
}
