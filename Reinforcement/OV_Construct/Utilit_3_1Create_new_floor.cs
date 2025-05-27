using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

//using System.Windows.Controls;




//создаем планы этажей не с типовым расположением, тут все ов на плане и оси

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_1Create_new_floor
    {
        public static Result Create_new_floor(ForgeTypeId units, ref string message, ElementSet elements)
        {

            Document doc = RevitAPI.Document;
            var Dict_sovpad_level = OV_Construct_All_Dictionary.Dict_sovpad_level;
            // Получаем все доступные типы видов
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ViewFamilyType viewFamilyType = collector.OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);

            OV_Construct_All_Dictionary.Dict_level_plan_floor.Clear();
            if (viewFamilyType == null)
            {
                TaskDialog.Show("Ошибка", "Не найден тип вида для плана этажа");
                return Result.Failed;
            }
            // Создаем StringBuilder для формирования сообщения
            StringBuilder messageBuilder = new StringBuilder();
            // удаление всех планов ОВ_ВШ
            DeleteViewsWithNamePattern(OV_Construct_All_Dictionary.Prefix_plan_floor);

            using (TransactionGroup tg = new TransactionGroup(doc, "Создание планов этажей"))
            {
                tg.Start();
                foreach (var otm in Dict_sovpad_level)
                {
                    var H_otm = otm.Key;
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

                    Level selectedLevel = sourceLevels[0]; // выбираем первый попавшийся,можно по умней сделать
                    ViewPlan newViewPlan;
                    // Создаем новый план этажа

                    string Plan_name = OV_Construct_All_Dictionary.Prefix_plan_floor + "(" + otm.Key + ")";//

                    List<ElementId> existingPlanIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.Name == Plan_name) // Точное совпадение имени
                    .Select(v => v.Id) // Выбираем только ElementId
                    .ToList();

                    bool proxod = true;
                    if (existingPlanIds.Count > 0)
                    {
                        // этот план не удалился он открыт и мы его выписываем
                        if (!OV_Construct_All_Dictionary.Dict_level_plan_floor.ContainsKey(H_otm))
                        {
                            OV_Construct_All_Dictionary.Dict_level_plan_floor[H_otm] = existingPlanIds[0];
                        }
                        continue;
                    }


                    using (Transaction t = new Transaction(doc, "создание этажа"))
                    {
                        t.Start();
                        // Создаем вид плана этажа
                        newViewPlan = ViewPlan.Create(doc, viewFamilyType.Id, selectedLevel.Id);
                        newViewPlan.Name = Plan_name;

                        t.Commit();
                    }

                    using (Transaction t2 = new Transaction(doc, "Настройка плана этажа"))
                    {
                        t2.Start();
                        Nastroy_floor(doc, newViewPlan, H_otm);
                        t2.Commit();
                    }

                    messageBuilder.AppendLine($"- %{newViewPlan.Name}% из %{selectedLevel.Name}%");

                    // запись в словарь плана для обращения к нему

                    if (!OV_Construct_All_Dictionary.Dict_level_plan_floor.ContainsKey(H_otm))
                    {
                        OV_Construct_All_Dictionary.Dict_level_plan_floor[H_otm] = newViewPlan.Id;
                    }



                }
                TaskDialog.Show("Созданные планы", messageBuilder.ToString());
                tg.Assimilate();

            }

            return Result.Succeeded;
        }

        public static void DeleteViewsWithNamePattern(string Prefix_plan_floor)
        {
            Document doc = RevitAPI.Document;

            // Собираем все планы этажей
            var viewPlans = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(vp =>
                    vp.Name.Contains(Prefix_plan_floor) && // Проверка имени
                    !vp.IsTemplate); // Исключаем шаблоны видов

            // Получаем ID элементов для удаления
            ICollection<ElementId> toDelete = viewPlans
                .Select(vp => vp.Id)
                .ToList();

            if (toDelete.Count == 0)
            {
                
                return;
            }

            // Удаляем в транзакции
            foreach (ElementId vp in toDelete)
            {
                using (Transaction t = new Transaction(doc, "Удаление планов ОВ_ВШ_"))
                {
                    try
                    {

                        t.Start();
                        doc.Delete(vp);
                        t.Commit();
                    }

                    catch (Exception ex)
                    {
                        t.RollBack();
                        TaskDialog.Show("Ошибка удаления плана", ex.Message);
                    }
                }
            
            }
        }
        public static void Nastroy_floor(Document doc, ViewPlan newViewPlan, string H_otm)
        {

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
                        BuiltInCategory.OST_DetailComponents,//видимость заливки
                        BuiltInCategory.OST_GenericModelTags// марки обобщенной модели
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
                    bool pr = true;
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

            catch (Exception ex) 
            {
                bool pr = true;
            }


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

    }

}




