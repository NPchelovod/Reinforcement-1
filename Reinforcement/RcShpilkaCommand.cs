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
using System.Windows.Media;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class RcShpilkaCommand : IExternalCommand
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
                IList<Reference> sec = sel.PickObjects(ObjectType.Element, "Выберите поперечное сечение стержня");
                IList<XYZ> Pt = new List<XYZ>();
                Options options = new Options();
                options.View = uidoc.ActiveView;
                foreach (Reference secObj in sec)
                {
                    Element element = doc.GetElement(secObj);
                    var geometryElement = element.get_Geometry(options);
                    foreach (var geom in geometryElement)
                    {
                        MessageBox.Show(geom.ToString());
                    };
                   // Pt.Add(maxPt); Pt.Add(minPt);                   
                }

                Line line = Line.CreateBound(Pt.ElementAt(0), Pt.ElementAt(3));
                // Retrieve elements from database
                FilteredElementCollector col = new FilteredElementCollector(doc);
                IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
                foreach (var elem in symbols)
                {
                    ElementType elemType = elem as ElementType;
                    if (elemType.FamilyName == FamName)
                    {
                        FamilySymbol symbol = elem as FamilySymbol;
                        using (Transaction t = new Transaction(doc, "действие"))
                        {
                            t.Start();
                            doc.Create.NewFamilyInstance(line, symbol, uidoc.ActiveView);
                            t.Commit();
                        }
                        break;
                    }
                }
                /*
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код
                    t.Commit();
                }
                */
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        public static string FamName { get; set; } = "ЕС_А-23 - Шпилька";

    }
}
