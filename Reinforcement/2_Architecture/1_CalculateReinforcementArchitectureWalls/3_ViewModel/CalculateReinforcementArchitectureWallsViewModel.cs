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
        private double _numberOfRowsInBricks;
        private double _stepOfTopJointsInBricks;
        private double _numberOfRowsInGasConcrete;
        private double _stepOfTopJointsInGasConcrete;

        private double _reinforcementInBricks;
        private double _numberOfJointsInBricks;
        private double _lengthOfVilatermInBricks;
        private double _reinforcementInGasConcrete;
        private double _numberOfJointsInGasConcrete;
        private double _lengthOfVilatermInGasConcrete;

        private double _numberOfSideJointsInBricks;
        private double _numberOfSideJointsInGasConcrete;
        private double _totalNumberOfSideJointsInBricks;
        private double _totalNumberOfSideJointsInGasConcrete;

        private double _rowsOfReductionReinforcementForGasConcreteWindow;
        private double _rowsOfReductionReinforcementForGasConcreteDoor;
        private double _rowsOfReductionReinforcementForBricksWindow;
        private double _rowsOfReductionReinforcementForBricksDoor;
        private double _rowsOfReductionReinforcementForBricksCurtains;
        private double _rowsOfReductionReinforcementForGasConcreteCurtains;



        #endregion


        #region Свойства
        public double NumberOfRowsInBricks
        {
            get => _numberOfRowsInBricks;
            set { _numberOfRowsInBricks = value; OnPropertyChanged(); }
        }
        public double StepOfTopJointsInBricks
        {
            get => _stepOfTopJointsInBricks;
            set { _stepOfTopJointsInBricks = value; OnPropertyChanged(); }        
        }
        public double NumberOfRowsInGasConcrete
        {
            get => _numberOfRowsInGasConcrete;
            set { _numberOfRowsInGasConcrete = value; OnPropertyChanged(); }
        }
        public double StepOfTopJointsInGasConcrete
        {
            get => _stepOfTopJointsInGasConcrete;
            set { _stepOfTopJointsInGasConcrete = value; OnPropertyChanged(); }
        }    
        public double ReinforcementInBricks
        {
            get => _reinforcementInBricks;
            set { _reinforcementInBricks = value; OnPropertyChanged(); }
        }
        public double NumberOfJointsInBricks
        {
            get => _numberOfJointsInBricks;
            set { _numberOfJointsInBricks = value; OnPropertyChanged(); }
        }
        public double LengthOfVilatermInBricks
        {
            get => _lengthOfVilatermInBricks;
            set { _lengthOfVilatermInBricks = value; OnPropertyChanged(); }
        }
        public double ReinforcementInGasConcrete
        {
            get => _reinforcementInGasConcrete;
            set { _reinforcementInGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfJointsInGasConcrete
        {
            get => _numberOfJointsInGasConcrete;
            set { _numberOfJointsInGasConcrete = value; OnPropertyChanged(); }
        }
        public double LengthOfVilatermInGasConcrete
        {
            get => _lengthOfVilatermInGasConcrete;
            set { _lengthOfVilatermInGasConcrete = value; OnPropertyChanged(); }
        }
        public double NumberOfSideJointsInBricks
        {
            get => _numberOfSideJointsInBricks;
            set { _numberOfSideJointsInBricks = value; OnPropertyChanged(); }
        }
        public double NumberOfSideJointsInGasConcrete
        {
            get => _numberOfSideJointsInGasConcrete;
            set { _numberOfSideJointsInGasConcrete = value; OnPropertyChanged(); }
        }
        public double TotalNumberOfSideJointsInBricks
        {
            get => _totalNumberOfSideJointsInBricks;
            set { _totalNumberOfSideJointsInBricks = value; OnPropertyChanged(); }
        }
        public double TotalNumberOfSideJointsInGasConcrete
        {
            get => _totalNumberOfSideJointsInGasConcrete;
            set { _totalNumberOfSideJointsInGasConcrete = value; OnPropertyChanged(); }
        }
        public double RowsOfReductionReinforcementForGasConcreteWindow
        {
            get => _rowsOfReductionReinforcementForGasConcreteWindow;
            set
            {
                _rowsOfReductionReinforcementForGasConcreteWindow = value;
                OnPropertyChanged();
            }
        }
        public double RowsOfReductionReinforcementForGasConcreteDoor
        {
            get => _rowsOfReductionReinforcementForGasConcreteDoor;
            set
            {
                _rowsOfReductionReinforcementForGasConcreteDoor = value;
                OnPropertyChanged();
            }
        }
        public double RowsOfReductionReinforcementForBricksWindow
        {
            get => _rowsOfReductionReinforcementForBricksWindow;
            set
            {
                _rowsOfReductionReinforcementForBricksWindow = value;
                OnPropertyChanged();
            }
        }
        public double RowsOfReductionReinforcementForBricksDoor
        {
            get => _rowsOfReductionReinforcementForBricksDoor;
            set
            {
                _rowsOfReductionReinforcementForBricksDoor = value;
                OnPropertyChanged();
            }
        }
        public double RowsOfReductionReinforcementForBricksCurtains
        {
            get => _rowsOfReductionReinforcementForBricksCurtains;
            set
            {
                _rowsOfReductionReinforcementForBricksCurtains = value;
                OnPropertyChanged();
            }
        }
        public double RowsOfReductionReinforcementForGasConcreteCurtains
        {
            get => _rowsOfReductionReinforcementForGasConcreteCurtains;
            set
            {
                _rowsOfReductionReinforcementForGasConcreteCurtains = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public void Calculate()
        {

            
            Document doc = RevitAPI.Document;
            View activeView = doc.ActiveView;
            var activeViewId = activeView.Id;
          
            // 1. Получаем экземпляры всех стен на активном виде
            var wallsCollector = new FilteredElementCollector(doc, activeViewId).OfClass(typeof(Wall)).WhereElementIsNotElementType().OfType<Wall>().ToList();

            // Разделяем на отдельные списки по типу стен
            var bricksWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Кирпич")).ToList();
            var gasConcreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Газобетон")).ToList();
            var concreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Бетон")).ToList();

            // Получаем витражные стены
            var curtainWallsCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(Wall))
            .WhereElementIsNotElementType()
            .Cast<Wall>()
            .Where(w =>
            {
                WallType type = doc.GetElement(w.GetTypeId()) as WallType;
                return type != null && type.Kind == WallKind.Curtain;
            }).ToList();
             

            // 2. Получаем экземпляры дверей в различных типах стен
            var doorInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            var doorInGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон");
            }).ToList();

            // 3. Получаем экземпляры окон в различных типах окон
            var windowsInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            var windowsInGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон");
            }).ToList();

            // 4. Считаем сколько раз АР стены касаются монолитных стен
            int numberOfBricksWallsTouchnig = CalculateNumbersOfWallsTouching(bricksWallsCollector, concreteWallsCollector);
            int numberOfGasConcreteWallsTouchnig = CalculateNumbersOfWallsTouching(gasConcreteWallsCollector, concreteWallsCollector);

            // 5. Считаем суммарную длину стен
            double sumBricksWallsLength = CalculateWallsLength(bricksWallsCollector);
            double sumGasConcreteWallsLength = CalculateWallsLength(gasConcreteWallsCollector);

            // 6. Считаем дополнительное армирование на окна и двери в газобетоне
            double additionalLengthReinforcementForGasConcrecteWindow = CalculateTotalOpeningsWidth(windowsInGasConcreteWallsCollector) + (1.5 * windowsInGasConcreteWallsCollector.Count);
            double additionalLengthReinforcementForGasConcrecteDoor = 0.5 * doorInGasConcreteWallsCollector.Count;

            // 7. Считаем уменьшение армирование из-за вырезания проемами
            double reductionReinforcementForGasConcreteWindow = CalculateTotalOpeningsWidth(windowsInGasConcreteWallsCollector) * RowsOfReductionReinforcementForGasConcreteWindow;
            double reductionReinforcementForGasConcreteDoor = CalculateTotalOpeningsWidth(doorInGasConcreteWallsCollector) * RowsOfReductionReinforcementForGasConcreteDoor;
            double reductionReinforcementForBricksWindow = CalculateTotalOpeningsWidth(windowsInBricksWallsCollector) * RowsOfReductionReinforcementForBricksWindow;
            double reductionReinforcementForBricksDoor = CalculateTotalOpeningsWidth(doorInBricksWallsCollector) * RowsOfReductionReinforcementForBricksDoor;
            double reductionReinforcementForBricksCurtains = LengthOfCurtainWalls(curtainWallsCollector, bricksWallsCollector) * RowsOfReductionReinforcementForBricksCurtains;
            double reductionReinforcementForGasConcreteCurtains = LengthOfCurtainWalls(curtainWallsCollector, gasConcreteWallsCollector) * RowsOfReductionReinforcementForGasConcreteCurtains;

            // 8. Расчет и вывод итоговых результатов для пользователя
            ReinforcementInBricks = (sumBricksWallsLength * NumberOfRowsInBricks) - reductionReinforcementForBricksDoor - reductionReinforcementForBricksWindow
                + (numberOfBricksWallsTouchnig * NumberOfSideJointsInBricks)*2 - reductionReinforcementForBricksCurtains;
            ReinforcementInGasConcrete = (sumGasConcreteWallsLength * NumberOfRowsInGasConcrete) - reductionReinforcementForGasConcreteDoor - reductionReinforcementForGasConcreteWindow 
                + additionalLengthReinforcementForGasConcrecteDoor + additionalLengthReinforcementForGasConcrecteWindow - reductionReinforcementForGasConcreteCurtains;

            NumberOfJointsInBricks = CalculateTopJoints(bricksWallsCollector, StepOfTopJointsInBricks);
            NumberOfJointsInGasConcrete = CalculateTopJoints(gasConcreteWallsCollector, StepOfTopJointsInGasConcrete);

            TotalNumberOfSideJointsInBricks = numberOfBricksWallsTouchnig * NumberOfSideJointsInBricks;
            TotalNumberOfSideJointsInGasConcrete = numberOfGasConcreteWallsTouchnig * NumberOfSideJointsInGasConcrete;

            LengthOfVilatermInBricks = sumBricksWallsLength*2;
            LengthOfVilatermInGasConcrete = sumGasConcreteWallsLength * 2;
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
        private bool IsPointInsideBoundingBox(XYZ point, BoundingBoxXYZ box)
        {
            return point.X >= box.Min.X && point.X <= box.Max.X &&
                   point.Y >= box.Min.Y && point.Y <= box.Max.Y &&
                   point.Z >= box.Min.Z && point.Z <= box.Max.Z;
        }

        public double LengthOfCurtainWalls (List<Wall> curtainWalls, List<Wall> hostWalls)
        {
            double length = 0.0;

            foreach (Wall curtainWall in curtainWalls)
            {
                BoundingBoxXYZ curtainBox = curtainWall.get_BoundingBox(null);
                if (curtainBox == null) continue;

                XYZ curtainCenter = (curtainBox.Min + curtainBox.Max) * 0.5;

                foreach (Wall hostWall in hostWalls)
                {
                    BoundingBoxXYZ hostBox = hostWall.get_BoundingBox(null);
                    if (hostBox == null) continue;

                    if (IsPointInsideBoundingBox(curtainCenter, hostBox))
                    {
                        // Считаем длину витражной стены
                        LocationCurve locCurve = curtainWall.Location as LocationCurve;
                        if (locCurve != null)
                        {
                            double lengthFt = locCurve.Curve.Length;
                            double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthFt, UnitTypeId.Meters);
                            length += lengthInMeters;
                        }
                        break;
                    }
                }
            }
            return length;
        }
    }
}
