using System;
using System.Collections;
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
    public class OV_Construct_Command_2before_Povtor_flour : IExternalCommand
    {

        static AddInId addinId = new AddInId(new Guid("424E29F8-20DE-49CB-8CF0-8627879F12C5"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ExecuteLogic(commandData, ref message, elements);

            // Создаем StringBuilder для формирования сообщения
            StringBuilder messageBuilder = new StringBuilder();

            // Перебираем все пары ключ-значение в словаре
            foreach (KeyValuePair<string, List<string>> entry in OV_Construct_All_Dictionary.Dict_sovpad_level)
            {
                // Добавляем ключ
                messageBuilder.AppendLine($"Базовый уровень: {entry.Key}");

                // Добавляем все значения из списка
                messageBuilder.AppendLine("Совпадающие уровни:");
                foreach (string value in entry.Value)
                {
                    messageBuilder.AppendLine($"- {value}");
                }

                // Добавляем пустую строку для разделения
                messageBuilder.AppendLine();
            }

            // Показываем диалог с собранным сообщением
            TaskDialog.Show("Совпадающие уровни ОВ", messageBuilder.ToString());

            return Result.Succeeded;
        }



        public static void ExecuteLogic(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            ForgeTypeId units = UnitTypeId.Millimeters;

            // !!!выполняем предшествующую команду
            OV_Construct_Command_1before_List_Size_OV.ExecuteLogic(commandData, ref message, elements);

            

            // создаёт словарь номер по порядку согласно радиальному расположению - номер группы вентшахты

            OV_Construct_All_Dictionary.Dict_numerateOV = Utilit_2_4Dict_numerateOV.Create_Dict_numerateOV( OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV);

            // Перезапись словаря по порядку в котором будут пронумерованы шахты на плане этажа

            OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV = Utilit_2_5ReDict_numOV_spisokOV.ReCreate_Dict_Grup_numOV_spisokOV(OV_Construct_All_Dictionary.Dict_numerateOV, OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV);

            // Повторные этажей

            OV_Construct_All_Dictionary.Dict_sovpad_level = Utilit_2_6ListPovtor_OV_on_Plans.Create_ListPovtor_OV_on_Plan(OV_Construct_All_Dictionary.Dict_Grup_numOV_spisokOV, OV_Construct_All_Dictionary.Dict_ventId_Properts);

        }
    }
}

