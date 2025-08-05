using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using View = Autodesk.Revit.DB.View;
//прошлая версия CopyTaskFromElectric
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class _CopyTaskFromElectric : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Инициализация Revit API

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;

            // Проверка активного вида
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");
                return Result.Failed;
            }

            // Выбор связанной модели
            ISelectionFilter selFilter = new SelectionFilter();
            TransparentNotificationWindow.ShowNotification("Выберите связанную модель ЭЛ", uidoc, 5);

            Reference selection;
            try
            {
                selection = sel.PickObject(ObjectType.Element, selFilter);
            }
            catch
            {
                return Result.Cancelled;
            }

            RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
            Document linkedDoc = linkedModel.GetLinkDocument();

            // Список требуемых семейств
            var requiredFamilies = new List<string>()
            {
                "Коробка ЭЛ_Л251",
                "Коробка ЭЛ_КУ1301",
            };

            // Проверка наличия семейств в проекте
            var missingFamilies = requiredFamilies
                .Where(familyName => !new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Any(f => f.Name.Equals(familyName)))
                    .ToList();

            if (missingFamilies.Any())
            {
                TaskDialog.Show("Ошибка", $"Отсутствуют следующие семейства:\n{string.Join("\n", missingFamilies)}");
                return Result.Failed;
            }

            // Удаление существующих коробок на активном виде
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(FamilyInstance));
            var boxesToDelete = collector
                .Cast<FamilyInstance>()
                .Where(fi => requiredFamilies.Contains(fi.Symbol.Family.Name))
                .ToList();

            // УДАЛЕНИЕ СУЩЕСТВУЮЩИХ ВИДОВ
            var viewsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.Name.Contains("40_ЭЛ"))
                .ToList();

            // Диалог подтверждения удаления
            if (boxesToDelete.Any() || viewsToDelete.Any())
            {
                string messageText = "";
                if (boxesToDelete.Any())
                {
                    messageText += $"Найдено {boxesToDelete.Count} коробок на активном виде.\n";
                }
                if (viewsToDelete.Any())
                {
                    messageText += $"Найдено {viewsToDelete.Count} видов с заданием ЭЛ.\n";
                }
                messageText += "Удалить их перед копированием новых?";

                TaskDialogResult dialogResult = TaskDialog.Show(
                    "Подтверждение",
                    messageText,
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                );

                if (dialogResult == TaskDialogResult.Yes)
                {
                    using (Transaction t = new Transaction(doc, "Удаление элементов"))
                    {
                        t.Start();

                        // Удаление коробок
                        foreach (Element box in boxesToDelete)
                        {
                            try
                            {
                                doc.Delete(box.Id);
                            }
                            catch { }
                        }

                        // Удаление видов
                        foreach (View view in viewsToDelete)
                        {
                            try
                            {
                                doc.Delete(view.Id);
                            }
                            catch { }
                        }

                        t.Commit();
                    }
                }
            }

            // Сбор коробок из связанной модели
            var allConduitFittings = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .ToList();

            var elementsToCopy = allConduitFittings
                .Where(x => requiredFamilies.Contains(x.Symbol.Family.Name))
                .ToList();

            var excludedElements = allConduitFittings
                .Where(x => !requiredFamilies.Contains(x.Symbol.Family.Name))
                .ToList();

            // Получение видов из связи
            var linkedViewIds = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => !x.IsTemplate && x.Name.Contains("40_ЭЛ"))
                .Select(x => x.Id)
                .ToList();

            // Подготовка данных для копирования линий
            var detailLinesData = new Dictionary<string, (ElementId, List<ElementId>)>();
            foreach (var viewId in linkedViewIds)
            {
                var view = linkedDoc.GetElement(viewId) as View;
                var lines = new FilteredElementCollector(linkedDoc, viewId)
                    .OfClass(typeof(CurveElement))
                    .ToElementIds()
                    .ToList();
                detailLinesData.Add(view.Name, (viewId, lines));
            }

            try
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();

                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    // КОПИРОВАНИЕ КОРОБОК
                    if (elementsToCopy.Count > 0)
                    {
                        Transform transform = linkedModel.GetTotalTransform();
                        ICollection<ElementId> copiedElementIds = ElementTransformUtils.CopyElements(
                            linkedDoc,
                            elementsToCopy.Select(e => e.Id).ToList(),
                            doc,
                            transform,
                            copyOptions);
                    }

                    // КОПИРОВАНИЕ ВИДОВ
                    if (linkedViewIds.Count > 0)
                    {
                        var copiedViewIds = ElementTransformUtils.CopyElements(
                            linkedDoc,
                            linkedViewIds,
                            doc,
                            Transform.Identity,
                            copyOptions);

                        // Копирование линий в виды
                        foreach (var viewId in copiedViewIds)
                        {
                            var view = doc.GetElement(viewId) as View;
                            if (view == null) continue;

                            if (!detailLinesData.TryGetValue(view.Name, out var data))
                                continue;

                            if (data.Item2.Count == 0)
                            {
                                SetViewParameter(view, "Задание ЭЛ");
                                continue;
                            }

                            var sourceView = linkedDoc.GetElement(data.Item1) as View;
                            if (sourceView == null) continue;

                            try
                            {
                                ElementTransformUtils.CopyElements(
                                    sourceView,
                                    data.Item2,
                                    view,
                                    Transform.Identity,
                                    copyOptions);

                                SetViewParameter(view, "Задание ЭЛ");
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка копирования", $"Не удалось скопировать линии в вид {view.Name}: {ex.Message}");
                            }
                        }
                    }

                    t.Commit();

                    // Вывод информации о не скопированных элементах
                    if (excludedElements.Count > 0)
                    {
                        TaskDialog.Show("Предупреждение",
                            $"Не скопировано элементов (неподходящие семейства): {excludedElements.Count}\n" +
                            $"ID элементов: {string.Join(", ", excludedElements.Select(x => x.Id.ToString()))}");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        // Вспомогательный метод для установки параметра вида
        private void SetViewParameter(View view, string value)
        {
            try
            {
                // GUID параметра "Директория"
                var param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));
                param?.Set(value);
            }
            catch { }
        }

        // Фильтр выбора (только связи)
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ point) => false;
        }

        // Обработчик дублирования типов
        public class DuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
