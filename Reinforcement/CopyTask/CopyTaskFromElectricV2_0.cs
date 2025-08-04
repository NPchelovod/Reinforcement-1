using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static Reinforcement.CopyTaskFromElectric;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CopyTaskFromElectricV2_0 : IExternalCommand
    {
        //словарь команда и что мы создаем
        // имя команды: (класс, тип класса, требует ли привязки к поверхности )
        public static Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)> elementCategories { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName, bool RequiresHost)>()
        {
                { "Коробка круглая", ("Коробка ЭЛ_Л251", "Л251", false) },
                { "Коробка квадратная", ("Коробка ЭЛ_КУ1301", "КУ1301", false) },
               // { "в стенах патроны", ("патрон", "патрон", true) },
                //{ "в стенах клеммы", ("Клеммник_для_распаячной", "Клеммник для распаячной и универсальной коробок, шаг крепления 60", true) }
        };

        //словарь что мы заменяем, на какую команду заменяем данные кубики (имя семейства, тип)
        public static Dictionary<string, (string FamilyName, string SymbolName)> replace_cubics { get; set; } = new Dictionary<string, (string FamilyName, string SymbolName)>()
        {
            { "Коробка круглая",("Коробка ЭЛ_Л251", "Л251") },
            { "Коробка квадратная",("Коробка ЭЛ_КУ1301", "КУ1301")}
        };
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            bool r = EL_panel_step0_allCommand.CopyTask(commandData, ref message, elements, elementCategories, replace_cubics);

            // просто скопировали коробки теперь надо удалить и создать этажи

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Delit_View_Prefix(doc);

            Document linkedDoc = EL_panel_step0_allCommand.linkedDoc; // она там переприсвоена
            if (linkedDoc == null) { return Result.Failed; }

            bool rr = CopyView(doc, linkedDoc);

            if (!rr && !r) { return Result.Failed; }
            return Result.Succeeded;
        }


        public static void Delit_View_Prefix(Document doc, string prefix = "40_ЭЛ")
        {
            // УДАЛЕНИЕ СУЩЕСТВУЮЩИХ ВИДОВ
            var viewsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
            .Cast<View>()
                .Where(v => !v.IsTemplate && v.Name.Contains(prefix))
                .ToList();

            // Диалог подтверждения удаления
            if (viewsToDelete.Any())
            {
                string messageText = "";


                messageText += $"Найдено {viewsToDelete.Count} видов с заданием ЭЛ.\n";

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
        }


        public static bool CopyView(Document doc, Document linkedDoc, string prefix = "40_ЭЛ")
        {
            // Получение видов из связи
            var linkedViews = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => !x.IsTemplate && x.Name.Contains(prefix))
                .ToList();
            if (linkedViews.Count == 0 ) {return false;}

            try
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());


                    // Копирование видов и линий
                    foreach (View linkedView in linkedViews)
                    {
                        // Создание вида в целевом документе
                        View newView = null;

                        if (linkedView is ViewPlan linkedViewPlan)
                        {
                            // Получение уровня из связанного вида
                            Level linkedLevel = linkedViewPlan.GenLevel;
                            if (linkedLevel == null) continue;

                            // Поиск ближайшего уровня в целевом документе
                            Level targetLevel = FindClosestLevel(doc, linkedLevel.Elevation);
                            if (targetLevel == null) continue;

                            // Создание плана
                            newView = CreateViewPlan(doc, linkedViewPlan, targetLevel);
                        }
                        else if (linkedView is ViewDrafting linkedViewDrafting)
                        {
                            // Создание чертежного вида
                            newView = CreateViewDrafting(doc, linkedViewDrafting);
                        }

                        if (newView == null) continue;

                        // Копирование линий
                        var lines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                            .OfClass(typeof(CurveElement))
                            .ToElementIds()
                            .ToList();

                        if (lines.Count > 0)
                        {
                            try
                            {
                                ElementTransformUtils.CopyElements(
                                    linkedView,
                                    lines,
                                    newView,
                                    Transform.Identity,
                                    copyOptions);
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка", $"Не удалось скопировать линии: {ex.Message}");
                            }
                        }

                        // Установка параметра вида
                        SetViewParameter(newView, "Задание ЭЛ");
                    }

                    t.Commit();


                }


            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return false;
            }
            return true;

        }
        // Вспомогательный метод для установки параметра вида
        private static void SetViewParameter(View view, string value)
        {
            try
            {
                // GUID параметра "Директория"
                var param = view.get_Parameter(new Guid("f3ce110c-806b-4581-82fa-17fe5fd900b2"));
                param?.Set(value);
            }
            catch { }
        }
        // Поиск ближайшего уровня (допуск 500 мм)
        private static Level FindClosestLevel(Document doc, double sourceElevation)
        {
            const double tolerance = 0.5; // 500 мм
            Level closestLevel = null;
            double minDifference = double.MaxValue;

            // Используем кеширование для производительности
            if (_levelsCache == null)
            {
                _levelsCache = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();
            }

            foreach (Level level in _levelsCache)
            {
                double diff = Math.Abs(level.Elevation - sourceElevation);
                if (diff < tolerance && diff < minDifference)
                {
                    minDifference = diff;
                    closestLevel = level;
                }
            }

            return closestLevel;
        }
        private static List<Level> _levelsCache;
        // Создание плана

        private static ViewPlan CreateViewPlan(Document doc, ViewPlan sourceView, Level targetLevel)
        {
            try
            {
                // Получаем тип исходного вида
                ElementId sourceViewTypeId = sourceView.GetTypeId();
                if (sourceViewTypeId == ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Ошибка", "Исходный вид не имеет типа вида");
                    return null;
                }

                // Определяем семейство вида на основе ViewType
                ViewFamily viewFamily;
                switch (sourceView.ViewType)
                {
                    case ViewType.FloorPlan:
                        viewFamily = ViewFamily.FloorPlan;
                        break;
                    case ViewType.CeilingPlan:
                        viewFamily = ViewFamily.CeilingPlan;
                        break;
                    case ViewType.EngineeringPlan:
                        viewFamily = ViewFamily.StructuralPlan;
                        break;
                    case ViewType.AreaPlan:
                        viewFamily = ViewFamily.AreaPlan;
                        break;
                    default:
                        viewFamily = ViewFamily.FloorPlan; // Значение по умолчанию
                        break;
                }

                // Ищем подходящий тип вида в целевом документе
                ViewFamilyType viewFamilyType = null;
                var viewFamilyTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .ToList();

                foreach (var vft in viewFamilyTypes)
                {
                    if (vft.ViewFamily == viewFamily)
                    {
                        viewFamilyType = vft;
                        break;
                    }
                }

                // Если не нашли, используем первый доступный тип плана этажа
                if (viewFamilyType == null)
                {
                    foreach (var vft in viewFamilyTypes)
                    {
                        if (vft.ViewFamily == ViewFamily.FloorPlan)
                        {
                            viewFamilyType = vft;
                            break;
                        }
                    }
                }

                if (viewFamilyType == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден подходящий тип вида для плана");
                    return null;
                }

                // Создаем новый план
                ViewPlan newView = ViewPlan.Create(doc, viewFamilyType.Id, targetLevel.Id);
                newView.Name = sourceView.Name;

                // Копируем основные параметры
                try
                {
                    newView.Scale = sourceView.Scale;
                    newView.DisplayStyle = sourceView.DisplayStyle;
                }
                catch { /* Игнорируем несущественные ошибки */ }

                return newView;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка создания плана", ex.Message);
                return null;
            }
        }


        // Создание чертежного вида
        private static  ViewDrafting CreateViewDrafting(Document doc, ViewDrafting sourceView)
        {
            // Получение типа семейства вида
            ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (viewFamilyType == null) return null;

            // Создание вида
            ViewDrafting newView = ViewDrafting.Create(doc, viewFamilyType.Id);
            newView.Name = sourceView.Name;
            return newView;
        }

        





    }
}
