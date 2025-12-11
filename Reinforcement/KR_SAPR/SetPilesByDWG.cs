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

            //FilteredElementCollector  FamNames = new FilteredElementCollector(doc);

            var Seacher = HelperSeach.GetExistFamily(Piles, commandData);
            FamilySymbol pile = Seacher.pile as FamilySymbol;
            Piles = Seacher.PossibleNamesFamilySymbol;

            if (pile == null)
            {
                MessageBox.Show("Семейство не загружено");
                return Result.Failed;
            }

            ForgeTypeId units = UnitTypeId.Millimeters;
            ForgeTypeId units2 = UnitTypeId.Feet;

            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();

            var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();

            TransparentNotificationWindow.ShowNotification("Выберите подложку dwg", uidoc, 5);
            int iter = -1;
            int iter2 = -1;
            Reference sel = null;
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
                double stepMM = 50;
                bool viravnPodlog = false;//выравнивание координат свай
                {
                    DialogResult result = MessageBox.Show(
                            "Округлять координаты сваи до 50?", "Округление",
                            MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        viravnPodlog = true;

                    }
                }
                if (stepMM < 1) { viravnPodlog = false; stepMM = 1; }


                // чтобы убрать дубликаты свай
                var HashNewPileDict = new Dictionary<(int x, int y), (double x, double y)>();
                foreach (XYZ point in geomList)
                {
                    double xd = UnitUtils.ConvertFromInternalUnits(point.X, units);
                    double yd = UnitUtils.ConvertFromInternalUnits(point.Y, units);

                    if (viravnPodlog)
                    {
                        xd = Math.Round(xd / stepMM) * stepMM; // округляем мм
                        yd = Math.Round(yd / stepMM) * stepMM;
                    }

                    int x = (int)xd; // a ConvertToInternalUnits переводит наоборот из метров в футы
                    int y = (int)yd;
                    HashNewPileDict[(x,y)]= (xd, yd);
                }


                



                //координата Z = 
                var Z = geomList.FirstOrDefault().Z; // а эта в футах

                var listNewPile = HashNewPileDict.Values.ToList();
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
                        foreach (var coord in listNewPile)
                        {
                            double x = UnitUtils.ConvertFromInternalUnits(coord.x, units2); // обратно в футы
                            double y = UnitUtils.ConvertFromInternalUnits(coord.y, units2);

                            var point = new XYZ(x, y, Z);

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
                
                //проверка пересечений 
                var listIntersect = new HashSet<(double x, double y)>();
                double minDist = 900;
                for (int i = 0; i < listNewPile.Count; i++)
                {
                    var coord1 = listNewPile[i];
                    for (int j = i + 1; j < listNewPile.Count; j++)
                    {
                        var coord2 = listNewPile[j];
                        double raznX = Math.Abs(coord2.x - coord1.x);
                        double raznY = Math.Abs(coord2.y - coord1.y);

                        if (raznX > minDist || raznY > minDist)
                        {
                            continue;
                        }

                        double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                        if (minDist - 1 > dist)
                        {
                            // пересечение
                            listIntersect.Add((coord1.x,coord1.y));
                        }
                    }
                }

                if (listIntersect.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("=== АУДИТ СВАЙ ===");
                    sb.AppendLine("\nКоординаты свай x,y пересекающих 3D=900mm (<= 12 свай):");

                    int i = 0;
                    int iterMax = 12;
                    foreach (var coord in listIntersect)
                    {
                        i++;
                        if (i > iterMax)
                        { break; }
                        sb.AppendLine($"(x,y) = ({coord.x}, {coord.y})");
                    }
                    TaskDialog.Show("АУДИТ СВАЙ", sb.ToString());
                }



                return Result.Succeeded;
            }
            return Result.Failed;
        }



        private (HashSet<(int x, int y)> HashNewPile, HashSet<(int x, int y)> listIntersect) MethodDepthPile( HashSet<(int x, int y)> HashNewPile, bool replaceCoord=false, double minDist = 900 )
        {
            // метод глубокой расстановки свай на основе 3D
            var listIntersect = new HashSet<(int x, int y)>();
            var listNewPile = HashNewPile.ToList();
            while (true)
            {

                //заполняем пересечения
                for (int i = 0; i < listNewPile.Count; i++)
                {
                    var coord1 = listNewPile[i];
                    for (int j = i + 1; j < listNewPile.Count; j++)
                    {
                        var coord2 = listNewPile[j];
                        double raznX = Math.Abs(coord2.x - coord1.x);
                        double raznY = Math.Abs(coord2.y - coord1.y);

                        if (raznX > minDist || raznY > minDist)
                        {
                            continue;
                        }

                        double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                        if (minDist > dist)
                        {
                            // пересечение
                            listIntersect.Add((coord1.x,coord1.y));
                        }
                    }
                }


                if (!replaceCoord || listIntersect.Count==0)
                {
                    break;
                }

                //корректируем пересечения

            }



            return (HashNewPile, listIntersect);

        }
    }
}
