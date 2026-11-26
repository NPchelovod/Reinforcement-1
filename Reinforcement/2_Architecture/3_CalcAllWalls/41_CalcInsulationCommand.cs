using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YourNamespace;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CalcInsulationCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;

            Document doc = uidoc.Document;
            var lw = CalcWalls.GetWalls(doc);                // ваш текущий метод
            if (lw == null || lw.Count == 0) return Result.Succeeded;

            // уже на входе — стены, отфильтрованные как «наружные»
            var chains = ECWallPerimeterChainBuilder.Build(lw);

            if (chains.Count == 0) return Result.Succeeded;

            var outer = chains[0];                  // наружный контур (сортировка по площади)
            if (!outer.IsClockwise)
                outer = ECWallPerimeterChainBuilder.BuildOuterClockwise(lw); // принудительно по часовой

            // outer.Walls — стены наружного контура в порядке обхода по часовой.
            // outer.IsClosed — замкнут ли контур.
            // chains[1..] — прочие цепочки (внутренние/тупиковые) — их можно игнорировать.
            return Result.Succeeded;
        }
    }
}
