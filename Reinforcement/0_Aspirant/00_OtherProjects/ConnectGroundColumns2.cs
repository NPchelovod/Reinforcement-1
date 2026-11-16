using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement.Aspirant
{
    /// <summary>
    /// Стратиграфическая единица: литология + её порядковый номер в колонке.
    /// Ключ (Key) уникален в пределах колонки, но совпадает у одинаковых
    /// стратиграфических единиц в разных колонках — по нему и идёт соединение.
    /// </summary>
    public class StratumUnit
    {
        public string Key;          // "Песок_1", "Песок_2"
        public string Lithology;    // "Песок" (для ЕС_Имя при создании балки)
        public int Position;        // 1, 2, 3... (значение ADSK_Позиция)
        public double TopZMm;
        public double BottomZMm;
        public double ThicknessMm => TopZMm - BottomZMm;
    }

    /// <summary>Грунтовая колонка с нормализованной стратиграфией.</summary>
   

    [Transaction(TransactionMode.Manual)]
    public class ConnectGroundColumns2 : IExternalCommand
    {
        private const string FamilyName = "ЕСБалкаАдапV1";
        private const string NameParam = "ЕС_Имя";
        private const string ColumnNumParam = "ЕС_Номер колонки";
        private const string PositionParam = "ADSK_Позиция";  // ← порядковый номер слоя в колонке
        private const double FtToMm = 304.8;
        private const double TolMm = 2.0;
        private const double PinchRatio = 0.6;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData?.Application?.ActiveUIDocument?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            // 1. Заполняем ADSK_Позиция и собираем нормализованные колонки
            List<GroundColumn> columns;
            using (Transaction t = new Transaction(doc, "Нормализация колонок (ADSK_Позиция)"))
            {
                t.Start();
                columns = FillPositionsAndCollect(doc);
                t.Commit();
            }

            if (columns.Count < 2)
            {
                TaskDialog.Show("Соединение колонок",
                    $"Найдено колонок: {columns.Count}. Нужно минимум 2.\n\n" +
                    "Колонки определяются по непустому 'ЕС_Номер колонки' у вертикальных балок.");
                return Result.Cancelled;
            }

            // 2. Типоразмер
            FamilySymbol symbol = HelperSeach
                .SeachElement(FamilyName, null, ElementTypeOrSymbol.Symbol, true)
                as FamilySymbol;
            if (symbol == null)
            {
                ErrorAnalyse.Error(null, $"Не найден FamilySymbol '{FamilyName}'");
                return Result.Failed;
            }

            // 3. Существующие соединения (защита от дублей)
            var existing = CollectExistingConnections(doc);

            int created = 0, skipped = 0;
            var report = new StringBuilder();
            report.AppendLine($"Колонок: {columns.Count}");
            report.AppendLine("Последовательности слоёв:");
            foreach (var c in columns.OrderBy(x => x.ColumnNumber))
            {
                report.AppendLine(
                    $"  Колонка {c.ColumnNumber}: " +
                    string.Join(" → ", c.Units.Select(u => u.Key)));
            }
            report.AppendLine();

            // 4. Создаём балки
            using (Transaction t = new Transaction(doc, "Соединение грунтовых колонок"))
            {
                t.Start();

                for (int i = 0; i < columns.Count; i++)
                {
                    for (int j = i + 1; j < columns.Count; j++)
                    {
                        ConnectPair(doc, symbol, existing,
                                    columns[i], columns[j],
                                    ref created, ref skipped, report);
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Соединение колонок",
                $"Создано балок: {created}\nПропущено: {skipped}\n\n" + report);
            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────
        //  Соединение одной пары колонок
        // ─────────────────────────────────────────────────────────

        private static void ConnectPair(
            Document doc, FamilySymbol symbol,
            HashSet<(long, long, long, long, string)> existing,
            GroundColumn A, GroundColumn B,
            ref int created, ref int skipped, StringBuilder report)
        {
            // Объединяем ключи слоёв: те, что есть хотя бы в одной колонке
            var allKeys = A.Units.Select(u => u.Key)
                .Union(B.Units.Select(u => u.Key), StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            int pairCreated = 0, pairSkipped = 0;

            foreach (var key in allKeys)
            {
                var uA = A.Units.FirstOrDefault(u =>
                    string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase));
                var uB = B.Units.FirstOrDefault(u =>
                    string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase));

                if (uA != null && uB != null)
                {
                    // Слой есть в обеих — прямое соединение верхов
                    TryCreateBeam(doc, symbol, existing,
                        A.XMm, A.YMm, uA.TopZMm,
                        B.XMm, B.YMm, uB.TopZMm,
                        uA.Lithology, uA.Position,
                        ref created, ref pairCreated, ref skipped, ref pairSkipped);
                }
                else if (uA != null)
                {
                    // Только в A — выклинивание в сторону B
                    var (x, y, z) = CalculatePinchOut(A, B, uA);
                    TryCreateBeam(doc, symbol, existing,
                        A.XMm, A.YMm, uA.TopZMm,
                        x, y, z,
                        uA.Lithology, uA.Position,
                        ref created, ref pairCreated, ref skipped, ref pairSkipped);
                }
                else if (uB != null)
                {
                    // Только в B — выклинивание в сторону A
                    var (x, y, z) = CalculatePinchOut(B, A, uB);
                    TryCreateBeam(doc, symbol, existing,
                        B.XMm, B.YMm, uB.TopZMm,
                        x, y, z,
                        uB.Lithology, uB.Position,
                        ref created, ref pairCreated, ref skipped, ref pairSkipped);
                }
            }

            if (pairCreated > 0 || pairSkipped > 0)
                report.AppendLine(
                    $"  {A.ColumnNumber}–{B.ColumnNumber}: " +
                    $"создано {pairCreated}, пропущено {pairSkipped}");
        }

        // ─────────────────────────────────────────────────────────
        //  Выклинивание: где обрывается балка и на какой отметке
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Вычисляет точку обрыва слоя, присутствующего только в source,
        /// на отрезке source → target. Положение в плане — PinchRatio
        /// от source к target. Z интерполируется между фактическим верхом
        /// слоя в source и «фантомной» отметкой, которую слой имел бы
        /// в target (оценка по ограничивающим слоям).
        /// </summary>
        private static (double x, double y, double z) CalculatePinchOut(
            GroundColumn source, GroundColumn target, StratumUnit unit)
        {
            double phantomZ = EstimatePhantomTopZ(source, target, unit);

            double x = source.XMm + (target.XMm - source.XMm) * PinchRatio;
            double y = source.YMm + (target.YMm - source.YMm) * PinchRatio;
            double z = unit.TopZMm + (phantomZ - unit.TopZMm) * PinchRatio;
            return (x, y, z);
        }

        /// <summary>
        /// Оценивает, на какой отметке была бы кровля слоя в target,
        /// если бы он там существовал. Использует ограничивающие слои:
        /// берём ближайшие слои сверху и снизу от искомого в source,
        /// которые есть и в target, и переносим относительное положение.
        /// </summary>
        private static double EstimatePhantomTopZ(
            GroundColumn source, GroundColumn target, StratumUnit unit)
        {
            int idx = source.Units.IndexOf(unit);
            if (idx < 0) return unit.TopZMm;

            // Ищем ближайший сверху, который есть и в target
            StratumUnit upperSource = null, upperTarget = null;
            for (int i = idx - 1; i >= 0; i--)
            {
                var t = target.Units.FirstOrDefault(u =>
                    string.Equals(u.Key, source.Units[i].Key, StringComparison.OrdinalIgnoreCase));
                if (t != null) { upperSource = source.Units[i]; upperTarget = t; break; }
            }

            // Ищем ближайший снизу, который есть и в target
            StratumUnit lowerSource = null, lowerTarget = null;
            for (int i = idx + 1; i < source.Units.Count; i++)
            {
                var t = target.Units.FirstOrDefault(u =>
                    string.Equals(u.Key, source.Units[i].Key, StringComparison.OrdinalIgnoreCase));
                if (t != null) { lowerSource = source.Units[i]; lowerTarget = t; break; }
            }

            // Оба ограничителя есть — линейно переносим относительное положение
            if (upperSource != null && lowerSource != null)
            {
                double rangeSource = upperSource.TopZMm - lowerSource.TopZMm;
                if (Math.Abs(rangeSource) < 1e-6) return unit.TopZMm;

                double ratio = (upperSource.TopZMm - unit.TopZMm) / rangeSource;
                return upperTarget.TopZMm - ratio * (upperTarget.TopZMm - lowerTarget.TopZMm);
            }

            // Есть только верхний — сохраняем мощность от верхнего ограничителя
            if (upperSource != null)
                return upperTarget.TopZMm - (upperSource.TopZMm - unit.TopZMm);

            // Есть только нижний — сохраняем мощность до нижнего ограничителя
            if (lowerSource != null)
                return lowerTarget.TopZMm + (unit.TopZMm - lowerSource.TopZMm);

            // Ограничителей нет — плоское продолжение на отметке source
            return unit.TopZMm;
        }

        // ─────────────────────────────────────────────────────────
        //  Создание одной балки + запись параметров
        // ─────────────────────────────────────────────────────────

        private static void TryCreateBeam(
            Document doc, FamilySymbol symbol,
            HashSet<(long, long, long, long, string)> existing,
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            string layerName, int position,
            ref int created, ref int pairCreated,
            ref int skipped, ref int pairSkipped)
        {
            XYZ p1 = new XYZ(x1, y1, z1);
            XYZ p2 = new XYZ(x2, y2, z2);

            string key = $"{layerName}_{position}";
            if (existing.Contains(KeyOf(p1, p2, key)))
            {
                skipped++; pairSkipped++; return;
            }

            var inst = AdaptiveBeamCreator.CreateAdaptiveBeam(doc, symbol, p1, p2);
            if (inst == null)
            {
                skipped++; pairSkipped++; return;
            }

            // ЕС_Имя = литология (без номера, как у исходных колонок)
            SetStringParameter(inst, NameParam, layerName);

            // ADSK_Позиция = порядковый номер — чтобы соединение имело тот же ключ
            SetIntOrStringParameter(inst, PositionParam, position);

            // Номер колонки у соединения очищаем (это не грунтовая балка)
            SetStringParameter(inst, ColumnNumParam, "");

            created++; pairCreated++;
        }

        // ─────────────────────────────────────────────────────────
        //  Сбор колонок + заполнение ADSK_Позиция
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Проходит по всем грунтовым балкам, группирует в колонки, в каждой
        /// колонке сортирует сверху вниз, заполняет ADSK_Позиция (1, 2, 3...
        /// для каждого литологического типа) и строит список StratumUnit.
        /// </summary>
        private static List<GroundColumn> FillPositionsAndCollect(Document doc)
        {
            // 1. Собираем все балки нужного семейства
            var beams = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsGroundBeamFamily)
                .ToList();

            // 2. Отбираем колонки: есть номер + вертикальные + есть литология
            var columnBeams = new List<BeamRecord>();
            foreach (var fi in beams)
            {
                string num = fi.LookupParameter(ColumnNumParam)?.AsString();
                if (string.IsNullOrWhiteSpace(num)) continue;

                var pts = GetBeamPoints(fi, doc);
                if (pts == null) continue;
                var (p0, p1) = pts.Value;

                double dxMm = Math.Abs(p0.X - p1.X) * FtToMm;
                double dyMm = Math.Abs(p0.Y - p1.Y) * FtToMm;
                if (dxMm > TolMm || dyMm > TolMm) continue;

                string lith = fi.LookupParameter(NameParam)?.AsString();
                if (string.IsNullOrEmpty(lith)) continue;

                double zTop = Math.Max(p0.Z, p1.Z) * FtToMm;
                double zBot = Math.Min(p0.Z, p1.Z) * FtToMm;

                columnBeams.Add(new BeamRecord
                {
                    Beam = fi,
                    ColumnNumber = num.Trim(),
                    Lithology = lith.Trim(),
                    P0 = p0,
                    P1 = p1,
                    TopZMm = zTop,
                    BottomZMm = zBot
                });
            }

            // 3. Группируем по номеру колонки, сортируем сверху вниз
            var byNumber = columnBeams
                .GroupBy(b => b.ColumnNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(b => b.TopZMm).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            // 4. В каждой колонке присваиваем ADSK_Позиция и собираем Units
            var columns = new List<GroundColumn>();
            foreach (var kvp in byNumber)
            {
                var col = new GroundColumn { ColumnNumber = kvp.Key };
                var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                double sumX = 0, sumY = 0;
                int count = 0;

                foreach (var rec in kvp.Value)
                {
                    int nextPos = (counters.TryGetValue(rec.Lithology, out int cur) ? cur : 0) + 1;
                    counters[rec.Lithology] = nextPos;

                    // Записываем ADSK_Позиция в элемент Revit
                    SetIntOrStringParameter(rec.Beam, PositionParam, nextPos);

                    col.Units.Add(new StratumUnit
                    {
                        Lithology = rec.Lithology,
                        Position = nextPos,
                        Key = $"{rec.Lithology}_{nextPos}",
                        TopZMm = rec.TopZMm,
                        BottomZMm = rec.BottomZMm
                    });

                    sumX += (rec.P0.X + rec.P1.X) * 0.5 * FtToMm;
                    sumY += (rec.P0.Y + rec.P1.Y) * 0.5 * FtToMm;
                    count++;
                }

                if (count > 0)
                {
                    col.XMm = sumX / count;
                    col.YMm = sumY / count;
                }

                columns.Add(col);
            }

            return columns.OrderBy(c => c.ColumnNumber, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ─────────────────────────────────────────────────────────
        //  Существующие соединения (защита от дублей)
        // ─────────────────────────────────────────────────────────

        private static HashSet<(long, long, long, long, string)> CollectExistingConnections(
            Document doc)
        {
            var set = new HashSet<(long, long, long, long, string)>();

            var beams = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsGroundBeamFamily)
                .ToList();

            foreach (var fi in beams)
            {
                string lith = fi.LookupParameter(NameParam)?.AsString();
                if (string.IsNullOrEmpty(lith)) continue;

                // Соединения — балки БЕЗ номера колонки
                string num = fi.LookupParameter(ColumnNumParam)?.AsString();
                if (!string.IsNullOrWhiteSpace(num)) continue;

                // Позицию берём из ADSK_Позиция, чтобы ключ совпал
                int pos = GetIntOrStringParameter(fi, PositionParam);

                var pts = GetBeamPoints(fi, doc);
                if (pts == null) continue;

                set.Add(KeyOf(pts.Value.p0, pts.Value.p1, $"{lith}_{pos}"));
            }
            return set;
        }

        // ─────────────────────────────────────────────────────────
        //  Утилиты
        // ─────────────────────────────────────────────────────────

        private class BeamRecord
        {
            public FamilyInstance Beam;
            public string ColumnNumber;
            public string Lithology;
            public XYZ P0, P1;
            public double TopZMm, BottomZMm;
        }

        private static (XYZ p0, XYZ p1)? GetBeamPoints(FamilyInstance fi, Document doc)
        {
            if (fi.Location is LocationCurve lc && lc.Curve is Line line)
                return (line.GetEndPoint(0), line.GetEndPoint(1));

            try
            {
                var ids = AdaptiveComponentInstanceUtils
                    .GetInstancePlacementPointElementRefIds(fi);
                if (ids != null && ids.Count >= 2)
                {
                    var rp0 = doc.GetElement(ids[0]) as ReferencePoint;
                    var rp1 = doc.GetElement(ids[1]) as ReferencePoint;
                    if (rp0?.Position != null && rp1?.Position != null)
                        return (rp0.Position, rp1.Position);
                }
            }
            catch { }
            return null;
        }

        private static bool IsGroundBeamFamily(FamilyInstance fi)
        {
            if (fi?.Symbol == null) return false;
            string fam = fi.Symbol.Family?.Name ?? "";
            string sym = fi.Symbol.Name ?? "";
            return string.Equals(fam, FamilyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sym, FamilyName, StringComparison.OrdinalIgnoreCase);
        }

        private static (long, long, long, long, string) KeyOf(XYZ a, XYZ b, string key)
        {
            long ax = (long)Math.Round(a.X * FtToMm / TolMm);
            long ay = (long)Math.Round(a.Y * FtToMm / TolMm);
            long bx = (long)Math.Round(b.X * FtToMm / TolMm);
            long by = (long)Math.Round(b.Y * FtToMm / TolMm);
            if (ax > bx || (ax == bx && ay > by))
            { (ax, bx) = (bx, ax); (ay, by) = (by, ay); }
            return (ax, ay, bx, by, key);
        }

        private static void SetStringParameter(Element el, string name, string value)
        {
            Parameter p = el.LookupParameter(name);
            if (p == null || p.IsReadOnly) return;
            if (p.StorageType != StorageType.String) return;
            p.Set(value ?? "");
        }

        /// <summary>Записывает int в параметр, поддерживая String и Integer.</summary>
        private static void SetIntOrStringParameter(Element el, string name, int value)
        {
            Parameter p = el.LookupParameter(name);
            if (p == null || p.IsReadOnly) return;

            if (p.StorageType == StorageType.Integer)
                p.Set(value);
            else if (p.StorageType == StorageType.String)
                p.Set(value.ToString());
        }

        /// <summary>Читает int из параметра (String или Integer), 0 — если пусто.</summary>
        private static int GetIntOrStringParameter(Element el, string name)
        {
            Parameter p = el.LookupParameter(name);
            if (p == null) return 0;

            if (p.StorageType == StorageType.Integer)
                return p.AsInteger();

            if (p.StorageType == StorageType.String)
            {
                string s = p.AsString();
                return int.TryParse(s, out int v) ? v : 0;
            }
            return 0;
        }
    }
}