using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;

using System.Data;


namespace Reinforcement

{

    [Transaction(TransactionMode.Manual)]


    public class OV_Constuct_Command : IExternalCommand
    {
        
        static AddInId addinId = new AddInId(new Guid ("424E29F8-20DE-49CB-8CF0-8627879F97C2"));
        

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            ForgeTypeId units = UnitTypeId.Millimeters;

            Control_Pick.MControl_Pick(uidoc, doc, units); //ref 
            // в будущем можно будет менять
            string name_OB = "ОтверстиеВПерекрытии";
            Type name_class = typeof(FamilyInstance);
            BuiltInCategory name_OST = BuiltInCategory.OST_GenericModel;


            // vents - id всех элементов каналов ОВ с именем name_OB
            List<ElementId> vents = new FilteredElementCollector(doc).OfClass(name_class).OfCategory(name_OST).Where(it => it.Name== name_OB).Where(it => it.LookupParameter("ADSK_Отверстие_Функция").AsValueString()== "Вентканал").Select(it=>it.Id).ToList();

            

            // Список осей

            //List<ElementId> axis = new FilteredElementCollector(doc).OfClass(typeof(Grid)).OfCategory(BuiltInCategory.OST_Grids).Select(it => it.Id).ToList();

            ICollection<Element> axis_Grid = new FilteredElementCollector(doc).OfClass(typeof(Grid)).ToElements();

            
            // создание словаря - id оси на уровне
            //var Dict_Axis = new Dictionary<string, Dictionary<string, object>>();

            var Dict_Axis = Utilit_1_1Dict_Axis.Create_Dict_Axis(axis_Grid, units);

            // лишние оси убрать из рассмотерния, но чушь не работает
            Dict_Axis = Utilit_1_1Dict_Axis_del_remuve.Re_Dict_Axis_del_remuve(Dict_Axis,doc);

            // создание словаря уровень - id вентшахты на уровне
            // var Dict_level_ventsId = new Dictionary<string, List<string>>();

            var Dict_level_ventsId = Utilit_1_2Dict_level_ventsId.Create_Dict_level_ventsId(doc, vents, units);

            // создание словаря id вентшахты - характеристики:
            //var Dict_ventId_Properts = new Dictionary<string, Dictionary<string, object>>();

            var Dict_ventId_Properts = Utilit_1_3Dict_ventId_Properts.Create_Utilit_Dict_ventId_Properts(doc, vents, units);

            // Группировка вентшахт как они стоят друг над другом
            var Dict_Grup_numOV_spisokOV = Utilit_2_1Dict_Grup_numOV_spisokOV.Create_Dict_Grup_numOV_spisokOV(Dict_ventId_Properts, Dict_level_ventsId);

            // создаёт лист с типоразмерами вентшахт
            var List_Size_OV = Utilit_2_2List_Size_OV.Create_List_Size_OV(Dict_ventId_Properts, Dict_Grup_numOV_spisokOV);

            //создаёт словарь - номер группы вентшахт, лист( ближайшие оси А и 1)
            var Dict_numOV_nearAxes = Utilit_2_3Dict_numOV_nearAxes.Create_Dict_numOV_nearAxes(Dict_Axis, Dict_Grup_numOV_spisokOV);

            // создаёт словарь номер по порядку согласно радиальному расположению - номер группы вентшахты

            var Dict_numerateOV = Utilit_2_4Dict_numerateOV.Create_Dict_numerateOV(Dict_Axis, Dict_Grup_numOV_spisokOV);

            // Перезапись словаря по порядку в котором будут пронумерованы шахты на плане этажа

            Dict_Grup_numOV_spisokOV = Utilit_2_5_ReDict_numOV_spisokOV.ReCreate_Dict_Grup_numOV_spisokOV(Dict_numerateOV, Dict_Grup_numOV_spisokOV);

            // Повторны этажей

            var Dict_sovpad_level = Utilit_2_6ListPovtor_OV_on_Plans.Create_ListPovtor_OV_on_Plan(Dict_Grup_numOV_spisokOV, Dict_ventId_Properts);



            // Запись словаря для эксперементов вне Revit Api
            string json_Dict_Axis = JsonConvert.SerializeObject(Dict_Axis);
            string json_Dict_level_ventsId = JsonConvert.SerializeObject(Dict_level_ventsId);
            string json_Dict_ventId_Properts = JsonConvert.SerializeObject(Dict_ventId_Properts);
            string json_Dict_Grup_numOV_spisokOV = JsonConvert.SerializeObject(Dict_Grup_numOV_spisokOV);
            string json_List_Size_OV = JsonConvert.SerializeObject(List_Size_OV);
            string json_Dict_numOV_nearAxes = JsonConvert.SerializeObject(Dict_numOV_nearAxes);
            string json_Dict_numerateOV = JsonConvert.SerializeObject(Dict_numerateOV);
            string json_Dict_sovpad_level = JsonConvert.SerializeObject(Dict_sovpad_level);

            string path_export = @"D:\образование\Ревит\";
            path_export = @"C:\Users\KVinogradov\Desktop\сборки\";
            File.WriteAllText(path_export+"json_Dict_Axis.json", json_Dict_Axis, Encoding.UTF8);
            File.WriteAllText(path_export +"json_Dict_level_ventsId.json", json_Dict_level_ventsId, Encoding.UTF8);
            File.WriteAllText(path_export + "json_Dict_ventId_Properts.json", json_Dict_ventId_Properts, Encoding.UTF8);
            File.WriteAllText(path_export + "json_Dict_Grup_numOV_spisokOV.json", json_Dict_Grup_numOV_spisokOV, Encoding.UTF8);
            File.WriteAllText(path_export + "json_List_Size_OV.json", json_List_Size_OV, Encoding.UTF8);
            File.WriteAllText(path_export + "json_Dict_numOV_nearAxes.json", json_Dict_numOV_nearAxes, Encoding.UTF8);
            File.WriteAllText(path_export + "json_Dict_numerateOV.json", json_Dict_numerateOV, Encoding.UTF8);
            File.WriteAllText(path_export + "json_Dict_sovpad_level.json", json_Dict_sovpad_level, Encoding.UTF8);


            //var mc = new Utilit_3_1Create_new_plan_floor();
            //new Utilit_3_1Create_new_plan_floor();
            //new CreateNamedFloorPlansCommand();
            new Utilit_3_2Create_razdel();

            return Result.Succeeded;

        }


    }

  
}


