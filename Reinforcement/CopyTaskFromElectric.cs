using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CopyTaskFromElectric : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Инициализация Revit API
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }

            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
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
                "Короб ЭЛ_Квадратный",
                "Короб ЭЛ_Круглый",
                "ЕС_Закладная ЭЛ в плите"
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

            // Получение типоразмеров
            var symbols = new List<FamilySymbol>();
            foreach (var familyName in requiredFamilies)
            {
                var symbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(x => x.FamilyName.Equals(familyName));

                if (symbol == null)
                {
                    TaskDialog.Show("Ошибка", $"Не найден типоразмер для семейства {familyName}");
                    return Result.Failed;
                }

                symbols.Add(symbol);
            }

            // Получение видов из связи
            var linkedViewIds = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => x.Name.ToLower().Contains("40_эл"))
                .Select(x => x.Id)
                .ToList();

            // Получение элементов для копирования
            var elementsToCopy = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .Where(x => x.Host != null && x.HostFace != null)
                .ToList();

            var excludedElements = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .Where(x => x.Host == null || x.HostFace == null)
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

                    // Активация типоразмеров
                    foreach (var symbol in symbols)
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();
                    }

                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    // Фильтр для поиска стен и перекрытий
                    var wallFloorFilter = new LogicalOrFilter(
                        new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                        new ElementCategoryFilter(BuiltInCategory.OST_Floors));

                    // Копирование элементов
                    foreach (var element in elementsToCopy)
                    {
                        var location = element.Location as LocationPoint;
                        if (location == null) continue;

                        XYZ point = location.Point;
                        XYZ orientation = element.HandOrientation;

                        // Поиск ближайшей поверхности
                        var intersector = new ReferenceIntersector(wallFloorFilter, FindReferenceTarget.Face, activeView as View3D);
                        var reference = intersector.FindNearest(point, XYZ.BasisZ)?.GetReference();

                        if (reference == null) continue;

                        // Создание экземпляра семейства
                        var symbolIndex = requiredFamilies.IndexOf(element.Symbol.FamilyName);
                        if (symbolIndex >= 0)
                        {
                            doc.Create.NewFamilyInstance(reference, point, orientation, symbols[symbolIndex]);
                        }
                    }

                    // Копирование видов
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
                        // Проверка на null
                        if (view == null)
                        {
                            // Логируем проблему, но продолжаем работу
                            TaskDialog.Show("Предупреждение",
                                $"Элемент с ID {viewId} не является видом или не может быть преобразован. Пропускаем.");
                            continue;
                        }

                        // Проверяем, есть ли данные для этого вида
                        if (!detailLinesData.TryGetValue(view.Name, out var data))
                        {
                            // Вид есть в списке скопированных, но нет в исходных данных
                            continue;
                        }

                        // Если нет линий для копирования, просто устанавливаем параметр
                        if (data.Item2.Count == 0)
                        {
                            var param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));
                            param?.Set("Задание ЭЛ");
                            continue;
                        }

                        // Получаем исходный вид из связанного документа
                        var sourceView = linkedDoc.GetElement(data.Item1) as View;
                        if (sourceView == null)
                        {
                            continue;
                        }

                        // Копируем линии
                        try
                        {
                            ElementTransformUtils.CopyElements(
                                sourceView,
                                data.Item2,
                                view,
                                Transform.Identity,
                                copyOptions);

                            // Устанавливаем параметр "Директория"
                            var param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));
                            param?.Set("Задание ЭЛ");
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Ошибка копирования",
                                $"Не удалось скопировать линии в вид {view.Name}: {ex.Message}");
                        }
                    }

                    t.Commit();

                    // Вывод информации о не скопированных элементах
                    if (excludedElements.Count > 0)
                    {
                        TaskDialog.Show("Предупреждение",
                            $"Не скопировано элементов: {excludedElements.Count}\n" +
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
