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
using System.Windows.Forms.VisualStyles;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class DecorViewPlan : IExternalCommand
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
            View activeView = doc.ActiveView;
            int viewScale = activeView.Scale;
            Options opt = new Options()
            {
                ComputeReferences = true,
                View = activeView
            };

            Options optFloor = new Options()
            {
                ComputeReferences = true,
                View = activeView,
                IncludeNonVisibleObjects = true
            };//options for joined walls

            List<Line> gridLinesListX = new List<Line>();
            List<Line> gridLinesListY = new List<Line>(); //to create dims

            List<Floor> floorList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Floor))
                .ToElements()
                .Cast<Floor>()
                .OrderByDescending(x => x.get_Geometry(opt).Select(n => n as Solid).First().Volume) //the biggest floor will be firsst in list
                .ToList(); //get all floors on activeView

            if (floorList.Count == 0)
            {
                MessageBox.Show("На виде должна быть плита!");
                return Result.Failed;
            }

            List<Wall> wallList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Wall))
                .ToElements()
                .Cast<Wall>()
                .Where(x => x.LevelId == activeView.GenLevel.Id && x.LookupParameter("• Тип элемента").AsString() == "Дж")
                .ToList();  //get all walls on activeView

            if (wallList.Count == 0)
            {
                MessageBox.Show("На виде должны быть стены!");
                return Result.Failed;
            }

            List<XYZ> minPtWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Min)
                .ToList();
            List<XYZ> maxPtWall = wallList
                .Select(w => w.get_BoundingBox(activeView).Max)
                .ToList();
            List<XYZ> minPtFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Min)
                .ToList();
            List<XYZ> maxPtFloor = floorList
                .Select(w => w.get_BoundingBox(activeView).Max)
                .ToList();
            List<Grid>  gridList = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Grid))
                .ToElements()
                .Cast<Grid>()
                .ToList(); //get all grids on activeView

            if (gridList.Count == 0)
            {
                MessageBox.Show("На виде должны быть оси!");
                return Result.Failed;
            }


            List<Grid> XGridList = gridList
                .Where(x => Math.Abs(x.get_Geometry(opt).Select(n => n as Line).First().Direction.X) == 1)
                .ToList();
            List<Grid> YGridList = gridList
                .Where(x => Math.Abs(x.get_Geometry(opt).Select(n => n as Line).First().Direction.Y) == 1)
                .ToList();


            try //ловим ошибкуs
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "Оформление плана"))
                {
                    tg.Start();
                    using (Transaction t1 = new Transaction(doc, "Изменение осей"))
                    {
                        t1.Start();

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
                            Line line = curve as Line;
                            XYZ directionView = activeView.ViewDirection;
                            double[] X = new double[] {curve.GetEndPoint(0).X, curve.GetEndPoint(1).X };
                            double[] Y = new double[] {curve.GetEndPoint(0).Y, curve.GetEndPoint(1).Y };
                            double[] Z = new double[] {curve.GetEndPoint(0).Z, curve.GetEndPoint(1).Z };//create arrays and then get min or max

                            double startXPtGrid = X.Min();
                            double startYPtGrid = Y.Min();
                            double startZPtGrid = Z.Min();
                            double endXPtGrid = X.Max();
                            double endYPtGrid = Y.Max();
                            double endZPtGrid = Z.Max();

                            if (Math.Abs(line.Direction.Y) == 1)
                            {
                                var endpoint1Y = minPtFloor.First().Y - RevitAPI.ToFoot(30*viewScale); //calcualte offset from floor
                                var endpoint2Y = maxPtFloor.First().Y + RevitAPI.ToFoot(7*viewScale); //calcualte offset from floor

                                XYZ endpoint1 = new XYZ(startXPtGrid, endpoint1Y, startZPtGrid);
                                XYZ endpoint2 = new XYZ(endXPtGrid, endpoint2Y, endZPtGrid);
                                Line newGridCurve = Line.CreateBound(endpoint1, endpoint2);
                                gridLinesListY.Add(newGridCurve);
                                grid.SetCurveInView(DatumExtentType.ViewSpecific, activeView, newGridCurve);//change grids line
                            }
                            else if (Math.Abs(line.Direction.X) == 1)
                            {
                                var endpoint1X = minPtFloor.First().X - RevitAPI.ToFoot(30*viewScale); //calcualte offset from floor
                                var endpoint2X = maxPtFloor.First().X + RevitAPI.ToFoot(7*viewScale); //calcualte offset from floor

                                XYZ endpoint1 = new XYZ(endpoint1X, startYPtGrid, startZPtGrid);
                                XYZ endpoint2 = new XYZ(endpoint2X, endYPtGrid, endZPtGrid);
                                Line newGridCurve = Line.CreateBound(endpoint1, endpoint2);
                                gridLinesListX.Add(newGridCurve);
                                grid.SetCurveInView(DatumExtentType.ViewSpecific, activeView, newGridCurve);//change grids line
                            }
                        }
                        t1.Commit();
                    }
                    using (Transaction t2 = new Transaction(doc, "Добавление размерных линий"))
                    {
                        t2.Start();
                        //creating dims for grids
                        var referenceArrayX = new ReferenceArray();
                        var referenceArrayY = new ReferenceArray();
                        var referenceArrayLeftRight = new ReferenceArray();
                        var referenceArrayUpDown = new ReferenceArray();
                        double[] ptXArray = new double[0], ptYArray = new double[0];
                        Reference[] referencesLineLR = new Reference[0]; //initialize this massive to get left and right grids, in array we can get max or min value
                        Reference[] referencesLineUD = new Reference[0]; //initialize this massive to get up and down grids, in array we can get max or min value

                        foreach (Grid grid in XGridList)
                        {
                            Line line = grid.get_Geometry(opt).First() as Line;
                            ptYArray = ptYArray.Append(line.GetEndPoint(0).Y).ToArray();

                            Reference refLine = line.Reference;
                            referencesLineUD = referencesLineUD.Append(refLine).ToArray();
                            referenceArrayX.Insert(refLine, referenceArrayX.Size);
                        }//create reference array from grids to create dimension

                        foreach (Grid grid in YGridList)
                        {
                            Line line = grid.get_Geometry(opt).First() as Line;
                            ptXArray = ptXArray.Append(line.GetEndPoint(0).X).ToArray();

                            Reference refLine = line.Reference;
                            referencesLineLR = referencesLineLR.Append(refLine).ToArray();
                            referenceArrayY.Insert(refLine, referenceArrayY.Size);
                        }//create reference array from grids to create dimension

                        var indexOfMaxX = Array.IndexOf(ptXArray, ptXArray.Max());
                        var indexOfMinX = Array.IndexOf(ptXArray, ptXArray.Min());
                        var indexOfMaxY = Array.IndexOf(ptYArray, ptYArray.Max());
                        var indexOfMinY = Array.IndexOf(ptYArray, ptYArray.Min());

                        referenceArrayLeftRight.Insert(referencesLineLR.ElementAt(indexOfMinX), 0);
                        referenceArrayLeftRight.Insert(referencesLineLR.ElementAt(indexOfMaxX), 1);

                        referenceArrayUpDown.Insert(referencesLineUD.ElementAt(indexOfMinY), 0);
                        referenceArrayUpDown.Insert(referencesLineUD.ElementAt(indexOfMaxY), 1);

                        var gridY1 = gridLinesListY.ElementAt(0) as Line;
                        var gridY2 = gridLinesListY.ElementAt(1) as Line;
                        XYZ endpoint1 = new XYZ(gridY1.GetEndPoint(0).X, gridY1.GetEndPoint(0).Y + RevitAPI.ToFoot(12 * viewScale), gridY1.GetEndPoint(0).Z );
                        XYZ endpoint2 = new XYZ(gridY2.GetEndPoint(0).X, gridY2.GetEndPoint(0).Y + RevitAPI.ToFoot(12 * viewScale), gridY2.GetEndPoint(0).Z );
                        Line lineDim = Line.CreateBound(endpoint1,endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayY); //create dimension between all grids

                        var gridX1 = gridLinesListX.ElementAt(0) as Line;
                        var gridX2 = gridLinesListX.ElementAt(1) as Line;
                        endpoint1 = new XYZ(gridX1.GetEndPoint(0).X + RevitAPI.ToFoot(12 * viewScale), gridX1.GetEndPoint(0).Y, gridX1.GetEndPoint(0).Z);
                        endpoint2 = new XYZ(gridX2.GetEndPoint(0).X + RevitAPI.ToFoot(12 * viewScale), gridX2.GetEndPoint(0).Y, gridX2.GetEndPoint(0).Z);
                        lineDim = Line.CreateBound(endpoint1, endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayX); //create dimension between all grids

                        endpoint1 = new XYZ(gridY1.GetEndPoint(0).X, gridY1.GetEndPoint(0).Y + RevitAPI.ToFoot(5 * viewScale), gridY1.GetEndPoint(0).Z);
                        endpoint2 = new XYZ(gridY2.GetEndPoint(0).X, gridY2.GetEndPoint(0).Y + RevitAPI.ToFoot(5 * viewScale), gridY2.GetEndPoint(0).Z);
                        lineDim = Line.CreateBound(endpoint1, endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayLeftRight); //create dimension between first and last grids
                        endpoint1 = new XYZ(gridX1.GetEndPoint(0).X + RevitAPI.ToFoot(5 * viewScale), gridX1.GetEndPoint(0).Y, gridX1.GetEndPoint(0).Z);
                        endpoint2 = new XYZ(gridX2.GetEndPoint(0).X + RevitAPI.ToFoot(5 * viewScale), gridX2.GetEndPoint(0).Y, gridX2.GetEndPoint(0).Z);
                        lineDim = Line.CreateBound(endpoint1, endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayUpDown); //create dimension between first and last grids





                        //creating dims for walls
                        //List<Wall> wallListDiafragm = wallList.Where(x => x.LookupParameter("• Тип элемента").AsString() == "Дж").ToList();

                        foreach (var wall in wallList)
                        {
                            List<Dimension> dimensions = new List<Dimension>();//create list of dimensions





                            //creating dims X direction
                            EdgeArray edges = wall.get_Geometry(optFloor).OfType<Solid>().Last().Edges; //get wall edges
                            List<Line> edgeLinesY = new List<Line>();
                            List<Edge> wallEdgesY = new List<Edge>();
                            ReferenceArray referenceArray = new ReferenceArray();
                            Line edgeLineX = null;
                            foreach (Edge edge in edges)
                            {
                                Line edgeLine = edge.AsCurve() as Line;
                                var directionY = Math.Abs(edgeLine.Direction.Y);
                                var directionX = Math.Abs(edgeLine.Direction.X);
                                if (edge.Reference.ElementReferenceType == ElementReferenceType.REFERENCE_TYPE_CUT_EDGE && directionY == 1)
                                {
                                    edgeLinesY.Add(edgeLine);
                                    wallEdgesY.Add(edge);
                                    string stableReference = edge.Reference.ConvertToStableRepresentation(doc);
                                    int lastIndex = stableReference.LastIndexOf(':');
                                    stableReference = stableReference.Substring(0, ++lastIndex) + "SURFACE";
                                    var newStableReference = Reference.ParseFromStableRepresentation(doc, stableReference);
                                    referenceArray.Insert(newStableReference, referenceArray.Size);
                                }
                                else if (edge.Reference.ElementReferenceType == ElementReferenceType.REFERENCE_TYPE_CUT_EDGE && directionX == 1)
                                {
                                    edgeLineX = edgeLine;
                                }
                            }
                            if (referenceArray.Size == 0)
                            {
                                continue;
                            }
                            var check = wall.Id.Value;
                            bool isAllDisjoint = YGridList.TrueForAll(x => x.get_Geometry(opt).OfType<Line>().First().Intersect(edgeLineX, out var res) == SetComparisonResult.Disjoint);
                            if (isAllDisjoint)
                            {
                                var wallLocation = wall.Location as LocationCurve;
                                var wallLocationX = wallLocation.Curve.GetEndPoint(0).X;
                                var nearestGrid = YGridList
                                    .OrderBy(x => Math.Abs(x.Curve.GetEndPoint(0).X - wallLocationX))
                                    .First();
                                int position = nearestGrid.Curve.GetEndPoint(0).X < wallLocationX ? 0 : 1;
                                referenceArray.Insert(new Reference(nearestGrid), position);
                            }
                            else
                            {
                                foreach (Grid grid in YGridList)
                                {
                                    Line gridCurve = grid.get_Geometry(opt).OfType<Line>().First();
                                    var intersectX = gridCurve.Intersect(edgeLineX, out var res);
                                    int i = 0;
                                    if (intersectX == SetComparisonResult.Overlap && !edgeLinesY.Any(x => gridCurve.Intersect(x) == SetComparisonResult.Equal))
                                    {
                                        referenceArray.Insert(new Reference(grid), referenceArray.Size / 2);
                                    }
                                    else if (intersectX == SetComparisonResult.Overlap)
                                    {
                                        foreach (Line edge in edgeLinesY)
                                        {
                                            if (gridCurve.Intersect(edge) == SetComparisonResult.Equal)
                                            {
                                                referenceArray.set_Item(i, new Reference(grid));
                                                break;
                                            }
                                            i++;
                                        }
                                    }
                                }//check for intersection between walls and grids, and find nearest grid to wall
                            }

                            endpoint1 = new XYZ(wall.get_BoundingBox(activeView).Min.X, wall.get_BoundingBox(activeView).Min.Y - RevitAPI.ToFoot(7 * viewScale), edgeLinesY.First().Origin.Z);
                            endpoint2 = new XYZ(wall.get_BoundingBox(activeView).Max.X, wall.get_BoundingBox(activeView).Min.Y - RevitAPI.ToFoot(7 * viewScale), edgeLinesY.First().Origin.Z);
                            lineDim = Line.CreateBound(endpoint1, endpoint2);
                            referenceArray.ReverseIterator();
                            var dimension = doc.Create.NewDimension(activeView, lineDim, referenceArray); //create dimension
                            dimensions.Add(dimension);
                            





                            //creating dims Y direction
                            List<Line> edgeLinesX = new List<Line>();
                            List<Edge> wallEdgesX = new List<Edge>();
                            referenceArray = new ReferenceArray();
                            Line edgeLineY = null;
                            foreach (Edge edge in edges)
                            {
                                Line edgeLine = edge.AsCurve() as Line;
                                var directionY = Math.Abs(edgeLine.Direction.Y);
                                var directionX = Math.Abs(edgeLine.Direction.X);
                                if (edge.Reference.ElementReferenceType == ElementReferenceType.REFERENCE_TYPE_CUT_EDGE && directionX == 1)
                                {
                                    edgeLinesX.Add(edgeLine);
                                    wallEdgesX.Add(edge);
                                    string stableReference = edge.Reference.ConvertToStableRepresentation(doc);
                                    int lastIndex = stableReference.LastIndexOf(':');
                                    stableReference = stableReference.Substring(0, ++lastIndex) + "SURFACE";
                                    var newStableReference = Reference.ParseFromStableRepresentation(doc, stableReference);
                                    referenceArray.Insert(newStableReference, referenceArray.Size);
                                }
                                else if (edge.Reference.ElementReferenceType == ElementReferenceType.REFERENCE_TYPE_CUT_EDGE && directionY == 1)
                                {
                                    edgeLineY = edgeLine;
                                }
                            }
                            if (referenceArray.Size == 0)
                            {
                                continue;
                            }

                            isAllDisjoint = XGridList.TrueForAll(x => x.get_Geometry(opt).OfType<Line>().First().Intersect(edgeLineY, out var res) == SetComparisonResult.Disjoint);
                            if (isAllDisjoint)
                            {
                                var wallLocation = wall.Location as LocationCurve;
                                var wallLocationY = wallLocation.Curve.GetEndPoint(0).Y;
                                var nearestGrid = XGridList
                                    .OrderBy(x => Math.Abs(x.Curve.GetEndPoint(0).Y - wallLocationY))
                                    .First();
                                int position = nearestGrid.Curve.GetEndPoint(0).Y < wallLocationY ? 0 : 1;
                                referenceArray.Insert(new Reference(nearestGrid), position);
                            }
                            else
                            {
                                foreach (Grid grid in XGridList)
                                {
                                    Line gridCurve = grid.get_Geometry(opt).OfType<Line>().First();
                                    var intersectY = gridCurve.Intersect(edgeLineY, out var res);
                                    int i = 0;
                                    if (intersectY == SetComparisonResult.Overlap && !edgeLinesX.Any(x => gridCurve.Intersect(x) == SetComparisonResult.Equal))
                                    {
                                        referenceArray.Insert(new Reference(grid), referenceArray.Size / 2);
                                    }
                                    else if (intersectY == SetComparisonResult.Overlap)
                                    {
                                        foreach (Line edge in edgeLinesX)
                                        {
                                            if (gridCurve.Intersect(edge) == SetComparisonResult.Equal)
                                            {
                                                referenceArray.set_Item(i, new Reference(grid));
                                                break;
                                            }
                                            i++;
                                        }
                                    }
                                }//check for intersection between walls and grids, and find nearest grid to wall
                            }
                            endpoint1 = new XYZ(wall.get_BoundingBox(activeView).Min.X - RevitAPI.ToFoot(7 * viewScale), wall.get_BoundingBox(activeView).Min.Y, edgeLinesX.First().Origin.Z);
                            endpoint2 = new XYZ(wall.get_BoundingBox(activeView).Min.X - RevitAPI.ToFoot(7 * viewScale), wall.get_BoundingBox(activeView).Max.Y, edgeLinesX.First().Origin.Z);
                            lineDim = Line.CreateBound(endpoint1, endpoint2);
                            dimension = doc.Create.NewDimension(activeView, lineDim, referenceArray); //create dimension
                            dimensions.Add(dimension);
                            
                            foreach (Dimension dim in dimensions)
                            {
                                MoveTextInDimension.Move(dim, viewScale, activeView);
                            }
                            
                            dimensions.Clear();
                        }
                        t2.Commit();
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
