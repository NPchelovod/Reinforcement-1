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
using System.Windows.Markup.Localizer;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class DecorWallReinfViewSection : IExternalCommand
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
            Autodesk.Revit.DB.View activeView = uidoc.ActiveView;
            if (activeView.ViewType != ViewType.Section)
            {
                MessageBox.Show("Не выбран активный вид стен!\n");
                return Result.Failed;
            }//check if activeView is setion view

            List<Grid> gridList =  new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Grid))
                .ToElements()
                .Cast<Grid>()
                .ToList(); //get all grids on activeView

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

            XYZ minPtWall = null, maxPtWall = null; //initialize wall points and start foreach
            IList<Line> gridLinesList = new List<Line>();
            var viewScale = activeView.Scale;
            foreach (Wall wall in wallList)
            {
                if (minPtWall == null && maxPtWall == null)
                {
                    minPtWall = wall.get_BoundingBox(activeView).Min;
                    maxPtWall = wall.get_BoundingBox(activeView).Max;
                }
                else if (wall.get_BoundingBox(activeView).Max.X > maxPtWall.X && wall.get_BoundingBox(activeView).Max.Y > maxPtWall.Y && wall.get_BoundingBox(activeView).Max.Z > maxPtWall.Z)
                {
                    maxPtWall = wall.get_BoundingBox(activeView).Max;
                }
                else if (wall.get_BoundingBox(activeView).Min.X < minPtWall.X && wall.get_BoundingBox(activeView).Min.Y < minPtWall.Y && wall.get_BoundingBox(activeView).Min.Z < minPtWall.Z)
                {
                    minPtWall = wall.get_BoundingBox(activeView).Min;
                }
            }//get min and max points of walls in active view
            try //ловим ошибку
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "Оформление вида стен подвала"))
                {
                    tg.Start();
                    using (Transaction t1 = new Transaction(doc, "Изменение осей"))
                    {
                        t1.Start();
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
                            var endpoint1Z = minPtWall.Z - RevitAPI.ToFoot(30*viewScale); //calcualte offset from minpt wall
                            XYZ endpoint1 = new XYZ(startXPtGrid,startYPtGrid, endpoint1Z);
                            XYZ endpoint2 = new XYZ(endXPtGrid, endYPtGrid, 1);
                            Line newGridCurve = Line.CreateBound(endpoint1, endpoint2);
                            gridLinesList.Add(newGridCurve);
                            grid.SetCurveInView(DatumExtentType.ViewSpecific, activeView, newGridCurve);
                        }
                        /*
                        var x = RevitAPI.ToMm(activeView.CropBox.Max.X);
                        BoundingBoxXYZ box = new BoundingBoxXYZ();
                        box.Max = new XYZ(32.0, -108.0, 0);
                        box.Min = new XYZ(-25.0, -122.0, -3.2);
                        activeView.CropBox = box;
                        Options opt = new Options 
                        {
                            View = activeView
                        };
                        */
                        //set cropbox
                        t1.Commit();
                    }
                    using (Transaction t2 = new Transaction(doc, "Создание размерных линий"))
                    {
                        t2.Start();
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
                        doc.Create.NewDimension(activeView,lineDim,referenceArray); //create dimension between all grids
                       
                        endpoint1 = new XYZ(grid1.GetEndPoint(0).X, grid1.GetEndPoint(0).Y, grid1.GetEndPoint(0).Z + RevitAPI.ToFoot(5 * viewScale));
                        endpoint2 = new XYZ(grid2.GetEndPoint(0).X, grid2.GetEndPoint(0).Y, grid2.GetEndPoint(0).Z + RevitAPI.ToFoot(5 * viewScale));
                        lineDim = Line.CreateBound(endpoint1,endpoint2);
                        doc.Create.NewDimension(activeView, lineDim, referenceArrayLeftRight); //create dimension between first and last grids
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
