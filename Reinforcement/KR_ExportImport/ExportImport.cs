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
    public class ExportImport : IExternalCommand
    {


        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);

            return Result.Succeeded;
        }
    }
}
