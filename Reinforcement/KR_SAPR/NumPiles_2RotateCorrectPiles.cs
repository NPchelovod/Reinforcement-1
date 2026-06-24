using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
//using static UIFramework.Widget.CustomControls.NativeMethods;

namespace Reinforcement
{
    public static class NumPilesRotateAndMove
    {
        //
        /*Мы должны написать функцию на C# для Revit, которая будет обрабатывать элементы свай (вероятно, семейства свай, квадратного сечения). Нужно: если свая повернута на плане, то надо определить ее угол поворота и повернуть ее так, чтобы угол стал как можно ближе к 0 (минимальный угол). При этом 
         свая квадратная, поэтому поворот на 90, 180, 270 градусов эквивалентны с точки зрения внешнего вида
         */
        public static Result RotatePiles(Document doc, HashSet<Element> Seacher)
        {
            if (doc == null || Seacher == null || Seacher.Count == 0)
                return Result.Failed;

            using (Transaction trans = new Transaction(doc, "Поворот свай"))
            {
                trans.Start();
                foreach (Element element in Seacher)
                {
                    if (element == null) continue;

                    // Проверяем, что элемент имеет точечное расположение (свая обычно FamilyInstance)
                    Location loc = element.Location;
                    if (!(loc is LocationPoint locationPoint))
                    { continue; }

                    // Текущий угол поворота (радианы → градусы)
                    double currentAngleRad = locationPoint.Rotation;
                    double currentAngleDeg = currentAngleRad * 180.0 / Math.PI;

                    // Приводим текущий угол к диапазону [0, 360)
                    double normalizedDeg = currentAngleDeg % 360.0;
                    if (normalizedDeg < 0) normalizedDeg += 360.0;

                    // Остаток от деления на 90° (базовый угол без учёта квадратности)
                    double remainder = normalizedDeg % 90.0;

                    // Целевой минимальный угол в диапазоне (-45°, 45°] → затем переводим в [0, 360)
                    double targetDeg;
                    if (remainder <= 45.0)
                        targetDeg = remainder;
                    else
                        targetDeg = remainder - 90.0;   // отрицательное значение, ближе к нулю

                    if (targetDeg < 0) targetDeg += 360.0;

                    // Разница между целевым и текущим углом
                    double deltaDeg = targetDeg - normalizedDeg;

                    // Сокращаем разницу до диапазона (-180°, 180°] для минимального вращения
                    if (deltaDeg > 180.0) deltaDeg -= 360.0;
                    else if (deltaDeg < -180.0) deltaDeg += 360.0;

                    // Если разница пренебрежимо мала, пропускаем элемент
                    if (Math.Abs(deltaDeg) < 1e-6)
                    {
                        continue;
                    }

                    // Поворот в радианах
                    double deltaRad = deltaDeg * Math.PI / 180.0;

                    // Ось вращения: вертикальная прямая через точку вставки сваи
                    XYZ origin = locationPoint.Point;
                    Line axis = Line.CreateBound(origin, origin + XYZ.BasisZ);

                    // Выполняем поворот
                    locationPoint.Rotate(axis, deltaRad);
                }
                trans.Commit();
            }
             return Result.Succeeded;
        }
        public static ForgeTypeId units => NumPiles.units;
        public static double e = 0.00001;
        public static int RoundCoordsPiles(Document doc, HashSet<Element> Seacher, double round,double dist3D=900, bool zCorrect=false)
        {
            if (doc == null || Seacher == null || Seacher.Count == 0)
            { return 0; }

            List<CoordCorrectData> coordsElements = GetCoordElements(Seacher);

            //соседей наёдем
            double sosedDistance = dist3D * 2;
            GetSosedCoordElement(coordsElements, sosedDistance);

            HashSet<CoordCorrectData> answerElement = RoundCoords(coordsElements, round,zCorrect);
            if(answerElement.Count == 0) {return 0;}

            int numCorrect = 0;
            using (Transaction trans = new Transaction(doc, "Позиции свай коррект"))
            {
                trans.Start();

                
                foreach (var element in answerElement)
                {
                    if (element == null) continue;
                    double dxi = element.X-element.pX;
                    double dyi = element.Y - element.pY;
                    double dzi = element.Z - element.pZ;


                    double dx = UnitUtils.ConvertToInternalUnits(dxi, units);
                    double dy = UnitUtils.ConvertToInternalUnits(dyi, units);
                    double dz = zCorrect? UnitUtils.ConvertToInternalUnits(dzi, units):0;
                    if (Math.Abs(dx) > e || Math.Abs(dy) > e || zCorrect&& Math.Abs(dz) > e)
                    {
                        // Создаём вектор сдвига
                        XYZ moveVector = new XYZ(dx, dy, dz);

                        // Перемещаем элемент
                        try
                        {
                            Location loc = element.Element.Location;
                            if (!(loc is LocationPoint locationPoint))
                            { continue; }

                            locationPoint.Move(moveVector);
                            numCorrect++;
                        }
                        catch (Exception ex)
                        {
                            //TaskDialog.Show("Ошибка", $"Не удалось переместить элемент {element.Id}: {ex.Message}");
                            continue;
                        }
                       
                    }
                   
                }
                trans.Commit();
            }
            return numCorrect;
        }

