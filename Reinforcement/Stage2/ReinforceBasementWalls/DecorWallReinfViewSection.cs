using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Markup.Localizer;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class DecorWallReinfViewSection : IExternalCommand
    {
        //Family name of brake line
        public static string FamNameBrakeLine { get; } = "Линейный обрыв";

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
            Selection sel = uidoc.Selection;
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;

            //check if activeView is section view
            if (activeView.ViewType != ViewType.Section)
            {
                MessageBox.Show("Не выбран активный вид стен!\n");
                return Result.Failed;
            }

            //create list to collect grids after cropBox change
            List<Grid> gridList = new FilteredElementCollector(doc, activeView.Id)
                        .OfClass(typeof(Grid))
                        .ToElements()
                        .Cast<Grid>()
                        .ToList(); //get all grids on activeView here because of changing cropbox
            if (gridList.Count <= 1)
            {
                MessageBox.Show("На виде должно быть как минимум 2 оси!");
                return Result.Failed;
            }
            //to create dimensions
            IList<Line> gridLinesList = new List<Line>();

            //get family symbol of brake line
            List<FamilySymbol> symbolBrakeLine =  new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .ToElements()
                .Where(x=> x.Name == FamNameBrakeLine)
                .Cast<FamilySymbol>()
                .ToList();

            //get all floors on activeView
            List<Floor> floorList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Floor))
                .ToElements()
                .Cast<Floor>()
                .ToList();
            if (floorList.Count == 0)
            {
                MessageBox.Show("На виде должна быть плита перекрытия выше выбранных стен Стм");
                return Result.Failed;
            }

            //get all walls on activeView           
             List<Wall> wallList =  new FilteredElementCollector(doc, activeView.Id)
                 .OfClass(typeof(Wall))
                 .ToElements()
                 .Cast<Wall>()
                 .Where(x => x.LookupParameter("• Тип элемента").AsString() == "Стм" || x.LookupParameter("• Тип элемента").AsString() == "Дж")
                 .ToList();
           /* ISelectionFilter selFilter = new MassSelectionFilterTypeName();
            List<Wall> wallList = sel.PickElementsByRectangle(selFilter,"Выделите все стены")
                .Select(x => x as Wall)
                .ToList();*/
            if (wallList.Count == 0)
            {
                MessageBox.Show("Не найдено ни одной стены!");
                return Result.Failed;
            }
            else if (!wallList.Any(x => x.LookupParameter("• Тип элемента").AsString() == "Стм"))
            {
                MessageBox.Show("Не найдено ни одной стены Стм!");
                return Result.Failed;
            }

            //get min and max points of walls in active view
            double minPtXWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Min.X)
                .OrderBy(w => w)
                .First(),
                   minPtYWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Min.Y)
                .OrderBy(w => w)
                .First(),
                   minPtZWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Min.Z)
                .OrderBy(w => w)
                .First(),

                   maxPtXWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Max.X)
                .OrderByDescending(w => w)
                .First(),
                   maxPtYWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Max.Y)
                .OrderByDescending(w => w)
                .First(),
                   maxPtZWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Max.Z)
                .OrderByDescending(w => w)
                .First();

            //get min and max points of floors in active view
            double minPtXFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Min.X)
                .OrderBy(w => w)
                .First(),
                   minPtYFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Min.Y)
                .OrderBy(w => w)
                .First(),
                   minPtZFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Min.Z)
                .OrderBy(w => w)
                .First(),

                   maxPtXFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Max.X)
                .OrderByDescending(w => w)
                .First(),
                   maxPtYFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Max.Y)
                .OrderByDescending(w => w)
                .First(),
                   maxPtZFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Max.Z)
                .OrderByDescending(w => w)
                .First();

            var viewScale = activeView.Scale;

            //Get active view transform
            Transform viewTransform = activeView.CropBox.Transform;

            try //ловим ошибку
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "Оформление вида стен подвала"))
                {
                    tg.Start();
                    using (Transaction t1 = new Transaction(doc, "Изменение обрезки вида стен"))
                    {
                        t1.Start();
                        ViewCropRegionShapeManager cropRegion = activeView.GetCropRegionShapeManager();

                        CurveLoop cropBoxBounds = new CurveLoop();

                        Dictionary<Wall,XYZ> wallTransform = new Dictionary<Wall, XYZ>();

                        //Transfrom Wall coordinates to View
                        foreach (Wall wall in wallList)
                        {
                            XYZ max = wall.get_BoundingBox(activeView).Max;
                            XYZ min = wall.get_BoundingBox(activeView).Min;
                            XYZ centr = (max + min) / 2;
                            wallTransform.Add(wall, viewTransform.Inverse.OfPoint(centr));
                        }

                        //Get wall from left to right
                        List<Wall> wallsFromRightToLeft = wallTransform.OrderByDescending(x => x.Value.X).Select(x => x.Key).ToList();

                        //Get "Стм" walls
                        List<Wall> wallsSTMFromRightToLeft = wallsFromRightToLeft
                            .Where(w => w.LookupParameter("• Тип элемента").AsString().Contains("Стм"))
                            .ToList();
                        //Get X cropBox line
                        Line cropBoxLine = activeView
                                .GetCropRegionShapeManager()
                                .GetCropShape()
                                .First()
                                .Select(x => x.CreateTransformed(viewTransform.Inverse) as Line)
                                .Where(x => x.Direction.X == 1)
                                .First();
                        double cropBoxLength = cropBoxLine.Length;
                        XYZ startpoint = cropBoxLine.GetEndPoint(0);
                        XYZ endpoint = cropBoxLine.GetEndPoint(1);
                        var cropBoxLineMinPtX = startpoint.X < endpoint.X ? startpoint.X : endpoint.X;

                        int i = 0;
                        while (i < wallsSTMFromRightToLeft.Count - 1)
                        {
                            var last = viewTransform.Inverse.OfPoint(wallsSTMFromRightToLeft[i].get_BoundingBox(activeView).Min).X;
                            var preLast = viewTransform.Inverse.OfPoint(wallsSTMFromRightToLeft[++i].get_BoundingBox(activeView).Max).X;
                            var cropBoxOffset = RevitAPI.ToFoot(800);
                            
                            if (Math.Abs(last - preLast) > RevitAPI.ToFoot(3000)) //length between two "Стм" more than 3000 mm
                            {
                                cropRegion.SplitRegionHorizontally
                                    (0,
                                    (Math.Abs(preLast - startpoint.X)  + cropBoxOffset) / cropBoxLength,
                                    (Math.Abs(last - startpoint.X)) / cropBoxLength);
                                cropBoxLength = (Math.Abs(last)  + cropBoxOffset);
                            }
                        }
                        t1.Commit();
                    }
                    using (Transaction t2 = new Transaction(doc, "Изменение осей"))
                    {
                        t2.Start();
                        //Тут пишем основной код для изменения элементов модели
                        gridList = new FilteredElementCollector(doc, activeView.Id)
                        .OfClass(typeof(Grid))
                        .ToElements()
                        .Cast<Grid>()
                        .ToList(); //get all grids on activeView here because of changing cropbox
                        foreach (Grid grid in gridList)
                        {
                            //check grids if they are 3D set to 2D
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End0, activeView) == DatumExtentType.Model)
                            {
                                grid.SetDatumExtentType(DatumEnds.End0, activeView, DatumExtentType.ViewSpecific);
                            }
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End1, activeView) == DatumExtentType.Model)
                            {
                                grid.SetDatumExtentType(DatumEnds.End1, activeView, DatumExtentType.ViewSpecific);
                            }
                            IList<Curve> curveList = grid.GetCurvesInView(DatumExtentType.ViewSpecific, activeView);
                            Curve curve = curveList.First();
                            XYZ directionView = activeView.ViewDirection;
                            double startXPtGrid = curve.GetEndPoint(0).X;
                            double startYPtGrid = curve.GetEndPoint(0).Y;
                            double endXPtGrid = curve.GetEndPoint(1).X;
                            double endYPtGrid = curve.GetEndPoint(1).Y;
                            var endpoint1Z = minPtZWall - RevitAPI.ToFoot(30*viewScale); //calcualte offset from minpt wall
                            var endpoint2Z = maxPtZWall + RevitAPI.ToFoot(10*viewScale); //calcualte offset from maxpt wall
                            XYZ endpoint1 = new XYZ(startXPtGrid,startYPtGrid, endpoint1Z);
                            XYZ endpoint2 = new XYZ(endXPtGrid, endYPtGrid, endpoint2Z);
                            Line newGridCurve = Line.CreateBound(endpoint1, endpoint2);
                            gridLinesList.Add(newGridCurve);
                            grid.SetCurveInView(DatumExtentType.ViewSpecific, activeView, newGridCurve);
                        }
                        t2.Commit();
                    }
                    using (Transaction t3 = new Transaction(doc, "Создание размерных линий"))
                    {
                        t3.Start();
                        var referenceArray = new ReferenceArray();
                        var referenceArrayLeftRight = new ReferenceArray();
                        Options opt = new Options()
                        {
                            ComputeReferences = true,
                            View = activeView
                        };

                        //sort grids from left to right
                        List<Grid> gridListFromLeftToRight = gridList
                            .OrderBy(x =>
                            {
                                var line = x.get_Geometry(opt).First() as Line;
                                var point = viewTransform.Inverse.OfPoint(line.GetEndPoint(0));
                                return point.X;
                            })
                            .ToList();

                        //get reference from first and last grid
                        foreach (Grid grid in gridList)
                        {
                            Line line = grid.get_Geometry(opt).First() as Line;

                        }
















                        /*
                        var referenceArray = new ReferenceArray();
                        var referenceArrayLeftRight = new ReferenceArray();
                        Options opt = new Options()
                        {
                            ComputeReferences = true,
                            View = activeView
                        };
                        double[] ptXArray = new double[0], ptYArray = new double[0];
                        Reference[] referencesLine = new Reference[0]; //initialize this massive to get left and right grids

                        foreach (Grid grid in gridList)
                        {
                            Line line = grid.get_Geometry(opt).First() as Line;
                            ptXArray = ptXArray.Append(line.GetEndPoint(0).X).ToArray();
                            ptYArray = ptYArray.Append(line.GetEndPoint(0).Y).ToArray();
                            referencesLine = referencesLine.Append(line.Reference).ToArray();

                            Reference refLine = line.Reference;
                            referenceArray.Insert(refLine, referenceArray.Size);
                        }//create reference array from grids to create dimension

                        if (activeView.RightDirection.X == 1 || activeView.RightDirection.X == -1)
                        {
                            var indexOfMax = Array.IndexOf(ptXArray, ptXArray.Max());
                            var indexOfMin = Array.IndexOf(ptXArray, ptXArray.Min());
                            referenceArrayLeftRight.Insert(referencesLine.ElementAt(indexOfMin), 0);
                            referenceArrayLeftRight.Insert(referencesLine.ElementAt(indexOfMax), 1);
                        }
                        else if (activeView.RightDirection.Y == 1 || activeView.RightDirection.Y == -1)
                        {
                            var indexOfMax = Array.IndexOf(ptYArray, ptYArray.Max());
                            var indexOfMin = Array.IndexOf(ptYArray, ptYArray.Min());
                            referenceArrayLeftRight.Insert(referencesLine.ElementAt(indexOfMin), 0);
                            referenceArrayLeftRight.Insert(referencesLine.ElementAt(indexOfMax), 1);

                        }//add to referenceArrayLeftRight grids
                        var grid1 = gridLinesList.ElementAt(0) as Line;
                        var grid2 = gridLinesList.ElementAt(1) as Line;

                        XYZ endpoint1 = new XYZ(grid1.GetEndPoint(0).X, grid1.GetEndPoint(0).Y, grid1.GetEndPoint(0).Z + RevitAPI.ToFoot(12 * viewScale));
                        XYZ endpoint2 = new XYZ(grid2.GetEndPoint(0).X, grid2.GetEndPoint(0).Y, grid2.GetEndPoint(0).Z + RevitAPI.ToFoot(12 * viewScale));
                        Line lineDim = Line.CreateBound(endpoint1,endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArray); //create dimension between all grids

                        endpoint1 = new XYZ(grid1.GetEndPoint(0).X, grid1.GetEndPoint(0).Y, grid1.GetEndPoint(0).Z + RevitAPI.ToFoot(5 * viewScale));
                        endpoint2 = new XYZ(grid2.GetEndPoint(0).X, grid2.GetEndPoint(0).Y, grid2.GetEndPoint(0).Z + RevitAPI.ToFoot(5 * viewScale));
                        lineDim = Line.CreateBound(endpoint1, endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayLeftRight); //create dimension between first and last grids
                        */
                        t3.Commit();
                    }
                    using (Transaction t4 = new Transaction(doc, "Создание линий разрыва"))
                    {
                        t4.Start();
                        XYZ botEndPoint1 = new XYZ(),
                            botEndPoint2 = new XYZ(),
                            topEndPoint1 = new XYZ(),
                            topEndPoint2 = new XYZ();
                        if (activeView.RightDirection.X == 1)
                        {
                            botEndPoint1 = new XYZ(minPtXWall - RevitAPI.ToFoot(6 * viewScale), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                            botEndPoint2 = new XYZ(maxPtXWall + RevitAPI.ToFoot(6 * viewScale), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                        }
                        else if (activeView.RightDirection.X == -1)
                        {
                            botEndPoint1 = new XYZ(maxPtXWall + RevitAPI.ToFoot(6 * viewScale), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                            botEndPoint2 = new XYZ(minPtXWall - RevitAPI.ToFoot(6 * viewScale), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                        }
                        else if (activeView.RightDirection.Y == -1)
                        {
                            botEndPoint1 = new XYZ(activeView.Origin.X, maxPtYWall + RevitAPI.ToFoot(6 * viewScale), minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                            botEndPoint2 = new XYZ(activeView.Origin.X, minPtYWall - RevitAPI.ToFoot(6 * viewScale), minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                        }
                        else if (activeView.RightDirection.Y == 1)
                        {
                            botEndPoint1 = new XYZ(activeView.Origin.X, minPtYWall - RevitAPI.ToFoot(6 * viewScale), minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                            botEndPoint2 = new XYZ(activeView.Origin.X, maxPtYWall + RevitAPI.ToFoot(6 * viewScale), minPtZWall - RevitAPI.ToFoot(6 * viewScale));
                        }//get points to create line
                        IList<BoundingBoxXYZ> listWallsBoundingBoxes = wallList
                            .Select(x=> x.get_BoundingBox(activeView))
                            .Where(bb => bb.Max.Z > maxPtZFloor)
                            .ToList(); //create list of bounding boxes of walls higher than floor
                        if (listWallsBoundingBoxes.Count > 0)
                        {
                            foreach (BoundingBoxXYZ box in listWallsBoundingBoxes)
                            {
                                if (activeView.RightDirection.X == 1)
                                {
                                    topEndPoint1 = new XYZ(box.Max.X, activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                    topEndPoint2 = new XYZ(box.Min.X, activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                }
                                else if (activeView.RightDirection.X == -1)
                                {
                                    topEndPoint1 = new XYZ(box.Min.X, activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                    topEndPoint2 = new XYZ(box.Max.X, activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                }
                                else if (activeView.RightDirection.Y == -1)
                                {
                                    topEndPoint1 = new XYZ(activeView.Origin.X, box.Min.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                    topEndPoint2 = new XYZ(activeView.Origin.X, box.Max.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                }
                                else if (activeView.RightDirection.Y == 1)
                                {
                                    topEndPoint1 = new XYZ(activeView.Origin.X, box.Max.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                    topEndPoint2 = new XYZ(activeView.Origin.X, box.Min.Y, maxPtZFloor + RevitAPI.ToFoot(8 * viewScale));
                                }//get points to create line
                                Line topLine = Line.CreateBound(topEndPoint1, topEndPoint2);
                                doc.Create.NewFamilyInstance(topLine, symbolBrakeLine.First(), activeView);
                            }//create brake lines on top
                        }
                        Line bottomLine = Line.CreateBound(botEndPoint1, botEndPoint2);
                        doc.Create.NewFamilyInstance(bottomLine, symbolBrakeLine.First(), activeView);

                        var addVector = new XYZ(0,0,RevitAPI.ToFoot(6 * viewScale));
                        Line leftLine = Line.CreateBound(botEndPoint1.Add(addVector),botEndPoint1);
                        doc.Create.NewFamilyInstance(leftLine, symbolBrakeLine.First(), activeView);
                        Line rightLine = Line.CreateBound(botEndPoint2,botEndPoint2.Add(addVector));
                        doc.Create.NewFamilyInstance(rightLine, symbolBrakeLine.First(), activeView);


                        t4.Commit();
                    }
                    tg.Assimilate();
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
        public class MassSelectionFilterTypeName : ISelectionFilter
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
