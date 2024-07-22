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
using System.Windows.Media.TextFormatting;

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
                double diameter = 0;
                foreach (Reference secObj in sec)
                {
                    Element element = doc.GetElement(secObj);
                    /*
                    var geometryElement = element.get_Geometry(options);
                    foreach (var geom in geometryElement)
                    {
                        MessageBox.Show(geom.ToString());
                    };
                    //Pt.Add(maxPt); Pt.Add(minPt);  
                    */
                    /*
                    var boundingBox = element.get_BoundingBox(uidoc.ActiveView);
                    Pt.Add(boundingBox.Min); Pt.Add(boundingBox.Max);
                    */
                    LocationPoint locationPts = element.Location as LocationPoint;
                    diameter = element.LookupParameter("• Диаметр").AsDouble();
                    //diameter = UnitUtils.ConvertFromInternalUnits(diameter, UnitTypeId.Millimeters);                      
                    Pt.Add(locationPts.Point);
                }
                XYZ xyz =  Pt.ElementAt(0).Subtract(Pt.ElementAt(1));
                double move = diameter / 2 ;
                XYZ subst1 = null, subst2 = null;
                if (xyz.X < 0)
                {
                    subst1 = new XYZ(move, 0, 0);
                    subst2 = new XYZ(-move, 0, 0);
                }
                else if (xyz.X > 0)
                {
                    subst1 = new XYZ(-move, 0, 0);
                    subst2 = new XYZ(move, 0, 0);
                }
                else if (xyz.Y < 0)
                {
                    subst1 = new XYZ(0, move, 0);
                    subst2 = new XYZ(0, -move, 0);
                }
                else if (xyz.Y > 0)
                {
                    subst1 = new XYZ(0, -move, 0);
                    subst2 = new XYZ(0, move, 0);
                }
                else if (xyz.Z != 0)
                {
                    subst1 = new XYZ(0, 0, move);
                    subst2 = new XYZ(0, 0, -move);
                }
                Line line = Line.CreateBound(Pt.ElementAt(0).Subtract(subst1), Pt.ElementAt(1).Subtract(subst2));
                // Retrieve elements from database
                FilteredElementCollector col = new FilteredElementCollector(doc);
                IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
                foreach (var elem in symbols)
                {
                    ElementType elemType = elem as ElementType;
                    if (elemType.FamilyName == FamName && elemType.Name == exampleName)
                    {
                        FamilySymbol symbol = elem as FamilySymbol;
                        using (Transaction t = new Transaction(doc, "Создание шпильки"))
                        {
                            t.Start();
                            FamilyInstance familyInstance = doc.Create.NewFamilyInstance(line, symbol, uidoc.ActiveView);
                            familyInstance.LookupParameter("Объемный вид").Set(1);
                            familyInstance.LookupParameter("Заливка").Set(0);
                            familyInstance.LookupParameter("Радиус основы").Set(diameter / 2);
                            familyInstance.LookupParameter("• Диаметр").Set(UnitUtils.ConvertToInternalUnits(6, UnitTypeId.Millimeters));
                            familyInstance.LookupParameter("Длина отгиба").Set(UnitUtils.ConvertToInternalUnits(30, UnitTypeId.Millimeters));
                            var asd = symbol.Family;
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
        public static string FamName { get; set; } = "ЕС_А-23_Шпилька";
        public static string exampleName { get; set; } = "А240";

    }
}
