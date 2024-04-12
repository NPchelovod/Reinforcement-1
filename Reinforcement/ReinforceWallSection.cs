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
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class ReinforceWallSection : IExternalCommand
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
                using (Transaction t = new Transaction(doc, "Армирование стены"))
                {
                    t.Start();
                    //Тут пишем основной код
                    bool returnSelectOption = SelectionUIOptions.GetSelectionUIOptions().SelectFaces,
                         selectOption = SelectionUIOptions.GetSelectionUIOptions().SelectFaces;
                    selectOption = true;
                    var reference = uidoc.Selection.PickObject(ObjectType.Element);
                    selectOption = returnSelectOption;
                    var elementId =  reference.ElementId;
                    Element element =  doc.GetElement(elementId);
                    Options options = new Options();
                    options.View = uidoc.ActiveView;
                    GeometryElement geometry = element.get_Geometry(options);
                    foreach (GeometryObject geometryObject in geometry)
                    {
                        if (geometryObject is Solid)
                        {
                            Solid solid = (Solid)geometryObject;
                            var faces = solid.Faces;

                        }
                    };
                    var asd = geometry.ToList();
                    /*
                    XYZ pt1 = new XYZ (0, 0, 0);
                    XYZ pt2 = new XYZ (10, 0, 1000);
                    Line line = Line.CreateBound(pt1, pt2);
                    doc.Create.NewDetailCurve(uidoc.ActiveView, sline);
                    */
                    MessageBox.Show(asd.ToString());
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
