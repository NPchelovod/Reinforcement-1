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
//using View = Autodesk.Revit.DB.View;

//namespace Reinforcement
//{
//    [Transaction(TransactionMode.Manual)]
//    public class CalculateReinforcementArchitectureWallsCommand : IExternalCommand
//    {
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            // 1. Создаем VM и окно
//            var inputVm = new InputViewModelCalculateReinforcementArchitectureWalls();
//            var inputWindow = new InputViewCalculateReinforcementArchitectureWalls { DataContext = inputVm };

//            var result = inputWindow.ShowDialog();

//            if (result == true)
//                return Result.Cancelled;

//            // 2. Получаем данные от VM
//            var userData = inputVm.InputData;

//            // 3. Выполняем расчет
//            var calculationModel = new ModelCalculateReinforcementArchitectureWalls();
//            var calcResult = calculationModel.Calculate(userData, commandData.Application);

//            // 4. Показываем окно с результатами
//            var resultVm = new ResultViewModelCalculateReinforcementArchitectureWalls(calcResult);
//            var resultWindow = new ResultViewCalculateReinforcementArchitectureWalls { DataContext = resultVm };

//            return Result.Succeeded;

//        }
//    }
//}
