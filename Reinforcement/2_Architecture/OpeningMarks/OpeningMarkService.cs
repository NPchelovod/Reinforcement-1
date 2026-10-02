using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
namespace Reinforcement
{
    public interface IOpeningMarkService
    {
        BatchReport Create(Document document, View view, IEnumerable<ElementId> selectedIds);
    }
    public sealed class OpeningMarkService : IOpeningMarkService
    {
        public BatchReport Create(Document document, View view, IEnumerable<ElementId> selectedIds)
        {
            if (document == null || view == null || selectedIds == null) throw new ArgumentNullException("context");
            if (document.IsFamilyDocument || document.IsReadOnly) throw new InvalidOperationException("Нужен доступный для редактирования проект Revit.");
            if (!(view is ViewPlan) && !(view is ViewSection)) throw new InvalidOperationException("Откройте план, разрез или фасад для маркировки.");
            if (view.IsTemplate) throw new InvalidOperationException("Нельзя создавать марки в шаблоне вида.");
            var result = new BatchReport();
            var tagged = new HashSet<ElementId>();
            using (var collector = new FilteredElementCollector(document, view.Id))
                foreach (var tag in collector.OfClass(typeof(IndependentTag)).Cast<IndependentTag>())
                    foreach (var id in tag.GetTaggedLocalElementIds()) tagged.Add(id);
            using (var transaction = new Transaction(document, "ЕС BIM: марки выбранных проёмов"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Не удалось начать транзакцию.");
                foreach (var id in selectedIds.Distinct().OrderBy(i => i.Value))
                {
                    var instance = document.GetElement(id) as FamilyInstance;
                    string reason = Validate(instance, tagged);
                    if (reason != null) { result.AddSkipped(id.Value, reason); continue; }
                    using (var item = new SubTransaction(document))
                    {
                        if (item.Start() != TransactionStatus.Started) throw new InvalidOperationException("Не удалось начать транзакцию проёма.");
                        try
                        {
                            var point = ((LocationPoint)instance.Location).Point;
                            double offset = RevitAPI.ToFoot(5 * view.Scale);
                            var tag = IndependentTag.Create(document, view.Id, new Reference(instance), false,
                                TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal,
                                point + (view.RightDirection + view.UpDirection) * offset);
                            if (tag == null) throw new InvalidOperationException("Revit не создал марку.");
                            if (item.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Не удалось сохранить марку.");
                            result.AddCompleted(id.Value); tagged.Add(id);
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException error)
                        {
                            if (item.GetStatus() == TransactionStatus.Started) item.RollBack();
                            result.AddSkipped(id.Value, "Нельзя создать марку: " + error.Message);
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException error)
                        {
                            if (item.GetStatus() == TransactionStatus.Started) item.RollBack();
                            result.AddSkipped(id.Value, "Марка недоступна: " + error.Message);
                        }
                    }
                }
                if (result.Completed.Count == 0) transaction.RollBack();
                else if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit отклонил сохранение марок; изменения отменены.");
            }
            return result;
        }
        private static string Validate(FamilyInstance element, HashSet<ElementId> tagged)
        {
            if (element == null || !element.IsValidObject) return "Элемент не является экземпляром семейства проёма";
            long category = element.Category?.Id.Value ?? 0;
            if (category != (long)BuiltInCategory.OST_Doors && category != (long)BuiltInCategory.OST_Windows && category != (long)BuiltInCategory.OST_GenericModel)
                return "Поддерживаются двери, окна и семейства проёмов категории «Обобщённые модели»";
            if (!(element.Location is LocationPoint)) return "Отсутствует точка размещения";
            if (tagged.Contains(element.Id)) return "На этом виде уже есть марка элемента";
            var mark = ParameterHelper.ReadText(ParameterHelper.Find(element, BuiltInParameter.ALL_MODEL_MARK, false));
            if (string.IsNullOrWhiteSpace(mark)) return "Не заполнен параметр «Марка»";
            double width, height;
            var widthParameter = ParameterHelper.Find(element, BuiltInParameter.DOOR_WIDTH) ?? ParameterHelper.Find(element, "Ширина");
            var heightParameter = ParameterHelper.Find(element, BuiltInParameter.DOOR_HEIGHT) ?? ParameterHelper.Find(element, "Высота");
            if (!ParameterHelper.TryReadLength(widthParameter, UnitTypeId.Millimeters, out width) || width <= 0)
                return "Не задана положительная ширина проёма";
            if (!ParameterHelper.TryReadLength(heightParameter, UnitTypeId.Millimeters, out height) || height <= 0)
                return "Не задана положительная высота проёма";
            return null;
        }
    }
}
