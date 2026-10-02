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

            // 1. Все экземпляры семейств во всём проекте
            List<FamilyInstance> allInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol != null)
                .ToList();

            // 2. Группировка по имени семейства
            var familyGroups = new Dictionary<string, List<FamilyInstance>>();
            foreach (var fi in allInstances)
            {
                string familyName = fi.Symbol.FamilyName;
                if (!familyGroups.TryGetValue(familyName, out var list))
                {
                    list = new List<FamilyInstance>();
                    familyGroups[familyName] = list;
                }
                list.Add(fi);
            }

            // 3. Все семейства проекта
            var allFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            var suffixRegex = new System.Text.RegularExpressions.Regex(@"(\d+)$");
            var replacementMap = new Dictionary<string, Family>();

            foreach (var kvp in familyGroups)
            {
                string currentFamilyName = kvp.Key;
                var match = suffixRegex.Match(currentFamilyName);
                if (!match.Success) continue;

                string baseName = currentFamilyName.Substring(0, currentFamilyName.Length - match.Length);
                if (allFamilies.TryGetValue(baseName, out Family originalFamily))
                    replacementMap[currentFamilyName] = originalFamily;
            }

            // === СЧЁТЧИКИ ===
            int replacedCount = 0;
            int skippedNoType = 0;
            int skippedGroup = 0;
            int skippedSameType = 0;
            int failedCount = 0;

            var replacedViews = new Dictionary<string, int>();

            // Список пропущенных из-за группы: имя семейства, Id элемента, имя группы, Id группы, имя вида
            var inGroupInfo = new List<(
                string FamilyName,
                int ElementId,
                string GroupName,
                int GroupId,
                string ViewName)>();

            // 4. Замена
            using (Transaction trans = new Transaction(doc, "Замена дубликатов во всех видах"))
            {
                trans.Start();

                foreach (var pair in replacementMap)
                {
                    string dupName = pair.Key;
                    Family originalFamily = pair.Value;

                    var originalSymbols = originalFamily.GetFamilySymbolIds()
                        .Select(id => doc.GetElement(id) as FamilySymbol)
                        .Where(s => s != null)
                        .ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

                    foreach (FamilyInstance instance in familyGroups[dupName])
                    {
                        try
                        {
                            // Пропускаем элементы в группах
                            if (instance.GroupId != ElementId.InvalidElementId)
                            {
                                skippedGroup++;

                                var (groupName, groupId, _) = GetGroupInfo(doc, instance);
                                string viewName = GetViewName(doc, instance);

                                inGroupInfo.Add((
                                    FamilyName: instance.Symbol?.FamilyName ?? "<без семейства>",
                                    ElementId: instance.Id.IntegerValue,
                                    GroupName: groupName,
                                    GroupId: groupId,
                                    ViewName: viewName
                                ));

                                continue;
                            }

                            string currentTypeName = instance.Symbol.Name;
                            FamilySymbol targetSymbol = null;

                            if (!originalSymbols.TryGetValue(currentTypeName, out targetSymbol))
                            {
                                var m = NumericSuffixRegex.Match(currentTypeName);
                                if (m.Success)
                                {
                                    string baseTypeName = currentTypeName
                                        .Substring(0, currentTypeName.Length - m.Length)
                                        .TrimEnd();
                                    if (baseTypeName.Length > 0)
                                        originalSymbols.TryGetValue(baseTypeName, out targetSymbol);
                                }
                            }

                            if (targetSymbol == null)
                            {
                                skippedNoType++;
                                continue;
                            }

                            if (targetSymbol.Id == instance.Symbol.Id)
                            {
                                skippedSameType++;
                                continue;
                            }

                            // Копируем параметры до замены
                            var paramValues = new Dictionary<string, object>();
                            foreach (Parameter p in instance.Parameters)
                            {
                                if (p.HasValue && p.StorageType != StorageType.None)
                                    paramValues[p.Definition.Name] = GetParameterValue(p);
                            }

                            if (!targetSymbol.IsActive)
                                targetSymbol.Activate();

                            ElementId failedId = instance.ChangeTypeId(targetSymbol.Id);
                            if (failedId != ElementId.InvalidElementId)
                            {
                                failedCount++;
                                continue;
                            }

                            // Возвращаем параметры
                            foreach (var pkv in paramValues)
                            {
                                Parameter np = instance.LookupParameter(pkv.Key);
                                if (np == null || np.IsReadOnly) continue;
                                if (np.StorageType == StorageType.ElementId) continue;
                                try { SetParameterValue(np, pkv.Value); } catch { }
                            }

                            // === УСПЕШНАЯ ЗАМЕНА ===
                            replacedCount++;

                            string replacedViewName = GetViewName(doc, instance);
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
                sb.AppendLine($"Не удалось заменить (ChangeTypeId вернул ошибку): {failedCount}");

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

            TaskDialog td = new TaskDialog("Замена дубликатов семейств");
            td.MainInstruction = $"Готово. Заменено: {replacedCount}";
            td.MainContent = sb.ToString();
            td.Show();

            return Result.Succeeded;
        }

        private static (string Name, int Id, ElementId GroupId) GetGroupInfo(Document doc, FamilyInstance fi)
        {
            ElementId gid = fi.GroupId;
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

        private static string GetViewName(Document doc, FamilyInstance fi)
        {
            try
            {
                ElementId vid = fi.OwnerViewId;
                if (vid == ElementId.InvalidElementId && fi.GroupId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(fi.GroupId) is Group grp)
                        vid = grp.OwnerViewId;
                }
                if (vid != ElementId.InvalidElementId && doc.GetElement(vid) is View v)
                    return v.Name;
            }
            catch { }
            return "<вид не определён>";
        }

        private static readonly System.Text.RegularExpressions.Regex NumericSuffixRegex =
            new System.Text.RegularExpressions.Regex(@"\s*(\d+)$");

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