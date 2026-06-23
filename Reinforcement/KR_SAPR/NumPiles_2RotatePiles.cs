using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

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
        public static Result CorrectCoordPiles(Document doc, HashSet<Element> Seacher, double round, bool zCorrect=false)
        {
            if (doc == null || Seacher == null || Seacher.Count == 0)
                return Result.Failed;

            HashSet<int> XCoords = new HashSet<int>();
            HashSet<int>YCoords = new HashSet<int>();
            HashSet<int> ZCoords = new HashSet<int>();
            using (Transaction trans = new Transaction(doc, "Позиции свай коррект"))
            {
                trans.Start();

                
                foreach (Element element in Seacher)
                {
                    if (element == null) continue;

                    // Проверяем, что элемент имеет точечное расположение (свая обычно FamilyInstance)
                    Location loc = element.Location;
                    if (!(loc is LocationPoint locationPoint))
                    { continue; }
                    XYZ tek_locate_point = locationPoint.Point;
                    double X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                    double Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                    double Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                    double newX = Math.Round(X / round) * round;
                    double newY = Math.Round(Y / round) * round;
                    double newZ = Math.Round(Z / round) * round; // Добавляем обработку Z
                                                                 // Вычисляем сдвиг в единицах Revit (внутренних)

                    if(!XCoords.Contains((int)newX))
                    {
                        double newX1 = Math.Floor(X / round) * round;
                        double newX2 = Math.Ceiling(X / round) * round;
                        if(XCoords.Contains((int)newX1))
                        {
                            newX= newX1;
                        }
                        else if(XCoords.Contains((int) newX2))
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

                    double dx = UnitUtils.ConvertToInternalUnits(newX - X, units);
                    double dy = UnitUtils.ConvertToInternalUnits(newY - Y, units);
                    double dz = UnitUtils.ConvertToInternalUnits(newZ - Z, units);
                    if (Math.Abs(dx) > e || Math.Abs(dy) > e || zCorrect&& Math.Abs(dz) > e)
                    {
                        // Создаём вектор сдвига
                        XYZ moveVector = new XYZ(dx, dy, zCorrect? dz:0);

                        // Перемещаем элемент
                        try
                        {
                            locationPoint.Move(moveVector);
                        }
                        catch (Exception ex)
                        {
                            //TaskDialog.Show("Ошибка", $"Не удалось переместить элемент {element.Id}: {ex.Message}");
                            continue;
                        }
                       
                    }
                    XCoords.Add((int)newX);
                    YCoords.Add((int)newY);
                    ZCoords.Add((int)newZ);
                }
                trans.Commit();
            }
            return Result.Succeeded;
        }
    }
}
