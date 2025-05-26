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
            foreach (var num_grup in list_num_grup)
            {
                // в конкретной группе вентшахт (ВШ1) идем по каждой из шахты
                var tek_num_grup = num_grup;
                var spisok_ov = OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV[tek_num_grup]["spisok_id_ov"] as List<string>; // список группы, id всех шахт группы, надо найти ту которая на данном уровне
                                                                                                                                    // проверка и поиск cdgfltybz
                Dict_Dict_num_grup_view[tek_num_grup] = new Dictionary<string , ElementId>();

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

                        string neme_ov_plan_new = viewPlan.Name + "_ВШ_" + num_grup.ToString(); // новое имя 
                        using (Transaction t = new Transaction(doc, "создание копии плана"))
                        {
                            t.Start();
                            ElementId newViewId = viewPlan.Duplicate(ViewDuplicateOption.WithDetailing);
                            newViewPlan = doc.GetElement(newViewId) as ViewPlan;

                            // Переименование (опционально)
                            newViewPlan.Name = neme_ov_plan_new;
                            chet += 1;
                            t.Commit();
                        }


                        Dict_Dict_num_grup_view[tek_num_grup][otm_H] = newViewPlan.Id;
                        


                    }



                }
            }

            return Result.Succeeded;
        }
    }
}



                

