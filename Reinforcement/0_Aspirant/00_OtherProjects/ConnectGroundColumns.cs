using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement.Aspirant;
using Reinforcement;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Net.NetworkInformation;

namespace Reinforcement.Aspirant
{
    //Проблема, которую вы описываете, — это классическая задача восстановления геологической поверхности по данным скважин с учётом выклинивания(pinch-out) и линз.
    public class GroundColumn
    {
        public string ColumnNumber = "";

        /// <summary>Средние X, Y по всем балкам колонки (мм).</summary>
        public double XMm;
        public double YMm;

        // Накопители для усреднения (используются только внутри CollectColumns)
        internal double SumX;
        internal double SumY;
        internal int PointCount;

        /// <summary>Имя слоя → абсолютная отметка верха слоя (мм).</summary>
        public Dictionary<string, double> LayerTopZMm = new Dictionary<string, double>();

       
        public List<StratumUnit> Units = new List<StratumUnit>();  // сверху вниз
    }
    /// <summary>Кандидат-ребро между двумя колонками в одном слое.</summary>
    public class GroundEdge
    {
        public GroundColumn A;
        public GroundColumn B;
        public string LayerName;
        public double Dist;
    }
    [Transaction(TransactionMode.Manual)]
    public class ConnectGroundColumns : IExternalCommand
    {
        private const string FamilyName = "ЕСБалкаАдапV1";
        private const string NameParam = "ЕС_Имя";
        private const string ColumnNumParam = "ЕС_Номер колонки";
        private const double FtToMm = 304.8;
        private const double TolMm = 2.0;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData?.Application?.ActiveUIDocument?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            // 1. Колонки — по номеру, а не по (X,Y)
            var columns = CollectColumns(doc);
            if (columns.Count < 2)
            {
                TaskDialog.Show("Соединение колонок",
                    $"Найдено колонок: {columns.Count}. Нужно минимум 2.\n\n" +
                    "Колонки определяются по непустому параметру 'ЕС_Номер колонки' " +
                    "у вертикальных балок.");
                return Result.Cancelled;
            }

            // 2. Предупреждение: все колонки в одной точке
            bool allSamePoint = columns.All(c =>
                Math.Abs(c.XMm - columns[0].XMm) < TolMm &&
                Math.Abs(c.YMm - columns[0].YMm) < TolMm);
            if (allSamePoint)
            {
                TaskDialog.Show("Соединение колонок",
                    "Все колонки находятся в одной точке (X,Y).\n" +
                    "Сначала разнесите их по плану, затем повторите команду.");
                return Result.Cancelled;
            }

