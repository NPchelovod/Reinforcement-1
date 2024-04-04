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
                    if (selectedIds.Count == 0)
                    {
                        MessageBox.Show("Ничего не выбрано");
                        return Result.Failed;
                    }
                    Reference line = sel.PickObject(ObjectType.Element, "Выберите линию");
                    Element lineElement = doc.GetElement(line);
                    CurveElement curve = lineElement as CurveElement;
                    Line ln = curve.GeometryCurve as Line;
                    // XYZ в ревит измеряется в ФУТАХ 1 фут = 304,8 мм

                    XYZ pt1 = ln.GetEndPoint(0),
                        pt2 = ln.GetEndPoint(1),
                        vectorLngth = pt2 - pt1;
                    double length = vectorLngth.GetLength() * 304.8;
                    int step = 200;
                    double n = length / step;
                    XYZ vector = vectorLngth / n;
                    n++;
                    var createdElements = LinearArray.ArrayElementsWithoutAssociation(doc, uidoc.ActiveView, selectedIds, Convert.ToInt32(n), vector, ArrayAnchorMember.Second);
                    sel.SetElementIds(createdElements); //выбрать все созданные элементы в т.ч. и первый
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
