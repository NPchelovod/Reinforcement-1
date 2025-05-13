using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class ReinforceWall
    {
        public Result Do()
        {
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            Selection sel = uidoc.Selection;
            FilteredElementCollector col = new FilteredElementCollector(doc);
            IList<Element> symbols = col.OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .ToElements();
            FamilySymbol symbol = null;
            Reference line = sel.PickObject(ObjectType.Element, "Выберите линию");
            Element lineElement = doc.GetElement(line);
            CurveElement curveElement = lineElement as CurveElement;
            Line curve = curveElement.GeometryCurve as Line;
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Армирование стены"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    foreach (var element in symbols)
                    {
                        ElementType elemType = element as ElementType;
                        if (elemType.FamilyName == FamName)
                        {
                            symbol = element as FamilySymbol;
                            break;
                        }
                    }
                    doc.Create.NewFamilyInstance(curve, symbol, doc.ActiveView);

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
        public static string FamName { get; set; } = "ЕС_А-01_Дополнительная";

    }
}
