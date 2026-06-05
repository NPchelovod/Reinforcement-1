using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public static class RecreatePiles
    {

        //
        /*Мы должны написать функцию на C# для Revit, которая будет обрабатывать элементы свай (вероятно, семейства свай, квадратного сечения). Нужно: если свая повернута на плане, то надо определить ее угол поворота и повернуть ее так, чтобы угол стал как можно ближе к 0 (минимальный угол). При этом 
         свая квадратная, поэтому поворот на 90, 180, 270 градусов эквивалентны с точки зрения внешнего вида
         */
        public static Result RecreatePile(Document doc, HashSet<PileData> PileDatas)
        {
            if (doc == null || PileDatas == null || PileDatas.Count == 0)
                return Result.Failed;

            using (Transaction recreateTrans = new Transaction(doc, "Пересоздание свай"))
            {
                try
                {
                    recreateTrans.Start();
                    foreach (var pile in PileDatas)
                    {
                        Element ePile = pile.Pile;
                        if (ePile == null) { continue; }
                        var locationPoint = ePile.Location as LocationPoint;
                        if (locationPoint == null) continue;
                        var familyInstance = ePile as FamilyInstance;
                        if (familyInstance == null) continue;
                        // Получаем уровень из старой сваи
                        var levelId = ePile.LevelId;
                        var level = doc.GetElement(levelId) as Level;
                        if (level == null) continue;
                        // Получаем тип сваи
                        var symbol = familyInstance.Symbol;
                        if (symbol == null || !symbol.IsActive)
                        {
                            if (symbol != null) symbol.Activate();
                            else continue;
                        }
                        // Вычисляем координаты
                        double newX = UnitUtils.ConvertToInternalUnits(pile.X, pile.units);
                        double newY = UnitUtils.ConvertToInternalUnits(pile.Y, pile.units);

                        var Point = new XYZ(newX, newY, locationPoint.Point.Z);


                        var newPoint = new XYZ(newX, newY, locationPoint.Point.Z);
                        // Создаем новую сваю
                        // Создаем новую сваю
                        FamilyInstance newPile = doc.Create.NewFamilyInstance(
                            newPoint, symbol, level,
                            Autodesk.Revit.DB.Structure.StructuralType.Footing);

                        pile.Pile = newPile;
                        doc.Delete(ePile.Id);


                    }
                    recreateTrans.Commit();
                }
                catch (Exception ex)
                {
                    recreateTrans.RollBack();
                    TaskDialog.Show("Ошибка", $"Ошибка обработки свай: {ex.Message}");
                    return Result.Failed;
                }
            }
            return Result.Succeeded;
        }
    }
}
