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
    public interface IPileCorrect
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
        HashSet<IPileCorrect> PilesSosed { get; set; }
    }


    [Transaction(TransactionMode.Manual)]

    public class SetPilesByDWG : IExternalCommand
    {

        //имена типоразмеров семейства

        public HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "С140.30-С","С 70.30-9", "Буронабивная d"
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
            ElementTypeOrSymbol Type_seach = ElementTypeOrSymbol.ElementType;
            var Seacher = HelperSeach.GetExistFamily(Piles, Type_seach);
            
            //Piles = Seacher.PossibleNamesFamilySymbol;

            if (Seacher == null)
            {
                MessageBox.Show("Семейство не загружено");
                return Result.Failed;
            }
            FamilySymbol pile = Seacher as FamilySymbol;
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
                while (iter < 3)
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



                bool applyRounding = false;

                // чтобы убрать дубликаты свай
                var HashNewPileDict = new Dictionary<(int x, int y), (double x, double y)>();
                foreach (XYZ point in geomList)
                {
                    double xd = UnitUtils.ConvertFromInternalUnits(point.X, units);
                    double yd = UnitUtils.ConvertFromInternalUnits(point.Y, units);

                    if (applyRounding||roundingStep > 0.01)
                    {
                        applyRounding=true;
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
                    var HashIPileCorrect = new HashSet<IPileCorrect>(HashPileDataCorrect) .ToHashSet();                // новый HashSet<PileDataCorrect>
                    

                     HashIPileCorrect = MethodDepthPile(HashIPileCorrect, minDistanceBetweenPiles, roundingStep, applyRounding);
                   
                    HashPileDataCorrect = new HashSet<IPileCorrect>(HashIPileCorrect)
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



        public static HashSet<IPileCorrect> MethodDepthPile(HashSet<IPileCorrect> piles, double minDist = 900, double roundingStep = 50, bool applyRounding=false, double minChangeThreshold = 2.0)
        {

            // Если roundingStep = 0 или applyRounding = false, округление не применяется
            if (applyRounding && roundingStep > 0)
            {
                // Начальное округление координат до заданного шага
                foreach (var pile in piles)
                {
                    pile.itogX = Math.Round(pile.initialX / roundingStep) * roundingStep;
                    pile.itogY = Math.Round(pile.initialY / roundingStep) * roundingStep;
                }
            }
            else
            {
                // Без округления используем исходные координаты
                foreach (var pile in piles)
                {
                    pile.itogX = pile.initialX;
                    pile.itogY = pile.initialY;
                }
            }

            var distNeighbor = 3 * minDist;
            var pileList = piles.ToList();
            var intersectingPiles = new Dictionary<IPileCorrect, HashSet<IPileCorrect>>();

            bool recalculateNeighbors = true;
            int iteration = 0;
            int maxIterations = 100;

            while (iteration < maxIterations)
            {
                iteration++;
                intersectingPiles.Clear();

                foreach (var pile in piles)
                {
                    pile.intersect = false;
                    pile.zapretChangeCoord = false;
                    pile.intersectDist = 0;
                }

                // Пересчет соседей
                if (recalculateNeighbors)
                {
                    recalculateNeighbors = false;

                    // Очищаем списки соседей
                    foreach (var pile in piles)
                    {
                        pile.PilesSosed.Clear();
                    }

                    // Находим соседей (сваи в пределах 3 * minDist)
                    for (int i = 0; i < pileList.Count; i++)
                    {
                        var pile1 = pileList[i];
                        var coord1 = (pile1.itogX, pile1.itogY);

                        for (int j = i + 1; j < pileList.Count; j++)
                        {
                            var pile2 = pileList[j];
                            var coord2 = (pile2.itogX, pile2.itogY);

                            double dx = Math.Abs(coord2.itogX - coord1.itogX);
                            double dy = Math.Abs(coord2.itogY - coord1.itogY);

                            if (dx > distNeighbor || dy > distNeighbor) continue;

                            double distance = Math.Sqrt(dx * dx + dy * dy);

                            if (distance < distNeighbor + 1) // +1 для учета погрешности
                            {
                                pile1.PilesSosed.Add(pile2);
                                pile2.PilesSosed.Add(pile1);
                            }
                        }
                    }
                }

                // Находим пересечения
                foreach (var pile1 in piles)
                {
                    var coord1 = (pile1.itogX, pile1.itogY);

                    foreach (IPileCorrect pile2Obj in pile1.PilesSosed)
                    {
                        if (pile1 == pile2Obj) continue;

                        var pile2 = pile2Obj;
                        var coord2 = (pile2.itogX, pile2.itogY);

                        double dx = Math.Abs(coord2.itogX - coord1.itogX);
                        double dy = Math.Abs(coord2.itogY - coord1.itogY);

                        if (dx > minDist || dy > minDist) continue;

                        double distance = Math.Sqrt(dx * dx + dy * dy);

                        if (distance < minDist - 1) // -1 для учета погрешности
                        {
                            pile1.intersect = true;
                            pile2.intersect = true;

                            pile1.intersectDist = Math.Max(pile1.intersectDist, distance);
                            pile2.intersectDist = Math.Max(pile2.intersectDist, distance);

                            // Добавляем в словарь пересечений
                            if (!intersectingPiles.ContainsKey(pile1))
                                intersectingPiles[pile1] = new HashSet<IPileCorrect> { pile2 };
                            else
                                intersectingPiles[pile1].Add(pile2);

                            if (!intersectingPiles.ContainsKey(pile2))
                                intersectingPiles[pile2] = new HashSet<IPileCorrect> { pile1 };
                            else
                                intersectingPiles[pile2].Add(pile1);
                        }
                    }
                }

                // Если пересечений нет, завершаем
                if (intersectingPiles.Count == 0) break;

                // Сортируем сваи по количеству пересечений (больше пересечений -> раньше корректируем)
                var sortedPiles = intersectingPiles
                    .OrderByDescending(kvp => kvp.Value.Count)
                    .Select(kvp => kvp.Key)
                    .ToList();

                bool anyCorrectionMade = false;

                foreach (var pile in sortedPiles)
                {
                    if (!intersectingPiles.TryGetValue(pile, out var conflictingPiles) ||
                        pile.zapretChangeCoord) continue;

                    // Сохраняем текущие координаты для сравнения
                    double oldX = pile.itogX;
                    double oldY = pile.itogY;

                    // Корректируем позицию сваи
                    CorrectPilePosition(pile, conflictingPiles, minDist, roundingStep, applyRounding, minChangeThreshold);
                    pile.zapretChangeCoord = true;

                    // Проверяем, изменились ли координаты
                    if (Math.Abs(oldX - pile.itogX) + Math.Abs(oldY - pile.itogY) >= minChangeThreshold)
                    {
                        anyCorrectionMade = true;
                    }

                    // Запрещаем изменение для конфликтующих свай в этой итерации
                    foreach (var other in conflictingPiles)
                    {
                        other.zapretChangeCoord = true;
                    }
                }

                // Если в этой итерации не было сделано ни одной коррекции, выходим
                if (!anyCorrectionMade) break;

                // После обработки всех свай сбрасываем флаги запрета для следующей итерации
                foreach (var pile in piles)
                {
                    pile.zapretChangeCoord = false;
                }

                // Пересчитываем соседей на следующей итерации
                recalculateNeighbors = true;
            }

            return piles;


            //return HashPileDataCorrect;

        }

        private static void CorrectPilePosition(IPileCorrect pile,
                                HashSet<IPileCorrect> conflictingPiles,
                                double minDist,
                                double roundingStep,
                                bool applyRounding,
                                              double minChangeThreshold = 2.0)
        {
            // Проверяем, есть ли вообще конфликты
            if (conflictingPiles == null || conflictingPiles.Count == 0) return;
            // Векторный подход: вычисляем результирующий вектор смещения
            double totalDx = 0;
            double totalDy = 0;
            int count = 0;

            foreach (var other in conflictingPiles)
            {
                double dx = pile.itogX - other.itogX;
                double dy = pile.itogY - other.itogY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDist && distance > 0)
                {
                    // Вычисляем необходимый сдвиг
                    double overlap = minDist - distance;
                    double pushFactor = overlap / distance;

                    totalDx += dx * pushFactor;
                    totalDy += dy * pushFactor;
                    count++;
                }
            }

            if (count == 0) return;

            // Усредняем вектор смещения
            double avgDx = totalDx / count;
            double avgDy = totalDy / count;

            
            // Проверяем, достаточно ли велико смещение для применения
            double totalChange = Math.Abs(avgDx) + Math.Abs(avgDy);
            if (totalChange < minChangeThreshold)
            {
                // Смещение слишком мало, пропускаем
                return;
            }
            // Применяем смещение
            double newX = pile.itogX + avgDx;
            double newY = pile.itogY + avgDy;
            // Если нужно, округляем координаты
            if (applyRounding && roundingStep > 0)
            {
                newX = Math.Round(newX / roundingStep) * roundingStep;
                newY = Math.Round(newY / roundingStep) * roundingStep;
            }

            // Проверяем, что новая позиция не создает новые пересечения
            if (IsValidPosition(newX, newY, pile, conflictingPiles, minDist))
            {
                pile.itogX = newX;
                pile.itogY = newY;
            }
            else
            {
                // Если позиция невалидна, ищем альтернативную позицию по спирали
                FindAlternativePosition(pile, conflictingPiles, minDist, roundingStep, applyRounding);
            }
        }

        private static bool IsValidPosition(double x, double y, IPileCorrect currentPile,
                                    HashSet<IPileCorrect> otherPiles, double minDist)
        {
            foreach (var other in otherPiles)
            {
                if (other == currentPile) continue;

                double dx = x - other.itogX;
                double dy = y - other.itogY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDist - 1) // -1 для погрешности
                    return false;
            }
            return true;
        }

        private static void FindAlternativePosition(IPileCorrect pile,
                                            HashSet<IPileCorrect> conflictingPiles,
                                            double minDist,
                                            double roundingStep,
                                            bool applyRounding, double minChangeThreshold = 1)
        {
            double minDistSq = minDist * minDist;

            // Поиск по спирали от начальной позиции
            for (int radius = 1; radius <= 10; radius++) // до 10 шагов
            {
                for (int angleStep = 0; angleStep < 360; angleStep += 45) // каждые 45 градусов
                {
                    double angle = angleStep * Math.PI / 180.0;
                    double testX = pile.initialX + radius * minDist * Math.Cos(angle);
                    double testY = pile.initialY + radius * minDist * Math.Sin(angle);

                    // Округляем если нужно
                    if (applyRounding && roundingStep > 0)
                    {
                        testX = Math.Round(testX / roundingStep) * roundingStep;
                        testY = Math.Round(testY / roundingStep) * roundingStep;
                    }
                    // Проверяем минимальное изменение
                    double totalChange = Math.Abs(pile.initialX - testX) + Math.Abs(pile.initialY - testY);
                    if (totalChange < minChangeThreshold)
                    {
                        continue;
                    }
                    if (IsValidPosition(testX, testY, pile, conflictingPiles, minDist))
                    {
                        pile.itogX = testX;
                        pile.itogY = testY;
                        return;
                    }
                }
            }

            // Если не нашли подходящую позицию, оставляем как есть
        }

    }


    public class PileDataCorrect : IPileCorrect
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

        public HashSet<IPileCorrect> PilesSosed { get; set; } = new HashSet<IPileCorrect>(); // сваи соседи


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
