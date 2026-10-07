using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class DeleteDublicateFamily : IExternalCommand
    {
        public Result Execute(
    ExternalCommandData commandData,
    ref string message,
    ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uiDoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            // Спрашиваем пользователя до старта транзакции.
            TaskDialog dialog = new TaskDialog("Замена семейств")
            {
                MainInstruction = "Исправлять группы на виде?",
                MainContent =
                    "Да  — обрабатывать элементы внутри групп (группы будут изменены).\n" +
                    "Нет — пропустить элементы внутри групп, группы останутся как есть.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No
            };

            TaskDialogResult answer = dialog.Show();




            // Yes → исправлять группы → skipGroups = false
            // No  → не трогать группы → skipGroups = true
            bool correctGroups = answer == TaskDialogResult.Yes;

            
            // Все элементы с вида (чтобы группы не менялись при отборе)
            List<Element> elems = ArmLengthEquels.SelectOrAllElements(false, true);

            HashSet<ElementId> GroupTrueCorrect = new HashSet<ElementId>();
            if (correctGroups)
            {
                //поиск элементов
                foreach (Element instance in elems)
                {

                    // Пропуск элементов внутри чужих групп
                    //allNoGroop=false - тогда все группы пытаемся подкорректировать
                    if (instance.GroupId != ElementId.InvalidElementId)
                    {
                        ElementId groupIdId = instance.GroupId;
                        if(groupIdId!= ElementId.InvalidElementId)
                        {
                            GroupTrueCorrect.Add(groupIdId);
                        }
                    }
                }
            }
            

            using (Transaction trans = new Transaction(doc, "Замена семейств и перенос параметров"))
            {
                trans.Start();
                ReplacedProcess(doc, elems, true, GroupTrueCorrect);// !correctGroups);
                trans.Commit();
            }

            return Result.Succeeded;
        }

        public static bool ReplacedProcess(Document doc, List<Element> elems, bool noGroop, HashSet<ElementId> GroupTrueCorrect, bool showReport = true)
        {
            // -------------------------------------------------------------------
            // 1. Группировка: "ИмяСемейства" -> список экземпляров.
            //    Берём ЛЮБОЙ Element, у которого тип — FamilySymbol.
            //    Так попадают и FamilyInstance, и IndependentTag (выноски),
            //    и другие аннотационные семейства.
            // -------------------------------------------------------------------
            var familyGroups = new Dictionary<string, List<Element>>(StringComparer.OrdinalIgnoreCase);

            foreach (Element elem in elems)
            {
                if (elem == null) continue;

                ElementId typeId = elem.GetTypeId();
                if (typeId == ElementId.InvalidElementId) continue;

                FamilySymbol sym = doc.GetElement(typeId) as FamilySymbol;
                if (sym == null || sym.Family == null) continue;

                string familyName = sym.FamilyName;
                if (string.IsNullOrEmpty(familyName)) continue;

                if (!familyGroups.ContainsKey(familyName))
                    familyGroups[familyName] = new List<Element>();
                familyGroups[familyName].Add(elem);
            }

            // -------------------------------------------------------------------
            // 2. Все загруженные семейства. Ключи — с обрезкой хвостовых пробелов
            //    и невидимых символов.
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
            // 3. Карта "дубликат -> оригинал"
            // -------------------------------------------------------------------
            var replacementMap = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            var unmatchedFamilies = new List<string>();

            foreach (var kvp in familyGroups)
            {
                string currentFamilyName = kvp.Key;
                string baseName = GetBaseNameWithNumericSuffix(currentFamilyName);

                if (baseName == null) continue; // числового суффикса нет — не дубликат

                if (allFamilies.TryGetValue(baseName, out Family originalFamily))
                {
                    replacementMap[currentFamilyName] = originalFamily;
                }
                else
                {
                    unmatchedFamilies.Add($"«{currentFamilyName}» → база «{baseName}»");
                }
            }

            // -------------------------------------------------------------------
            // 4. Счётчики
            // -------------------------------------------------------------------
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
            foreach (var pair in replacementMap)
            {
                string duplicateName = pair.Key;
                Family originalFamily = pair.Value;

                // Словарь типов оригинала. Ключи — обрезанные.
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

                foreach (Element instance in familyGroups[duplicateName])
                {
                    try
                    {
                        // Пропуск элементов внутри чужих групп
                        //allNoGroop=false - тогда все группы пытаемся подкорректировать
                        if ( noGroop && instance.GroupId != ElementId.InvalidElementId)
                        {
                            ElementId groupIdId = instance.GroupId;
                            if (groupIdId != App.OnGroupCurrent.Id && !GroupTrueCorrect.Contains(groupIdId))
                            {
                                var (groupName, groupId, _) = GetGroupInfo(doc, instance);
                                skippedGroup++;
                                string viewName = GetViewName(doc, instance);

                                string famNameForReport = "<без семейства>";
                                var curSymForReport = doc.GetElement(instance.GetTypeId()) as FamilySymbol;
                                if (curSymForReport != null)
                                    famNameForReport = curSymForReport.FamilyName;

                                inGroupInfo.Add((
                                    FamilyName: famNameForReport,
                                    ElementId: instance.Id.IntegerValue,
                                    GroupName: groupName,
                                    GroupId: groupId,
                                    ViewName: viewName
                                ));
                                continue;
                            }
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
                            string diagKey = duplicateName + "|" + (currentTypeName ?? "");
                            if (unmatchedTypesSeen.Add(diagKey))
                            {
                                unmatchedTypes.Add(
                                    $"«{duplicateName}» / «{currentTypeName}»" +
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

                        // --- Замена ---
                        if (!targetSymbol.IsActive)
                            targetSymbol.Activate();

                        ElementId newId = instance.ChangeTypeId(targetSymbol.Id);

                        Element newElement = (newId != ElementId.InvalidElementId)
                            ? doc.GetElement(newId)
                            : instance;

                        if (newElement == null)
                        {
                            App_Apdater_1.AppErrors.LogError(new InvalidOperationException(
                                $"После замены типа для элемента {instance.Id} не удалось получить Element."));
                            failedCount++;
                            continue;
                        }

                        // Применяем сохранённые значения
                        foreach (var kvp in paramValues)
                        {
                            Parameter newParam = newElement.LookupParameter(kvp.Key);
                            if (newParam == null || newParam.IsReadOnly) continue;
                            if (newParam.StorageType == StorageType.ElementId) continue;
                            try { SetParameterValue(newParam, kvp.Value); }
                            catch (Exception) { continue; }
                        }

                        replacedCount++;

                        string replacedViewName = GetViewName(doc, instance);
                        if (!string.IsNullOrEmpty(replacedViewName))
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

            // -------------------------------------------------------------------
            // 6. Отчёт
            // -------------------------------------------------------------------
            if (showReport)
            {
                var sb = new System.Text.StringBuilder();
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
                td.MainInstruction = $"Заменено: {replacedCount}";
                td.MainContent = sb.ToString();
                td.Show();
            }
            return true;
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
            return null;
        }

        // -------------------------------------------------------------------
        // Trim + работа с числовым суффиксом
        // -------------------------------------------------------------------

        // Символы, которые убираем по краям имён.
        private static readonly char[] TrimChars = new[]
        {
            ' ', '\t', '\r', '\n', '\f', '\v',   // обычные whitespace
            '\u00A0',                             // неразрывный пробел
            '\uFEFF',                             // BOM / zero-width no-break
            '\u200B', '\u200C', '\u200D',         // zero-width space/non-joiner/joiner
            '\u2060'                              // word joiner
        };

        private static string SafeTrim(string s)
            => string.IsNullOrEmpty(s) ? (s ?? "") : s.Trim(TrimChars);

        // Регекс: <база> [пробелы/подчёркивания] <цифры> [пробелы] конец
        private static readonly System.Text.RegularExpressions.Regex TrailingSuffixRegex =
            new System.Text.RegularExpressions.Regex(@"^(.*?)[\s_]*(\d+)\s*$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Снимает с конца имени числовой суффикс (с опциональным пробелом/подчёркиванием).
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