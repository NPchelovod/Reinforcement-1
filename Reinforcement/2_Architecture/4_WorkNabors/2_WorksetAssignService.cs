using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement.Worksets
{
    public static class WorksetAssignService
    {
        /// <summary>
        /// Основной метод — вызывать ТОЛЬКО из ExternalEventHandler.
        /// </summary>
        public static void Assign(
            Document doc,
            IList<WorksetRule> rules,
            bool onlyVisibleInActiveView,
            bool onlySelected,
            bool createMissingWorksets,
            out int changedCount,
            out int skippedCount,
            out List<string> log)
        {
            changedCount = 0;
            skippedCount = 0;
            log = new List<string>();

            if (doc == null)
            {
                log.Add("Нет активного документа.");
                return;
            }

            // --- Worksharing ---
            if (!doc.IsWorkshared)
            {
                if (doc.CanEnableWorksharing())
                {
                    try
                    {
                        doc.EnableWorksharing("Общие уровни и сетки", "Рабочий набор 1");
                        log.Add("Включён режим совместной работы (worksharing).");
                    }
                    catch (Exception ex)
                    {
                        log.Add($"Не удалось включить worksharing: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    log.Add("Документ не workshared и не может быть переведён в этот режим.");
                    return;
                }
            }

            // --- Сбор элементов ---
            UIDocument uidoc = new UIDocument(doc);
            IEnumerable<Element> elements;

            if (onlySelected)
            {
                var ids = uidoc.Selection.GetElementIds();
                elements = ids.Select(id => doc.GetElement(id)).Where(e => e != null);
            }
            else if (onlyVisibleInActiveView && doc.ActiveView != null)
            {
                elements = new FilteredElementCollector(doc, doc.ActiveView.Id)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }
            else
            {
                elements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements();
            }

            var list = elements.ToList();

            // --- Существующие рабочие наборы ---
            var existingWorksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToDictionary(w => w.Name, w => w.Id, StringComparer.OrdinalIgnoreCase);

            // --- Создание недостающих ---
            if (createMissingWorksets)
            {
                var requiredNames = rules
                    .Where(r => r.IsEnabled && !string.IsNullOrWhiteSpace(r.WorksetName))
                    .Select(r => r.WorksetName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                using (var t = new Transaction(doc, "Создание рабочих наборов"))
                {
                    t.Start();
                    foreach (var name in requiredNames)
                    {
                        if (existingWorksets.ContainsKey(name)) continue;
                        try
                        {
                            var ws = Workset.Create(doc, name);
                            existingWorksets[name] = ws.Id;
                            log.Add($"Создан рабочий набор «{name}».");
                        }
                        catch (Exception ex)
                        {
                            log.Add($"Не удалось создать «{name}»: {ex.Message}");
                        }
                    }
                    t.Commit();
                }
            }

            // --- Применение правил ---
            using (var t = new Transaction(doc, "Распределение по рабочим наборам"))
            {
                t.Start();

                foreach (var el in list)
                {
                    if (el is ElementType) { skippedCount++; continue; }

                    // Спец-исключение: двери, уже лежащие в «АР_двери непечатные», не трогаем
                    if (IsDoorInNonPrintableWorkset(el, existingWorksets))
                    {
                        skippedCount++;
                        continue;
                    }

                    WorksetRule matched = FindMatchingRule(el, rules, doc);
                    if (matched == null) { skippedCount++; continue; }

                    if (!existingWorksets.TryGetValue(matched.WorksetName, out var targetWsId))
                    {
                        log.Add($"Рабочий набор «{matched.WorksetName}» не найден — элемент {el.Id.Value} пропущен.");
                        skippedCount++;
                        continue;
                    }

                    if (el.WorksetId == targetWsId) { skippedCount++; continue; }

                    try
                    {
                        var p = el.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (p == null || p.IsReadOnly) { skippedCount++; continue; }

                        p.Set(targetWsId.IntegerValue);
                        changedCount++;
                    }
                    catch (Exception ex)
                    {
                        log.Add($"Ошибка на элементе {el.Id.Value}: {ex.Message}");
                        skippedCount++;
                    }
                }

                t.Commit();
            }

            log.Add($"Готово. Изменено: {changedCount}, пропущено: {skippedCount}.");
        }

        // ------------------------------------------------------------------
        private static WorksetRule FindMatchingRule(Element el, IList<WorksetRule> rules, Document doc)
        {
            foreach (var rule in rules)
            {
                if (!rule.IsEnabled) continue;
                if (Matches(rule, el, doc)) return rule;
            }
            return null;
        }

        // ------------------------------------------------------------------
        private static bool Matches(WorksetRule rule, Element el, Document doc)
        {
            // --- 1. Фильтр по классу/категории ---
            if (rule.MatchImportInstanceOnly)
            {
                if (!(el is ImportInstance imp) || imp.IsLinked) return false;
            }
            else if (rule.MatchCadLinkOnly)
            {
                if (!(el is ImportInstance imp) || !imp.IsLinked) return false;
            }
            else if (rule.Categories != null && rule.Categories.Count > 0)
            {
                if (el.Category == null) return false;
                if (!rule.Categories.Contains(el.Category.BuiltInCategory)) return false;
            }

            // --- 2. Фильтр по имени файла связи ---
            if (!string.IsNullOrWhiteSpace(rule.LinkNameContains))
            {
                string linkName = GetLinkName(el, doc);
                if (string.IsNullOrEmpty(linkName)) return false;
                if (linkName.IndexOf(rule.LinkNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            // --- 3. Фильтр по имени типа ---
            string typeName = GetTypeName(el, doc);

            if (!string.IsNullOrWhiteSpace(rule.TypeNameContains))
            {
                if (string.IsNullOrEmpty(typeName)) return false;
                if (typeName.IndexOf(rule.TypeNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(rule.TypeNameExcludes))
            {
                if (!string.IsNullOrEmpty(typeName) &&
                    typeName.IndexOf(rule.TypeNameExcludes, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        private static string GetTypeName(Element el, Document doc)
        {
            try
            {
                var typeId = el.GetTypeId();
                if (typeId == null || typeId == ElementId.InvalidElementId) return null;
                var type = doc.GetElement(typeId);
                return type?.Name;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------
        private static string GetLinkName(Element el, Document doc)
        {
            try
            {
                // Revit-связь: имя файла = Name у RevitLinkType
                if (el is RevitLinkInstance rli)
                {
                    var typeId = rli.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        var linkType = doc.GetElement(typeId) as RevitLinkType;
                        if (linkType != null) return linkType.Name;
                    }
                    return rli.Name;
                }

                // CAD-связь: имя файла = Name у CADLinkType
                if (el is ImportInstance ii && ii.IsLinked)
                {
                    var typeId = ii.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        var cadType = doc.GetElement(typeId) as CADLinkType;
                        if (cadType != null) return cadType.Name;
                    }
                }
            }
            catch { /* ignore */ }
            return null;
        }

        // ------------------------------------------------------------------
        private static bool IsDoorInNonPrintableWorkset(
            Element el,
            Dictionary<string, WorksetId> worksets)
        {
            if (el.Category?.BuiltInCategory != BuiltInCategory.OST_Doors) return false;
            if (!worksets.TryGetValue("АР_двери непечатные", out var wsId)) return false;
            return el.WorksetId == wsId;
        }
    }
}