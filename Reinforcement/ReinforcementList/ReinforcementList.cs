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

    public class ReinforcementList : IExternalCommand
    {
        public static string ConstrMark { get; set; }
        public static string ElementMark { get; set; }
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            var dialog = new MainViewReinforcementList();
            var result = dialog.ShowDialog();
            if (String.IsNullOrEmpty(ConstrMark) || String.IsNullOrEmpty(ElementMark) || result == false)
            {
                return Result.Cancelled;
            }

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var reinfList = collector.
                OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilyInstance>()
                .Where(x => (x.LookupParameter("◦ Марка конструкции")?.AsValueString() ?? "").Contains(ConstrMark))
                .Where(x =>
                        (x.LookupParameter("• Считать Г-шки")?.AsInteger() ?? 0) == 1 ||
                        (x.LookupParameter("• Считать П-шки")?.AsInteger() ?? 0) == 1 ||
                        (x.LookupParameter("• Учет в спецификации")?.AsInteger() ?? 0) == 1
                      )
                .Where(x => x.SuperComponent == null)
                .Where(x => (x.LookupParameter("• Марка элемента")?.AsValueString() ?? "").Contains(ElementMark))
                .Select(x => x.Id)              
                .ToList();



            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    foreach (var reinf in reinfList)
                    {
                        var element = doc.GetElement(reinf);
                        var viewId = element.OwnerViewId;
                        var view = doc.GetElement(viewId) as Autodesk.Revit.DB.View;
                        uidoc.ActiveView = view;
                        t.Start();
                        doc.ActiveView.IsolateElementsTemporary(reinfList);
                        t.Commit();
                    }

                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    Selection sel = uidoc.Selection;
                    sel.SetElementIds(reinfList);
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}
