using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
//using System.Windows.Controls;




//тут в общем виде 

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_1Create_new_floor 
    {
        public static Result Create_new_floor( ForgeTypeId units, ref string message, ElementSet elements, Document doc)
        {


            var Dict_sovpad_level = OV_Construct_All_Dictionary.Dict_sovpad_level;
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

                Level selectedLevel = sourceLevels[0]; // выбираем первый попавшийся,можно по умней сделать

                // Создаем новый план этажа
                using (Transaction trans = new Transaction(doc, "Создание плана этажа"))
                {
                    trans.Start();

                    string Plan_name = "ОВ_" + otm.Key;//selectedLevel.Name;
                    //удаляем план если он существует, заново затем создаём
                    // Проверяем существование плана с таким именем и удаляем его
                    var existingPlans = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .Where(v => v.Name == Plan_name)
                        .ToList();

                    bool proxod = true;
                    if (existingPlans != null)
                    {
                        foreach (var plan in existingPlans)
                        {
                            try
                            {
                                // Проверяем, можно ли удалить элемент
                                if (plan.IsValidObject && !doc.IsReadOnly)
                                {
                                    doc.Delete(plan.Id);
                                }
                                else
                                {
                                    TaskDialog.Show("Ошибка", $"План {plan.Name} не может быть удален.");
                                    proxod = false;
                                }
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                            {
                                TaskDialog.Show("Ошибка удаления", $"Не удалось удалить план {plan.Name}: {ex.Message}");
                                proxod = false;
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Ошибка", $"Ошибка при удалении плана {plan.Name}: {ex.Message}");
                                proxod = false;
                            }
                        }
                    }
                    if (proxod == false) { continue; }

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

                    // 2. Показываем 

                    var list_BuiltInCategor = new List<BuiltInCategory>()
                    {
                    BuiltInCategory.OST_Dimensions,
                        BuiltInCategory.OST_Grids,
                        //BuiltInCategory.OST_Assemblies, 
                        BuiltInCategory.OST_GenericModel, // к ней относятся шахты
                        BuiltInCategory.OST_DetailComponents //видимость заливки
                    };


                    foreach (var categor in list_BuiltInCategor)
                    {
                        try
                        {
                            Category gridCategory = Category.GetCategory(doc, categor);
                            newViewPlan.SetCategoryHidden(gridCategory.Id, false);
                        }
                        catch
                        {
                            // Пропускаем категории, которые нельзя открыть
                        }
                    }

                    // переводим оси в 2д
                    //check grids if they are 3D set to 2D
                    try
                    {
                        List<Grid> gridList = new FilteredElementCollector(doc, newViewPlan.Id)
                            .OfClass(typeof(Grid))
                            .ToElements()
                            .Cast<Grid>()
                            .ToList(); //get all grids on activeView
                        foreach (Grid grid in gridList)
                        {
                            try
                            {
                                if (grid.GetDatumExtentTypeInView(DatumEnds.End0, newViewPlan) == DatumExtentType.Model)
                                {
                                    grid.SetDatumExtentType(DatumEnds.End0, newViewPlan, DatumExtentType.ViewSpecific);
                                }
                                if (grid.GetDatumExtentTypeInView(DatumEnds.End1, newViewPlan) == DatumExtentType.Model)
                                {
                                    grid.SetDatumExtentType(DatumEnds.End1, newViewPlan, DatumExtentType.ViewSpecific);
                                }
                            }
                            catch (Exception ex) { }
                        }
                    }

                    catch (Exception ex) { }

                    

                    // Включаем видимость подкатегории "Элементы узлов"
                    //SetSubcategoryVisibility( doc,newViewPlan, "Элементы узлов", true);

                    // 3. Показываем только нужные вентканалы
                    //ShowSpecificVents(doc, newViewPlan);

                    // 3. Скрываем все элементы OST_GenericModel, кроме нужных по ID
                    Category genericCategory = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
                    if (genericCategory != null)
                    {
                        // Получаем ВСЕ элементы OST_GenericModel на виде
                        FilteredElementCollector collector1 = new FilteredElementCollector(doc, newViewPlan.Id);
                        collector1.OfCategory(BuiltInCategory.OST_GenericModel);
                        List<ElementId> allGenericIds = collector1.Select(e => e.Id).ToList();

                        // Скрываем все элементы категории
                        newViewPlan.HideElements(allGenericIds);

                        // Ваш список ID элементов, которые нужно показать (пример)
                        List<ElementId> idsToShow = new List<ElementId>();
                        
                        foreach (var id_el in OV_Construct_All_Dictionary.Dict_ventId_Properts)
                        {
                            int tek_id = Convert.ToInt32(id_el.Key);
                            ElementId elementId = new ElementId(tek_id);
                            idsToShow.Add(elementId);
                        }

                        // Показываем только нужные элементы
                        newViewPlan.UnhideElements(idsToShow);
                    }

                    // запись в словарь плана для обращения к нему
                    if (!OV_Construct_All_Dictionary.Dict_level_plan_floor.ContainsKey(otm.Key))
                    {
                        OV_Construct_All_Dictionary.Dict_level_plan_floor[otm.Key] = newViewPlan;
                    }


                    trans.Commit();

                    TaskDialog.Show("Успех", $"Создан новый план этажа: {newViewPlan.Name}");
                }


            }

            return Result.Succeeded;
        }
        public static GraphicsStyle GetGraphicsStyleByName(Document doc, string styleName)
        {
            // Получаем все графические стили в документе
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(GraphicsStyle));

            foreach (GraphicsStyle style in collector)
            {
                if (style.Name.Equals(styleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return style;
                }
            }
            return null;
        }
        public static void SetSubcategoryVisibility(Document doc,View view, string subcategoryName, bool isVisible = false)
        {
            
            // Находим графический стиль (подкатегорию)
            GraphicsStyle subcategory = GetGraphicsStyleByName(doc, subcategoryName);

            if (subcategory != null)
            {
                // Устанавливаем видимость
                view.SetCategoryHidden(subcategory.Id, isVisible);
            }
            else
            {
                TaskDialog.Show("Ошибка", $"Подкатегория '{subcategoryName}' не найдена!");
            }
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

        private static void ShowSpecificVents(Document doc, View view)
        {
            string name_OB = "ОтверстиеВПерекрытии";
            BuiltInCategory name_OST = BuiltInCategory.OST_GenericModel;
            //Category gridCategory = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
            //view.SetCategoryHidden(gridCategory.Id, false);

            // Получаем нужные вентканалы
            /*
            List<Element> vents = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(name_OST)
                .Where(it => it.Name == name_OB)
                .Where(it => it.LookupParameter("ADSK_Отверстие_Функция")?.AsValueString() == "Вентканал")
                .ToList();
            */

            var vents = new List <Element>(); // так надежней все привязано к начальным ов шахтам
            foreach (var id_el in OV_Construct_All_Dictionary.Dict_ventId_Properts)
            {
                int tek_id = Convert.ToInt32(id_el.Key);
                ElementId elementId = new ElementId( tek_id);
                vents.Add(doc.GetElement(elementId));
            }
            
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




