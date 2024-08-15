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

            XYZ minPtWall, maxPtWall; //initialize wall points
            XYZ minPt = null, maxPt = null; //initialize points and start foreach
            foreach (Wall wall in wallList) //get min and max points of walls to set CropBox
            {
                if (minPt == null && maxPt == null)
                {
                    minPt = wall.get_BoundingBox(activeView).Min;
                    maxPt = wall.get_BoundingBox(activeView).Max;
                }
            }
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    var x = RevitAPI.ToMm(activeView.CropBox.Max.X);
                    BoundingBoxXYZ box = new BoundingBoxXYZ();
                    box.Max = new XYZ(32.0, -108.0, 0);
                    box.Min = new XYZ(-25.0, -122.0, -3.2);
                    activeView.CropBox = box;
                    Options opt = new Options 
                    {
                        View = activeView
                    };
                    var asd = gridList.First().get_Geometry(opt).First() as Line;

                    XYZ dd = new XYZ (asd.GetEndPoint(0).X, asd.GetEndPoint(0).Y, -20);
                    asd.GetEndPoint(0);
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
