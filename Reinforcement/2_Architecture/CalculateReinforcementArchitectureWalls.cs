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
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CalculateReinforcementArchitectureWalls : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // 0. Инициализация Revit API
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }

            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;
            var activeViewId = activeView.Id;

            // 1. Получаем экземпляры стен кирпичных и газобетонных стен на активном виде
            var wallsCollector = new FilteredElementCollector(doc, activeViewId).OfClass(typeof(Wall)).WhereElementIsNotElementType().OfType<Wall>().ToList();

            // Разделяем на отдельные списки
            var bricksWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Кирпич")).ToList();
            var gasConcreteWallsCollector = wallsCollector.Where(wall => wall.WallType.Name.Contains("Газобетон")).ToList();

            // 2. Получаем экземпляры дверей
            // В кирпичных стенах
            var doorInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            // В газобетонных стенах
            var doorInGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(door =>
            {
                var host = door.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон");
            }).ToList();

            // 3. Получаем экземпляры окон
            // В кирпичных стенах
            var windowsInBricksWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Кирпич");
            }).ToList();

            // В газобетонных стенах
            var windowsInGasConcreteWallsCollector = new FilteredElementCollector(doc, activeViewId).OfCategory(BuiltInCategory.OST_Windows).WhereElementIsNotElementType().Cast<FamilyInstance>().Where(window =>
            {
                var host = window.Host as Wall;
                return host != null && host.WallType.Name.Contains("Газобетон");
            }).ToList();

            // 4. Объявление основных переменных
            // Объявляем переменные для кирпичных стен
            double sumBricksWallsLength = 0;
            double numberDetailsOnTheTopOfTheBricksWalls = 0;
            double numberDetailsOnTheSideOfTheBricksWalls = 0;
            double numberStaticBricksReinforcementRows = 3;


            // Объявляем переменные для газобетонных стен
            double sumGasConcreteWallsLength = 0;
            double numberDetailsOnTheTopOfTheGasWalls = 0;
            double numberDetailsOnTheSideOfTheGasConcreteWalls = 0;
            double numberGasConcreteReinforcementRows = 5;

            // 5. Получаем максимальную высоту стен
            var maxWallsHeigthInFeet = wallsCollector.OrderByDescending(wall => wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble()).First()
                .get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            var maxWallsHeigthInMeters = UnitUtils.ConvertFromInternalUnits(maxWallsHeigthInFeet, UnitTypeId.Meters);

            // 6. Количество дополнительных рядов армирования, в зависимости от высоты
            double numberAdditionalBricksReinforcementRows = Math.Floor((maxWallsHeigthInMeters - 0.175) / 0.375) - 1;


            // 5. Получаем максимальную высоту стен

            //double areaInSquareFeet = door.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
            //double areaInSquareMeters = UnitUtils.ConvertFromInternalUnits(areaInSquareFeet, UnitTypeId.SquareMeters);

            foreach (var wall in bricksWallsCollector)
            {
                Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.StorageType == StorageType.Double)
                {
                    double lengthInFeet = lengthParam.AsDouble();
                    double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthInFeet, UnitTypeId.Meters);
                    sumBricksWallsLength += lengthInMeters;
                    numberDetailsOnTheTopOfTheBricksWalls += Math.Ceiling(lengthInMeters);
                }
            }

            //foreach (var wall in gasConcreteWallsCollector)
            //{
            //    Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            //    if (lengthParam != null && lengthParam.StorageType == StorageType.Double)
            //    {
            //        double lengthInFeet = lengthParam.AsDouble();
            //        double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthInFeet, UnitTypeId.Meters);
            //        sumWallsLength += lengthInMeters;
            //        numberDetailsOnTheTopOfTheWalls += Math.Ceiling(lengthInMeters);
            //    }
            //}

            TaskDialog.Show("Результаты расчета армирования кирпичных стен", $"Суммарная длина стен: {sumBricksWallsLength:F2} м\n" +
                $"Общая длина вилатерма: {sumBricksWallsLength * 2:F2}  м\n" +
                $"Общее количество монтажных деталей по верху стены: {numberDetailsOnTheTopOfTheBricksWalls}  шт\n" +
                $"Общая длина армирования: {sumBricksWallsLength * (numberStaticBricksReinforcementRows + numberAdditionalBricksReinforcementRows)}  м \n" +
                $"Высота стен, принятая для расчета: {maxWallsHeigthInMeters:F2} м\n" +
                $"Количество рядов армирования по верху стены {numberStaticBricksReinforcementRows} шт \n" +
                $"Количество рядов армирования по остальной высоте стены {numberAdditionalBricksReinforcementRows} шт \n");

            // Вывод результатов расчета
            // TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");


            return Result.Succeeded;



        }
    }
}
