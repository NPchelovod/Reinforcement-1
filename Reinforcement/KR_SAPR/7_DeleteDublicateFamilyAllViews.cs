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

            var replacedViews = new Dictionary<string, int>();

            var inGroupInfo = new List<(
                string FamilyName,
                int ElementId,
                string GroupName,
                int GroupId,
                string ViewName)>();

            var unmatchedTypes = new List<string>();
            var unmatchedTypesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                                string viewName = GetViewName(doc, instance);

                                var curSym = doc.GetElement(instance.GetTypeId()) as FamilySymbol;
                                string famName = curSym?.FamilyName ?? "<без семейства>";

                                inGroupInfo.Add((
                                    FamilyName: famName,
                                    ElementId: instance.Id.IntegerValue,
                                    GroupName: groupName,
                                    GroupId: groupId,
                                    ViewName: viewName
                                ));
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

                            string replacedViewName = GetViewName(doc, newElement);
                            if (!string.IsNullOrEmpty(replacedViewName)
                                && replacedViewName != "<вид не определён>")
                            {
                                replacedViews.TryGetValue(replacedViewName, out int c);
                                replacedViews[replacedViewName] = c + 1;
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

            if (replacedViews.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("По видам:");
                foreach (var kv in replacedViews.OrderByDescending(k => k.Value))
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
            }

            if (inGroupInfo.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Пропущены, т.к. внутри групп ({inGroupInfo.Count}) ===");
                foreach (var g in inGroupInfo
                         .GroupBy(x => new { x.GroupId, x.GroupName })
                         .OrderByDescending(g => g.Count()))
                {
                    sb.AppendLine($"  Группа «{g.Key.GroupName}» (Id {g.Key.GroupId}) — {g.Count()} шт.:");
                    foreach (var row in g)
                        sb.AppendLine($"      [{row.FamilyName}] Id {row.ElementId}  →  вид: {row.ViewName}");
                }
            }

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