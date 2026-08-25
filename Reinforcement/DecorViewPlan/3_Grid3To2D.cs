using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Grid3To2D : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            UIDocument uidoc = RevitAPI.UiDocument;


            View activeView = doc.ActiveView;
            int viewScale = activeView.Scale;
            ConvertGrid3To2D(doc, activeView);
            return Result.Succeeded;
        }
        public static void ConvertGrid3To2D(Document doc, View activeView)
        {
            List<Grid> gridList = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(Grid))
                .ToElements()
                .Cast<Grid>()
                .ToList(); //get all grids on activeView
            if (gridList.Count == 0)
            {

                return;
            }
            try //ловим ошибкуs
            {
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
                    }
                    t1.Commit();
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
