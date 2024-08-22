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
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;
            if (activeView.ViewType != ViewType.Section)
            {
                MessageBox.Show("Не выбран активный вид стен!\n");
                return Result.Failed;
            }//check if activeView is setion view

            List<FamilySymbol> symbolBrakeLine =  new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .ToElements()
                .Where(x=> x.Name == FamNameBrakeLine)
                .Cast<FamilySymbol>()
                .ToList(); //get family symbol of brake line

            List<Floor> floorList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Floor))
                .ToElements()
                .Cast<Floor>()
                .ToList(); //get all floors on activeView

            List<Wall> wallList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Wall))
                .ToElements()
                .Cast<Wall>()
                .ToList();  //get all walls on activeView

            IList<Line> gridLinesList = new List<Line>();//to create dimensions

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
                .First(); //get min and max points of walls in active view

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
                .First(); //get min and max points of floors in active view
            var viewScale = activeView.Scale;

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

                        IList<Wall> wallsFromLeftToRight = wallList
                            .Where(w => w.get_BoundingBox(activeView).Max.Z < maxPtZFloor)                           
                            .ToList();//take only walls that under the floor

                        if (activeView.RightDirection.X == 1)
                        {
                            XYZ minPtCropBox = new XYZ(minPtXWall - RevitAPI.ToFoot(800), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(800));
                            XYZ maxPtCropBox = new XYZ(maxPtXWall + RevitAPI.ToFoot(800), activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(800));
                            XYZ minUpPtCropBox = new XYZ(minPtCropBox.X, activeView.Origin.Y, maxPtCropBox.Z);
                            XYZ maxDnPtCropBox = new XYZ(maxPtCropBox.X, activeView.Origin.Y, minPtCropBox.Z);

                            Curve curve1 = Line.CreateBound(minPtCropBox, minUpPtCropBox),
                                curve2 = Line.CreateBound(minUpPtCropBox, maxPtCropBox),
                                curve3 = Line.CreateBound(maxPtCropBox, maxDnPtCropBox),
                                curve4 = Line.CreateBound(maxDnPtCropBox, minPtCropBox);

                            IList<Curve> curves = new List<Curve>()
                            {curve1, curve2, curve3, curve4 };                          
                            cropBoxBounds = CurveLoop.Create(curves);
                           // cropRegion.SetCropShape(cropBoxBounds);                           
                            IList<Wall> wallsSTMFromLeftToRight = wallsFromLeftToRight
                                .Where(w => w.LookupParameter("• Тип элемента").AsString().Contains("Стм"))
                                .OrderBy(w => w.get_BoundingBox(activeView).Min.X)
                                .ToList();// order STM walls from min to max                                                                   
                            int i = 0;
                            int cropRegionCount = 0;
                            double cropBoxLength = Math.Abs(activeView.CropBox.Min.X) + Math.Abs(activeView.CropBox.Max.X);
                            while (i < wallsSTMFromLeftToRight.Count - 1)
                            {
                                var first = wallsSTMFromLeftToRight.ElementAt(i).get_BoundingBox(activeView).Max.X;  
                                var second = wallsSTMFromLeftToRight.ElementAt(++i).get_BoundingBox(activeView).Min.X;                              
                                if (Math.Abs(second) - Math.Abs(first) > RevitAPI.ToFoot(3000)) //length between two STM more than 3000 mm
                                {            
                                   cropRegion.SplitRegionHorizontally(cropRegionCount, second / cropBoxLength, first / cropBoxLength);
                                   cropRegionCount++;
                                }                               
                            }
                        }//set cropbox
                        else if (activeView.RightDirection.X == -1)
                        {
                            XYZ minPtCropBox = new XYZ(minPtXWall - RevitAPI.ToFoot(800), activeView.Origin.Y, minPtZWall - RevitAPI.ToFoot(800));
                            XYZ maxPtCropBox = new XYZ(maxPtXWall + RevitAPI.ToFoot(800), activeView.Origin.Y, maxPtZFloor + RevitAPI.ToFoot(800));
                            XYZ minUpPtCropBox = new XYZ(minPtCropBox.X, activeView.Origin.Y, maxPtCropBox.Z);
                            XYZ maxDnPtCropBox = new XYZ(maxPtCropBox.X, activeView.Origin.Y, minPtCropBox.Z);

                            Curve curve1 = Line.CreateBound(minPtCropBox, minUpPtCropBox),
                                curve2 = Line.CreateBound(minUpPtCropBox, maxPtCropBox),
                                curve3 = Line.CreateBound(maxPtCropBox, maxDnPtCropBox),
                                curve4 = Line.CreateBound(maxDnPtCropBox, minPtCropBox);

                            IList<Curve> curves = new List<Curve>()
                            {curve1, curve2, curve3, curve4 };
                            cropBoxBounds = CurveLoop.Create(curves);
                            // cropRegion.SetCropShape(cropBoxBounds);                           
                            IList<Wall> wallsSTMFromLeftToRight = wallsFromLeftToRight
                                .Where(w => w.LookupParameter("• Тип элемента").AsString().Contains("Стм"))
                                .OrderBy(w => w.get_BoundingBox(activeView).Min.X)
                                .ToList();// order STM walls from min to max                                                                   
                            int i = 0;
                            int cropRegionCount = 0;
                            double cropBoxLength = Math.Abs(activeView.CropBox.Min.X) + Math.Abs(activeView.CropBox.Max.X);
                            while (i < wallsSTMFromLeftToRight.Count - 1)
                            {
                                var first = wallsSTMFromLeftToRight.ElementAt(i).get_BoundingBox(activeView).Max.X;
                                var second = wallsSTMFromLeftToRight.ElementAt(++i).get_BoundingBox(activeView).Min.X;
                                if (Math.Abs(second) - Math.Abs(first) > RevitAPI.ToFoot(3000)) //length between two STM more than 3000 mm
                                {
                                    cropRegion.SplitRegionHorizontally(cropRegionCount, second / cropBoxLength, 0.8);
                                   cropRegionCount++;
                                }
                            }

                        }
                        else if (activeView.RightDirection.Y == -1)
                        {
                            XYZ minPtCropBox = new XYZ(activeView.Origin.X, minPtYWall - RevitAPI.ToFoot(800), minPtZWall - RevitAPI.ToFoot(800));
                            XYZ maxPtCropBox = new XYZ(activeView.Origin.X, maxPtYWall + RevitAPI.ToFoot(800), maxPtZFloor + RevitAPI.ToFoot(800));
                            XYZ minUpPtCropBox = new XYZ(activeView.Origin.X, minPtCropBox.Y,  maxPtCropBox.Z);
                            XYZ maxDnPtCropBox = new XYZ(activeView.Origin.X, maxPtCropBox.Y,  minPtCropBox.Z);

                            Curve curve1 = Line.CreateBound(minPtCropBox, minUpPtCropBox);
                            Curve curve2 = Line.CreateBound(minUpPtCropBox, maxPtCropBox);
                            Curve curve3 = Line.CreateBound(maxPtCropBox, maxDnPtCropBox);
                            Curve curve4 = Line.CreateBound(maxDnPtCropBox, minPtCropBox);
                            IList<Curve> curves = new List<Curve>()
                            {curve1, curve2, curve3, curve4 };
                            cropBoxBounds = CurveLoop.Create(curves);
                            //cropRegion.SetCropShape(cropBoxBounds);                        
                            IList<Wall> wallsSTMFromLeftToRight = wallsFromLeftToRight
                                .Where(w => w.LookupParameter("• Тип элемента").AsString().Contains("Стм"))
                                .OrderBy(w => w.get_BoundingBox(activeView).Min.Y)
                                .ToList();// order STM walls from min to max
                            int i = 0;
                            int cropRegionCount = 0;
                            double cropBoxLength = Math.Abs(activeView.CropBox.Min.Y) + Math.Abs(activeView.CropBox.Max.Y);
                            while (i < wallsSTMFromLeftToRight.Count - 1)
                            {
                                var first = wallsSTMFromLeftToRight.ElementAt(i).get_BoundingBox(activeView).Max.Y;
                                var second = wallsSTMFromLeftToRight.ElementAt(++i).get_BoundingBox(activeView).Min.Y;
                                if (Math.Abs(second) - Math.Abs(first) > RevitAPI.ToFoot(3000)) //length between two STM more than 3000 mm
                                {
                                     cropRegion.SplitRegionHorizontally(cropRegionCount, 0.3, 0.6);
                                    cropRegionCount++;
                                }
                            }

                        }
                        else if (activeView.RightDirection.Y == 1)
                        {
                            XYZ minPtCropBox = new XYZ(activeView.Origin.X, minPtYWall - RevitAPI.ToFoot(800), minPtZWall - RevitAPI.ToFoot(800));
                            XYZ maxPtCropBox = new XYZ(activeView.Origin.X, maxPtYWall + RevitAPI.ToFoot(800), maxPtZFloor + RevitAPI.ToFoot(800));
                            XYZ minUpPtCropBox = new XYZ(activeView.Origin.X, minPtCropBox.Y,  maxPtCropBox.Z);
                            XYZ maxDnPtCropBox = new XYZ(activeView.Origin.X, maxPtCropBox.Y,  minPtCropBox.Z);

                            Curve curve1 = Line.CreateBound(minPtCropBox, minUpPtCropBox);
                            Curve curve2 = Line.CreateBound(minUpPtCropBox, maxPtCropBox);
                            Curve curve3 = Line.CreateBound(maxPtCropBox, maxDnPtCropBox);
                            Curve curve4 = Line.CreateBound(maxDnPtCropBox, minPtCropBox);
                            IList<Curve> curves = new List<Curve>()
                            {curve1, curve2, curve3, curve4 };
                            cropBoxBounds = CurveLoop.Create(curves);
                            //cropRegion.SetCropShape(cropBoxBounds);                        
                            IList<Wall> wallsSTMFromLeftToRight = wallsFromLeftToRight
                                .Where(w => w.LookupParameter("• Тип элемента").AsString().Contains("Стм"))
                                .OrderBy(w => w.get_BoundingBox(activeView).Min.Y)
                                .ToList();// order STM walls from min to max
                            int i = 0;
                            int cropRegionCount = 0;
                            Line cropBoxLine = activeView
                                .GetCropRegionShapeManager()
                                .GetCropShape()
                                .First()
                                .OrderByDescending(n => n.Length)
                                .First() as Line;
                            IList<double> cropBoxLineY = new List<double>()
                            {cropBoxLine.GetEndPoint(0).Y, cropBoxLine.GetEndPoint(1).Y};
                            double cropBoxLength = cropBoxLine.Length;
                            while (i < wallsSTMFromLeftToRight.Count - 1)
                            {
                                var first = wallsSTMFromLeftToRight.ElementAt(i).get_BoundingBox(activeView).Max.Y;
                                var second = wallsSTMFromLeftToRight.ElementAt(++i).get_BoundingBox(activeView).Min.Y;
                                if (Math.Abs(second) - Math.Abs(first) > RevitAPI.ToFoot(3000)) //length between two "Стм" more than 3000 mm
                                {
                                    cropRegion.SplitRegionHorizontally
                                        (cropRegionCount, 
                                        (1-(Math.Abs((cropBoxLineY.Max() - first)) - RevitAPI.ToFoot(800)) / cropBoxLength), 
                                        (1-(RevitAPI.ToFoot(800) + Math.Abs(( cropBoxLineY.Max() - second))) / cropBoxLength));
                                    cropRegionCount++;
                                    cropBoxLength = cropBoxLineY.Max() - second;
                                }
                            }
                        }//set cropbox
                        t1.Commit();
                    }
                    List<Grid> gridList =  new FilteredElementCollector(doc, activeView.Id)
                             .OfClass(typeof(Grid))
                             .ToElements()
                             .Cast<Grid>()
                             .ToList(); //get all grids on activeView here because of changing cropbox

                    using (Transaction t2 = new Transaction(doc, "Изменение осей"))
                    {
                        t2.Start();
                        //Тут пишем основной код для изменения элементов модели
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
                            XYZ endpoint1 = new XYZ(startXPtGrid,startYPtGrid, endpoint1Z);
                            XYZ endpoint2 = new XYZ(endXPtGrid, endYPtGrid, 1);
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
    }
}
