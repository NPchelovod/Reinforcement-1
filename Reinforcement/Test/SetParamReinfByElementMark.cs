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

namespace Reinforcement.Test
{
    [Transaction(TransactionMode.Manual)]

    public class SetParamReinfByElementMark : IExternalCommand
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
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            string elementMark = "Ск";
            var familyInstances1 = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilyInstance>()
                .Where(x => (x.LookupParameter("• Марка элемента")?.AsValueString() ?? "").Contains(elementMark))
                .Where(x => x.SuperComponent == null)
                .ToList();

            elementMark = "М";
            var familyInstances2 = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilyInstance>()
                .Where(x => (x.LookupParameter("• Марка элемента")?.AsValueString() ?? "").Contains(elementMark))
                .Where(x => x.SuperComponent == null)
                .ToList();


            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    foreach (var familyInstance in familyInstances1)
                    {
                        familyInstance.LookupParameter("• Считать П-шки").Set(1);
                    }
                    foreach (var familyInstance in familyInstances2)
                    {
                        familyInstance.LookupParameter("• Считать Г-шки").Set(1);
                    }
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
