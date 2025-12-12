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
                double stepMM = 25;
                bool viravnPodlog = false;//выравнивание координат свай
                {
                    DialogResult result = MessageBox.Show(
                            "Округлять координаты сваи до 25?", "Округление",
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
                    else
                    {
                        //просто округлим на всякий случай до 0?
                    }

                    int x = (int)xd; // a ConvertToInternalUnits переводит наоборот из метров в футы
                    int y = (int)yd;
                    HashNewPileDict[(x,y)]= (xd, yd);
                }
                //координата Z = 
                var Z = geomList.FirstOrDefault().Z; // а эта в футах

                var HashPileDataCorrect = new HashSet<PileDataCorrect>();
                foreach(var coordData in HashNewPileDict)
                {
                    HashPileDataCorrect.Add(new PileDataCorrect(coordData.Value.x, coordData.Value.y, coordData.Key.x, coordData.Key.y, Z));
                }



                //хотел чтоб этот метод покоординатно двигал
                HashPileDataCorrect = MethodDepthPile(HashPileDataCorrect);


                var listPileDataIntersect = HashPileDataCorrect.Where(x => x.intersect).ToList();



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
                        foreach (var pileDataCorrect in HashPileDataCorrect)
                        {
                            // Правильный перевод из мм в внутренние единицы Revit (футы)
                            double x = UnitUtils.ConvertToInternalUnits(pileDataCorrect.itogXint, units);
                            double y = UnitUtils.ConvertToInternalUnits(pileDataCorrect.itogYint, units);

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
                
                if (listPileDataIntersect.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("=== АУДИТ СВАЙ ===");
                    sb.AppendLine("\nКоординаты свай x,y пересекающих 3D=900mm (<= 12 свай):");

                    int i = 0;
                    int iterMax = 12;
                    foreach (var pileDataCorrect in listPileDataIntersect)
                    {
                        i++;
                        if (i > iterMax)
                        { break; }
                        sb.AppendLine($"(x,y) = ({pileDataCorrect.itogXint}, {pileDataCorrect.itogYint})");
                    }
                    TaskDialog.Show("АУДИТ СВАЙ", sb.ToString());
                }



                return Result.Succeeded;
            }
            return Result.Failed;
        }



        private HashSet<PileDataCorrect> MethodDepthPile(HashSet<PileDataCorrect> HashPileDataCorrect, bool replaceCoord=false, double minDist = 900 )
        {

            // метод глубокой расстановки свай на основе 3D

            //создания секторов перемещаемых свай и которые взаимно пересекаются, ой чушь

            var distSosed = 3 * minDist;



            var listHashPileDataCorrect = HashPileDataCorrect.ToList();
            var HashIntersect = new Dictionary<PileDataCorrect, HashSet< PileDataCorrect>>();

            bool perehetSosed= true;

            while (true)
            {
                HashIntersect.Clear();
                foreach (var pile in HashPileDataCorrect)
                {
                    pile.intersect = false;
                }
                if (perehetSosed)// пересчет соседей
                {
                    perehetSosed = false;

                    foreach (var pile in HashPileDataCorrect)
                    {
                        pile.PilesSosed.Clear();
                    }

                    //заполняем пересечения
                    for (int i = 0; i < listHashPileDataCorrect.Count; i++)
                    {
                        var pile1 = listHashPileDataCorrect[i];
                        (int x, int y) coord1 = (pile1.itogXint, pile1.itogYint);
                        for (int j = i + 1; j < listHashPileDataCorrect.Count; j++)
                        {
                            var pile2 = listHashPileDataCorrect[j];
                            (int x, int y) coord2 = (pile2.itogXint, pile2.itogYint);

                            int raznX = Math.Abs(coord2.x - coord1.x);
                            int raznY = Math.Abs(coord2.y - coord1.y);

                            if (raznX > distSosed || raznY > distSosed)
                            {
                                continue;
                            }

                            double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                            if (distSosed > dist)
                            {
                                //значит соседи
                                pile1.PilesSosed.Add(pile2);
                                pile2.PilesSosed.Add(pile1);
                            }
                        }
                    }
                }


                //заполняем пересечения смотрим только соседей

                foreach (var pile1 in HashPileDataCorrect)
                {
                    (int x, int y) coord1 = (pile1.itogXint, pile1.itogYint);
                    foreach (var pile2 in pile1.PilesSosed)
                    {
                        if(pile1 == pile2) {  continue; }// на всякий случай
                        (int x, int y) coord2 = (pile2.itogXint, pile2.itogYint);

                        int raznX = Math.Abs(coord2.x - coord1.x);
                        int raznY = Math.Abs(coord2.y - coord1.y);

                        if (raznX > minDist || raznY > minDist)
                        {
                            continue;
                        }

                        double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                        if (minDist > dist)
                        {
                            // пересечение
                            pile1.intersect = true;
                            pile2.intersect = true;


                            pile1.intersectDist=Math.Max(pile1.intersectDist, dist);
                            pile2.intersectDist = Math.Max(pile2.intersectDist, dist);


                            if (!HashIntersect.ContainsKey(pile1))
                            {
                                HashIntersect[pile1] = new HashSet<PileDataCorrect> { pile2 };
                            }
                            else
                            {
                                HashIntersect[pile1].Add(pile2);    
                            }


                            if (!HashIntersect.ContainsKey(pile2))
                            {
                                HashIntersect[pile2] = new HashSet<PileDataCorrect> { pile1 };
                            }
                            else
                            {
                                HashIntersect[pile2].Add(pile1);
                            }
                            
                        }
                    }
                }
                

                if (!replaceCoord || HashIntersect.Count==0)
                {
                    break;
                }

                //корректируем пересечения идём по HashIntersect
                // у кого меньше соседей или больше пересечения того и двигаем

            }



            return HashPileDataCorrect;

        }
    }


    public class PileDataCorrect
    {
        public double initialX = 0;
        public double initialY = 0;

        public int initialXint = 0; // эти нужны для итераций
        public int initialYint = 0;


        public int itogXint = 0; // эти нужны для итераций
        public int itogYint = 0;


        //public double initialXfeet = 0;//в футах
        //public double initialYfeet = 0;
        public double initialZfeet = 0;


        public bool intersect=false; // пересекается ли с кем либо
        public bool zapretChangeCoord = false;// запрет двигать координаты если мы соседа подвигали
        public double intersectDist = 0; // величина пересечения

        public HashSet<PileDataCorrect> PilesSosed = new HashSet<PileDataCorrect>(); // сваи соседи


        public (int Xs, int Ys) Sector = (0, 0);

        public PileDataCorrect(double initialX, double initialY, int initialXint, int initialYint, double initialZfeet)
        {
            this.initialX = initialX;
            this.initialY = initialY;
            this.initialXint = initialXint;
            this.initialYint = initialYint;
            this.initialZfeet = initialZfeet;


            itogXint = initialXint;
            itogYint = initialYint;
        }


    }
}
