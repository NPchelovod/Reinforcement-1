using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ZacladInSlab : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;



            ElementTypeOrSymbol Type_seach = ElementTypeOrSymbol.Symbol;

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
        public static HashSet<string> list_Name = new HashSet<string>
        {
            "ЕС_Закладная ЭЛ в плите"
        };
        public static HashSet<string> Type_Name = new HashSet<string>
        {
            "А500С"
        };
    }
}
