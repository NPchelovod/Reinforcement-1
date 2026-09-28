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
using System.Windows.Controls;
using System.Windows.Forms;
using static Autodesk.Revit.DB.SpecTypeId;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ArmLengthEquels : IExternalCommand
    {
        //автоматическое заполнение 
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            var uiDocument = RevitAPI.UiDocument;
            Document doc = uiDocument.Document;


            //надо длины всем прописать

            var elems = SelectOrAllElements();
            using (Transaction t = new Transaction(doc, "Установка длины стержня"))
            {
                t.Start();
                // цикл с установкой параметров

                foreach (var elem in elems)
                {
                    // Ищем параметры
                    Parameter paramTarget = elem.LookupParameter("ДлинаСтержня");
                    Parameter paramSource = elem.LookupParameter("Длина");

                    if (paramTarget == null || paramSource == null)
                        continue;

                    // Проверяем, что это параметры длины (опционально, но надёжно)
                    if (paramSource.StorageType != StorageType.Double ||
                        paramTarget.StorageType != StorageType.Double)
                        continue;

                    double lengthReal = paramSource.AsDouble(); // в Revit длина в футах (внутренние единицы)

                    if (lengthReal <= 0)
                        continue;
                    //paramTarget.Set(lengthReal * mmPerFoot); // если целевой параметр ожидает мм
                    paramTarget.Set(lengthReal);


                }
                t.Commit();
            }

            return Result.Succeeded;
        }
        const double mmPerFoot = 304.8;
        public List<Element> SelectOrAllElements()
        {

            var uiDocument = RevitAPI.UiDocument;
            Document doc = uiDocument.Document;

            var selection = uiDocument.Selection;
            var selectedIds = selection.GetElementIds();
            Autodesk.Revit.DB.View activeView = uiDocument.ActiveView;
            List<Element> selectedElements = new List<Element>();
            foreach (var id in selectedIds)
            {
                var elem = doc.GetElement(id);
                if (elem != null)
                    selectedElements.Add(elem);
            }
            if (selectedElements.Count > 0)
            {
                return selectedElements;
            }
            var collector = new FilteredElementCollector(doc, activeView.Id);
            var allElements = collector.WhereElementIsNotElementType().ToElements();

            return allElements.ToList();

        }

        public bool ArmLengthSet(Element elem)
        {
            // Ищем параметры
            Parameter paramTarget = elem.LookupParameter("ДлинаСтержня");


            if (paramTarget == null)
                return false;
            Parameter paramSource = elem.LookupParameter("Длина");
            if (paramSource == null)
                return false;
            // Проверяем, что это параметры длины (опционально, но надёжно)
            if (paramSource.StorageType != StorageType.Double ||
                paramTarget.StorageType != StorageType.Double)
                return false;

            double lengthReal = paramSource.AsDouble(); // в Revit длина в футах (внутренние единицы)

            if (lengthReal <= 0)
                return false;
            double lengthUser = paramTarget.AsDouble();

            //paramTarget.Set(lengthReal * mmPerFoot); // если целевой параметр ожидает мм
            bool proxod = false;
            if (lengthUser < lengthReal)
            {
                proxod = true;
            }
            else if (PastLength.TryGetValue(elem.Id, out double pastL))
            {
                if (Math.Abs(lengthUser - pastL) < 10)
                {
                    proxod = true; // иначе считаем что пользователь поменял ддину
                }
            }
            else
            {
                proxod = true;
            }

            if (proxod)
            {
                paramTarget.Set(lengthReal);
                PastLength[elem.Id] = lengthReal;
            }
            return proxod;
        }

        public Dictionary<ElementId, double> PastLength = new Dictionary<ElementId, double>();
    }

}

