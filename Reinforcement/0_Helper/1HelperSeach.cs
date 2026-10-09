using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
namespace Reinforcement
{
    // Compatibility facade: existing command signatures are retained.
    public class HelperSeach
    {
        public static Dictionary<Document, Dictionary<string, Element>> PastElements = new Dictionary<Document, Dictionary<string, Element>>();
        public static bool ResetNamesParam;
        public static void ClearCache(Document document = null)
        {
            if (document == null) PastElements.Clear();
            else PastElements.Remove(document);
        }
        public static HashSet<string> PereopPossibleNamesType(HashSet<string> names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            ResetNamesParam = false;
            if (MessageBox.Show($"Переопределить семейство '{names.FirstOrDefault()}'?", "Переопределение семейства", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var input = HelperPrivateStatic.GetUserInputWithForm();
                if (input.Item2 && !string.IsNullOrWhiteSpace(input.Item1))
                { names.Clear(); names.Add(input.Item1.Trim()); }
            }
            return names;
        }
        private static string NameKey(IEnumerable<string> names) => SearchQueryKey.Names(names);
        private static string Key(HashSet<string> families, HashSet<string> types, ElementTypeOrSymbol mode)
            => SearchQueryKey.Build(families, types, (int)mode);
        private static List<ElementType> Collect(Document document, ElementTypeOrSymbol mode)
        {
            using (var collector = new FilteredElementCollector(document))
                return collector.OfClass(mode == ElementTypeOrSymbol.Symbol ? typeof(FamilySymbol) : typeof(ElementType))
                    .WhereElementIsElementType().Cast<ElementType>().OrderBy(t => t.Id.Value).ToList();
        }
        private static Element Cached(Document document, string key)
        {
            Dictionary<string, Element> cache; Element value;
            if (PastElements.TryGetValue(document, out cache) && cache.TryGetValue(key, out value))
            {
                if (value != null && value.IsValidObject && document.GetElement(value.Id) != null) return value;
                cache.Remove(key);
            }
            return null;
        }
        private static Element Remember(Document document, string key, Element value)
        {
            if (value == null) return null;
            Dictionary<string, Element> cache;
            if (!PastElements.TryGetValue(document, out cache)) PastElements[document] = cache = new Dictionary<string, Element>();
            cache[key] = value; return value;
        }
        private static double Score(HashSet<string> names, string actual)
        {
            var usable = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            return usable.Count == 0 ? 1 : usable.Max(n => StringSimilarity.Calculate(n, actual));
        }
        private static bool Exact(HashSet<string> names, string actual)
        {
            var usable = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            return usable.Count == 0 || usable.Any(n => StringSimilarity.Normalize(n) == StringSimilarity.Normalize(actual));
        }
        public static Element GetExistFamily(HashSet<string> PossibleNamesFamily, HashSet<string> PossibleNamesType, ElementTypeOrSymbol Type_seach)
        {
            if (PossibleNamesFamily == null || PossibleNamesType == null) throw new ArgumentNullException("names");
            if (ResetNamesParam) { ClearCache(); PereopPossibleNamesType(PossibleNamesType); }
            var doc = RevitAPI.Document;
            if (NameKey(PossibleNamesFamily).Length + NameKey(PossibleNamesType).Length == 0) return null;
            var found = Cached(doc, Key(PossibleNamesFamily, PossibleNamesType, Type_seach));
            if (found != null) return found;
            var candidates = Collect(doc, Type_seach);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var exact = candidates.Where(t => Exact(PossibleNamesFamily, t.FamilyName) && Exact(PossibleNamesType, t.Name)).ToList();
                ElementType best;
                if (exact.Count > 0)
                {
                    // Несколько точных совпадений — молча берём первое попавшееся.
                    best = exact.First();
                }
                else
                {
                    best = candidates.OrderByDescending(t => Score(PossibleNamesFamily, t.FamilyName) + Score(PossibleNamesType, t.Name)).FirstOrDefault();
                    string description = best == null ? "Совпадений нет." : $"Использовать '{best.FamilyName}: {best.Name}' (ID {best.Id})?";
                    if (best == null || MessageBox.Show(description, "Точное семейство не найдено", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    {
                        var input = HelperPrivateStatic.GetUserInputWithForm(PossibleNamesType.FirstOrDefault());
                        if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) return null;
                        PossibleNamesType.Clear(); PossibleNamesType.Add(input.Item1.Trim()); continue;
                    }
                }
                if (best == null) return null;
                PossibleNamesType.Add(best.Name);
                return Remember(doc, Key(PossibleNamesFamily, PossibleNamesType, Type_seach), best);
            }
            return null;
        }
        public static Element GetExistFamily(HashSet<string> PossibleNamesFamilySymbol, ElementTypeOrSymbol Type_seach)
        {
            if (PossibleNamesFamilySymbol == null) throw new ArgumentNullException(nameof(PossibleNamesFamilySymbol));
            if (ResetNamesParam) { ClearCache(); PereopPossibleNamesType(PossibleNamesFamilySymbol); }
            if (NameKey(PossibleNamesFamilySymbol).Length == 0) return null;
            var doc = RevitAPI.Document;
            var empty = new HashSet<string>();
            string key = "name|" + Key(empty, PossibleNamesFamilySymbol, Type_seach);
            var cached = Cached(doc, key); if (cached != null) return cached;
            var candidates = Collect(doc, Type_seach);
            Func<ElementType, string> name = t => Type_seach == ElementTypeOrSymbol.ElementType ? t.Name : t.FamilyName;
            var answer = SeachNameElement(PossibleNamesFamilySymbol, candidates.Select(name).Distinct().ToList());
            if (string.IsNullOrEmpty(answer.ePile)) return null;
            var matches = candidates.Where(t => name(t) == answer.ePile).ToList();
            if (matches.Count == 0) return null;
            // Если тип/имя встречается в нескольких семействах — молча берём первое попавшееся,
            // без диалога «Несколько совпадений».
            PossibleNamesFamilySymbol.Add(answer.ePile);
            return Remember(doc, "name|" + Key(empty, PossibleNamesFamilySymbol, Type_seach), matches[0]);
        }
        public static (string nPile, string ePile) SeachNameElement(HashSet<string> PossibleNamesFamilySymbol, List<string> elements)
        {
            if (PossibleNamesFamilySymbol == null || elements == null) throw new ArgumentNullException("names");
            var requested = PossibleNamesFamilySymbol.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var actual = elements.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (requested.Count == 0 || actual.Count == 0) return ("", null);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                string bestRequest = null, bestActual = null; double maximum = 0;
                foreach (var n in requested)
                    foreach (var e in actual)
                    {
                        if (StringSimilarity.Normalize(n) == StringSimilarity.Normalize(e)) return (n, e);
                        double score = StringSimilarity.Calculate(n, e);
                        if (score > maximum) { maximum = score; bestRequest = n; bestActual = e; }
                    }
                if (bestActual != null && MessageBox.Show($"Точное имя не найдено. Использовать '{bestActual}'?", "Поиск семейства", MessageBoxButtons.YesNo) == DialogResult.Yes) return (bestRequest, bestActual);
                var input = HelperPrivateStatic.GetUserInputWithForm(requested.FirstOrDefault());
                if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) break;
                requested.Clear(); requested.Add(input.Item1.Trim());
            }
            return ("", null);
        }
    }
}