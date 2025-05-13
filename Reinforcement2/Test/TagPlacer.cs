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

namespace Reinforcement.Test
{
    [Transaction(TransactionMode.Manual)]

    public static class TagPlacer
    {
        public static void PlaceAnnotationTag(Document doc, ViewPlan view, XYZ hostPoint, FamilySymbol annotationSymbol, string text)
        {
            double textWidthMm = GetCharacterWidth(text);
            double textHeightMm = 3.5; // мм, под шрифт

            double step = 0.3; // шаг смещения (футы)
            int maxAttempts = 20;

            using (Transaction tx = new Transaction(doc, "Place Annotation Tag"))
            {
                tx.Start();

                for (int i = 1; i <= maxAttempts; i++)
                {
                    double dx = step * i;
                    double dy = step * i;

                    List<XYZ> offsets = new List<XYZ>
                {
                    new XYZ(dx, 0, 0),
                    new XYZ(-dx, 0, 0),
                    new XYZ(0, dy, 0),
                    new XYZ(0, -dy, 0),
                    new XYZ(dx, dy, 0),
                    new XYZ(-dx, dy, 0)
                };

                    foreach (XYZ offset in offsets)
                    {
                        XYZ tagPosition = hostPoint + offset;

                        Outline outline = GetTextOutline(tagPosition, textWidthMm, textHeightMm, view.Scale);

                        // Визуализируем рамку
                        VisualizeOutline(doc, outline, view);

                        if (!IsTextOverlapping(doc, outline, view))
                        {
                            if (!annotationSymbol.IsActive)
                                annotationSymbol.Activate();

                            FamilyInstance instance = doc.Create.NewFamilyInstance(tagPosition, annotationSymbol, view);

                            // Присваиваем текст, если в семействе есть параметр "Текст"
                            Parameter param = instance.LookupParameter("Текст");
                            if (param != null && !param.IsReadOnly)
                                param.Set(text);

                            tx.Commit();
                            return;
                        }
                    }
                }

                tx.RollBack();
            }
        }

        public static double GetCharacterWidth(string text)
        {
            Font font = new Font("ISOCPEUR", 3.5f, FontStyle.Regular);
            Size textSize = TextRenderer.MeasureText(text, font);
            return textSize.Width * 0.8; // мм
        }

        public static Outline GetTextOutline(XYZ basePoint, double widthMm, double heightMm, int viewScale)
        {
            double mmToFt = 1 / 304.8;
            double scaleFactor = mmToFt * viewScale;

            double halfWidth = (widthMm / 2) * scaleFactor;
            double halfHeight = (heightMm / 2) * scaleFactor;

            XYZ min = new XYZ(basePoint.X - halfWidth, basePoint.Y - halfHeight, basePoint.Z);
            XYZ max = new XYZ(basePoint.X + halfWidth, basePoint.Y + halfHeight, basePoint.Z + 0.1);

            return new Outline(min, max);
        }

        public static bool IsTextOverlapping(Document doc, Outline outline, View view)
        {
            BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);
            FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id)
            .WherePasses(filter)
            .WhereElementIsNotElementType();

            foreach (Element e in collector)
            {
                if (e.Category != null && e.Category.CategoryType == CategoryType.Model)
                    return true;
            }

            return false;
        }

        public static void VisualizeOutline(Document doc, Outline outline, View view)
        {
            XYZ p1 = new XYZ(outline.MinimumPoint.X, outline.MinimumPoint.Y, outline.MinimumPoint.Z);
            XYZ p2 = new XYZ(outline.MaximumPoint.X, outline.MinimumPoint.Y, outline.MinimumPoint.Z);
            XYZ p3 = new XYZ(outline.MaximumPoint.X, outline.MaximumPoint.Y, outline.MinimumPoint.Z);
            XYZ p4 = new XYZ(outline.MinimumPoint.X, outline.MaximumPoint.Y, outline.MinimumPoint.Z);

            CreateLine(doc, view, p1, p2);
            CreateLine(doc, view, p2, p3);
            CreateLine(doc, view, p3, p4);
            CreateLine(doc, view, p4, p1);
        }

        public static void CreateLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            Line line = Line.CreateBound(p1, p2);
            doc.Create.NewDetailCurve(view, line);
        }
    }


}

