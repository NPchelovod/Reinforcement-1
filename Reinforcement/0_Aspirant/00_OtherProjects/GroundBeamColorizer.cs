using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reinforcement.Aspirant
{
    /// <summary>
    /// Раскраска грунтовых балок (ЕСБалкаАдапV1) по имени грунта (ЕС_Имя).
    /// Цвета распределяются равномерно по кругу HSV: соседние имена
    /// отстоят друг от друга максимально далеко в цветовом пространстве.
    /// На больших N добавляются «кольца» по насыщенности/яркости.
    /// Оверрайды применяются к активному виду.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ColorizeGroundBeams : IExternalCommand
    {
        private const string FamilyName = "ЕСБалкаАдапV1";
        private const string NameParam = "ЕС_Имя";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData?.Application?.ActiveUIDocument?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            View view = doc.ActiveView;
            if (view == null || view.IsTemplate ||
                view.ViewType == ViewType.Legend)
                //view.ViewType == ViewType.Sheet)   // ← на листах оверрайды не работают
            {
                message = "Нужен активный графический вид (план, 3D, разрез). " +
                          "Листы, шаблоны и легенды оверрайды не поддерживают.";
                return Result.Failed;
            }

            var beams = CollectGroundBeams(doc, view.Id);
            if (beams.Count == 0)
            {
                TaskDialog.Show("Раскраска балок",
                    $"На виде \"{view.Name}\" нет балок '{FamilyName}' " +
                    $"с заполненным '{NameParam}'.");
                return Result.Cancelled;
            }

            // 1. Уникальные имена грунта — собираем и сортируем
            var uniqueNames = beams
                .Select(fi => fi.LookupParameter(NameParam)?.AsString()?.Trim() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (uniqueNames.Count == 0)
            {
                TaskDialog.Show("Раскраска балок",
                    $"У балок на виде не заполнен параметр '{NameParam}'.");
                return Result.Cancelled;
            }

            // 2. Распределяем цвета равномерно по HSV-кругу
            var colorMap = BuildColorMap(uniqueNames);

            // 3. Сплошная заливка — чтобы цвет был виден и на гранях в 3D
            FillPatternElement solidPattern = GetSolidFillPattern(doc);

            // 4. Группируем балки по имени и применяем оверрайды
            var byName = beams
                .Select(fi => new
                {
                    Beam = fi,
                    Name = fi.LookupParameter(NameParam)?.AsString()?.Trim() ?? ""
                })
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int colored = 0;
            var failed = new List<string>();

            using (Transaction t = new Transaction(doc, "Цвет грунтовых балок"))
            {
                t.Start();

                foreach (var g in byName)
                {
                    if (!colorMap.TryGetValue(g.Key, out Color c)) continue;

                    var ogs = new OverrideGraphicSettings();

                    // Линии — основное для плана/разреза
                    ogs.SetProjectionLineColor(c);
                    ogs.SetProjectionLineWeight(3);

                    // Грани — для 3D / затенённого режима
                    if (solidPattern != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                        ogs.SetSurfaceForegroundPatternColor(c);
                        ogs.SetSurfaceBackgroundPatternId(solidPattern.Id);
                        ogs.SetSurfaceBackgroundPatternColor(c);
                        ogs.SetSurfaceTransparency(20);
                    }

                    foreach (var item in g)
                    {
                        try
                        {
                            view.SetElementOverrides(item.Beam.Id, ogs);
                            colored++;
                        }
                        catch (Exception ex)
                        {
                            if (failed.Count < 5)
                                failed.Add($"{item.Beam.Id}: {ex.Message}");
                        }
                    }
                }

                t.Commit();
            }

            // 5. Отчёт
            var report = string.Join("\n", uniqueNames.Select(n =>
            {
                Color c = colorMap[n];
                int cnt = byName.First(g =>
                    g.Key.Equals(n, StringComparison.OrdinalIgnoreCase)).Count();
                return $"  «{n}» — {cnt} шт., RGB({c.Red},{c.Green},{c.Blue})";
            }));

            string summary =
                $"Вид: \"{view.Name}\"\n" +
                $"Уникальных имён грунта: {uniqueNames.Count}\n" +
                $"Раскрашено балок: {colored}\n\n" + report;

            if (failed.Count > 0)
                summary += "\n\n⚠ Ошибки (первые):\n" + string.Join("\n", failed);

            TaskDialog.Show("Раскраска балок", summary);
            return Result.Succeeded;
        }

        // ─────────────────────────────────────────────────────────
        //  Сбор балок
        // ─────────────────────────────────────────────────────────

        private static List<FamilyInstance> CollectGroundBeams(Document doc, ElementId viewId)
        {
            return new FilteredElementCollector(doc, viewId)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(IsGroundBeamFamily)
                .Where(fi => fi.LookupParameter(NameParam) != null)
                .ToList();
        }

        private static bool IsGroundBeamFamily(FamilyInstance fi)
        {
            if (fi?.Symbol == null) return false;
            string fam = fi.Symbol.Family?.Name ?? "";
            string sym = fi.Symbol.Name ?? "";
            return string.Equals(fam, FamilyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sym, FamilyName, StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────
        //  Равномерное распределение цветов
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Строит карту «имя → цвет», равномерно распределяя оттенки
        /// по кругу HSV. Для N ≤ 8 — один ряд насыщенных цветов,
        /// для N > 8 — добавляются «кольца» (пастель, тёмные, приглушённые),
        /// чтобы даже 30 разных имён визуально различались.
        /// </summary>
        private static Dictionary<string, Color> BuildColorMap(List<string> sortedNames)
        {
            var result = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            int n = sortedNames.Count;
            if (n == 0) return result;

            // Хотим «базовых» оттенков не больше 8 — больше уже плохо различимо
            int hueCount = Math.Min(n, 8);
            // Сколько колец S/V понадобится
            int ringCount = (int)Math.Ceiling((double)n / hueCount);

            // Сдвиг 30° — чтобы не начинать с чистого красного/зелёного/синего
            const double hueOffset = 30.0;

            for (int i = 0; i < n; i++)
            {
                int ring = i / hueCount;
                int posInRing = i % hueCount;
                int countInRing = (ring == ringCount - 1)
                    ? n - ring * hueCount
                    : hueCount;

                double hue = hueOffset + 360.0 * posInRing / countInRing;

                GetSatVal(ring, out double sat, out double val);

                result[sortedNames[i]] = HsvToColor(hue, sat, val);
            }
            return result;
        }

        /// <summary>
        /// S/V по номеру кольца. Каждое следующее кольцо — визуально
        /// заметно отличается от предыдущих на любом hue.
        /// </summary>
        private static void GetSatVal(int ring, out double sat, out double val)
        {
            switch (ring % 4)
            {
                case 0: sat = 0.90; val = 0.95; break;  // насыщенный яркий
                case 1: sat = 0.55; val = 0.95; break;  // пастель (светлый)
                case 2: sat = 0.90; val = 0.60; break;  // насыщенный тёмный
                default: sat = 0.55; val = 0.70; break; // приглушённый
            }
        }

        /// <summary>HSV → RGB (hue в градусах 0..360, sat/val 0..1).</summary>
        private static Color HsvToColor(double hue, double sat, double val)
        {
            hue = ((hue % 360.0) + 360.0) % 360.0;

            double c = val * sat;
            double x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            double m = val - c;

            double r1, g1, b1;
            if (hue < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (hue < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (hue < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (hue < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (hue < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);
            return new Color(r, g, b);
        }

        // ─────────────────────────────────────────────────────────
        //  Сплошная заливка
        // ─────────────────────────────────────────────────────────

        private static FillPatternElement GetSolidFillPattern(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill);
        }
    }
}