            // 3. Режим
            TaskDialog dlg = new TaskDialog("Соединение колонок");
            dlg.MainInstruction = $"Найдено {columns.Count} колонок. Как соединять?";
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Минимальное дерево (MST)",
                "Только необходимые балки; рёбра — по возрастанию длины.");
            dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "Полный граф",
                "Каждая с каждой в каждом слое.");
            dlg.CommonButtons = TaskDialogCommonButtons.Cancel;

            var choice = dlg.Show();
            bool useComplete;
            if (choice == TaskDialogResult.CommandLink1) useComplete = false;
            else if (choice == TaskDialogResult.CommandLink2) useComplete = true;
            else return Result.Cancelled;

            // 4. Типоразмер
            Element beamEl = HelperSeach.SeachElement(
                FamilyName, null, ElementTypeOrSymbol.Symbol, true);
            FamilySymbol symbol = beamEl as FamilySymbol;
            if (symbol == null)
            {
                ErrorAnalyse.Error(null, $"Не найден FamilySymbol '{FamilyName}'");
                return Result.Failed;
            }

            var existing = CollectExistingConnections(doc);

            int created = 0, skipped = 0;
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Колонок: {columns.Count}, режим: " +
                              (useComplete ? "полный граф" : "MST"));
            report.AppendLine("Номера: " + string.Join(", ",
                columns.Select(c => c.ColumnNumber)));

            using (Transaction t = new Transaction(doc, "Соединение грунтовых колонок"))
            {
                t.Start();

                var layerNames = columns
                    .SelectMany(c => c.LayerTopZMm.Keys)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                foreach (var layerName in layerNames)
                {
                    var cols = columns
                        .Where(c => c.LayerTopZMm.ContainsKey(layerName))
                        .ToList();
                    if (cols.Count < 2) continue;

                    var edges = new List<GroundEdge>();
                    for (int i = 0; i < cols.Count; i++)
                        for (int j = i + 1; j < cols.Count; j++)
                        {
                            double dx = cols[i].XMm - cols[j].XMm;
                            double dy = cols[i].YMm - cols[j].YMm;
                            edges.Add(new GroundEdge
                            {
                                A = cols[i],
                                B = cols[j],
                                LayerName = layerName,
                                Dist = Math.Sqrt(dx * dx + dy * dy)
                            });
                        }
                    edges = edges.OrderBy(e => e.Dist).ToList();

                    var selected = useComplete ? edges : BuildMST(cols, edges);

                    int layerCreated = 0, layerSkipped = 0;
                    foreach (var e in selected)
                    {
                        double zA = e.A.LayerTopZMm[layerName];
                        double zB = e.B.LayerTopZMm[layerName];
                        XYZ startMm = new XYZ(e.A.XMm, e.A.YMm, zA);
                        XYZ endMm = new XYZ(e.B.XMm, e.B.YMm, zB);

                        if (existing.Contains(KeyOf(startMm, endMm, layerName)))
                        { layerSkipped++; continue; }

                        FamilyInstance inst = AdaptiveBeamCreator
                            .CreateAdaptiveBeam(doc, symbol, startMm, endMm);
                        if (inst == null) { layerSkipped++; continue; }

                        Parameter p = inst.LookupParameter(NameParam);
                        if (p != null && !p.IsReadOnly) p.Set(layerName);

                        // Номер колонки у соединения НЕ ставим — иначе оно будет
                        // ошибочно принято за грунтовую балку при следующем запуске.
                        // Можно очистить, если параметр вдруг заполнен:
                        Parameter pc = inst.LookupParameter(ColumnNumParam);
                        if (pc != null && !pc.IsReadOnly) pc.Set("");

                        layerCreated++;
                        created++;
                    }
                    skipped += layerSkipped;

                    report.AppendLine(
                        $"  [{layerName}] колонок {cols.Count}, " +
                        $"создано {layerCreated}, пропущено {layerSkipped}");
                }

                t.Commit();
            }

            TaskDialog.Show("Соединение колонок",
                $"Создано балок: {created}\nПропущено: {skipped}\n\n" + report);
            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────────
        //  1. Сбор колонок — группировка по ЕС_Номер колонки
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Группирует вертикальные грунтовые балки в колонки по значению
        /// ЕС_Номер колонки. X,Y колонки — среднее по концам всех её балок
        /// (пользователь может перемещать колонку целиком; среднее устойчивее
        /// к частичному перемещению, чем «первая попавшаяся»).
        /// </summary>
        private static List<GroundColumn> CollectColumns(Document doc)
        {
            var byNumber = new Dictionary<string, GroundColumn>();

            var beams = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi => IsGroundBeamFamily(fi))
            .ToList();

            foreach (var fi in beams)
            {
                string num = fi.LookupParameter(ColumnNumParam)?.AsString();
                if (string.IsNullOrWhiteSpace(num)) continue;   // не колонка

                var pts = GetBeamPoints(fi, doc);
                if (pts == null) continue;
                var (p0, p1) = pts.Value;

                // только вертикальные
                double dxMm = Math.Abs(p0.X - p1.X) * FtToMm;
                double dyMm = Math.Abs(p0.Y - p1.Y) * FtToMm;
                if (dxMm > TolMm || dyMm > TolMm) continue;

                if (!byNumber.TryGetValue(num, out var col))
                {
                    col = new GroundColumn { ColumnNumber = num };
                    byNumber[num] = col;
                }

                // накапливаем X,Y для последующего усреднения
                col.SumX += (p0.X + p1.X) * 0.5 * FtToMm;
                col.SumY += (p0.Y + p1.Y) * 0.5 * FtToMm;
                col.PointCount += 1;

                string name = fi.LookupParameter(NameParam)?.AsString();
                if (string.IsNullOrEmpty(name)) continue;

                double zTopMm = Math.Max(p0.Z, p1.Z) * FtToMm;
                if (!col.LayerTopZMm.TryGetValue(name, out var prev) || zTopMm > prev)
                    col.LayerTopZMm[name] = zTopMm;
            }

            foreach (var col in byNumber.Values)
            {
                if (col.PointCount > 0)
                {
                    col.XMm = col.SumX / col.PointCount;
                    col.YMm = col.SumY / col.PointCount;
                }
            }
            return byNumber.Values.ToList();
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

        // ─────────────────────────────────────────────────────────────
        //  2. MST
        // ─────────────────────────────────────────────────────────────

        private static List<GroundEdge> BuildMST(
            List<GroundColumn> cols, List<GroundEdge> sortedEdges)
        {
            var idx = new Dictionary<GroundColumn, int>();
            for (int i = 0; i < cols.Count; i++) idx[cols[i]] = i;

            var parent = Enumerable.Range(0, cols.Count).ToArray();
            int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
            void Union(int a, int b) => parent[Find(a)] = Find(b);

            var result = new List<GroundEdge>();
            foreach (var e in sortedEdges)
            {
                int ia = idx[e.A], ib = idx[e.B];
                if (Find(ia) == Find(ib)) continue;
                Union(ia, ib);
                result.Add(e);
                if (result.Count == cols.Count - 1) break;
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  3. Существующие соединения
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Соединения — это балки с ЕС_Имя, но БЕЗ номера колонки
        /// (у грунтовых колонок номер всегда задан).
        /// </summary>
        private static HashSet<(long, long, long, long, string)> CollectExistingConnections(
            Document doc)
        {
            var set = new HashSet<(long, long, long, long, string)>();
            var beams = new FilteredElementCollector(doc)
         .WhereElementIsNotElementType()
         .OfClass(typeof(FamilyInstance))
         .Cast<FamilyInstance>()
         .Where(fi => IsGroundBeamFamily(fi))
         .ToList();

            foreach (var fi in beams)
            {
                string name = fi.LookupParameter(NameParam)?.AsString();
                if (string.IsNullOrEmpty(name)) continue;

                string num = fi.LookupParameter(ColumnNumParam)?.AsString();
                if (!string.IsNullOrWhiteSpace(num)) continue;   // грунтовая, не соединение

                var pts = GetBeamPoints(fi, doc);
                if (pts == null) continue;
                set.Add(KeyOf(pts.Value.p0, pts.Value.p1, name));
            }
            return set;
        }
       

        /// <summary>
        /// true, если fi — экземпляр нашего адаптивного семейства.
        /// Категорию не проверяем: семейство может лежать в OST_GenericModel,
        /// OST_StructuralFraming, OST_Columns и т.д.
        /// </summary>
        private static bool IsGroundBeamFamily(FamilyInstance fi)
        {
            if (fi?.Symbol == null) return false;

            string fam = fi.Symbol.Family?.Name ?? "";
            string sym = fi.Symbol.Name ?? "";

            return string.Equals(fam, FamilyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sym, FamilyName, StringComparison.OrdinalIgnoreCase);
        }
        private static (long, long, long, long, string) KeyOf(
            XYZ a, XYZ b, string layer)
        {
            long ax = (long)Math.Round(a.X * FtToMm / TolMm);
            long ay = (long)Math.Round(a.Y * FtToMm / TolMm);
            long bx = (long)Math.Round(b.X * FtToMm / TolMm);
            long by = (long)Math.Round(b.Y * FtToMm / TolMm);
            if (ax > bx || (ax == bx && ay > by)) { (ax, bx) = (bx, ax); (ay, by) = (by, ay); }
            return (ax, ay, bx, by, layer);
        }
    }
}