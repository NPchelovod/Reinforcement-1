#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Reinforcement.LinearCopy;
using Reinforcement.PickFilter;
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
        public static string copyStep { get; set; }
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            var dialogueView = new MainViewLinearCopyElement();
            dialogueView.ShowDialog();
            if (copyStep == "stop")
            {
                return Result.Cancelled;
            }
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
                    // XYZ в ревит измеряется в ФУТАХ 1 фут = 304,8 мм

                    Reference line = sel.PickObject(ObjectType.Element, "Выберите линию");
                    Element lineElement = doc.GetElement(line);
                    CurveElement curve = lineElement as CurveElement;
                    Line ln = curve.GeometryCurve as Line;
                    XYZ pt1 = ln.GetEndPoint(0),
                        pt2 = ln.GetEndPoint(1),
                        vectorLngth = pt2 - pt1;
                    double length = UnitUtils.ConvertFromInternalUnits(vectorLngth.GetLength(), UnitTypeId.Millimeters); //перевод в мм
                    
                    int step = int.Parse(copyStep); //copy step
                    
                    double n = length / step;
                    XYZ vector = vectorLngth / n;
                    int a = (int)n;
                    a++;
                    var createdElements = LinearArray.ArrayElementsWithoutAssociation(doc, uidoc.ActiveView, selectedIds, a, vector, ArrayAnchorMember.Second);
                    a--;
                    if (a < 5)
                    {
                        MessageBox.Show("Создано " + a + " элемента");
                    }
                    else
                    {
                        MessageBox.Show("Создано " + a + " элементов");
                    }
                    sel.SetElementIds(createdElements); //выбрать все созданные элементы в т.ч. и первый
                    t.Commit();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Не создано ни одного элемента!");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

    }
}
