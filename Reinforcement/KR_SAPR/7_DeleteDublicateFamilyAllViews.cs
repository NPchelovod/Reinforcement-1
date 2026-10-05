using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class ReplaceDuplicatesAllViews : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;


            // Спрашиваем пользователя до старта транзакции.
            TaskDialog dialog = new TaskDialog("Замена семейств")
            {
                MainInstruction = "Уверены, Исправлять во всём проекте?",
                MainContent =
                    "Да  — обрабатывать элементы на всех видах.\n" +
                    "Нет — отмена операции.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No
            };

            TaskDialogResult answer = dialog.Show();

            // Yes → исправлять группы → skipGroups = false
            // No  → не трогать группы → skipGroups = true
           
            if (answer!=TaskDialogResult.Yes)
            {
                return Result.Succeeded;
            }

            // -------------------------------------------------------------------
            // 1. Все элементы с типом-семейством (FamilyInstance + IndependentTag,
            //    TextNote-аннотации и т.п.), у которых есть FamilySymbol.
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
            // 2. Группировка по имени семейства
            // -------------------------------------------------------------------
            var familyGroups = new Dictionary<string, List<Element>>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in allElements)
            {
                var sym = doc.GetElement(el.GetTypeId()) as FamilySymbol;
                if (sym == null || sym.Family == null) continue;

                string familyName = sym.FamilyName;
                if (string.IsNullOrEmpty(familyName)) continue;

                if (!familyGroups.TryGetValue(familyName, out var list))
                {
                    list = new List<Element>();
                    familyGroups[familyName] = list;
                }
                list.Add(el);
            }

            // -------------------------------------------------------------------
            // 3. Все семейства проекта (ключи с обрезкой)
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
            // 4. Карта "дубликат -> оригинал"
            // -------------------------------------------------------------------
            var replacementMap = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            var unmatchedFamilies = new List<string>();

            foreach (var kvp in familyGroups)
            {
                string currentFamilyName = kvp.Key;
                string baseName = GetBaseNameWithNumericSuffix(currentFamilyName);
                if (baseName == null) continue;

                if (allFamilies.TryGetValue(baseName, out Family originalFamily))
                    replacementMap[currentFamilyName] = originalFamily;
                else
                    unmatchedFamilies.Add($"«{currentFamilyName}» → база «{baseName}»");
            }

            // === СЧЁТЧИКИ ===
            int replacedCount = 0;
            int skippedNoType = 0;
            int skippedGroup = 0;
            int skippedSameType = 0;
            int failedCount = 0;

            

           

            var unmatchedTypes = new List<string>();
            var unmatchedTypesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Иерархические накопители для замен:
            // sheetKey → ReplaceSheetData  (только листы)
            var replaceSheetData = new Dictionary<string, ReplaceSheetData>(StringComparer.OrdinalIgnoreCase);
            // viewName → ReplaceViewData    (виды без листа, идут в конце)
            var replaceNoSheet = new Dictionary<string, ReplaceViewData>(StringComparer.OrdinalIgnoreCase);

            var inGroupInfo = new List<GroupSkipInfo>();
            // -------------------------------------------------------------------
            // 4a. Карта "viewId → (имя листа, номер листа)"
            //     Виды попадают на лист только через Viewport'ы.
            // -------------------------------------------------------------------
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
            // -------------------------------------------------------------------
            // 5. Замена
            // -------------------------------------------------------------------
            using (Transaction trans = new Transaction(doc, "Замена дубликатов во всех видах"))
            {
                trans.Start();

                foreach (var pair in replacementMap)
                {
                    string dupName = pair.Key;
                    Family originalFamily = pair.Value;

                    // Типы оригинала. Ключи — обрезанные.
                    var originalSymbols = new Dictionary<string, FamilySymbol>(StringComparer.OrdinalIgnoreCase);
                    foreach (var symId in originalFamily.GetFamilySymbolIds())
                    {
                        var sym = doc.GetElement(symId) as FamilySymbol;
                        if (sym == null) continue;
                        string symName = SafeTrim(sym.Name);
                        if (string.IsNullOrEmpty(symName)) continue;
                        if (!originalSymbols.ContainsKey(symName))
                            originalSymbols[symName] = sym;
                    }

                    foreach (Element instance in familyGroups[dupName])
                    {
                        try
                        {
                            // Пропуск элементов в группах
                            if (instance.GroupId != ElementId.InvalidElementId)
                            {
                                skippedGroup++;

                                var (groupName, groupId, _) = GetGroupInfo(doc, instance);
                                var (_, viewName, sheetName, sheetNumber) = GetViewAndSheet(doc, instance, viewToSheet);

                                var curSym = doc.GetElement(instance.GetTypeId()) as FamilySymbol;
                                string famName = curSym?.FamilyName ?? "<без семейства>";

                                inGroupInfo.Add(new GroupSkipInfo
                                {
                                    FamilyName = famName,
                                    OrigFamilyName = originalFamily.Name,
                                    ElementId = instance.Id.IntegerValue,
                                    GroupName = groupName,
                                    GroupId = groupId,
                                    ViewName = viewName,
                                    SheetName = sheetName,
                                    SheetNumber = sheetNumber
                                });
                                continue;
                            }

                            FamilySymbol currentSymbol = doc.GetElement(instance.GetTypeId()) as FamilySymbol;
                            if (currentSymbol == null)
                            {
                                skippedNoType++;
                                continue;
                            }

                            string currentTypeName = SafeTrim(currentSymbol.Name);
                            FamilySymbol targetSymbol = null;

                            // 1) прямое совпадение
                            if (!string.IsNullOrEmpty(currentTypeName))
                                originalSymbols.TryGetValue(currentTypeName, out targetSymbol);

                            // 2) со снятием числового суффикса
                            string baseTypeName = null;
                            if (targetSymbol == null)
                            {
                                baseTypeName = GetBaseNameWithNumericSuffix(currentTypeName);
                                if (baseTypeName != null)
                                    originalSymbols.TryGetValue(baseTypeName, out targetSymbol);
                            }

                            if (targetSymbol == null)
                            {
                                string diagKey = dupName + "|" + (currentTypeName ?? "");
                                if (unmatchedTypesSeen.Add(diagKey))
                                {
                                    unmatchedTypes.Add(
                                        $"«{dupName}» / «{currentTypeName}»" +
                                        (baseTypeName != null ? $" → база «{baseTypeName}»" : " (нет суффикса)"));
                                }
                                skippedNoType++;
                                continue;
                            }

                            if (targetSymbol.Id == currentSymbol.Id)
                            {
                                skippedSameType++;
                                continue;
                            }

                            // Копируем параметры ДО замены
                            var paramValues = new Dictionary<string, object>();
                            foreach (Parameter p in instance.Parameters)
                            {
                                if (p.HasValue && p.StorageType != StorageType.None)
                                    paramValues[p.Definition.Name] = GetParameterValue(p);
                            }

                            if (!targetSymbol.IsActive)
                                targetSymbol.Activate();

                            // ВАЖНО: ChangeTypeId возвращает:
                            //   - InvalidElementId, если элемент изменён "на месте";
                            //   - новый ElementId, если элемент был пересоздан.
                            // Ошибка — исключение, а не возвращаемое значение.
                            ElementId newId = instance.ChangeTypeId(targetSymbol.Id);

                            Element newElement = (newId != ElementId.InvalidElementId)
                                ? doc.GetElement(newId)
                                : instance;

                            if (newElement == null)
                            {
                                App_Apdater_1.AppErrors.LogError(new InvalidOperationException(
                                    $"После ChangeTypeId для элемента {instance.Id} не удалось получить Element."));
                                failedCount++;
                                continue;
                            }

                            // Возвращаем параметры — в newElement (не в instance!)
                            foreach (var pkv in paramValues)
                            {
                                Parameter np = newElement.LookupParameter(pkv.Key);
                                if (np == null || np.IsReadOnly) continue;
                                if (np.StorageType == StorageType.ElementId) continue;
                                try { SetParameterValue(np, pkv.Value); } catch { }
                            }

                            replacedCount++;

                            string dupFamilyName = dupName;
                            string origFamilyName = originalFamily.Name;

                            var (_, replacedViewName, replacedSheetName, replacedSheetNumber) =
                                GetViewAndSheet(doc, newElement, viewToSheet);

                            if (!string.IsNullOrEmpty(replacedViewName) &&
                                replacedViewName != "<вид не определён>")
                            {
                                string replaceKey = dupFamilyName + " → " + origFamilyName;

                                if (!string.IsNullOrEmpty(replacedSheetName))
                                {
                                    string sheetKey = replacedSheetNumber + "|" + replacedSheetName;

                                    if (!replaceSheetData.TryGetValue(sheetKey, out var sheetBucket))
                                    {
                                        sheetBucket = new ReplaceSheetData
                                        {
                                            SheetName = replacedSheetName,
                                            SheetNumber = replacedSheetNumber
                                        };
                                        replaceSheetData[sheetKey] = sheetBucket;
                                    }

                                    if (!sheetBucket.Views.TryGetValue(replacedViewName, out var vBucket))
                                    {
                                        vBucket = new ReplaceViewData();
                                        sheetBucket.Views[replacedViewName] = vBucket;
                                    }

                                    vBucket.FamilyReplaces.TryGetValue(replaceKey, out int c);
                                    vBucket.FamilyReplaces[replaceKey] = c + 1;
                                    sheetBucket.TotalReplaces++;
                                }
                                else
                                {
                                    if (!replaceNoSheet.TryGetValue(replacedViewName, out var vBucket))
                                    {
                                        vBucket = new ReplaceViewData();
                                        replaceNoSheet[replacedViewName] = vBucket;
                                    }

                                    vBucket.FamilyReplaces.TryGetValue(replaceKey, out int c);
                                    vBucket.FamilyReplaces[replaceKey] = c + 1;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App_Apdater_1.AppErrors.LogError(ex);
                            continue;
                        }
                    }
                }

                trans.Commit();
            }

            // === ОТЧЁТ ===
            var sb = new StringBuilder();
            sb.AppendLine($"Успешно заменено: {replacedCount}");

            if (skippedNoType > 0)
                sb.AppendLine($"Пропущено (нет подходящего типоразмера): {skippedNoType}");
            if (skippedGroup > 0)
                sb.AppendLine($"Пропущено (внутри групп): {skippedGroup}");
            if (skippedSameType > 0)
                sb.AppendLine($"Пропущено (тип уже совпадает): {skippedSameType}");
            if (failedCount > 0)
                sb.AppendLine($"Не удалось заменить (после ChangeTypeId не получен Element): {failedCount}");

            // --- Замены по листам ---
            if (replaceSheetData.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Замены по листам ===");

                foreach (var bucket in replaceSheetData.Values
                    .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.SheetName, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Лист «{bucket.SheetName}» №{bucket.SheetNumber} (заменено: {bucket.TotalReplaces})");

                    foreach (var vk in bucket.Views.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"  Вид «{vk.Key}»:");
                        foreach (var fk in vk.Value.FamilyReplaces
                            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            sb.AppendLine($"    {fk.Key} — {fk.Value} шт.");
                        }
                    }
                }
            }

            // --- Замены по видам без листа ---
            if (replaceNoSheet.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Замены на видах без листа ===");

                foreach (var vk in replaceNoSheet.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  Вид «{vk.Key}»:");
                    foreach (var fk in vk.Value.FamilyReplaces
                        .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"    {fk.Key} — {fk.Value} шт.");
                    }
                }
            }

            // --- Пропущенные в группах ---
            if (inGroupInfo.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Пропущены, т.к. внутри групп ({inGroupInfo.Count}) ===");

                // Сначала по листам, потом без листа.
                var withSheet = inGroupInfo
                    .Where(g => !string.IsNullOrEmpty(g.SheetName))
                    .GroupBy(g => new { g.SheetNumber, g.SheetName })
                    .OrderBy(gr => gr.Key.SheetNumber, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(gr => gr.Key.SheetName, StringComparer.OrdinalIgnoreCase);

                foreach (var sheetGroup in withSheet)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Лист «{sheetGroup.Key.SheetName}» №{sheetGroup.Key.SheetNumber}:");

                    foreach (var viewGroup in sheetGroup
                        .GroupBy(g => g.ViewName)
                        .OrderBy(vg => vg.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"  Вид «{viewGroup.Key}»:");

                        foreach (var grp in viewGroup
                            .GroupBy(g => new { g.GroupId, g.GroupName })
                            .OrderByDescending(gg => gg.Count()))
                        {
                            sb.AppendLine($"    Группа «{grp.Key.GroupName}» (Id {grp.Key.GroupId}) — {grp.Count()} шт.:");
                            foreach (var row in grp.OrderBy(r => r.ElementId))
                            {
                                sb.AppendLine(
                                    $"      {row.FamilyName} → {row.OrigFamilyName}  [Id {row.ElementId}]");
                            }
                        }
                    }
                }

                var noSheet = inGroupInfo.Where(g => string.IsNullOrEmpty(g.SheetName));
                if (noSheet.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("Виды без листа:");

                    foreach (var viewGroup in noSheet
                        .GroupBy(g => g.ViewName)
                        .OrderBy(vg => vg.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"  Вид «{viewGroup.Key}»:");

                        foreach (var grp in viewGroup
                            .GroupBy(g => new { g.GroupId, g.GroupName })
                            .OrderByDescending(gg => gg.Count()))
                        {
                            sb.AppendLine($"    Группа «{grp.Key.GroupName}» (Id {grp.Key.GroupId}) — {grp.Count()} шт.:");
                            foreach (var row in grp.OrderBy(r => r.ElementId))
                            {
                                sb.AppendLine(
                                    $"      {row.FamilyName} → {row.OrigFamilyName}  [Id {row.ElementId}]");
                            }
                        }
                    }
                }
            }

            // --- Unmatched ---
            if (unmatchedFamilies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Дубликаты-семейства без базового семейства ({unmatchedFamilies.Count}) ===");
                foreach (var s in unmatchedFamilies)
                    sb.AppendLine($"  {s}");
            }

            if (unmatchedTypes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Типы-дубликаты без базового типа ({unmatchedTypes.Count}) ===");
                foreach (var s in unmatchedTypes.Take(30))
                    sb.AppendLine($"  {s}");
                if (unmatchedTypes.Count > 30)
                    sb.AppendLine($"  ... и ещё {unmatchedTypes.Count - 30}");
            }

            TaskDialog td = new TaskDialog("Замена дубликатов семейств");
            td.MainInstruction = $"Готово. Заменено: {replacedCount}";
            td.MainContent = sb.ToString();
            td.Show();

            return Result.Succeeded;
        }

        // -------------------------------------------------------------------
        // Служебные
        // -------------------------------------------------------------------
        /// <summary>
        /// Определяет вид и лист, на которых отображается элемент.
        /// Если элемент лежит прямо на листе (без промежуточного вида) — ViewName = "&lt;на листе&gt;".
        /// Если вид не на листе — SheetName/SheetNumber = null.
        /// </summary>
        private static (ElementId ViewId, string ViewName, string SheetName, string SheetNumber)
            GetViewAndSheet(Document doc, Element elem,
                            Dictionary<ElementId, (string Name, string Number)> viewToSheet)
        {
            try
            {
                ElementId vid = elem.OwnerViewId;
                if (vid == ElementId.InvalidElementId && elem.GroupId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(elem.GroupId) is Group grp)
                        vid = grp.OwnerViewId;
                }

                if (vid == ElementId.InvalidElementId)
                    return (ElementId.InvalidElementId, "<вид не определён>", null, null);

                Element viewElem = doc.GetElement(vid);

                // Случай: элемент прямо на листе.
                if (viewElem is ViewSheet directSheet)
                    return (vid, "<на листе>", directSheet.Name, directSheet.SheetNumber);

                // Случай: обычный вид.
                if (viewElem is View v)
                {
                    if (viewToSheet != null && viewToSheet.TryGetValue(vid, out var s))
                        return (vid, v.Name, s.Name, s.Number);
                    return (vid, v.Name, null, null);
                }
            }
            catch { }

            return (ElementId.InvalidElementId, "<вид не определён>", null, null);
        }
        private sealed class ReplaceSheetData
        {
            public string SheetName;
            public string SheetNumber;
            public int TotalReplaces;
            public Dictionary<string, ReplaceViewData> Views =
                new Dictionary<string, ReplaceViewData>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ReplaceViewData
        {
            // Ключ: "Дубликат → Оригинал", значение — сколько раз заменили.
            public Dictionary<string, int> FamilyReplaces =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class GroupSkipInfo
        {
            public string FamilyName;       // имя семейства-дубликата
            public string OrigFamilyName;   // на какое семейство хотели бы заменить
            public int ElementId;
            public string GroupName;
            public int GroupId;
            public string ViewName;
            public string SheetName;        // null, если вид не на листе
            public string SheetNumber;
        }
        private static (string Name, int Id, ElementId GroupId) GetGroupInfo(Document doc, Element elem)
        {
            ElementId gid = elem.GroupId;
            if (gid == null || gid == ElementId.InvalidElementId)
                return ("<без группы>", -1, ElementId.InvalidElementId);

            string name = $"<группа Id {gid.IntegerValue}>";
            try
            {
                if (doc.GetElement(gid) is Group grp)
                {
                    if (grp.GroupType != null && !string.IsNullOrEmpty(grp.GroupType.Name))
                        name = grp.GroupType.Name;
                    else if (!string.IsNullOrEmpty(grp.Name))
                        name = grp.Name;
                }
            }
            catch { }

            return (name, gid.IntegerValue, gid);
        }

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

        /// <summary>
        /// Снимает с конца имени числовой суффикс.
        /// Возвращает null, если суффикса нет.
        /// </summary>
        private static string GetBaseNameWithNumericSuffix(string name)
        {
            string trimmed = SafeTrim(name);
            if (string.IsNullOrEmpty(trimmed)) return null;

            var m = TrailingSuffixRegex.Match(trimmed);
            if (!m.Success) return null;

            string baseName = SafeTrim(m.Groups[1].Value);
            return baseName.Length > 0 ? baseName : null;
        }

        // -------------------------------------------------------------------
        // Параметры
        // -------------------------------------------------------------------

        private static object GetParameterValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.Integer: return p.AsInteger();
                case StorageType.Double: return p.AsDouble();
                case StorageType.String: return p.AsString();
                case StorageType.ElementId: return p.AsElementId();
                default: return null;
            }
        }

        private static void SetParameterValue(Parameter p, object value)
        {
            switch (p.StorageType)
            {
                case StorageType.Integer: p.Set((int)value); break;
                case StorageType.Double: p.Set((double)value); break;
                case StorageType.String: p.Set((string)value); break;
                case StorageType.ElementId: p.Set((ElementId)value); break;
            }
        }
    }
}