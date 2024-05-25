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

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class PastePerforationToWall : IExternalCommand
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
                ISelectionFilter selFilter = new SelectionFilter();
                Reference sel = uidoc.Selection.PickObject(ObjectType.Element, selFilter);
                Element element = doc.GetElement(sel);
                var option = new Options()
                {
                    View = doc.ActiveView
                };
                var geometryElement = element.get_Geometry(option);
                GeometryInstance geometryInstance = null;
                foreach (var geometryObject in geometryElement)
                {
                    if (geometryObject is GeometryInstance instance)
                    {
                        geometryInstance = instance;
                    }
                }
                IList<PolyLine> lines = new List<PolyLine>();
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    foreach (var cadElem in geometryInstance.GetInstanceGeometry())
                    {
                        if (cadElem is PolyLine polyLine)
                        {
                            var style = (GraphicsStyle)doc.GetElement(polyLine.GraphicsStyleId);
                            if (style.GraphicsStyleCategory.Name == "КР_Перфорация")
                            {
                                lines.Add(polyLine);
                            }
                        }
                    }
                    ISelectionFilter selFilterWall = new SelectionFilterWall();
                    sel = uidoc.Selection.PickObject(ObjectType.Element, selFilterWall);
                    element = doc.GetElement(sel);
                    ElementId levelId = element.LevelId;
                    Level level = doc.GetElement(levelId) as Level;
                    foreach (var polyLine in lines)
                    {
                        XYZ maxPt = polyLine.GetOutline().MaximumPoint;
                        XYZ minPt = polyLine.GetOutline().MinimumPoint;
                        var center = (maxPt + minPt) / 2;
                        FilteredElementCollector col = new FilteredElementCollector(doc);
                        IList<Element> symbols = col.OfClass(typeof(FamilySymbol))
                        .WhereElementIsElementType()
                        .ToElements();
                        FamilySymbol symbol = null;
                        foreach (var elem in symbols)
                        {
                            ElementType elemType = elem as ElementType;
                            if (elemType.FamilyName == "ADSK_ОтверстиеПрямоугольное_ВСтене")
                            {
                                symbol = elem as FamilySymbol;
                                break;
                            }
                        }
                        var wallLocation = element.get_BoundingBox(doc.ActiveView);
                        XYZ wallMin = wallLocation.Min,
                            wallMax = wallLocation.Max;
                        bool a = wallMin.X < center.X &
                                 wallMin.Y < center.Y &
                                 wallMax.X > center.X &
                                 wallMax.Y > center.Y;
                        if (a)
                        {
                            Line l1 = Line.CreateBound(minPt, maxPt);
                            //var test = doc.Create.NewDetailCurve(doc.ActiveView, l1);
                            FamilyInstance familyInstance = doc.Create.NewFamilyInstance(center, symbol, element, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            var diagLength = minPt.DistanceTo(maxPt);
                            XYZ pt1 = new XYZ(minPt.X, maxPt.Y, minPt.Z);
                            var height = minPt.DistanceTo(pt1);
                            var width = maxPt.DistanceTo(pt1);
                            // height = UnitUtils.ConvertFromInternalUnits(height, UnitTypeId.Millimeters);
                            // width = UnitUtils.ConvertFromInternalUnits(width, UnitTypeId.Millimeters);
                            familyInstance.LookupParameter("Отметка от уровня").Set(0);
                            familyInstance.LookupParameter("ADSK_Отверстие_Высота").Set(0.2);
                            if (height > width)
                            {
                                //familyInstance.LookupParameter("ADSK_Отверстие_Ширина").Set(height);
                            }
                            else
                            {
                                // familyInstance.LookupParameter("ADSK_Отверстие_Ширина").Set(width);
                            }
                        }
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
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element is ImportInstance)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
        public class SelectionFilterWall : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element is Wall)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference refer, XYZ point)
            {
                return false;
            }
        }
    }
}
