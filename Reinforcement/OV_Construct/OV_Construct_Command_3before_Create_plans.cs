﻿using System;
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
    // выполнение алгоритма до (включительно) типоразмеров вентшахт
    public class OV_Construct_Command_3before_Create_plans : IExternalCommand
    {
        static AddInId addinId = new AddInId(new Guid("424E29F8-20DE-49CB-8CF0-8627879F12C7"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            OV_Construct_All_Dictionary.ClearAll(); // чистим всё
            ExecuteLogic(commandData, ref message, elements);

            return Result.Succeeded;
        }

        public static void ExecuteLogic(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            ForgeTypeId units = UnitTypeId.Millimeters;
            OV_Construct_Command_2before_Povtor_flour.ExecuteLogic(commandData, ref message, elements);


            Utilit_3_1Create_new_floor.Create_new_floor(units, ref message, elements);

            Utilit_3_2Dict_Axis.Dict_Axis();

            OV_Construct_All_Dictionary.Dict_numOV_nearAxes = Utilit_3_3Dict_numOV_nearAxes.Create_Dict_numOV_nearAxes(doc, OV_Construct_All_Dictionary.Dict_Axis, OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV);

            Utilit_3_4Create_dimensions_on_plans.Create_dimensions_on_plans(uidoc, ref message, elements, doc);

            Utilit_3_5Vinosky_ov_type_size.Create_vinosky_on_plans();

            Utilit_3_6Create_new_plan_floor.Create_new_plan_floor();
            try
            {
                output_json();
            }
            catch { }

        }

        internal static void output_json()
        {
            // Запись словаря для эксперементов вне Revit Api
            string json_Dict_Axis = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_Axis);
            string json_Dict_level_ventsId = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_level_ventsId);
            string json_Dict_ventId_Properts = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_ventId_Properts);
            string json_Dict_Grup_numOV_spisokOV = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV);
            string json_List_Size_OV = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.List_Size_OV);
            string json_Dict_numOV_nearAxes = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_numOV_nearAxes);
            string json_Dict_numerateOV = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_numerateOV);
            string json_Dict_sovpad_level = JsonConvert.SerializeObject(OV_Construct_All_Dictionary.Dict_sovpad_level);

            string path_export = @"D:\образование\Ревит\";
            string path_export2 = @"C:\Users\KVinogradov\Desktop\сборки\";

            bool proxod = false;
            if (File.Exists(path_export2))
            {
                path_export = path_export2;
                proxod = true;
            }
            else if (File.Exists(path_export))
            {
                proxod = true;
            }

            if (proxod)
            {
                File.WriteAllText(path_export + "json_Dict_Axis.json", json_Dict_Axis, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_level_ventsId.json", json_Dict_level_ventsId, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_ventId_Properts.json", json_Dict_ventId_Properts, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_Grup_numOV_spisokOV.json", json_Dict_Grup_numOV_spisokOV, Encoding.UTF8);
                File.WriteAllText(path_export + "json_List_Size_OV.json", json_List_Size_OV, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_numOV_nearAxes.json", json_Dict_numOV_nearAxes, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_numerateOV.json", json_Dict_numerateOV, Encoding.UTF8);
                File.WriteAllText(path_export + "json_Dict_sovpad_level.json", json_Dict_sovpad_level, Encoding.UTF8);
            }

        }


    }


}