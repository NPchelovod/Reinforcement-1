using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class OpeningsColors : IExternalCommand
    {
        public static readonly List<ColorFilterRule> GetColorsOpenings = new List<ColorFilterRule>
        {
            // Id, Имя (если Id=0), Цвет
            new ColorFilterRule(13177742, "Отверстия_АПТ",        new Color(200, 0, 100)),
            new ColorFilterRule(10119785, "Отверстия_АР",         new Color(255, 128, 0)),
            new ColorFilterRule(10015139, "Отверстия_Вентканал",  new Color(0, 0, 255)),
            new ColorFilterRule(6364113,  "Отверстия_ВК",         new Color(0, 127, 0)),
            new ColorFilterRule(10013253, "Отверстия_КЖ",         new Color(191, 0, 255)),
            new ColorFilterRule(6364114,  "Отверстия_ОВ",         new Color(165, 165, 0)),
            new ColorFilterRule(10119788, "Отверстия_Перфорация", new Color(127, 191, 255)),
            new ColorFilterRule(6364116,  "Отверстия_СС",         new Color(255, 128, 0)),
            new ColorFilterRule(6364115,  "Отверстия_ЭОМ",        new Color(255, 63, 0)),
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            var doc = commandData.Application.ActiveUIDocument.Document;

            ViewFilterColorizer.ApplyByRules(
                doc,
                doc.ActiveView,
                GetColorsOpenings,
                "Применение цветов отверстий");

            return Result.Succeeded;
        }
    }
}