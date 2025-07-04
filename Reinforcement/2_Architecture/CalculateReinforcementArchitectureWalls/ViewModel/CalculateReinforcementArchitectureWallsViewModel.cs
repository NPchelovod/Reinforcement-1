using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;



namespace Reinforcement
{
    public class CalculateReinforcementArchitectureWallsViewModel : INotifyPropertyChanged
    {
        #region Поля
        private double _numbersOfRowsInBricks;
        private double _stepOfTopJointsInBricks;
        private double _numbersOfRowsInInternalGasConcrete;
        private double _stepOfTopJointsInInternalGasConcrete;
        private double _numbersOfRowsInExternalGasConcrete;
        private double _stepOfTopJointsInExternalGasConcrete;

        private double _reinforcementInBricks;
        private double _numbersOfJointsInBricks;
        private double _lengthOfVilatermInBricks;
        private double _reinforcementInInternalGasConcrete;
        private double _numberOfJointsInInternalGasConcrete;
        private double _lengthOfVilatermInInternalGasConcrete;
        private double _reinforcementInExternalGasConcrete;
        private double _numberOfJointsInExternalGasConcrete;
        private double _lengthOfVilatermInExternalGasConcrete;

        private double _numbersOfSideJointsInBricks;
        private double _numberOfSideJointsInInternalGasConcrete;
        private double _numberOfSideJointsInExternalGasConcrete;
        #endregion


