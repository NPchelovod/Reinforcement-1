using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI.Selection;
using Reinforcement;
using System;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class RoundDistanceForOpeningsInSlabs : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            
            // Конвертируем 5 мм в футы
            const double roundingStepInMm = 5.0;
            double roundingStepInFeet = UnitUtils.ConvertToInternalUnits(
                roundingStepInMm,
                UnitTypeId.Millimeters
            );

            // Получаем внутреннее начало координат
            XYZ internalOrigin = GetInternalOrigin(doc);

            // ПРЕДЛАГАЕМ ПОЛЬЗОВАТЕЛЮ ВЫБРАТЬ ЭЛЕМЕНТЫ
            IList<Element> selectedCubes;
            try
            {
                // Создаем фильтр для выбора только нужных семейств
                var filter = new FamilyInstanceSelectionFilter("Кубик_Перекрытие_Прямоугольный");

                // Выбор прямоугольной областью
                TransparentNotificationWindow.ShowNotification("Выберите кубики в перекрытии", uidoc, 5);
                selectedCubes = uidoc.Selection.PickElementsByRectangle(
                    filter,
                    "Выберите элементы 'Кубик_Перекрытие_Прямоугольный'"
                );

                if (selectedCubes.Count == 0)
                    return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            using (Transaction trans = new Transaction(doc, "Округление позиций кубов"))
            {
                trans.Start();

                var results = new List<string>();
                int movedCount = 0;

                foreach (Element element in selectedCubes)
                {
                    if (element is FamilyInstance cube && cube.Location is LocationPoint location)
                    {
                        XYZ currentPosition = location.Point;                      

                        // Вычисляем смещение
                        XYZ offset = currentPosition - internalOrigin;

                        // Округляем компоненты
                        double roundedX = RoundValue(offset.X, roundingStepInFeet);
                        double roundedY = RoundValue(offset.Y, roundingStepInFeet);
                        double roundedZ = RoundValue(offset.Z, roundingStepInFeet);

                        // Новая позиция
                        XYZ newPosition = new XYZ(
                            internalOrigin.X + roundedX,
                            internalOrigin.Y + roundedY,
                            internalOrigin.Z + roundedZ
                        );

                        // Перемещаем при изменении позиции
                        if (!currentPosition.IsAlmostEqualTo(newPosition))
                        {
                            ElementTransformUtils.MoveElement(doc, cube.Id, newPosition - currentPosition);
                            movedCount++;

                            // Расчет расстояния для отчета
                            double distanceInMm = UnitUtils.ConvertFromInternalUnits(
                                newPosition.DistanceTo(internalOrigin),
                                UnitTypeId.Millimeters
                            );
                            results.Add($"ID {cube.Id}: {distanceInMm:0.00} мм");
                        }
                        // Поворачиваем элемент для Ведомости отверстий КР
                        if (cube.FacingOrientation.IsAlmostEqualTo(XYZ.BasisX) || cube.FacingOrientation.IsAlmostEqualTo(-XYZ.BasisX))
                        {
                            LocationPoint newLocation = cube.Location as LocationPoint;
                            XYZ position = location.Point;
                            Line axis = Line.CreateUnbound(position, XYZ.BasisZ);
                            double angle = Math.PI / 2;
                            ElementTransformUtils.RotateElement(doc, cube.Id, axis, angle);
                            Parameter paramWidth = cube.LookupParameter("Ширина");
                            Parameter paramLength = cube.LookupParameter("Длина");
                            double tempLength = paramLength.AsDouble();
                            paramLength.Set(paramWidth.AsDouble());
                            paramWidth.Set(tempLength);
                        }

                        if (cube.FacingOrientation.IsAlmostEqualTo(-XYZ.BasisY))
                        {
                            LocationPoint newLocation = cube.Location as LocationPoint;
                            XYZ position = location.Point;
                            Line axis = Line.CreateUnbound(position, XYZ.BasisZ);
                            double angle = Math.PI;
                            ElementTransformUtils.RotateElement(doc, cube.Id, axis, angle);
                        }

                    }
                }

                trans.Commit();

                // Результаты
                if (movedCount > 0)
                    TaskDialog.Show("Успех", $"Перемещено {movedCount} объектов:\n" + string.Join("\n", results));
                else
                    TaskDialog.Show("Информация", "Выбранные элементы не требуют перемещения");
            }

            return Result.Succeeded;
        }

        // Округление значения
        private double RoundValue(double value, double step)
        {
            return System.Math.Round(value / step) * step;
        }

        // Получение внутреннего начала координат
        private XYZ GetInternalOrigin(Document doc)
        {
            BasePoint basePoint = new FilteredElementCollector(doc)
                .OfClass(typeof(BasePoint))
                .Cast<BasePoint>()
                .FirstOrDefault(bp => !bp.IsShared);

            return basePoint?.Position ?? XYZ.Zero;
        }
    }

    // Класс фильтра для выбора элементов
    public class FamilyInstanceSelectionFilter : ISelectionFilter
    {
        private readonly string _familyNamePart;

        public FamilyInstanceSelectionFilter(string familyNamePart)
        {
            _familyNamePart = familyNamePart;
        }

        public bool AllowElement(Element elem)
        {
            // Разрешаем только экземпляры семейств с заданным именем
            if (elem is FamilyInstance fi)
            {
                return fi.Symbol?.Family?.Name.Contains(_familyNamePart) == true;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false; // Работаем только с элементами, не с гранями
        }
    }
}

