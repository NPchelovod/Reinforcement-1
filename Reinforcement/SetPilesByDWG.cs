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

    public class SetPilesByDWG : IExternalCommand
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

            FilteredElementCollector col = new FilteredElementCollector(doc);

            //Типоразмер нужной сваи
            var pile = col.OfClass(typeof(FamilySymbol))
                .Where(x => x.Name == "Бурокасательная d450 l=19000")
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            //Уровень Этаж -2
            var level = collection.OfClass(typeof(Level))
                .Where(x => x.Name == "Этаж -2")
                .Cast<Level>()
                .FirstOrDefault();

            Reference sel = uidoc.Selection.PickObject(ObjectType.Element);          
            var dwg = doc.GetElement(sel);
            if (!(dwg is ImportInstance))
            {
                MessageBox.Show("Выбрана не подложка!\n" + "Класс должен быть ImportInstance");
                return Result.Failed;
            }

            Options opt = new Options()
            {
                ComputeReferences = true,
                View = doc.ActiveView
            };

            var geom = dwg.get_Geometry(opt).First() as GeometryInstance;
            var geomList = geom.GetInstanceGeometry()
                .OfType<Point>()
                .Select(x => x.Coord)                
                .ToList();

            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    foreach (XYZ point in geomList) 
                    {
                        doc.Create.NewFamilyInstance(point, pile, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
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
    }
}