        #region Свойства
        public double NumbersOfRowsInBricks
        {
            get => _numbersOfRowsInBricks;
            set { _numbersOfRowsInBricks = value; OnPropertyChanged(); }
        }
        public double StepOfTopJointsInBricks
        {
            get => _stepOfTopJointsInBricks;
            set { _stepOfTopJointsInBricks = value; OnPropertyChanged(); }        }
        public double NumbersOfRowsInInternalGasConcrete
        {
            get => _numbersOfRowsInInternalGasConcrete;
            set { _numbersOfRowsInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double StepOfTopJointsInInternalGasConcrete
        {
            get => _stepOfTopJointsInInternalGasConcrete;
            set { _stepOfTopJointsInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumbersOfRowsInExternalGasConcrete
        {
            get => _numbersOfRowsInExternalGasConcrete;
            set { _numbersOfRowsInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double StepOfTopJointsInExternalGasConcrete
        {
            get => _stepOfTopJointsInExternalGasConcrete;
            set { _stepOfTopJointsInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double ReinforcementInBricks
        {
            get => _reinforcementInBricks;
            set { _reinforcementInBricks = value; OnPropertyChanged(); }
        }
        public double NumberOfJointsInBricks
        {
            get => _numbersOfJointsInBricks;
            set { _numbersOfJointsInBricks = value; OnPropertyChanged(); }
        }
        public double LengthOfVilatermInBricks
        {
            get => _lengthOfVilatermInBricks;
            set { _lengthOfVilatermInBricks = value; OnPropertyChanged(); }
        }
        public double ReinforcementInInternalGasConcrete
        {
            get => _reinforcementInInternalGasConcrete;
            set { _reinforcementInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfJointsInInternalGasConcrete
        {
            get => _numberOfJointsInInternalGasConcrete;
            set { _numberOfJointsInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double LengthOfVilatermInInternalGasConcrete
        {
            get => _lengthOfVilatermInInternalGasConcrete;
            set { _lengthOfVilatermInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double ReinforcementInExternalGasConcrete
        {
            get => _reinforcementInExternalGasConcrete;
            set { _reinforcementInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfJointsInExternalGasConcrete
        {
            get => _numberOfJointsInExternalGasConcrete;
            set { _numberOfJointsInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double LengthOfVilatermInExternalGasConcrete
        {
            get => _lengthOfVilatermInExternalGasConcrete;
            set { _lengthOfVilatermInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfSideJointsInBricks
        {
            get => _numbersOfSideJointsInBricks;
            set { _numbersOfSideJointsInBricks = value; OnPropertyChanged(); }
        }
        public double NumberOfSideJointsInInternalGasConcrete
        {
            get => _numberOfSideJointsInInternalGasConcrete;
            set { _numberOfSideJointsInInternalGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfSideJointsInExternalGasConcrete
        {
            get => _numberOfSideJointsInExternalGasConcrete;
            set { _numberOfSideJointsInExternalGasConcrete = value; OnPropertyChanged(); }
        }
        #endregion

        public void Calculate()
        {
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            View activeView = doc.ActiveView;
            var activeViewId = activeView.Id;
          
            // 1. Получаем экземпляры всех стен на активном виде
            var wallsCollector = new FilteredElementCollector(doc, activeViewId).OfClass(typeof(Wall)).WhereElementIsNotElementType().OfType<Wall>().ToList();

            // Разделяем на отдельные списки по типу стен
            var bricksWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Кирпич")).ToList();
            var gasInternalConcreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Газобетон внутренний")).ToList();
            var gasExternalConcreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Газобетон наружный")).ToList();
            var concreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Бетон")).ToList();

            // 2. Получаем экземпляры дверей в различных типах стен
            var doorInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            var doorInInternalGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон внутренний");
            }).ToList();

            var doorInExternalGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон наружный");
            }).ToList();

            // 3. Получаем экземпляры окон в различных типах окон
            var windowsInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            var windowsInInternalGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон внутренний");
            }).ToList();

            var windowsInExternalGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон наружный");
            }).ToList();

            // 4. Считаем сколько раз АР стены касаются монолитных стен
            int numberOfBricksWallsTouchnig = CalculateNumbersOfWallsTouching(bricksWallsCollector, concreteWallsCollector);
            int numberOfInternalGasConcreteWallsTouchnig = CalculateNumbersOfWallsTouching(gasInternalConcreteWallsCollector, concreteWallsCollector);
            int numberOfExternalGasConcreteWallsTouchnig = CalculateNumbersOfWallsTouching(gasExternalConcreteWallsCollector, concreteWallsCollector);

            // 5. Считаем суммарную длину стен
            double sumBricksWallsLength = CalculateWallsLength(bricksWallsCollector);
            double sumInternalGasConcreteWallsLength = CalculateWallsLength(gasInternalConcreteWallsCollector);
            double sumExternalGasConcreteWallsLength = CalculateWallsLength(gasExternalConcreteWallsCollector);

            // 6. Считаем дополнительное армирование на окна и двери в газобетоне
            double additionalLengthReinforcementForInternalGasConcrecteWindow = CalculateTotalOpeningsWidth(windowsInInternalGasConcreteWallsCollector) + (1.5 * windowsInInternalGasConcreteWallsCollector.Count);
            double additionalLengthReinforcementForInternalGasConcrecteDoor = 0.5 * doorInInternalGasConcreteWallsCollector.Count;
            double additionalLengthReinforcementForExternalGasConcrecteWindow = CalculateTotalOpeningsWidth(windowsInExternalGasConcreteWallsCollector) + (1.5 * windowsInExternalGasConcreteWallsCollector.Count);
            double additionalLengthReinforcementForExternalGasConcrecteDoor = 0.5 * doorInExternalGasConcreteWallsCollector.Count;


            // 6. Считаем уменьшение армирование из-за вырезания проемами
            double reductionReinforcementForInternalGasConcreteWindow = CalculateTotalOpeningsWidth(windowsInInternalGasConcreteWallsCollector) * 2;
            double reductionReinforcementForInternalGasConcreteDoor = CalculateTotalOpeningsWidth(doorInInternalGasConcreteWallsCollector) * 2;
            double reductionReinforcementForExternalGasConcreteWindow = CalculateTotalOpeningsWidth(windowsInExternalGasConcreteWallsCollector) * 2;
            double reductionReinforcementForExternalGasConcreteDoor = CalculateTotalOpeningsWidth(doorInExternalGasConcreteWallsCollector) * 2;
            double reductionReinforcementForBricksWindow = CalculateTotalOpeningsWidth(windowsInBricksWallsCollector) * 4;
            double reductionReinforcementForBricksDoor = CalculateTotalOpeningsWidth(doorInBricksWallsCollector) * 5;

            // 7. Расчет и вывод итоговых результатов для пользователя
            ReinforcementInBricks = (sumBricksWallsLength * NumbersOfRowsInBricks) - reductionReinforcementForBricksDoor - reductionReinforcementForBricksWindow + (numberOfBricksWallsTouchnig * NumberOfSideJointsInBricks)*2;
            ReinforcementInExternalGasConcrete = (sumExternalGasConcreteWallsLength * NumbersOfRowsInExternalGasConcrete) - reductionReinforcementForExternalGasConcreteDoor - reductionReinforcementForExternalGasConcreteWindow 
                + additionalLengthReinforcementForExternalGasConcrecteWindow + additionalLengthReinforcementForExternalGasConcrecteDoor;
            ReinforcementInInternalGasConcrete = (sumInternalGasConcreteWallsLength * NumbersOfRowsInInternalGasConcrete) - reductionReinforcementForInternalGasConcreteDoor - reductionReinforcementForInternalGasConcreteWindow 
                + additionalLengthReinforcementForInternalGasConcrecteDoor + additionalLengthReinforcementForInternalGasConcrecteWindow;

            NumberOfJointsInBricks = CalculateTopJoints(bricksWallsCollector, StepOfTopJointsInBricks) + numberOfBricksWallsTouchnig * NumberOfSideJointsInBricks;
            NumberOfJointsInExternalGasConcrete = CalculateTopJoints(gasExternalConcreteWallsCollector, StepOfTopJointsInExternalGasConcrete) + numberOfExternalGasConcreteWallsTouchnig * NumberOfSideJointsInExternalGasConcrete;
            NumberOfJointsInInternalGasConcrete = CalculateTopJoints(gasInternalConcreteWallsCollector, StepOfTopJointsInInternalGasConcrete) + numberOfInternalGasConcreteWallsTouchnig * NumberOfSideJointsInInternalGasConcrete;

            LengthOfVilatermInBricks = sumBricksWallsLength*2;
            LengthOfVilatermInInternalGasConcrete = sumInternalGasConcreteWallsLength * 2;
            LengthOfVilatermInExternalGasConcrete = sumExternalGasConcreteWallsLength * 2;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public double CalculateWallsLength(List<Wall> walls)
        {
            double wallsLength = 0;

            foreach (var wall in walls)
            {
                Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.StorageType == StorageType.Double)
                {
                    double lengthInFeet = lengthParam.AsDouble();
                    double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthInFeet, UnitTypeId.Meters);
                    wallsLength += lengthInMeters;                
                }
            }
            return wallsLength;
        }
        public double CalculateTotalOpeningsWidth(List<FamilyInstance> openings)
        {
            double totalOpeningsWidth = 0;
            foreach (var opening in openings)
            {
                
                double openingLength = opening.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                double openingLengthInMeters = UnitUtils.ConvertFromInternalUnits(openingLength, UnitTypeId.Meters);

                totalOpeningsWidth += openingLengthInMeters;
            }
            return totalOpeningsWidth;
        }

        public int CalculateTopJoints(List<Wall> walls, double step)
        {
            int topJoints = 0;
            foreach (var wall in walls)
            {
                Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.StorageType == StorageType.Double)
                {
                    double lengthInFeet = lengthParam.AsDouble();
                    double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthInFeet, UnitTypeId.Meters);
                    if (lengthInMeters < 3 && lengthInMeters > 0.5)
                    {
                        topJoints += 2;
                    }
                    if (lengthInMeters >= 3)
                    {
                        lengthInMeters = (int)(Math.Floor(lengthInMeters));
                        topJoints += (int)(Math.Ceiling(lengthInMeters / step));
                    }
                }
            }
            return topJoints;
        }

        public bool IsTouchingConcreteWall(Wall wall1, Wall wall2)
        {
            // Получаем BoundingBox первой стены
            BoundingBoxXYZ bb1 = wall1.get_BoundingBox(null);
            Outline outline1 = new Outline(bb1.Min, bb1.Max);

            // Получаем BoundingBox второй стены
            BoundingBoxXYZ bb2 = wall2.get_BoundingBox(null);
            Outline outline2 = new Outline(bb2.Min, bb2.Max);

            if (outline1.Intersects(outline2, 0.001)) // Допуск на точность
            {
                return true;
            }
            return false;
        }

        public int CalculateNumbersOfWallsTouching(List<Wall> walls1, List<Wall> walls2)
        {
            int numberOfTouching = 0; ;
            foreach (var wall1 in walls1)
            {
                foreach (var wall2 in walls2)
                {
                    if (IsTouchingConcreteWall(wall1, wall2))
                    {
                        numberOfTouching++;
                    }
                }
            }
            return numberOfTouching;
        }
    }
}
