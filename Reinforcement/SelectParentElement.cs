using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using System.Windows.Controls;
using System.Windows.Forms;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class SelectParentElement : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            try
            {
                Selection sel = uidoc.Selection;
                if (sel == null)
                {
                    MessageBox.Show("Ничего не выбрано");
                }
                ICollection<ElementId> selectedIds = sel.GetElementIds();
                List<ElementId> parentCollection = new List<ElementId>();
                List<ElementId> collection = new List<ElementId>();
                foreach (ElementId id in selectedIds)
                {
                    Element element = doc.GetElement(id);
                    FamilyInstance family = element as FamilyInstance;
                    var parentFamily = family.SuperComponent;
                    if (parentFamily == null)
                    {
                        collection.Add(id);
                    }
                    else
                    {
                        parentCollection.Add(parentFamily.Id);
                    }
                }
                DialogResult dialogResult = MessageBox.Show("Выбрать родительские или обычные? (да/нет)" ,"Выбор" , MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    sel.SetElementIds(collection);
                }
                else if (dialogResult == DialogResult.No)
                {
                    sel.SetElementIds(parentCollection);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Чет пошло не так!" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
    }
}
