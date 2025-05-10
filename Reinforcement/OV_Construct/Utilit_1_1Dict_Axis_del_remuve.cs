using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


using Autodesk.Revit.Attributes;
//using Aspose.Cells.Charts;
//using Aspose.Cells;

/*
 * из осей удаляем те, которые странные, например, неудаленные лишние, они отличаются тем что не на всех планах
 */

namespace Reinforcement
{
    internal class Utilit_1_1Dict_Axis_del_remuve
    {
        public static Dictionary<string, Dictionary<string, object>> Re_Dict_Axis_del_remuve(Dictionary<string, Dictionary<string, object>> Dict_Axis, Document doc) //ref 
        {
            // 2. Получаем все поэтажные планы (Floor Plans)
            FilteredElementCollector viewCollector = new FilteredElementCollector(doc);
            ICollection<ViewPlan> floorPlans = viewCollector
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(vp => vp.ViewType == ViewType.FloorPlan)
                .ToList();

            bool IsElementVisible(Element element,Grid grid, View view)
            {
                // 1.Получаем ось по ID
                
                // Проверка явного скрытия
                if (element.IsHidden(view)) return false;

                // Проверка видимости категории
                if (element.Category != null && view.GetCategoryHidden(element.Category.Id))
                    return false;

                // 6. Проверяем уровень элемента (актуально для планов этажей)
                if (element.LevelId != ElementId.InvalidElementId &&
                    element.LevelId != view.GenLevel.Id)
                {
                    // Для элементов без уровня или с другим уровнем
                    return false;
                }

                foreach (ElementId filterId in view.GetFilters())
                {
                    ParameterFilterElement filter = view.Document.GetElement(filterId) as ParameterFilterElement;

                    ElementFilter elementFilter = filter.GetElementFilter();
                    if (filter != null && view.GetFilterVisibility(filterId))
                    {
                        if (elementFilter.PassesFilter(element))
                            return false;
                    }
                }
                return true;
                /*
                BoundingBoxXYZ elemBox = element.get_BoundingBox(view);
                if (elemBox == null)
                    return true; // Элемент без геометрии может быть видим
                */
                // Проверка 3D элементов на 2D виде

            }
            int CountParameters(Element element)
            {
                if (element == null) return 0;

                int count = 0;
                foreach (Parameter param in element.Parameters)
                {
                    
                    count++;
                }
                return count;
            }


        // создание списка id
        var list_id_axe = new List<string>();
            foreach (var iter in Dict_Axis)
            {
                list_id_axe.Add(iter.Key);
            }

            foreach (var iter in list_id_axe)
            {
                var id_axe = Convert.ToInt32(iter);
                var parametrs_axe = Dict_Axis[iter];
                ElementId elementId_axe = new ElementId(id_axe);
                Element element = doc.GetElement(elementId_axe);

                Grid grid = doc.GetElement(elementId_axe) as Grid;
                // 3. Проверяем видимость элемента на каждом плане
                //List<ViewPlan> visiblePlans = new List<ViewPlan>();
                int num_plans = 0;
                if (grid == null)
                {
                    continue;
                }
                
                foreach (ViewPlan plan in floorPlans)
                {
                    // Проверяем, находится ли элемент в пределах видимости вида
                    if (IsElementVisible(element, grid, plan))
                    {
                        //visiblePlans.Add(plan);
                        num_plans += 1;
                    }
                }

                // запись кол-ва попаданий осей
                

                parametrs_axe["количество попаданий в план"] = num_plans.ToString();
                parametrs_axe["количество параметров"] = CountParameters(element);
                parametrs_axe["контроль ссылки"] = elementId_axe.ToString();
                Dict_Axis[iter] = parametrs_axe;

            }

            return Dict_Axis;
        }
    }
      
}
