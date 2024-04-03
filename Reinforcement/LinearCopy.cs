#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class LinearCopyElement : IExternalCommand
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
                using (Transaction t = new Transaction(doc, "Прямолинейный массив"))
                {
                    t.Start();
                    Selection sel = uidoc.Selection;
                    ICollection<ElementId> selectedIds = sel.GetElementIds();
                    // XYZ в ревит измеряется в ФУТАХ 1 фут = 304,8 мм
                    XYZ pt1 = sel.PickPoint(),
                        pt2 = sel.PickPoint(),
                        vectorLngth = pt2 - pt1;
                    int length = Convert.ToInt32(vectorLngth.GetLength() * 304.8),
                        step = 200,
                        n = length / step;
                    XYZ vector = ;
                    TaskDialog.Show("шаг", $"{vectorLngth.GetLength() * 304.8}\n\n" +
                        $"Pt1: {pt1}\n\n" +
                        $"Pt2: {pt2}");
                    LinearArray.ArrayElementsWithoutAssociation(doc, uidoc.ActiveView, selectedIds, ++n, vector, ArrayAnchorMember.Second);
                    t.Commit();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }

    }
}
