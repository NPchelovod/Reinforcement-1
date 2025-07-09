using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Reinforcement.Stage1.DecorViewPlan;
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

    public class TestMoveDims : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    var sel = uidoc.Selection;
                    var list = sel.GetElementIds();
                    var elem = list.First();
                    var dim = doc.GetElement(elem) as Dimension;
                    var oldPos = dim.Segments.get_Item(0).TextPosition;
                    dim.Segments.get_Item(0).TextPosition = Transform.CreateTranslation(new XYZ(-3,0,0)).OfPoint(oldPos);
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
