using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class CalcInsulationCommand : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;

            Document doc = uidoc.Document;
            ExecuteLogic(doc);
            // outer.Walls — стены наружного контура в порядке обхода по часовой.
            // outer.IsClosed — замкнут ли контур.
            // chains[1..] — прочие цепочки (внутренние/тупиковые) — их можно игнорировать.
            return Result.Succeeded;
        }
        private static double minWidthM = 0.1;// минимальная толщина в метрах - 10 см, иначе это явно не наружная стена

        public static HashSet<EWallTypes> NotOuterWalls = new HashSet<EWallTypes>() { EWallTypes.AR_Vitrage, EWallTypes.AR_VentFasad, EWallTypes.AR_Uteplitel };
        public static void ExecuteLogic(Document doc)
        {
            var lw = CalcWalls.GetWalls(doc);                // ваш текущий метод - получаем все стены



            if (lw == null || lw.Count == 0) return;

            //фильтруем стены - Не внутринние, и не Вентфасад
            //С     КР СТЕНАМИ могут быть проблемки
            lw = lw.Where(x=>!x.ECInsulation.InWallFromNAmeMaterial && !NotOuterWalls.Contains(x.EWallTypes)&& x.WidthM> minWidthM).ToList();

            //теперь данные стены группируем по уровням

            var groupsLevel = lw.GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key)
                .ToList();

            var answerWall = new List<List<IExportStringDoubleData>>();
            var answerIsolWall = new List<List<IExportStringDoubleData>>();
            var answerDoors = new List<List<IExportStringDoubleData>>();
            var answerWindows = new List<List<IExportStringDoubleData>>();
            //идем по уровням
            foreach (var group in groupsLevel)
            {
                var walls = group.ToList();
                //нам надо стены тоже сгруппировать по типу помещения, хотя не, это потом

                // уже на входе — стены, отфильтрованные как «наружные»
                var sortedWalls = ECWallPerimeterChainBuilder.BuildContinuousPerimeter(walls);
                if (sortedWalls.Count == 0) return;

               
                //получили цепочку стен теперь с ней надо что-то делать
                //сразу несколько данных по выгрузу

                var LevelAnswerWall= new List<IExportStringDoubleData>();
                var LevelAnswerIsolWall = new List<IExportStringDoubleData>();
                var LevelAnswerDoors = new List<IExportStringDoubleData>();

                //ну и у стен есть окна и двери и.. витражи
                foreach (var wall in sortedWalls)
                {
                    LevelAnswerIsolWall.Add(wall.ECInsulation);
                    LevelAnswerWall .Add(wall);
                    if (wall.ECDoorOrWindows.Count > 0)
                    {
                        LevelAnswerDoors.Add(wall.ECDoorOrWindows.First());
                    }
                }
                if (LevelAnswerIsolWall.Count > 0)
                {
                    answerWall.Add(LevelAnswerWall);
                    answerIsolWall.Add(LevelAnswerIsolWall);
                }
            }
            var sheetWalls = new ExportElementOnSheetExcell
            {
                SheetName = "Стены",
                exportStringDoubleDatas = answerWall
            };
            var sheetIsol = new ExportElementOnSheetExcell
            {
                SheetName = "ТеплоИзол",
                exportStringDoubleDatas = answerIsolWall
            };
            var eg = new List<ExportElementOnSheetExcell>() { sheetWalls, sheetIsol };
            ExcelGroupExporter.Exporter(eg, "ТеплоизолСтен");
        }
    }
}
