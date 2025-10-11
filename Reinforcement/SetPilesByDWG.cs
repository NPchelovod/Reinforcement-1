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


        public HashSet<string> Piles = new HashSet<string>()
        {
            "ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
        };


        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            FilteredElementCollector collection = new FilteredElementCollector(doc);

            var Seacher = HelperSeach.GetExistFamily(Piles, commandData);
            ElementId pileId = Seacher.pileId;
            Piles = Seacher.PossibleNames;



            var pile = doc.GetElement(pileId) as FamilySymbol;

            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();

            var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();

            TransparentNotificationWindow.ShowNotification("Выберите подложку dwg", uidoc, 5);

            Reference sel = uidoc.Selection.PickObject(ObjectType.Element);          
            var dwg = doc.GetElement(sel);
            if (!(dwg is ImportInstance))
            {
                MessageBox.Show("Выбрана не подложка!\n" + "Категория должна быть ImportInstance");
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
                    if (pile != null && !pile.IsActive)
                    {
                        pile.Activate();
                        doc.Regenerate();
                    }
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
