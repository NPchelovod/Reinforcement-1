using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    // выполнение алгоритма до (включительно) типоразмеров вентшахт
    public class OV_Construct_Command_before_List_Size_OV : IExternalCommand
    {

        static AddInId addinId = new AddInId(new Guid("424E29F8-20DE-49CB-8CF0-8627879F12C5"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ExecuteLogic(commandData, ref message, elements);
            //List < List<double> >
            // Преобразуем каждый внутренний список в строку
            List<string> lines = OV_Construct_All_Dictionary.List_Size_OV.Select(
            innerList => string.Join(", ", innerList.Select(num => num.ToString("0.##"))))
            .ToList();

            // Объединяем все строки через перенос
            string outputText = string.Join("\n", lines);

            // Выводим в Revit
            TaskDialog.Show("Все типоразмеры шахт ОВ", outputText);

            return Result.Succeeded;
        }

        

        public static void ExecuteLogic(ExternalCommandData commandData, ref string message, ElementSet elements)
        
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            ForgeTypeId units = UnitTypeId.Millimeters;

            //Control_Pick.MControl_Pick(uidoc, doc, units); //ref 
                                                            // в будущем можно будет менять
            string name_OB = "ОтверстиеВПерекрытии";
            Type name_class = typeof(FamilyInstance);
            BuiltInCategory name_OST = BuiltInCategory.OST_GenericModel;


            // vents - id всех элементов каналов ОВ с именем name_OB
            List<ElementId> vents = new FilteredElementCollector(doc).OfClass(name_class).OfCategory(name_OST).Where(it => it.Name == name_OB).Where(it => it.LookupParameter("ADSK_Отверстие_Функция").AsValueString() == "Вентканал").Select(it => it.Id).ToList();

            // Список осей

            //List<ElementId> axis = new FilteredElementCollector(doc).OfClass(typeof(Grid)).OfCategory(BuiltInCategory.OST_Grids).Select(it => it.Id).ToList();

            ICollection<Element> axis_Grid = new FilteredElementCollector(doc).OfClass(typeof(Grid)).ToElements();


            // создание словаря - id оси на уровне
            //var Dict_Axis = new Dictionary<string, Dictionary<string, object>>();

            OV_Construct_All_Dictionary.Dict_Axis = Utilit_1_1Dict_Axis.Create_Dict_Axis(axis_Grid, units);

            // лишние оси убрать из рассмотерния, но чушь не работает
            OV_Construct_All_Dictionary.Dict_Axis = Utilit_1_1Dict_Axis_del_remuve.Re_Dict_Axis_del_remuve(OV_Construct_All_Dictionary.Dict_Axis, doc);

            // создание словаря уровень - id вентшахты на уровне
            // var Dict_level_ventsId = new Dictionary<string, List<string>>();

            OV_Construct_All_Dictionary.Dict_level_ventsId = Utilit_1_2Dict_level_ventsId.Create_Dict_level_ventsId(doc, vents, units);

            // создание словаря id вентшахты - характеристики:
            //var Dict_ventId_Properts = new Dictionary<string, Dictionary<string, object>>();

            OV_Construct_All_Dictionary.Dict_ventId_Properts = Utilit_1_3Dict_ventId_Properts.Create_Utilit_Dict_ventId_Properts(doc, vents, units);

            // Группировка вентшахт как они стоят друг над другом
            OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV = Utilit_2_1Dict_Grup_numOV_spisokOV.Create_Dict_Grup_numOV_spisokOV(OV_Construct_All_Dictionary.Dict_ventId_Properts, OV_Construct_All_Dictionary.Dict_level_ventsId);

            // создаёт лист с типоразмерами вентшахт
            OV_Construct_All_Dictionary.List_Size_OV = Utilit_2_2List_Size_OV.Create_List_Size_OV(OV_Construct_All_Dictionary.Dict_ventId_Properts, OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV);


            //return (Dict_Axis, Dict_level_ventsId, Dict_ventId_Properts, Dict_Grup_numOV_spisokOV, List_Size_OV);

        }

    }



}
