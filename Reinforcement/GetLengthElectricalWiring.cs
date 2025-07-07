using Autodesk.Internal.Windows.ToolBars;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Autodesk.Revit.DB.SpecTypeId;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class GetLengthElectricalWiring : IExternalCommand
    {
        public float GetCurveLength(DetailCurve detailCurve)
        {
            float curveLength = float.Parse(detailCurve.LookupParameter("Длина").AsValueString());
            return curveLength;
        }


        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            Document doc = uiDocument.Document;
            Autodesk.Revit.DB.View activeView = uiDocument.ActiveView;

            // Retrieve elements from database

            FilteredElementCollector colLines = new FilteredElementCollector(doc, activeView.Id).OfCategory(BuiltInCategory.OST_Lines)
                .WhereElementIsNotElementType();

            IList<Element> lines = colLines.ToElements();

            //префик электрики
            string prefix_EL = "ЭЛ_";

            var all_diameters = new List<string>()
            {
                "d25", "d32" ,"d40" , "d50"
            };

            var dict_answer = new Dictionary<string, double>();

            foreach (var diam in all_diameters)
            { dict_answer[diam] = 0; }

            double error = 0;
            string neraspozn_name = "";
            double l_lotkov = 0;
            foreach (var line in lines)
            {

                if (line is DetailCurve detailCurve)
                {
                    string lineStyleName = detailCurve.LineStyle.Name;
                    float length = GetCurveLength(detailCurve);

                    if (lineStyleName.Contains(prefix_EL))
                    {
                        bool edin_proxod = false;
                        l_lotkov += length;
                        // дробим по запятой
                        string[] parts = lineStyleName.Split(' ').Select(p => p.Trim()).ToArray();
                        string past_diam = " ";
                        double past_len = 0;

                        foreach (var tek_name_trub in parts)
                        {
                            int kol_vo = 1; //d32

                            //вдруг x2 крат и тп, тогда длина в 2 раза больше
                            // Регулярное выражение для поиска паттерна: x + пробелы + число
                            Match match = Regex.Match(tek_name_trub, @"[xXхХ]\s*(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                            {
                                kol_vo = number;
                                if (kol_vo <= 0)
                                { kol_vo = 1; }
                            }

                            bool proxod = false;
                            foreach (var tek_diametr in all_diameters)
                            {

                                if (tek_name_trub.Contains(tek_diametr))
                                {
                                    double dobavka = kol_vo * length;
                                    dict_answer[tek_diametr] += dobavka;
                                    proxod = true;
                                    past_diam = tek_diametr;
                                    past_len = length;
                                    edin_proxod = true;
                                }
                            }
                            if (!proxod && kol_vo > 1 && past_diam != " ")
                            {
                                //развиба строка неверно сейчас только число
                                double dobavka = (kol_vo - 1) * past_len;
                                dict_answer[past_diam] += dobavka;
                            }


                        }

                        if (!edin_proxod)
                        {
                            error += length;
                            neraspozn_name += lineStyleName +"l=" + length.ToString()+ ", ";
                        }

                    }



                }

            }
            // Создаем StringBuilder для форматирования содержимого
            StringBuilder messageBuilder = new StringBuilder("Длины электроразводки:\n\n");

            double sum_all = 0;
            // Форматируем каждую пару ключ-значение
            if (error > 10)
            {
                messageBuilder.AppendLine($" Суммарная ошибка: {Math.Round(error / 1000, 2)} м.");
                messageBuilder.AppendLine($" Нераспознанные имена: {neraspozn_name}");
            }

            foreach (KeyValuePair<string, double> entry in dict_answer)
            {
                double dobavka = Math.Round(entry.Value / 1000, 2);
                sum_all += dobavka;
                messageBuilder.AppendLine($"{entry.Key}: {dobavka} м.");
            }

            messageBuilder.AppendLine($" Суммарная длина путей кабелей: {sum_all} м.");
            messageBuilder.AppendLine($" Суммарная длина лотков под кабели: {Math.Round(l_lotkov / 1000, 2)} м.");
            // Показываем диалог
            TaskDialog.Show("Длина Электроразводки", messageBuilder.ToString());



            return Result.Succeeded;
        }
    }
}
