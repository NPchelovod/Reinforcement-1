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

        //имена типоразмеров семейства

        public HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "С140.30-С", "Буронабивная d"
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
            FamilySymbol pile = Seacher.pile as FamilySymbol;
            Piles = Seacher.PossibleNamesFamilySymbol;

            if (pile == null)
            {
                MessageBox.Show("Семейство не загружено");
                return Result.Failed;
            }

            

            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();

            var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();

            TransparentNotificationWindow.ShowNotification("Выберите подложку dwg", uidoc, 5);
            int iter = -1;
            int iter2 = -1;
            Reference sel=null;
            Element dwg = null;

            while (iter2 < 2)
            {
                iter2++;
                while (iter < 7)
                {
                    try //ловим ошибку
                    {
                        sel = uidoc.Selection.PickObject(ObjectType.Element);
                        dwg = doc.GetElement(sel);
                    }
                    catch
                    {
                        sel = null;
                    }
                    iter++;
                    if (sel == null || !(dwg is ImportInstance))
                    {

                        DialogResult result = MessageBox.Show(
                        "Выбрана не подложка!\n" + "Выбрать подложку? Категория должна быть ImportInstance",
                        "Подложка неверная",
                        MessageBoxButtons.YesNo);
                        if (result != DialogResult.Yes)
                        {
                            MessageBox.Show("Неверная подложка");
                            return Result.Failed;

                        }
                    }
                    else
                    {
                        break;
                    }

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
                if (geomList == null || geomList.Count == 0)
                {
                    DialogResult result = MessageBox.Show(
                        "нет точек на подложке dwg. выбрать другую подложку?", "Подложка неверная",
                        MessageBoxButtons.YesNo);
                    if (result != DialogResult.Yes)
                    {
                        continue;

                    }
                    else
                    {
                        MessageBox.Show("Неверная подложка");
                        return Result.Failed;
                    }
                }
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
                    MessageBox.Show("Всё не так, ребята!\n" + ex.Message);
                    return Result.Failed;
                }
                return Result.Succeeded;
            }
            return Result.Failed;
        }
        
    }
}
