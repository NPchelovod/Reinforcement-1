using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;



//в revit api 2024 c# найти название уровней находящихся на заданной отметке, затем с одного из найденных уровней сделать копию и назвать её ОВ_+ уровень, сделать чтобы данный уровень был виден в диспетчере проекта, назначить ему раздел "РД"

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_1Create_new_floor 
    {
        public static Result Create_new_floor(Dictionary<string, List<string>> Dict_sovpad_level, ForgeTypeId units, ref string message, ElementSet elements, Document doc)
        {
            
               

            foreach (var otm in Dict_sovpad_level)
            {
                // 1. Задаём искомую высотную отметку (в единицах проекта)
                double targetElevation = Convert.ToDouble(otm.Key); // Например, 3 метра

                // 2. Находим уровень с заданной отметкой

                var sourceLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .Where(level => Math.Abs(UnitUtils.ConvertFromInternalUnits(level.Elevation, units) - targetElevation) < 300).ToList(); // Учитываем погрешность

                if (sourceLevels == null || sourceLevels.Count == 0)
                {
                    TaskDialog.Show("Ошибка", $"Уровень с отметкой {targetElevation} не найден!");
                    continue;
                }
                // Выводим список найденных уровней в заданном диапазоне для выбора
                var levelNames = sourceLevels.Select(l => l.Name).ToList();

                Level selectedLevel = sourceLevels[0]; // выбираем первый попавшийся

                // Получаем все доступные типы видов
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ViewFamilyType viewFamilyType = collector.OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);

                if (viewFamilyType == null)
                {
                    TaskDialog.Show("Ошибка", "Не найден тип вида для плана этажа");
                    return Result.Failed;
                }

                // Создаем новый план этажа
                using (Transaction trans = new Transaction(doc, "Создание плана этажа"))
                {
                    trans.Start();

                    string Plan_name = "ОВ_" + selectedLevel.Name;
                    //удаляем план если он существует, заново затем создаём
                    // Проверяем существование плана с таким именем и удаляем его
                    var existingPlans = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .Where(v => v.Name == Plan_name)
                        .ToList();
                    foreach (var plan in existingPlans)
                    {
                        doc.Delete(plan.Id);
                    }
                    // Создаем вид плана этажа
                    ViewPlan newViewPlan = ViewPlan.Create(doc, viewFamilyType.Id, selectedLevel.Id);
                    newViewPlan.Name = Plan_name;


                    // Делаем вид видимым в диспетчере проекта
                    Parameter isVisibleParam = newViewPlan.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                    if (isVisibleParam != null && !isVisibleParam.IsReadOnly)
                    {
                        isVisibleParam.Set("РД"); // Устанавливаем раздел "РД"
                    }

                    // 1. Скрываем ВСЕ элементы на виде
                    HideAllElements(doc, newViewPlan);

                    // 2. Показываем только оси (Grid)
                    
                    ShowGrids(doc, newViewPlan);
                    ShowSize(doc, newViewPlan);
                    ShowElem_corner(doc, newViewPlan);
                    // 3. Показываем только нужные вентканалы
                    ShowSpecificVents(doc, newViewPlan);


                    trans.Commit();

                    TaskDialog.Show("Успех", $"Создан новый план этажа: {newViewPlan.Name}");
                }

               
            }

            return Result.Succeeded;
        }

        private static void HideAllElements(Document doc, View view)
        {
            // Получаем все категории в проекте
            Categories categories = doc.Settings.Categories;

            foreach (Category category in categories)
            {
                try
                {
                    if (!view.GetCategoryHidden(category.Id))
                    {
                        view.SetCategoryHidden(category.Id, true);
                    }
                }
                catch
                {
                    // Пропускаем категории, которые нельзя скрыть
                }
            }
        }

        private static void ShowGrids(Document doc, View view)
        {
            // Показываем категорию осей
            Category gridCategory = Category.GetCategory(doc, BuiltInCategory.OST_Dimensions);
            view.SetCategoryHidden(gridCategory.Id, false);
        }

        private static void ShowSize(Document doc, View view)
        {
            // Показываем категорию осей
            Category gridCategory = Category.GetCategory(doc, BuiltInCategory.OST_Grids);
            view.SetCategoryHidden(gridCategory.Id, false);
        }


        private static void ShowElem_corner(Document doc, View view)
        {
            // Показываем категорию осей
            Category gridCategory = Category.GetCategory(doc, BuiltInCategory.OST_Assemblies);
            view.SetCategoryHidden(gridCategory.Id, false);
        }

        private static void ShowSpecificVents(Document doc, View view)
        {
            string name_OB = "ОтверстиеВПерекрытии";
            BuiltInCategory name_OST = BuiltInCategory.OST_GenericModel;
            //Category gridCategory = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
            //view.SetCategoryHidden(gridCategory.Id, false);

            // Получаем нужные вентканалы
            List<Element> vents = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(name_OST)
                .Where(it => it.Name == name_OB)
                .Where(it => it.LookupParameter("ADSK_Отверстие_Функция")?.AsValueString() == "Вентканал")
                .ToList();

            // Создаем временный фильтр
            ParameterFilterElement filter = CreateFilterForElements(doc, vents, "Temp Vent Filter - " + view.Name);

            // Применяем фильтр к виду
            view.AddFilter(filter.Id);
            view.SetFilterVisibility(filter.Id, true);
        }

        private static ParameterFilterElement CreateFilterForElements(Document doc, ICollection<Element> elements, string filterName)
        {
            // Удаляем старый фильтр с таким же именем, если он существует
            ParameterFilterElement existingFilter = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .FirstOrDefault(x => x.Name == filterName) as ParameterFilterElement;

            if (existingFilter != null)
            {
                doc.Delete(existingFilter.Id);
            }

            // Создаем новый фильтр
            ICollection<ElementId> elementIds = elements.Select(x => x.Id).ToList();
            return ParameterFilterElement.Create(doc, filterName, elementIds);
        }
    }

}




                