        public static HashSet<CoordCorrectData> RoundCoords(List<CoordCorrectData> coordCorrectDatas, double round, bool zCorrect = false)
        {
            HashSet<int> XCoords = new HashSet<int>();
            HashSet<int> YCoords = new HashSet<int>();
            HashSet<int> ZCoords = new HashSet<int>();
            int numCorrect = 0;
            var listCorrectPiles = new HashSet<CoordCorrectData>();

            //округление координат до целого однопроходный алгоритм, можно сделать с учетом соседей

            foreach (var element in coordCorrectDatas)
            {
                
                double X = element.X; // a ConvertToInternalUnits переводит наоборот из метров в футы
                double Y = element.Y;
                double Z = element.Z;

                double newX = Math.Round(X / round) * round;
                double newY = Math.Round(Y / round) * round;
                double newZ = Math.Round(Z / round) * round; // Добавляем обработку Z
                                                                // Вычисляем сдвиг в единицах Revit (внутренних)

                if (!XCoords.Contains((int)newX))
                {
                    double newX1 = Math.Floor(X / round) * round;
                    double newX2 = Math.Ceiling(X / round) * round;
                    if (XCoords.Contains((int)newX1))
                    {
                        newX = newX1;
                    }
                    else if (XCoords.Contains((int)newX2))
                    {
                        newX = newX2;
                    }
                }
                if (!YCoords.Contains((int)newY))
                {
                    double newY1 = Math.Floor(Y / round) * round;
                    double newY2 = Math.Ceiling(Y / round) * round;
                    if (YCoords.Contains((int)newY1))
                    {
                        newY = newY1;
                    }
                    else if (YCoords.Contains((int)newY2))
                    {
                        newY = newY2;
                    }
                }
                if (!ZCoords.Contains((int)newZ))
                {
                    double newZ1 = Math.Floor(Z / round) * round;
                    double newZ2 = Math.Ceiling(Z / round) * round;
                    if (XCoords.Contains((int)newZ1))
                    {
                        newZ = newZ1;
                    }
                    else if (XCoords.Contains((int)newZ2))
                    {
                        newZ = newZ2;
                    }
                }

                double dx =newX - X;
                double dy = newY - Y;
                double dz = newZ - Z;
                if (Math.Abs(dx) > e || Math.Abs(dy) > e || zCorrect && Math.Abs(dz) > e)
                {
                    // Создаём вектор сдвига
                    element.X = newX;
                    element.Y = newY;
                    if (zCorrect)
                    {
                        element.Z = newZ;
                    }
                    numCorrect++;
                    listCorrectPiles.Add(element);

                }
                XCoords.Add((int)newX);
                YCoords.Add((int)newY);
                ZCoords.Add((int)newZ);
            }
            return listCorrectPiles;


        }

        public static List<CoordCorrectData> GetCoordElements(HashSet<Element> Seacher)=> Seacher.Select(x=>(CoordCorrectData) new CoordElement(x)).ToList();


