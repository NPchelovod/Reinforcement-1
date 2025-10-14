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


// Выноска СПДС - начало
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class NoteLineCommand35mm : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;

            

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
             "ЕС_Аннотация_Текст_Выноска_3,5мм"
         };
       

    }
}

