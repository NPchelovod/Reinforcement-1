using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Reinforcement
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CheckWallsMarkaJ : IExternalCommand
    {
        private const double FtToMm = 304.8;
        private const double MatchToleranceMm = 400.0;   // поиск "стены под нами"
        private const double DiffToleranceMm = 1.0;     // различие позиции / длины
        private const double LengthStepMm = 50.0;
        private const double AngleToleranceRad = 5.0 * Math.PI / 180.0;
        private const string MarkaParamName = "Марка";
        private const string MarkaValue = "Дж";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData?.Application?.ActiveUIDocument;
            var doc = uidoc?.Document;
            var view = doc?.ActiveView;
            if (doc == null || view == null) { message = "Нет активного вида."; return Result.Failed; }

            // 1. Собираем стены с маркой "Дж" на виде
            //var walls = new FilteredElementCollector(doc, view.Id)
            //    .OfClass(typeof(Wall))
            //    .Cast<Wall>()
            //    .Where(w => string.Equals(GetMarka(w), MarkaValue, StringComparison.OrdinalIgnoreCase))
            //    .ToList();
            // 1. Собираем стены с маркой "Дж" на виде.
            //    Если в выделении есть стены — берём только их, иначе весь вид.
            IEnumerable<Wall> candidateWalls=null;

            var preselectedIds = uidoc.Selection.GetElementIds();
            bool hasSelection = preselectedIds != null && preselectedIds.Count > 0;

            if (hasSelection)
            {
                candidateWalls = preselectedIds
                    .Select(id =>
                    {
                        try { return doc.GetElement(id) as Wall; }
                        catch { return null; }
                    })
                    .Where(w => w != null);
            }
            if (candidateWalls == null && !candidateWalls.Any())
            {
                candidateWalls = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>();
            }
            

            var walls = candidateWalls
                .Where(w => string.Equals(GetMarka(w), MarkaValue, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (walls.Count == 0)
            {
                TaskDialog.Show("Проверка стен",
                    $"На виде нет стен с \"{MarkaParamName}\" = \"{MarkaValue}\".");
                return Result.Cancelled;
            }

            var info = new List<WallInfo>();
            int skipped = 0;
            foreach (var w in walls)
            {
                var wi = WallInfo.TryCreate(doc, w);
                if (wi == null) { skipped++; continue; }
                info.Add(wi);
            }

            // 2. Проверка кратности длины
            var issues = new Dictionary<ElementId, List<string>>();

            foreach (var wi in info)
            {
                double rem = wi.LengthMm - Math.Floor(wi.LengthMm / LengthStepMm) * LengthStepMm;
                if (rem > DiffToleranceMm && (LengthStepMm - rem) > DiffToleranceMm)
                {
                    AddIssue(issues, wi.Wall.Id,
                        $"длина {wi.LengthMm:F1} мм не кратна {LengthStepMm:F0}");
                }
            }

            // 3. Группировка по уровням, снизу вверх
            var byLevel = info
                .GroupBy(i => i.LevelId)
                .Select(g => new LevelGroup
                {
                    LevelId = g.Key,
                    Elevation = g.First().LevelElevation,
                    Name = GetLevelName(doc, g.Key),
                    Walls = g.ToList()
                })
                .OrderBy(g => g.Elevation)
                .ToList();

            var levelNameById = byLevel.ToDictionary(g => g.LevelId, g => g.Name);

            // 4. Каждую верхнюю стену сравниваем с САМОЙ НИЖНЕЙ стеной в её "колонне"
            for (int li = 1; li < byLevel.Count; li++)
            {
                foreach (var uw in byLevel[li].Walls)
                {
                    WallInfo reference = null;
                    for (int lj = 0; lj < li && reference == null; lj++)
                    {
                        foreach (var cand in byLevel[lj].Walls)
                        {
                            if (Math.Abs(NormalizeAngle(uw.AngleRad - cand.AngleRad)) > AngleToleranceRad)
                                continue;
                            if (XyDistMm(uw.Mid, cand.Mid) > MatchToleranceMm)
                                continue;
                            reference = cand;
                            break;
                        }
                    }

                    if (reference == null) continue;

                    double ds = XyDistMm(uw.Start, reference.Start);
                    double de = XyDistMm(uw.End, reference.End);
                    double dlen = Math.Abs(uw.LengthMm - reference.LengthMm);

                    var diffs = new List<string>();
                    if (ds > DiffToleranceMm) diffs.Add($"начало смещено на {ds:F1} мм");
                    if (de > DiffToleranceMm) diffs.Add($"конец смещён на {de:F1} мм");
                    if (dlen > DiffToleranceMm) diffs.Add($"длина {uw.LengthMm:F1} vs {reference.LengthMm:F1} мм");

                    if (diffs.Count == 0) continue;

                    string refLvl = levelNameById.TryGetValue(reference.LevelId, out var rn) ? rn : "?";
                    AddIssue(issues, uw.Wall.Id,
                        $"отличается от стены [{reference.Wall.Id.IntegerValue}] " +
                        $"уровня \"{refLvl}\": {string.Join(", ", diffs)}");
                }
            }

            // 5. Итог
            if (issues.Count == 0)
            {
                uidoc.Selection.SetElementIds(new List<ElementId>());
                TaskDialog.Show("Проверка стен",
                    $"Проверено стен: {info.Count}.\nНарушений не найдено.");
                return Result.Succeeded;
            }

            uidoc.Selection.SetElementIds(issues.Keys.Distinct().ToList());
            ShowReport(doc, levelNameById, info.Count, skipped, issues);
            return Result.Succeeded;
        }

        // ================== helpers ==================

        private class WallInfo
        {
            public Wall Wall;
            public ElementId LevelId;
            public double LevelElevation;
            public XYZ Start, End, Mid;
            public double LengthMm;
            public double AngleRad;

            public static WallInfo TryCreate(Document doc, Wall wall)
            {
                if (wall.LevelId == null || wall.LevelId == ElementId.InvalidElementId) return null;

                var lc = wall.Location as LocationCurve;
                if (lc?.Curve == null) return null;
                if (!(lc.Curve is Line)) return null;   // только прямые — для дуг логика другая

                var s = lc.Curve.GetEndPoint(0);
                var e = lc.Curve.GetEndPoint(1);
                var diff = e - s;
                if (diff.GetLength() < 1e-6) return null;

                var lvl = doc.GetElement(wall.LevelId) as Level;

                return new WallInfo
                {
                    Wall = wall,
                    LevelId = wall.LevelId,
                    LevelElevation = lvl?.Elevation ?? 0.0,
                    Start = s,
                    End = e,
                    Mid = (s + e) * 0.5,
                    LengthMm = diff.GetLength() * FtToMm,
                    AngleRad = Math.Atan2(diff.Y, diff.X)
                };
            }
        }

        private class LevelGroup
        {
            public ElementId LevelId;
            public double Elevation;
            public string Name;
            public List<WallInfo> Walls;
        }

        private static void AddIssue(Dictionary<ElementId, List<string>> dict, ElementId id, string msg)
        {
            if (!dict.TryGetValue(id, out var list)) { list = new List<string>(); dict[id] = list; }
            if (!list.Contains(msg)) list.Add(msg);
        }

        private static string GetParamValue(Parameter p)
        {
            if (p == null || !p.HasValue) return null;
            try { var v = p.AsString(); if (!string.IsNullOrEmpty(v)) return v; } catch { }
            try { var v = p.AsValueString(); if (!string.IsNullOrEmpty(v)) return v; } catch { }
            return null;
        }

        private static string GetMarka(Wall w)
        {
            var v = GetParamValue(w.LookupParameter(MarkaParamName));
            if (v != null) return v;
            v = GetParamValue(w.get_Parameter(BuiltInParameter.ALL_MODEL_MARK));
            if (v != null) return v;

            if (w.WallType != null)
            {
                v = GetParamValue(w.WallType.LookupParameter(MarkaParamName));
                if (v != null) return v;
                v = GetParamValue(w.WallType.get_Parameter(BuiltInParameter.ALL_MODEL_MARK));
                if (v != null) return v;
            }
            return null;
        }

        private static string GetLevelName(Document doc, ElementId id) =>
            (doc.GetElement(id) as Level)?.Name ?? "?";

        private static double XyDistMm(XYZ a, XYZ b)
        {
            double dx = (a.X - b.X) * FtToMm;
            double dy = (a.Y - b.Y) * FtToMm;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double NormalizeAngle(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }

        private static void ShowReport(
            Document doc,
            Dictionary<ElementId, string> levelNameById,
            int checkedCount, int skipped,
            Dictionary<ElementId, List<string>> issues)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Стен с \"{MarkaParamName}\" = \"{MarkaValue}\": {checkedCount}");
            if (skipped > 0)
                sb.AppendLine($"Пропущено (не прямолинейные / без уровня): {skipped}");
            sb.AppendLine($"Нарушений: {issues.Count} стен");
            sb.AppendLine(new string('─', 80));

            int idx = 0;
            foreach (var kv in issues.OrderBy(x => x.Key.IntegerValue))
            {
                idx++;
                Element el = null;
                try { el = doc.GetElement(kv.Key); } catch { }
                string lvl = el is Wall w && levelNameById.TryGetValue(w.LevelId, out var nm) ? nm : "?";
                sb.AppendLine($"{idx,3}. [{kv.Key.IntegerValue}] уровень \"{lvl}\"");
                foreach (var m in kv.Value)
                    sb.AppendLine($"       {m}");
            }

            var win = new Window
            {
                Title = $"Проверка стен: {issues.Count} нарушений",
                Width = 880,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            win.Content = new System.Windows.Controls.TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(8),
                Padding = new Thickness(6)
            };
            win.ShowDialog();
        }
    }
}