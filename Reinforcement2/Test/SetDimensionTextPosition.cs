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
using System.Windows.Shapes;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class SetDimensionTextPosition : IExternalCommand
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
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    if (doc.ActiveView.SketchPlane == null)
                    {
                        SketchPlane sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin
                        (doc.ActiveView.ViewDirection, doc.ActiveView.Origin)
                        );
                        doc.ActiveView.SketchPlane = sp;
                        Reference dimension = uidoc.Selection.PickObject(ObjectType.Element);
                        Dimension dimensionElement = doc.GetElement(dimension) as Dimension;
                        XYZ point = uidoc.Selection.PickPoint("Выберите точку для перемещения текста");
                        dimensionElement.TextPosition = point;
                        doc.Delete(sp.Id);
                    }
                    else
                    {
                        Reference dimension = uidoc.Selection.PickObject(ObjectType.Element);
                        Dimension dimensionElement = doc.GetElement(dimension) as Dimension;
                        XYZ point = uidoc.Selection.PickPoint("Выберите точку для перемещения текста");
                        dimensionElement.TextPosition = point;
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
