using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Dimensions_MultipleOf100 : IExternalCommand
    {
        private const long Step1 = 100; // шаг кратности в мм
        private const long Min1 = 200; // строго больше этого

        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            return App_Dimensions_MultipleOf200.Exec(commandData,
                              ref message,
                              elements, Step1, Min1);
        }
    }
}