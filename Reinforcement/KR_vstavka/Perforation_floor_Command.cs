#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


#endregion

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Perforation_floor_Command : IExternalCommand
    {

        public static HashSet<string> list_Name = new HashSet<string>
        {
            "ЕС_Отверстие прямоугольное_В перекрытии",
            "Кубик_Перекрытие_Прямоугольный"
        };
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;



            ElementTypeOrSymbol Type_seach = ElementTypeOrSymbol.ElementType;

            try
            {
                Utilit_1_1_Depth_Seach.GetResult(list_Name, Type_seach);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        
    }
}

