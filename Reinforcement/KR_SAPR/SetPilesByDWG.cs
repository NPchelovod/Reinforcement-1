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
    //для корректировки позиций
    public interface PileCorrect
    {
        double initialX { get; set; }
        double initialY { get; set; }

        //int initialXint { get; set; }
        //int initialYint { get; set; }


        double itogX { get; set; }
        double itogY { get; set; }

        bool intersect { get; set; }
        bool zapretChangeCoord { get; set; }
        double intersectDist { get; set; }
        HashSet<PileCorrect> PilesSosed { get; set; }
    }


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

                // Показываем окно настроек с текущими значениями по умолчанию
                bool adjustPilePositions = false;
                double roundingStep = 25;
                double minDistanceBetweenPiles = 900;

                var settingsWindow = new PileDWGSettingsWindow(
                    adjustPilePositions,
                    roundingStep,
                    minDistanceBetweenPiles
                );

                if (settingsWindow.ShowDialog() == true && settingsWindow.ContinueExecution)
                {
                    adjustPilePositions = settingsWindow.AdjustPilePositions;
                    roundingStep = settingsWindow.RoundingStep;
                    minDistanceBetweenPiles = settingsWindow.MinDistanceBetweenPiles;
                }
                else
                {
                    return Result.Cancelled;
                }





                // чтобы убрать дубликаты свай
                var HashNewPileDict = new Dictionary<(int x, int y), (double x, double y)>();
                foreach (XYZ point in geomList)
                {
                    double xd = UnitUtils.ConvertFromInternalUnits(point.X, units);
                    double yd = UnitUtils.ConvertFromInternalUnits(point.Y, units);

                    if (roundingStep>0.01)
                    {
                        xd = Math.Round(xd / roundingStep) * roundingStep; // округляем мм
                        yd = Math.Round(yd / roundingStep) * roundingStep;
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
                foreach (var coordData in HashNewPileDict)
                {
                    HashPileDataCorrect.Add(new PileDataCorrect(
                        coordData.Value.x,
                        coordData.Value.y,
                        coordData.Key.x,
                        coordData.Key.y,
                        Z
                    ));
                }



                //хотел чтоб этот метод покоординатно двигал
                if (adjustPilePositions)
                {
                    var HashPileCorrect = new HashSet<PileCorrect>(HashPileDataCorrect) .ToHashSet();                // новый HashSet<PileDataCorrect>
                    if (roundingStep > 1)
                    {
                        HashPileCorrect = MethodDepthPile(HashPileCorrect, minDistanceBetweenPiles, roundingStep);
                    }
                    else
                    {
                        HashPileCorrect = MethodDepthPile(HashPileCorrect,  minDistanceBetweenPiles);
                    }
                    HashPileDataCorrect = new HashSet<PileCorrect>(HashPileCorrect)
                    .OfType<PileDataCorrect>()  // обратно в PileDataCorrect
                    .ToHashSet();                // новый HashSet<PileDataCorrect>
                }


                var listPileDataIntersect = HashPileDataCorrect.Where(x => x.intersect).ToList();



                //var listNewPile = HashNewPileDict.Values.ToList();
                try //ловим ошибку
                {
                    using (Transaction t = new Transaction(doc, "Создание свай по DWG"))
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
                            double x = UnitUtils.ConvertToInternalUnits(pileDataCorrect.itogX, units);
                            double y = UnitUtils.ConvertToInternalUnits(pileDataCorrect.itogY, units);

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
                    sb.AppendLine($"\nНайдено пересечений: {listPileDataIntersect.Count}");
                    sb.AppendLine("Координаты свай x,y пересекающих минимальную дистанцию:");

                    int i = 0;
                    int iterMax = 12;
                    foreach (var pileDataCorrect in listPileDataIntersect)
                    {
                        i++;
                        if (i > iterMax)
                        {
                            sb.AppendLine($"... и еще {listPileDataIntersect.Count - iterMax} пересечений");
                            break;
                        }
                        sb.AppendLine($"(x,y) = ({Math.Round(pileDataCorrect.itogX)}, {Math.Round(pileDataCorrect.itogY)}) - пересечение {pileDataCorrect.intersectDist:F0} мм");
                    }

                    TaskDialog.Show("АУДИТ СВАЙ", sb.ToString());
                }

                return Result.Succeeded;
            }
            return Result.Failed;
        }



        private HashSet<PileCorrect> MethodDepthPile(HashSet<PileCorrect> HashPileDataCorrect, double minDist = 900, double GRID_STEP = 50)
        {

            // метод глубокой расстановки свай на основе 3D

            //создания секторов перемещаемых свай и которые взаимно пересекаются, ой чушь

            var distSosed = 3 * minDist;
            


            var listHashPileDataCorrect = HashPileDataCorrect.ToList();
            var HashIntersect = new Dictionary< PileCorrect, HashSet<PileCorrect>>();

            bool perehetSosed= true;

            int iter = 0;
            int maxIter = 1000;


            while (iter < maxIter)
            {
                iter++;
                HashIntersect.Clear();
                foreach (var pile in HashPileDataCorrect)
                {
                    pile.intersect = false;
                    pile.zapretChangeCoord = false;
                    pile.intersectDist = 0;
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
                        (double x, double y) coord1 = (pile1.itogX, pile1.itogY);

                        for (int j = i + 1; j < listHashPileDataCorrect.Count; j++)
                        {
                            var pile2 = listHashPileDataCorrect[j];
                            (double x, double y) coord2 = (pile2.itogX, pile2.itogY);

                            double raznX = Math.Abs(coord2.x - coord1.x);
                            double raznY = Math.Abs(coord2.y - coord1.y);

                            if (raznX > distSosed || raznY > distSosed)
                            {
                                continue;
                            }

                            double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                            if (distSosed > dist+1)
                            {
                                //значит соседи
                                pile1.PilesSosed.Add(pile2);
                                pile2.PilesSosed.Add(pile1);
                            }
                        }
                    }
                }




                // Проверяем пересечения только среди соседей

                foreach (var pile1 in HashPileDataCorrect)
                {
                    (double x, double y) coord1 = (pile1.itogX, pile1.itogY);
                    foreach (var pile2 in pile1.PilesSosed)
                    {
                        if(pile1 == pile2) {  continue; }// на всякий случай
                        (double x, double y) coord2 = (pile2.itogX, pile2.itogY);

                        double raznX =  Math.Abs(coord2.x - coord1.x);
                        double raznY =  Math.Abs(coord2.y - coord1.y);

                        if (raznX > minDist || raznY > minDist)
                        {
                            continue;
                        }

                        double dist = Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY));

                        if (minDist > dist+1) // 1 mm в запас
                        {
                            // пересечение
                            pile1.intersect = true;
                            pile2.intersect = true;


                            pile1.intersectDist=Math.Max(pile1.intersectDist, dist);
                            pile2.intersectDist = Math.Max(pile2.intersectDist, dist);


                            if (!HashIntersect.ContainsKey(pile1))
                            {
                                HashIntersect[pile1] = new HashSet<PileCorrect> { pile2 };
                            }
                            else
                            {
                                HashIntersect[pile1].Add(pile2);    
                            }


                            if (!HashIntersect.ContainsKey(pile2))
                            {
                                HashIntersect[pile2] = new HashSet<PileCorrect> { pile1 };
                            }
                            else
                            {
                                HashIntersect[pile2].Add(pile1);
                            }
                            
                        }
                    }
                }
                
                if (HashIntersect.Count==0)
                {
                    break;
                }
                var sortedKeys = HashIntersect
                .OrderByDescending(kvp => kvp.Value.Count)  // по убыванию количества
                .Select(kvp => kvp.Key)                      // только ключи
                .ToList();

                foreach (var pile in sortedKeys)
                { 
                    if(!HashIntersect.TryGetValue(pile, out var intersectingPiles) || pile.zapretChangeCoord)
                    {
                        continue; 
                    }
                    //корректируем pile относительно остальных
                    // Корректируем pile относительно всех конфликтующих
                    CorrectPilePosition(pile, intersectingPiles, minDist, GRID_STEP);
                    pile.zapretChangeCoord = true;
                    HashIntersect.Remove(pile);
                    foreach (var otherPile in intersectingPiles)
                    {
                        otherPile.zapretChangeCoord = true;
                    }
                }
                
               
            }


            return HashPileDataCorrect;

        }

        void CorrectPilePosition(PileCorrect pile, HashSet<PileCorrect> others, double minDist, double GRID_STEP)
        {
            double bestX = pile.initialX, bestY = pile.initialY;
           
            double minDistSq = Math.Pow(minDist, 2);
            // Начинаем с ближайших ячеек и расширяем радиус
            // Начинаем с ближайших ячеек и расширяем радиус
            for (int radius = 1; radius <= 20; radius++)  // ±50мм, ±100мм, ...
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Верхняя и нижняя границы радиуса
                    int dy1 = radius;
                    int dy2 = -radius;

                    if (IsPositionValid(pile.initialX + dx * GRID_STEP,
                                       pile.initialY + dy1 * GRID_STEP,
                                       others, minDistSq, GRID_STEP))
                    {
                        pile.itogX = (int)Math.Round((pile.initialX + dx * GRID_STEP) / GRID_STEP);
                        pile.itogY = (int)Math.Round((pile.initialY + dy1 * GRID_STEP) / GRID_STEP);
                        return;  // первая валидная!
                    }
                    if (IsPositionValid(pile.initialX + dx * GRID_STEP,
                                       pile.initialY + dy2 * GRID_STEP,
                                       others, minDistSq, GRID_STEP))
                    {
                        pile.itogX = (int)Math.Round((pile.initialX + dx * GRID_STEP) / GRID_STEP);
                        pile.itogY = (int)Math.Round((pile.initialY + dy2 * GRID_STEP) / GRID_STEP);
                        return;
                    }
                }
            }
        }
        bool IsPositionValid(double testX, double testY, HashSet<PileCorrect> others,
                     double minDistSq, double gridStep)
        {
            foreach (var other in others)
            {
                double dx = testX - other.itogX * gridStep;
                double dy = testY - other.itogY * gridStep;
                if (dx * dx + dy * dy < minDistSq)  // квадрат расстояния
                    return false;
            }
            return true;
        }
       
    }


    public class PileDataCorrect : PileCorrect
    {
        public double initialX { get; set; } = 0;
        public double initialY { get; set; } = 0;

        //public int initialXint { get; set; } = 0; // эти нужны для итераций
        //public int initialYint { get; set; } = 0;


        public double itogX { get; set; } = 0; // эти нужны для итераций
        public double itogY { get; set; } = 0;


        //public double initialXfeet = 0;//в футах
        //public double initialYfeet = 0;
        public double initialZfeet = 0;


        public bool intersect { get; set; } = false; // пересекается ли с кем либо
        public bool zapretChangeCoord { get; set; } = false;// запрет двигать координаты если мы соседа подвигали для корректной отработки
        public double intersectDist { get; set; } = 0; // величина пересечения

        public HashSet<PileCorrect> PilesSosed { get; set; } = new HashSet<PileCorrect>(); // сваи соседи


        public (int Xs, int Ys) Sector = (0, 0);

        public PileDataCorrect(double initialX, double initialY, int initialXint, int initialYint, double initialZfeet)
        {
            this.initialX = initialX;
            this.initialY = initialY;

            //this.initialXint = initialXint;
            //this.initialYint = initialYint;

            this.initialZfeet = initialZfeet;

            itogX = initialX;
            itogY = initialY;
        }


    }
}
