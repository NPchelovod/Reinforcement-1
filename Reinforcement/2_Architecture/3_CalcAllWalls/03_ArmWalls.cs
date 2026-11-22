using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement.Aspirant;
using Spire.Xls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    


    [Transaction(TransactionMode.Manual)]
    public class ArmWalls : IExternalCommand
    {
        public Result Execute(
           ExternalCommandData commandData,
           ref string message,
           ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Succeeded;
            Document doc = uidoc.Document;
            //сбор всех стен
            var eCWalls = ReadAndGet(doc);


            //затем мои методы подсчета, мой алгоритм
            return Result.Succeeded;
        }

        public static List<ECWall> ReadAndGet(Document doc)
        {
            var eCWalls = CalcWalls.GetWalls(doc);

            //обрезаем наши стены тут стены КР есть
            eCWalls = GetArmCalcWalls(eCWalls);

            //находим пересечения с ними - все стены друг с другом
            IntersectWalls.CalcIntersects(doc, eCWalls);
            //затем инициируем начальные данные или сразу рассчитываем??
            //но тут можно записывать начальные данные

            //рассчитываем
            CalculateArm(doc, eCWalls);
            if (ArmElementConstants.FillParametersInProject)
            {
                using (Transaction transaction = new Transaction(doc, "Расчёт армирования архитектурных стен"))
                {
                    transaction.Start();

                    try
                    {
                        //устанавливаем
                        SetArmForWallInRevit(doc, eCWalls);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        //message = ex.ToString(); // если это IExternalCommand — верните Result.Failed с этим message
                        //return Result.Failed;
                    }
                }
            }

            eCWalls = GetArmExportWalls(eCWalls);



            //ArmExport.Export(eCWalls);

            GroopWallsAndExportArm(eCWalls);
            //возврат стен не КР!
            return eCWalls;
        }

        private static void GroopWallsAndExportArm(List<ECWall> eCWalls)
        {
            var answerArm = new List<List<IExportStringDoubleData>>();
            var answerWalls = new List<List<IExportStringDoubleData>>();
            if (eCWalls == null || eCWalls.Count == 0)
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }

            var groups = eCWalls
                .Where(x => x != null && x.Curve != null && x.ECLevel != null)
                .GroupBy(x => x.ECLevel.LevelElevation)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups.Count == 0)
            {
                MessageBox.Show("Нет стен для экспорта.");
                return;
            }

            foreach (var levelGroup in groups)
            {
                var levelWalls = new List<IExportStringDoubleData>();
                foreach (var typeGroup in levelGroup.GroupBy(x => x.EWallTypes).OrderBy(g => g.Key))
                {
                    var walls = typeGroup
                        .OrderBy(x => x.WallType?.EWallTypes ?? 0)
                        .ThenByDescending(x => x.WidthM)
                        .ThenByDescending(x => x.WallType?.Name ?? string.Empty)
                        .ThenByDescending(x => x.RelationsWallsKR.Count)
                        .ThenByDescending(x => x.RelationsWalls.Count)
                        .ThenByDescending(x => x.ECDoorOrWindows.Count)
                        .ThenBy(x => x.AreaNetM2)
                        .ThenBy(x => x.Id)
                        .ToList();
                    var LevelAndTypeArm = new List<IExportStringDoubleData>();
                    var LevelAndTypeWalls = new List<IExportStringDoubleData>();
                    foreach (var wall in walls)
                    {
                        LevelAndTypeArm.Add((IExportStringDoubleData)wall.ECArmWallElement);
                        LevelAndTypeWalls.Add((IExportStringDoubleData)wall);
                    }
                    if (LevelAndTypeArm.Count > 0)
                    {
                        answerArm.Add(LevelAndTypeArm);
                        answerWalls.Add(LevelAndTypeWalls);
                    }
                }
            }
            var sheetWalls = new ExportElementOnSheetExcell
            {
                SheetName = "Стены",
                exportStringDoubleDatas = answerWalls
            };
            var sheetArm = new ExportElementOnSheetExcell
            {
                SheetName = "СтеныАрмирование",
                exportStringDoubleDatas = answerArm
            };

            var eg=new List<ExportElementOnSheetExcell>() { sheetWalls, sheetArm };
            ExcelGroupExporter.Exporter(eg, "Армирование стен");

        }


        /// <summary>
        /// Получение только тех стен которые будут участвовать в наших расчетах
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="eCWalls"></param>
        /// <returns></returns>
        private static List<ECWall> GetArmCalcWalls( List<ECWall> eCWalls)
        {
            return eCWalls.Where(x => EWallTypesForCalc.Contains(x.EWallTypes)).ToList();
        }
        private static List<ECWall> GetArmExportWalls(List<ECWall> eCWalls)
        {
            return eCWalls.Where(x => ArmEWallTypes.Contains(x.EWallTypes)).ToList();
        }

        private static HashSet<EWallTypes> EWallTypesForCalc = new HashSet<EWallTypes>() { EWallTypes.KR_Reinforcement, EWallTypes.AR_Brick, EWallTypes.AR_Aerocrete };

        public static HashSet<EWallTypes> ArmEWallTypes = new HashSet<EWallTypes>() { EWallTypes.AR_Brick, EWallTypes.AR_Aerocrete };


        private static void CalculateArm(Document doc, List<ECWall> eCWalls)
        {
            foreach (var wall in eCWalls)
            {
                var type = wall.EWallTypes;
                if (!ArmEWallTypes.Contains(type)) { continue; }
                CalcOneArm(wall, type);
            }
        }

        private static void CalcOneArm(ECWall wall, EWallTypes eWallTypes)
        {
            var el = wall.ECArmWallElement;
            el.ECWall = wall;
            el.InitialValueSet();
            el.CalculateArm();//расчет армирования стены
        }

        private static void SetArmForWallInRevit(Document doc, List<ECWall> eCWalls)
        {
            foreach (var wall in eCWalls)
            {
                var type = wall.EWallTypes;
                if (!ArmEWallTypes.Contains(type)) { continue; }
                SetOneWallInRevit(wall, type);
            }
        }
        private static void SetOneWallInRevit(ECWall wall, EWallTypes eWallTypes)
        {
            ArmElementConstants.SetParametersInWallElement(eWallTypes, wall.ECArmWallElement, wall.Element);
        }

    }
}
