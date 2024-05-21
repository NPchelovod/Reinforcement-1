using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Reinforcement.PickFilter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using static Reinforcement.ViewModelPickWithFilter;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CommandPickWithFilter : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            try //ловим ошибку
            {
                //Тут пишем основной код для изменения элементов модели
                var dialogueView = new MainViewPickWithFilter();
                dialogueView.ShowDialog();
                ISelectionFilter selFilter = new MassSelectionFilterTypeName();
                IList<Element> eList = RevitAPI.UiDocument.Selection.PickElementsByRectangle(selFilter, "Выберите че то");
                List<ElementId> ids = new List<ElementId>();
                foreach (Element e in eList)
                {
                    ids.Add(e.Id);
                }
                RevitAPI.UiDocument.Selection.SetElementIds(ids);
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        public static string constrTypeName { get; set; }

        public class MassSelectionFilterTypeName : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element.LookupParameter("• Тип элемента").AsString() == constrTypeName)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }

        }
    }
}
