using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using ExcelDataReader.Log;
using Autodesk.Revit.Attributes;
using System.Collections;
using System.Collections.ObjectModel;




namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_6Create_new_plan_floor
    {
        
        public static Result Create_new_plan_floor()
        {
            var options = new Options() { ComputeReferences = true };
            Document doc = RevitAPI.Document;

            var List_otm = new List<string>();
            var List_num_grup_ov = new List<int>();

            var Dict_Dict_num_grup_view = new Dictionary<int, Dictionary<string , ElementId>>();

            var list_id_axe = List_id_axe(); // лист 
            var list_num_grup = new List<int>();
            foreach (var num in OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV)
            {
                list_num_grup.Add(num.Key);
            }
            list_num_grup.Sort();

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ViewFamilyType viewFamilyType = collector.OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan);
            ViewPlan newViewPlan;


            int chet = 0;
            using (TransactionGroup tg = new TransactionGroup(doc, "Создание выносок"))
            {
                tg.Start();
                foreach (var num_grup in list_num_grup)
                {
                    // в конкретной группе вентшахт (ВШ1) идем по каждой из шахты
                    var tek_num_grup = num_grup;
                    var spisok_ov = OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV[tek_num_grup]["spisok_id_ov"] as List<string>; // список группы, id всех шахт группы, надо найти ту которая на данном уровне
                                                                                                                                        // проверка и поиск cdgfltybz
                    Dict_Dict_num_grup_view[tek_num_grup] = new Dictionary<string, ElementId>();

                    if (chet > 5)
                    {
                        break;
                    }

                    foreach (var num_ov in spisok_ov)
                    {
                        ElementId id_ov = new ElementId(Convert.ToInt64(num_ov));


                        foreach (var onm_data in OV_Construct_All_Dictionary.Dict_level_plan_floor)
                        {
                            string otm_H = onm_data.Key;
                            var levelPlan = onm_data.Value;
                            var viewPlan = doc.GetElement(levelPlan) as View;

                            if (!OV_Construct_All_Dictionary.Dict_plan_ov_axis[levelPlan].ContainsKey(id_ov))
                            {
                                continue; // нет на данном виде
                            }

                            string neme_ov_plan_new = OV_Construct_All_Dictionary.Prefix_plan_floor  + num_grup.ToString() + "_(" + otm_H + ")"; // новое имя 
                            using (Transaction t = new Transaction(doc, "создание копии плана"))
                            {
                                t.Start();
                                ElementId newViewId = viewPlan.Duplicate(ViewDuplicateOption.AsDependent);//Duplicate(ViewDuplicateOption.WithDetailing);
                                newViewPlan = doc.GetElement(newViewId) as ViewPlan;
                                
                                // Переименование (опционально)
                                try
                                {
                                    newViewPlan.Name = neme_ov_plan_new;
                                    t.Commit();
                                }
                                catch
                                {
                                    t.RollBack();
                                    continue; // такое имя уже есть и оно не удалилось
                                }
                                chet += 1;
                                
                            }


                            Dict_Dict_num_grup_view[tek_num_grup][otm_H] = newViewPlan.Id;
                            var axisId_vert = OV_Construct_All_Dictionary.Dict_plan_ov_axis[levelPlan][id_ov][0];
                            var axisId_hor = OV_Construct_All_Dictionary.Dict_plan_ov_axis[levelPlan][id_ov][1];
                            List<ElementId> excludedIds = new List<ElementId>()
                            {
                                id_ov,axisId_vert, axisId_hor
                            };
                            using (Transaction t = new Transaction(doc, "настройка плана"))
                            {
                                t.Start();

                                // Собираем все оси и обобщенные модели на виде

                                FilteredElementCollector collector_ov = new FilteredElementCollector(doc, newViewPlan.Id)
                                    .OfCategory(BuiltInCategory.OST_GenericModel); // Обобщенные модели
                                List<ElementId> elementsToHide = collector_ov
                                   .Where(e => !excludedIds.Contains(e.Id))
                                   .Select(e => e.Id)
                                   .ToList();
                                List<ElementId> hide_list_id_axe = list_id_axe.Where(e => !excludedIds.Contains(e)).ToList();

                                FilteredElementCollector collector_axe = new FilteredElementCollector(doc, newViewPlan.Id)
                                    .WhereElementIsNotElementType()
                                    .OfCategory(BuiltInCategory.OST_Grids); // Оси
                                elementsToHide = elementsToHide.Union(hide_list_id_axe).ToList();

                                // Скрываем элементы
                                newViewPlan.HideElements(elementsToHide);

                                t.Commit();
                            }
                            // Получить BoundingBox элемента
                            BoundingBoxXYZ bbox = doc.GetElement(id_ov).get_BoundingBox(newViewPlan);

                            // Установить обрезку вида
                            using (Transaction tx = new Transaction(doc))
                            {
                                tx.Start("Обрезка по BoundingBox");
                                newViewPlan.CropBox = bbox;
                                newViewPlan.CropBoxActive = true;
                                //newViewPlan.AreAnnotationCategoriesHidden = true;
                                // Активация обрезки аннотаций
                                newViewPlan.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE).Set(1);
                                // Установка смещений
                                //newViewPlan.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_OFFSET_TOP).Set(0);

                                newViewPlan.CropBoxVisible = true;
                                tx.Commit();
                                
                            }

                        }


                    }
                }
                tg.Assimilate();
            }

            return Result.Succeeded;
        }



        public static List<ElementId>  List_id_axe()
        {
            // лист со всеми осями id
            var list_id_axe = new List<ElementId>();
            foreach(var id_axe in OV_Construct_All_Dictionary.Dict_Axis)
            {
                list_id_axe.Add(new ElementId(Convert.ToInt64(id_axe.Key)));
            }
            return list_id_axe;
           
        }
    }
}



                

