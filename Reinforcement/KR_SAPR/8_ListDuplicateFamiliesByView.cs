using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ListDuplicateFamiliesByView : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;

            // -------------------------------------------------------------------
            // 1. Все элементы с типом-семейством
            // -------------------------------------------------------------------
            var allElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    ElementId tId = e.GetTypeId();
                    if (tId == ElementId.InvalidElementId) return false;
                    return doc.GetElement(tId) is FamilySymbol;
                })
                .ToList();

            // -------------------------------------------------------------------
            // 2. Используемые имена семейств
            // -------------------------------------------------------------------
            var usedFamilyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in allElements)
            {
                var sym = doc.GetElement(el.GetTypeId()) as FamilySymbol;
                if (sym == null || sym.Family == null) continue;
                string familyName = sym.FamilyName;
                if (string.IsNullOrEmpty(familyName)) continue;
                usedFamilyNames.Add(familyName);
            }

            // -------------------------------------------------------------------
            // 3. Все семейства проекта
            // -------------------------------------------------------------------
            var allFamilies = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            foreach (Family f in new FilteredElementCollector(doc).OfClass(typeof(Family)))
            {
                string key = SafeTrim(f.Name);
                if (string.IsNullOrEmpty(key)) continue;
                if (!allFamilies.ContainsKey(key))
                    allFamilies[key] = f;
            }

            // -------------------------------------------------------------------
            // 4. Определяем дубликаты
            // -------------------------------------------------------------------
            var duplicateFamilyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatchedFamilies = new List<string>();

            foreach (var usedName in usedFamilyNames)
            {
                string baseName = GetBaseNameWithNumericSuffix(usedName);
                if (baseName == null) continue;

                if (allFamilies.ContainsKey(baseName))
                    duplicateFamilyNames.Add(usedName);
                else
                    unmatchedFamilies.Add($"«{usedName}» → база «{baseName}»");
            }

            if (duplicateFamilyNames.Count == 0)
            {
                TaskDialog info = new TaskDialog("Дубликаты семейств");
                info.MainInstruction = "Дубликаты семейств не найдены";
                info.MainContent = unmatchedFamilies.Count > 0
                    ? "Найдены семейства с числовым суффиксом, но без базового семейства:\n" +
                      string.Join("\n", unmatchedFamilies.Take(30))
                    : "В проекте нет семейств-дубликатов (с числовым суффиксом при наличии базового).";
                info.Show();
                return Result.Succeeded;
            }

            // -------------------------------------------------------------------
            // 5. Собираем данные: вид -> { семейства, группы }
            // -------------------------------------------------------------------
            // viewName -> (familyName -> count)
            var viewToFamilies = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            // viewName -> (groupName -> count)   (только для экземпляров дубликатов внутри групп)
            var viewToGroups = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

           

            // -------------------------------------------------------------------
            // 5. Собираем данные: Лист → Вид → { семейства, группы }
            //    Плюс отдельно виды без листа (в конце отчёта).
            // -------------------------------------------------------------------

            // Предварительно: viewId → (имя листа, номер листа)
            // Виды попадают на лист только через Viewport, поэтому идём по всем Viewport'ам.
            var viewToSheet = new Dictionary<ElementId, (string Name, string Number)>();
            foreach (Viewport vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)))
            {
                ElementId sheetId = vp.SheetId;
                ElementId viewId = vp.ViewId;
                if (sheetId == ElementId.InvalidElementId || viewId == ElementId.InvalidElementId)
                    continue;
                if (!(doc.GetElement(sheetId) is ViewSheet sheet)) continue;
                if (!viewToSheet.ContainsKey(viewId))
                    viewToSheet[viewId] = (sheet.Name, sheet.SheetNumber);
            }

            // Лист → ViewBucket (Виды и их содержимое)
            var sheetData = new Dictionary<string, SheetBucket>(StringComparer.OrdinalIgnoreCase);

            // Виды без листа — как раньше: viewName → { families }, viewName → { groups }
            var noSheetFamilies = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var noSheetGroups = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

            int totalDuplicateInstances = 0;
            int skippedNoType = 0;
            int skippedNoView = 0;

            foreach (var el in allElements)
            {
                var sym = doc.GetElement(el.GetTypeId()) as FamilySymbol;
                if (sym == null) { skippedNoType++; continue; }

                string familyName = sym.FamilyName;
                if (string.IsNullOrEmpty(familyName)) { skippedNoType++; continue; }

                if (!duplicateFamilyNames.Contains(familyName)) continue;

                // --- Определяем вид и лист ---
                string viewName = null;
                string sheetName = null;
                string sheetNumber = null;

                ElementId vid = el.OwnerViewId;
                if (vid == ElementId.InvalidElementId && el.GroupId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(el.GroupId) is Group grp)
                        vid = grp.OwnerViewId;
                }

                if (vid != ElementId.InvalidElementId)
                {
                    Element viewElem = doc.GetElement(vid);
                    if (viewElem is ViewSheet sheetDirect)
                    {
                        // Элемент лежит прямо на листе — нет промежуточного вида.
                        sheetName = sheetDirect.Name;
                        sheetNumber = sheetDirect.SheetNumber;
                        viewName = "<на листе>";
                    }
                    else if (viewElem is View v)
                    {
                        viewName = v.Name;
                        if (viewToSheet.TryGetValue(vid, out var s))
                        {
                            sheetName = s.Name;
                            sheetNumber = s.Number;
                        }
                    }
                }

                if (string.IsNullOrEmpty(viewName))
                {
                    skippedNoView++;
                    viewName = "<вид не определён>";
                }

                // --- Имя группы ---
                string groupName = null;
                if (el.GroupId != ElementId.InvalidElementId)
                    groupName = GetGroupName(doc, el);

                // --- Раскладываем по структурам ---
                if (!string.IsNullOrEmpty(sheetName))
                {
                    string sheetKey = sheetNumber + "|" + sheetName;
                    if (!sheetData.TryGetValue(sheetKey, out var bucket))
                    {
                        bucket = new SheetBucket { Name = sheetName, Number = sheetNumber };
                        sheetData[sheetKey] = bucket;
                    }

                    if (!bucket.Views.TryGetValue(viewName, out var vb))
                    {
                        vb = new ViewBucket();
                        bucket.Views[viewName] = vb;
                    }

                    vb.IncrementFamily(familyName);
                    if (!string.IsNullOrEmpty(groupName))
                        vb.IncrementGroup(groupName);

                    bucket.TotalInstances++;
                }
                else
                {
                    if (!noSheetFamilies.TryGetValue(viewName, out var famDict))
                    {
                        famDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        noSheetFamilies[viewName] = famDict;
                    }
                    famDict.TryGetValue(familyName, out int fc);
                    famDict[familyName] = fc + 1;

                    if (!string.IsNullOrEmpty(groupName))
                    {
                        if (!noSheetGroups.TryGetValue(viewName, out var grpDict))
                        {
                            grpDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            noSheetGroups[viewName] = grpDict;
                        }
                        grpDict.TryGetValue(groupName, out int gc);
                        grpDict[groupName] = gc + 1;
                    }
                }

                totalDuplicateInstances++;
            }

            // -------------------------------------------------------------------
            // 6. Формируем отчёт: Листы → Виды, затем виды без листа
            // -------------------------------------------------------------------
            var sb = new StringBuilder();
            sb.AppendLine($"Всего экземпляров дубликатов: {totalDuplicateInstances}");
            sb.AppendLine($"Листов с дубликатами: {sheetData.Count}");
            sb.AppendLine($"Видов без листа с дубликатами: {noSheetFamilies.Count}");
            sb.AppendLine();

            // --- Листы (сортировка по номеру, потом по имени) ---
            foreach (var bucket in sheetData.Values
                .OrderBy(s => s.Number, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"=== Лист «{bucket.Name}» №{bucket.Number} (экз.: {bucket.TotalInstances}) ===");

                foreach (var viewKv in bucket.Views.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    AppendViewLine(sb, viewKv.Key, viewKv.Value);

                sb.AppendLine();
            }

            // --- Виды без листа — в конце, как раньше ---
            if (noSheetFamilies.Count > 0)
            {
                sb.AppendLine("=== Виды без листа ===");
                foreach (var kv in noSheetFamilies.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string viewName = kv.Key;
                    var vb = new ViewBucket();
                    vb.Families = kv.Value;
                    if (noSheetGroups.TryGetValue(viewName, out var grpDict))
                        vb.Groups = grpDict;

                    AppendViewLine(sb, viewName, vb);
                }
            }

            if (skippedNoType > 0 || skippedNoView > 0)
            {
                sb.AppendLine();
                if (skippedNoType > 0)
                    sb.AppendLine($"Пропущено (нет типа/семейства): {skippedNoType}");
                if (skippedNoView > 0)
                    sb.AppendLine($"Экземпляров без определённого вида: {skippedNoView}");
            }

            if (unmatchedFamilies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Семейства с суффиксом, но без базового ({unmatchedFamilies.Count}) ===");
                foreach (var s in unmatchedFamilies.Take(30))
                    sb.AppendLine($"  {s}");
                if (unmatchedFamilies.Count > 30)
                    sb.AppendLine($"  ... и ещё {unmatchedFamilies.Count - 30}");
            }

            TaskDialog td = new TaskDialog("Дубликаты семейств по видам");
            td.MainInstruction = $"Листов: {sheetData.Count}, видов без листа: {noSheetFamilies.Count}";
            td.MainContent = sb.ToString();
            td.Show();

            return Result.Succeeded;
        }

        // -------------------------------------------------------------------
        // Вспомогательные
        // -------------------------------------------------------------------

        private static void AppendViewLine(StringBuilder sb, string viewName, ViewBucket vb)
        {
            var famList = vb.Families
                .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.Value > 1 ? $"{f.Key} ({f.Value})" : f.Key);

            var line = new StringBuilder();
            line.Append($"  Вид «{viewName}» дубликаты: {string.Join(", ", famList)}");

            if (vb.Groups.Count > 0)
            {
                var grpList = vb.Groups
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Value > 1 ? $"{g.Key} ({g.Value})" : g.Key);
                line.Append($" Группы: {string.Join(", ", grpList)}");
            }

            sb.AppendLine(line.ToString());
        }

        private sealed class ViewBucket
        {
            public Dictionary<string, int> Families =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> Groups =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public void IncrementFamily(string name)
            {
                Families.TryGetValue(name, out int c);
                Families[name] = c + 1;
            }

            public void IncrementGroup(string name)
            {
                Groups.TryGetValue(name, out int c);
                Groups[name] = c + 1;
            }
        }

        private sealed class SheetBucket
        {
            public string Name;
            public string Number;
            public Dictionary<string, ViewBucket> Views =
                new Dictionary<string, ViewBucket>(StringComparer.OrdinalIgnoreCase);
            public int TotalInstances;
        }
        // -------------------------------------------------------------------
        // Служебные
        // -------------------------------------------------------------------

        private static string GetViewName(Document doc, Element elem)
        {
            try
            {
                ElementId vid = elem.OwnerViewId;
                if (vid == ElementId.InvalidElementId && elem.GroupId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(elem.GroupId) is Group grp)
                        vid = grp.OwnerViewId;
                }
                if (vid != ElementId.InvalidElementId && doc.GetElement(vid) is View v)
                    return v.Name;
            }
            catch { }
            return "<вид не определён>";
        }

        /// <summary>
        /// Имя группы, в которой находится элемент.
        /// Если группа не определена — возвращает null.
        /// </summary>
        private static string GetGroupName(Document doc, Element elem)
        {
            try
            {
                ElementId gid = elem.GroupId;
                if (gid == null || gid == ElementId.InvalidElementId) return null;

                if (doc.GetElement(gid) is Group grp)
                {
                    if (grp.GroupType != null && !string.IsNullOrEmpty(grp.GroupType.Name))
                        return grp.GroupType.Name;
                    if (!string.IsNullOrEmpty(grp.Name))
                        return grp.Name;
                }
                return $"<группа Id {gid.IntegerValue}>";
            }
            catch { }
            return null;
        }

        // -------------------------------------------------------------------
        // Trim + числовой суффикс
        // -------------------------------------------------------------------

        private static readonly char[] TrimChars = new[]
        {
            ' ', '\t', '\r', '\n', '\f', '\v',
            '\u00A0',
            '\uFEFF',
            '\u200B', '\u200C', '\u200D',
            '\u2060'
        };

        private static string SafeTrim(string s)
            => string.IsNullOrEmpty(s) ? (s ?? "") : s.Trim(TrimChars);

        private static readonly System.Text.RegularExpressions.Regex TrailingSuffixRegex =
            new System.Text.RegularExpressions.Regex(@"^(.*?)[\s_]*(\d+)\s*$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string GetBaseNameWithNumericSuffix(string name)
        {
            string trimmed = SafeTrim(name);
            if (string.IsNullOrEmpty(trimmed)) return null;

            var m = TrailingSuffixRegex.Match(trimmed);
            if (!m.Success) return null;

            string baseName = SafeTrim(m.Groups[1].Value);
            return baseName.Length > 0 ? baseName : null;
        }
    }
}