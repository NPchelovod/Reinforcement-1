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
    //выгруз эксренный всех статистик и данныех
    
    [Transaction(TransactionMode.Manual)]
    public class FlushOut : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            App_Apdater_1.LookUsers.ForceFlush(closeRevit: false);
            return Result.Succeeded;
        }
    }
}