        public static int CorrectCoord3D(Document doc, double minDist, HashSet<Element> Seacher)
        {


            var coordElements = GetCoordElements(Seacher);

            int numCorrect = CorrectCoordMinDist( minDist, Seacher, coordElements);

            numCorrect = 0;
            //теперь в транзакции заменяем сваи
            using (Transaction trans = new Transaction(doc, "Подвижка свай"))
            {
                trans.Start();
                foreach (var element in coordElements)
                {
                    if (element == null) continue;
                    double dx = UnitUtils.ConvertToInternalUnits(element.X - element.pX, units);
                    double dy = UnitUtils.ConvertToInternalUnits(element.Y - element.pY, units);
                    if (Math.Abs(dx) > e || Math.Abs(dy) > e)
                    {
                        // Создаём вектор сдвига
                        XYZ moveVector = new XYZ(dx, dy, 0);

                        // Перемещаем элемент
                        try
                        {
                            Location loc = element.Element.Location;
                            if (!(loc is LocationPoint locationPoint))
                            { continue; }
                            XYZ tek_locate_point = locationPoint.Point;
                            locationPoint.Move(moveVector);
                            numCorrect++;
                        }
                        catch (Exception ex)
                        {
                            //TaskDialog.Show("Ошибка", $"Не удалось переместить элемент {element.Id}: {ex.Message}");
                            continue;
                        }

                    }
                }
                trans.Commit();
            }
            return numCorrect;
        }
        public static int CorrectCoordMinDist(double minDist, HashSet<Element> Seacher=null, List<CoordCorrectData> coordElements=null)
        {
            //берём все координаты и переводим в интерфейс

            double sosedDistance = 1.8 * minDist;
            double correctDistance = Math.Min(minDist, 0.5 * sosedDistance);
            if (coordElements == null)
            {
                if (Seacher == null) { return 0; }
                coordElements = GetCoordElements(Seacher);
            }
            GetSosedCoordElement(coordElements, sosedDistance);
            //возвращает изменённые координаты чтобы дистанция между сваями была 3д
            int maxAtempt = 20;//20 попыток корректировки свай
            int i = 0;
            double sumDistanceCorrect = 0;
            int elementsChange = 0;
            while (i < maxAtempt)
            {
                i++;
                (int countElement, double distanceMaxCorrect) = OneCorrectMinDistPiles(coordElements, minDist);
                if (countElement == 0)
                {
                    break;
                }
                elementsChange += countElement;
                sumDistanceCorrect += distanceMaxCorrect;
                if(correctDistance< sumDistanceCorrect)
                {
                    sumDistanceCorrect = 0;
                    GetSosedCoordElement(coordElements, sosedDistance);// соседи могут стать другими
                }
            }

            return elementsChange;
        }
        public static void GetSosedCoordElement(List<CoordCorrectData> coordElements, double sosedDistance)
        {
            //поиск соседей
            coordElements.ForEach(x=>x.Neighbours.Clear());
            for (int i = 0; i < coordElements.Count; i++)
            {
                var e1 = coordElements[i];
                for(int j = i+1; j< coordElements.Count; j++)
                {
                    var e2 = coordElements[j];
                    if(e1.Dist(e2)< sosedDistance)
                    {
                        e1.Neighbours.Add(e2);
                        e2.Neighbours.Add(e1);
                    }
                }
            }
        }
        private static (int countElement, double distanceMaxCorrect) OneCorrectMinDistPiles(List<CoordCorrectData> coordElements, double minDist)
        {
            //однапроходная корректировка свай

            //сначала ищем главные сваи, затем второстепенны корректируем??
            //нет сначала ищем "неверные" сваи
            //coordElements.ForEach(x => { x.pX = x.X; x.pY = x.Y; x.pZ = x.Z; });
            int attempt = 10;// до 10 попыток скорректировать
            double e = 0.001;
            HashSet<CoordCorrectData> UnCorrectPiles = new HashSet<CoordCorrectData>();
            (int countElement, double distanceMaxCorrect) = (0, 0);
            foreach (var coordElement in coordElements)
            {
                foreach(var  sosed in coordElement.Neighbours)
                {
                    if(sosed.Dist(coordElement) < minDist)
                    {
                        UnCorrectPiles.Add(coordElement);
                        UnCorrectPiles.Add(sosed);
                    }
                }
            }
            if (UnCorrectPiles.Count == 0) { return (countElement, distanceMaxCorrect); }

            //сортируем по минимальному соседу того надежней двигать
            var UnCorrectList= UnCorrectPiles.OrderBy(x=>x.Neighbours.Count).ToList();

            HashSet<CoordCorrectData> PastCorrectData = new HashSet<CoordCorrectData>();
            foreach (var coordElement in UnCorrectList)
            {
                if (PastCorrectData.Contains(coordElement)) { continue; }
                PastCorrectData.Add(coordElement);

                int a = 0;//последовательно двигаем сваи
                bool correct = false;
                double itogCorrect = 0;
                while (a < attempt)
                {
                    a++;
                    //теперь определяем смещение
                    foreach (var sosed in coordElement.Neighbours)
                    {
                        double distance = sosed.Dist(coordElement);
                        double smes =   minDist- distance;
                        if (smes<e) { continue; }
                        correct = true;

                        (double newX, double newY) = ExtendLineFromTwoPoints(sosed.X, sosed.Y, coordElement.X, coordElement.Y, smes);
                        coordElement.X = newX; coordElement.Y = newY;
                        itogCorrect += smes;
                    }
                    if(!correct)
                    {
                        break;
                    }

                   
                }
                if(itogCorrect>e)
                {
                    countElement++;
                    distanceMaxCorrect = Math.Max(itogCorrect, distanceMaxCorrect);
                }
                

            }

            return (countElement, distanceMaxCorrect);
        }
        /// <summary>
        /// Удлиняет линию от второй точки в направлении первой ко второй.
        /// </summary>
        /// <param name="x1">X первой точки</param>
        /// <param name="y1">Y первой точки</param>
        /// <param name="x2">X второй точки</param>
        /// <param name="y2">Y второй точки</param>
        /// <param name="extensionLength">Расстояние удлинения (в тех же единицах, что и координаты)</param>
        /// <returns>Координаты новой точки (X, Y)</returns>
        public static (double X, double Y) ExtendLineFromTwoPoints(
            double x1, double y1,
            double x2, double y2,
            double extensionLength)
        {
            // 1. Находим вектор направления от точки 1 к точке 2
            double dirX = x2 - x1;
            double dirY = y2 - y1;

            // 2. Вычисляем длину этого вектора
            double currentLength = Math.Sqrt(dirX * dirX + dirY * dirY);
            
            if (currentLength == 0)
            {
                return (x1+ extensionLength, y1);
            }

            // 3. Нормализуем вектор (делаем его длиной 1)
            double unitX = dirX / currentLength;
            double unitY = dirY / currentLength;

            // 4. Вычисляем новую точку, отталкиваясь от ВТОРОЙ точки (x2, y2)
            double newX = x2 + (unitX * extensionLength);
            double newY = y2 + (unitY * extensionLength);

            return (newX, newY);
        }
    }
}
