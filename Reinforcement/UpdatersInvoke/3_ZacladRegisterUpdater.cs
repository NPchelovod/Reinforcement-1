using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace Updaters
{
    [Transaction(TransactionMode.Manual)]
    public class RegisterZakladkaUpdater
    {//Мы должны вернуть UpdaterId, который состоит из 2 частей: AddinId, который должен совпасть с AddinId нашего приложения, и Guid апдейтера. Я решил использовать свойство ActiveAddinId класса Application, почему бы и нет:
        public static AddInId addInId { get; set; }

        public static void Register(bool recurce=false)
        {
            var updater = new ZakladkaMotionUpdater();

            if (!UpdaterRegistry.IsUpdaterRegistered(updater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(updater, true);
                var updaterId = updater.GetUpdaterId();

                // Фильтр: семейства (FamilyInstance) категории Generic Model (или любой нужной)
                ElementClassFilter classFilter = new ElementClassFilter(typeof(FamilyInstance));
                // При желании можно ограничить категорией, например, Generic ModelOST_GenericModel
                ElementCategoryFilter catFilter = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);
                LogicalAndFilter andFilter = new LogicalAndFilter(classFilter, catFilter);

                // Триггер на изменение геометрии (перемещение, поворот, изменение формы)
                UpdaterRegistry.AddTrigger(updaterId, andFilter, Element.GetChangeTypeGeometry());
                // Дополнительно можно добавить триггер на изменение параметра Location
                // UpdaterRegistry.AddTrigger(updaterId, andFilter, Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.INSTANCE_LOCATION_PARAM)));
            }
            else
            {
                if (recurce) { return; }
                recurce=true;
                // Если нужно перерегистрировать – очищаем
                UpdaterRegistry.RemoveAllTriggers(updater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(updater.GetUpdaterId());
                Register(); // рекурсивно зарегистрируем заново
            }
        }
    }

    public class ZakladkaMotionUpdater : IUpdater
    {
        private static AddInId _addInId => RegisterZakladkaUpdater.addInId;
        private static UpdaterId _updaterId = new UpdaterId(_addInId, new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457266E"));

        public UpdaterId GetUpdaterId() => _updaterId;

        public string GetUpdaterName() => "Сброс согласования при перемещении закладных";

        public string GetAdditionalInformation() => "При перемещении элемента семейства, содержащего 'Закладная', сбрасывает параметр 'Согласовано'.";

        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls; // приоритет не принципиален
        List<string> Soglass = new List<string>() { "Согласовано АР", "Согласовано ИОС", "Согласовано КР" };
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            // Все элементы, которые были изменены (включая перемещённые)
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();
            if (modifiedIds == null || modifiedIds.Count == 0)
                return;

            //using (Transaction t = new Transaction(doc, "Сброс согласования"))
            //{
            //    t.Start();
                foreach (ElementId id in modifiedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (elem == null) continue;

                    // Проверяем, является ли элемент экземпляром семейства
                    if (!(elem is FamilyInstance fi)) continue;

                    // Получаем имя семейства
                    ElementType type = doc.GetElement(fi.GetTypeId()) as ElementType;
                    if (type == null) continue;
                    string familyName = type.FamilyName;
                    if (string.IsNullOrEmpty(familyName) || !familyName.Contains("Закладная"))
                        continue;
                    // Получаем все параметры элемента

                    // Ищем параметр "Согласовано" (или "Согласновано" с опечаткой)

                    foreach (string soglasSeach in Soglass)
                    {
                        Parameter soglas = fi.LookupParameter(soglasSeach);

                        if (soglas != null && soglas.StorageType == StorageType.Integer)
                        {
                            // Для Yes/No: 0 = false, 1 = true
                            if (soglas.AsInteger() != 0)
                                soglas.Set(0);
                        }
                    }


                    // Альтернативно: если параметр определён GUID
                    // var guid = new Guid("ваш-GUID-параметра");
                    // Parameter p = fi.get_Parameter(guid);
                }
            //    t.Commit();
            //}

        }

    }
}