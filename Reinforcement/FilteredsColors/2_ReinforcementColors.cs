using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ReinforcementColors : IExternalCommand
    {
        public static readonly List<ColorFilterRule> GetColorsArm = new List<ColorFilterRule>
        {
            // Id, Имя, Цвет
            new ColorFilterRule(3956171, "D6",  new Color(153, 153, 0)),
            new ColorFilterRule(3956184, "D8",  new Color(0, 127, 255)),
            new ColorFilterRule(3956173, "D10", new Color(102, 204, 0)),
            new ColorFilterRule(3956174, "D12", new Color(255, 0, 0)),
            new ColorFilterRule(3956175, "D14", new Color(255, 127, 127)),
            new ColorFilterRule(3956176, "D16", new Color(0, 255, 255)),
            new ColorFilterRule(3956177, "D18", new Color(255, 127, 223)),
            new ColorFilterRule(3956178, "D20", new Color(159, 127, 255)),
            new ColorFilterRule(3956179, "D22", new Color(0, 153, 0)),
            new ColorFilterRule(3956180, "D25", new Color(0, 0, 255)),
            new ColorFilterRule(3956181, "D28", new Color(255, 127, 0)),
            new ColorFilterRule(3956182, "D32", new Color(204, 102, 102)),
            new ColorFilterRule(3956183, "D36", new Color(255, 0, 255)),
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            var doc = commandData.Application.ActiveUIDocument.Document;

            // Просто применяем наши правила. 
            // Метод сам найдет их по ID или по имени (если ID не совпадет)
            ViewFilterColorizer.ApplyByRules(
                doc,
                doc.ActiveView,
                GetColorsArm,
                "Применение цветов арматуры");

            return Result.Succeeded;
        }
    }
}