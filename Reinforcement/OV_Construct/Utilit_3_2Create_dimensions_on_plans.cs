using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;




//тут в общем виде 

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class Utilit_3_2Create_dimensions_on_plans
    {
        public static Result Create_new_floor(Dictionary<string, List<string>> Dict_sovpad_level, ForgeTypeId units, ref string message, ElementSet elements, Document doc)
        {
            return Result.Succeeded;
        }
    }
}