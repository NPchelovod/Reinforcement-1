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
// стрелка вида
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ArrowViewCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            

            string Type_seach = "Symbols";

            try 
            {
                Utilit_1_1_Depth_Seach.GetResult(doc, uidoc, list_Name, Type_seach);
            }
            catch (Exception)
            {
                return Result.Failed;
            }
            return Result.Succeeded;


        }
        public static HashSet<string> list_Name = new HashSet<string>
        {
            "ЕС_ОбозначениеВида",
            "ЕС_Обозначение вида",
            "ЕС_Обозначение Вида",
            "ЕС_О_Обозначение вида"
        };

    }
}

