using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class PropertiesPereop : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            HelperSeach.ResetNamesParam = true;

            return Result.Succeeded;
        }
    }
}

