using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CheckReinfTag : IExternalCommand
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
            Selection sel = uidoc.Selection;


            
            try //ловим ошибку
            {
                var reinforcmentListId = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .ToElements()
                .Where(x => x.Name.Contains("А500") || x.Name.Contains("А240") || x.Name.Contains("A500") || x.Name.Contains("A240")) //check кириллицу и латинницу
                .Select(x => x.Id)
                .ToList();

                List<ElementId> taggedElementsIds = new FilteredElementCollector (doc, doc.ActiveView.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .Select(x => x.GetTaggedElementIds())
                    .SelectMany(x => x)
                    .Select(x => x.HostElementId)
                    .ToList();

                List<ElementId> doubleTaggedElementsIds = taggedElementsIds
                    .GroupBy(x => x)
                    .Where(group => group.Count() > 1)
                    .SelectMany(x => x)
                    .Distinct()
                    .ToList();

                List<ElementId> noTagElement = reinforcmentListId.Except(taggedElementsIds).ToList();

                if (doubleTaggedElementsIds.Count != 0)
                {
                    DialogResult dialogResult = MessageBox.Show("Показать задвоенные выноски?" ,"Выбор" , MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                    {
                        foreach (var element in doubleTaggedElementsIds)
                        {
                            uidoc.ShowElements(element);
                            sel.SetElementIds(new List<ElementId> { element });
                            dialogResult = MessageBox.Show("Показать далее?", "Выбор", MessageBoxButtons.YesNo);
                            if (dialogResult == DialogResult.Yes)
                            {
                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                if (noTagElement.Count != 0)
                {
                    DialogResult dialogResult = MessageBox.Show("Показать не замаркированные?", "Выбор", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        foreach (var element in noTagElement)
                        {
                            uidoc.ShowElements(element);
                            sel.SetElementIds(new List<ElementId> { element });
                            dialogResult = MessageBox.Show("Показать далее?", "Выбор", MessageBoxButtons.YesNo);
                            if (dialogResult == DialogResult.Yes)
                            {
                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                if (noTagElement.Count == 0 && doubleTaggedElementsIds.Count == 0)
                {
                    MessageBox.Show("Все ок");
                }

                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели

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
