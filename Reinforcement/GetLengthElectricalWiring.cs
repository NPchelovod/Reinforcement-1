using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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

            float d25Sum = 0;
            float d32Sum = 0;
            float d40Sum = 0;
            float totalLength = 0;
            float totalLengthCheck = 0;

            // Retrieve elements from database

            FilteredElementCollector colLines = new FilteredElementCollector(doc, activeView.Id).OfCategory(BuiltInCategory.OST_Lines)
                .WhereElementIsNotElementType();

            IList<Element> lines = colLines.ToElements();

            //Create the dictionary
            var lineStyleActions = new Dictionary<string, Action<float>>
            {
                { "_d25", length => d25Sum += length },
                { "_d25 d25", length => d25Sum += (length * 2) },
                { "_d32", length => d32Sum += length },
                { "_d32 d25", length =>
                {
                    d25Sum += length;
                    d32Sum += length;
                }},
                { "_d32 d32", length => d32Sum += (length * 2) },
                { "_d32 d32 d25", length =>
                {
                    d25Sum += length;
                    d32Sum += (length * 2);
                }},
                { "_d40", length => d40Sum += length }
            };

            //Calculate lengths of electicalWiring
            foreach (var line in lines)
            {
                if (line is DetailCurve detailCurve)
                {
                    string lineStyleName = detailCurve.LineStyle.Name;
                    float length = GetCurveLength(detailCurve);

                    if (lineStyleActions.TryGetValue(lineStyleName, out Action<float> action))
                    {
                        action(length);
                        totalLengthCheck += length;
                    }
                    totalLength += length;
                }
            }

            //foreach (var line in lines)
            //{
            //    DetailCurve detailCurve = line as DetailCurve;
            //    if (detailCurve.LineStyle.Name == "_d25")
            //    {
            //        d25Sum += GetCurveLength(detailCurve);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d25 d25")
            //    {
            //        d25Sum += (GetCurveLength(detailCurve) * 2);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d32")
            //    {
            //        d32Sum += GetCurveLength(detailCurve);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d32 d25")
            //    {
            //        d25Sum += GetCurveLength(detailCurve);
            //        d32Sum += GetCurveLength(detailCurve);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d32 d32")
            //    {
            //        d32Sum += (GetCurveLength(detailCurve) * 2);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d32 d32 d25")
            //    {
            //        d25Sum += GetCurveLength(detailCurve);
            //        d32Sum += (GetCurveLength(detailCurve) * 2);
            //    }
            //    else if (detailCurve.LineStyle.Name == "_d40")
            //    {
            //        d40Sum += GetCurveLength(detailCurve);
            //    }
            //}
            bool flag = (totalLength == totalLengthCheck);
            MessageBox.Show($"Сумма d25 равна {d25Sum}\n Сумма d32 равна {d32Sum}\n Сумма d40 равна {d40Sum} \n Корректность подсчета {flag}");

            return Result.Succeeded;
        }
    }
}
