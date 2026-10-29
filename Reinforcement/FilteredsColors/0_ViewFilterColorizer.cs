using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    /// <summary>
    /// Правило: если имя фильтра содержит MatchText — применить Override.
    /// </summary>
    public class ColorFilterRule
    {
        public int Id { get; }
        public string MatchText { get; }
        public OverrideGraphicSettings Override { get; }

        public ColorFilterRule(int id, string matchText, Color lineColor)
        {
            Id = id;
            MatchText = matchText;
            Override = new OverrideGraphicSettings().SetProjectionLineColor(lineColor);
        }
    }

    /// <summary>
    /// Единая точка применения цветовых переопределений к фильтрам вида.
    /// Автоматически разрешает вид/шаблон вида.
    /// </summary>
    public static class ViewFilterColorizer
    {
        /// <summary>
        /// Применяет единый список правил к виду. 
        /// Сначала ищет по ID, если ID=0 или не найден - ищет по имени.
        /// </summary>
        public static int ApplyByRules(
            Document doc,
            View activeView,
            IList<ColorFilterRule> rules,
            string transactionName = "Применение фильтров")
        {
            if (doc == null || activeView == null || rules == null || !rules.Any()) return 0;

            var allFilters = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .ToList();

            View targetView = ResolveTargetView(doc, activeView);
            int applied = 0;
            var processedFilterIds = new HashSet<ElementId>();

            using (var t = new Transaction(doc, transactionName))
            {
                t.Start();
                try
                {
                    foreach (var rule in rules)
                    {
                        ParameterFilterElement targetFilter = null;

                        // 1. Поиск по ID (если задан)
                        if (rule.Id != 0)
                        {
                            targetFilter = allFilters.FirstOrDefault(f => f.Id.Value == rule.Id);
                        }

                        // 2. Поиск по имени (если ID = 0 или по ID не нашли)
                        if (targetFilter == null && !string.IsNullOrEmpty(rule.MatchText))
                        {
                            targetFilter = allFilters.FirstOrDefault(f => f.Name.Contains(rule.MatchText));
                        }

                        // 3. Применяем, если нашли и еще не обрабатывали
                        if (targetFilter != null && processedFilterIds.Add(targetFilter.Id))
                        {
                            var id = targetFilter.Id;
                            if (!targetView.GetFilters().Contains(id))
                                targetView.AddFilter(id);

                            targetView.SetFilterVisibility(id, true);
                            targetView.SetFilterOverrides(id, rule.Override);
                            applied++;
                        }
                    }
                    t.Commit();
                }
                catch
                {
                    if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                    throw;
                }
            }
            return applied;
        }

        private static View ResolveTargetView(Document doc, View activeView)
        {
            if (activeView.ViewTemplateId == ElementId.InvalidElementId)
                return activeView;

            var template = doc.GetElement(activeView.ViewTemplateId) as View;
            return template ?? activeView;
        }
    }
}
