using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
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

    public class GetAllAnnotationTagBounds : IExternalCommand
    {
        public static string famNameBrakeLine { get; } = "Линейный обрыв";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            var activeView = doc.ActiveView;
            var scale = activeView.Scale;
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    var tags = GetAllTags(doc, activeView);
                    foreach (var element in tags)
                    {
                        var location = ((LocationPoint)element.Location).Point;
                        var text = element.LookupParameter("Текст верх").AsValueString();
                        double textWidth, textHeight;
                        GetCharacterWidth(doc, text, out textWidth, out textHeight);
                        textWidth = RevitAPI.ToFoot(scale*textWidth);
                        textHeight = RevitAPI.ToFoot(scale*textHeight);
                        var offset = 0;

                        XYZ p1 = new XYZ(location.X - textWidth/2 - offset, location.Y - offset, 0);
                        XYZ p2 = new XYZ(location.X - textWidth/2 - offset, location.Y + textHeight, 0);
                        XYZ p3 = new XYZ(location.X + textWidth/2 + offset, location.Y + textHeight, 0);
                        XYZ p4 = new XYZ(location.X + textWidth/2 + offset, location.Y - offset, 0);

                        Line line = Line.CreateBound(p1, p2);
                        doc.Create.NewDetailCurve(activeView, line);

                        line = Line.CreateBound(p2, p3);
                        doc.Create.NewDetailCurve(activeView, line);

                        line = Line.CreateBound(p3, p4);
                        doc.Create.NewDetailCurve(activeView, line);

                        line = Line.CreateBound(p4, p1);
                        doc.Create.NewDetailCurve(activeView, line);
                        
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


        public static void GetCharacterWidth(Document doc, string text, out double width, out double height)
        {
            Font font = new Font("ISOCPEUR", 2.5f, FontStyle.Regular);
            Size textSize = TextRenderer.MeasureText(text, font);
            width = textSize.Width * 0.70;
            height = textSize.Height * 0.72;
        }
        private static List<Element> GetAllTags(Document doc, View activeView)
        {
            var col = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                .OfType<Element>()
                .ToList();
            return col;
        }
    }
}
