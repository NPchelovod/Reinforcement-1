using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public static class NumPilesRotate
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

    }
}
