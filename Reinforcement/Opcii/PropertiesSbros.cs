using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    
        public class PropertiesSbros : IExternalCommand
        {
            public Result Execute(
                ExternalCommandData commandData,
                ref string message,
                ElementSet elements)
            {
            RevitAPI.Initialize(commandData);
            //сброс поисковых параметров статики
            HelperSeach.PastElements.Clear();
            HelperSeachAllElements.newNames.Clear();
            return Result.Succeeded;
            }
        }
    }

