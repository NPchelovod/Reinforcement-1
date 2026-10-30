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
        // Переопределённые (эффективные) наборы имён.
        // Исходные HashSet'ы, которые передаёт вызывающий код, НИКОГДА не мутируются.
        // На каждый (документ, исходный набор имён) хранится свой эффективный набор,
        // в который дописываются пользовательские переопределения и выбранные кандидаты.
        // Сравнение исходных наборов — по содержимому, а не по ссылке,
        // чтобы свежесозданный HashSet с теми же строками тоже находил переопределение.
        private static readonly Dictionary<Document, Dictionary<HashSet<string>, HashSet<string>>> _overriddenNames
            = new Dictionary<Document, Dictionary<HashSet<string>, HashSet<string>>>();

        private sealed class HashSetByNameComparer : IEqualityComparer<HashSet<string>>
        {
            public static readonly HashSetByNameComparer Instance = new HashSetByNameComparer();

            public bool Equals(HashSet<string> x, HashSet<string> y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                if (x.Count != y.Count) return false;
                foreach (var s in x)
                    if (!y.Contains(s)) return false;
                return true;
            }

            public int GetHashCode(HashSet<string> obj)
            {
                if (obj == null) return 0;
                unchecked
                {
                    int hash = 17;
                    foreach (var s in obj.OrderBy(n => n, StringComparer.Ordinal))
                        hash = hash * 31 + (s?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
        /// <summary>
        /// Возвращает эффективный набор имён для (doc, original):
        ///   - если переопределение уже есть — вернёт его;
        ///   - если нет и createIfMissing=false — вернёт original;
        ///   - если нет и createIfMissing=true — создаст копию original,
        ///     зарегистрирует её как переопределение и вернёт.
        /// Оригинал никогда не мутируется.
        /// </summary>
        private static HashSet<string> GetEffective(Document doc, HashSet<string> original,
                                                    bool createIfMissing = false)
        {
            if (original == null) return null;
            if (doc == null) return original;

            Dictionary<HashSet<string>, HashSet<string>> perDoc;
            if (!_overriddenNames.TryGetValue(doc, out perDoc))
            {
                if (!createIfMissing) return original;
                perDoc = new Dictionary<HashSet<string>, HashSet<string>>(HashSetByNameComparer.Instance);
                _overriddenNames[doc] = perDoc;
            }

            HashSet<string> effective;
            if (perDoc.TryGetValue(original, out effective)) return effective;
            if (!createIfMissing) return original;

            effective = new HashSet<string>(original);
            perDoc[original] = effective;
            return effective;
        }

        // Оригинальные содержимые HashSet'ов, которые были изменены
        

        public static bool ResetNamesParam;
        public static void ClearCache(Document document = null)
        {
           
            if (document == null)
            {
                PastElements.Clear();
                
                _overriddenNames.Clear(); //← раскомментировать, только если действительно нужен полный сброс
            }
            else
            {
                PastElements.Remove(document);
               
                _overriddenNames.Remove(document);
            }
        }
        public static HashSet<string> PereopPossibleNamesType(HashSet<string> names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            ResetNamesParam = false;

            if (MessageBox.Show($"Переопределить семейство '{names.FirstOrDefault()}'?",
                                "Переопределение семейства",
                                MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var input = HelperPrivateStatic.GetUserInputWithForm();
                if (input.Item2 && !string.IsNullOrWhiteSpace(input.Item1))
                {
                    var doc = RevitAPI.Document;
                    // Пишем в эффективный набор — исходный не трогаем.
                    var effective = GetEffective(doc, names, createIfMissing: true);
                    effective.Clear();
                    effective.Add(input.Item1.Trim());
                }
            }

            // Возвращаем исходный набор как есть — он не мутируется.
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
        //public static Element GetExistFamily(HashSet<string> PossibleNamesFamily, HashSet<string> PossibleNamesType, ElementTypeOrSymbol Type_seach)
        //{
        //    if (PossibleNamesFamily == null || PossibleNamesType == null) throw new ArgumentNullException("names");
        //    if (ResetNamesParam) { ClearCache(); PereopPossibleNamesType(PossibleNamesType); }
        //    var doc = RevitAPI.Document;
        //    if (NameKey(PossibleNamesFamily).Length + NameKey(PossibleNamesType).Length == 0) return null;
        //    var found = Cached(doc, Key(PossibleNamesFamily, PossibleNamesType, Type_seach));
        //    if (found != null) return found;
        //    var candidates = Collect(doc, Type_seach);
        //    for (int attempt = 0; attempt < 3; attempt++)
        //    {
        //        var exact = candidates.Where(t => Exact(PossibleNamesFamily, t.FamilyName) && Exact(PossibleNamesType, t.Name)).ToList();
        //        ElementType best;
        //        if (exact.Count > 0 && attempt==0)
        //        {
        //            // Несколько точных совпадений — молча берём первое попавшееся.
        //            //наверно надо то, которое можно разместить
        //            best = exact.First();
        //        }
        //        else
        //        {
        //            best = candidates.OrderByDescending(t => Score(PossibleNamesFamily, t.FamilyName) + Score(PossibleNamesType, t.Name)).FirstOrDefault();
        //            string description = best == null ? "Совпадений нет." : $"Использовать '{best.FamilyName}: {best.Name}' (ID {best.Id})?";
        //            if (best == null || MessageBox.Show(description, "Точное семейство не найдено", MessageBoxButtons.YesNo) != DialogResult.Yes)
        //            {
        //                var input = HelperPrivateStatic.GetUserInputWithForm(PossibleNamesType.FirstOrDefault());
        //                if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) return null;
        //                PossibleNamesType.Clear(); PossibleNamesType.Add(input.Item1.Trim()); continue;
        //            }
        //        }
        //        if (best == null) return null;
        //        PossibleNamesType.Add(best.Name);
        //        return Remember(doc, Key(PossibleNamesFamily, PossibleNamesType, Type_seach), best);
        //    }
        //    return null;
        //}

        public static Element GetExistFamily(HashSet<string> PossibleNamesFamily,
                                     HashSet<string> PossibleNamesType,
                                     ElementTypeOrSymbol Type_seach)
        {
            if (PossibleNamesFamily == null || PossibleNamesType == null)
                throw new ArgumentNullException("names");
            if (ResetNamesParam) { ClearCache(); PereopPossibleNamesType(PossibleNamesType); }

            var doc = RevitAPI.Document;

            var effFamily = GetEffective(doc, PossibleNamesFamily);
            var effType = GetEffective(doc, PossibleNamesType);

            if (NameKey(effFamily).Length + NameKey(effType).Length == 0) return null;

            var found = Cached(doc, Key(effFamily, effType, Type_seach));
            if (found != null) return found;

            var candidates = Collect(doc, Type_seach);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                var exact = candidates
                    .Where(t => Exact(effFamily, t.FamilyName) && Exact(effType, t.Name))
                    .ToList();

                ElementType best;
                bool userPicked = false;

                if (exact.Count == 1)
                {
                    best = exact[0];
                }
                else
                {
                    var pool = exact.Count > 1 ? exact : candidates;
                    var ranked = pool
                        .OrderByDescending(t => Score(effFamily, t.FamilyName)
                                              + Score(effType, t.Name))
                        .Take(6)
                        .ToList();

                    string title = exact.Count > 1
                        ? "Несколько точных совпадений"
                        : "Точное семейство не найдено";

                    best = ShowCandidateSelection(ranked, title);

                    if (best == null)
                    {
                        // Отказались от окна — ручной ввод имени типоразмера.
                        var input = HelperPrivateStatic.GetUserInputWithForm(effType.FirstOrDefault());
                        if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) return null;

                        var effectiveType = GetEffective(doc, PossibleNamesType, createIfMissing: true);
                        effectiveType.Add(input.Item1.Trim());
                        effType = effectiveType;
                        continue;
                    }
                    userPicked = true;
                }

                if (best == null) return null;

                if (userPicked)
                {
                    // ФИКС: пользователь сделал явный выбор — фиксируем его
                    // в эффективных наборах, чтобы следующий вызов (даже при пустом
                    // кэше элементов или нестабильном порядке HashSet) уже
                    // не считался неоднозначным.
                    if (!string.IsNullOrEmpty(best.FamilyName))
                    {
                        var effFamilyFinal = GetEffective(doc, PossibleNamesFamily, createIfMissing: true);
                        effFamilyFinal.Clear();
                        effFamilyFinal.Add(best.FamilyName);
                        effFamily = effFamilyFinal;
                    }
                    var effTypeFinal = GetEffective(doc, PossibleNamesType, createIfMissing: true);
                    effTypeFinal.Clear();
                    effTypeFinal.Add(best.Name);
                    effType = effTypeFinal;
                }
                else
                {
                    // Тихий выбор (одно точное совпадение) — просто фиксируем имя типа.
                    var effTypeFinal = GetEffective(doc, PossibleNamesType, createIfMissing: true);
                    effTypeFinal.Add(best.Name);
                    effType = effTypeFinal;
                }

                return Remember(doc, Key(effFamily, effType, Type_seach), best);
            }

            return null;
        }
        public static Element GetExistFamily(HashSet<string> PossibleNamesFamilySymbol,
                                     ElementTypeOrSymbol Type_seach)
        {
            if (PossibleNamesFamilySymbol == null)
                throw new ArgumentNullException(nameof(PossibleNamesFamilySymbol));
            if (ResetNamesParam) { ClearCache(); PereopPossibleNamesType(PossibleNamesFamilySymbol); }

            var doc = RevitAPI.Document;

            var effNames = GetEffective(doc, PossibleNamesFamilySymbol);
            if (NameKey(effNames).Length == 0) return null;

            var empty = new HashSet<string>();
            string key = "name|" + Key(empty, effNames, Type_seach);
            var cached = Cached(doc, key);
            if (cached != null) return cached;

            var candidates = Collect(doc, Type_seach);
            Func<ElementType, string> name = t => Type_seach == ElementTypeOrSymbol.ElementType
                                                    ? t.Name
                                                    : t.FamilyName;

            var chosen = SeachNameElement(effNames, candidates, name, out bool userPicked);
            if (chosen == null) return null;

            var effNamesFinal = GetEffective(doc, PossibleNamesFamilySymbol, createIfMissing: true);

            if (userPicked)
            {
                // ФИКС: сужаем набор до выбранного имени.
                effNamesFinal.Clear();
            }
            effNamesFinal.Add(name(chosen));

            return Remember(doc, "name|" + Key(empty, effNamesFinal, Type_seach), chosen);
        }
        public static ElementType SeachNameElement(
    HashSet<string> PossibleNamesFamilySymbol,
    List<ElementType> elements,
    Func<ElementType, string> getName)
    => SeachNameElement(PossibleNamesFamilySymbol, elements, getName, out _);

        public static ElementType SeachNameElement(
            HashSet<string> PossibleNamesFamilySymbol,
            List<ElementType> elements,
            Func<ElementType, string> getName,
            out bool userPicked)
        {
            userPicked = false;
            if (PossibleNamesFamilySymbol == null || elements == null)
                throw new ArgumentNullException("names");

            var requested = PossibleNamesFamilySymbol.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (requested.Count == 0 || elements.Count == 0) return null;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                // Тихий выбор — точное совпадение
                foreach (var n in requested)
                    foreach (var el in elements)
                        if (StringSimilarity.Normalize(n) == StringSimilarity.Normalize(getName(el)))
                            return el;

                var ranked = elements
                    .Select(el => new { El = el, Score = requested.Max(n => StringSimilarity.Calculate(n, getName(el))) })
                    .OrderByDescending(x => x.Score)
                    .Take(5)
                    .Select(x => x.El)
                    .ToList();

                var chosen = ShowCandidateSelection(ranked, "Точное имя не найдено");
                if (chosen != null)
                {
                    userPicked = true;
                    return chosen;
                }

                var input = HelperPrivateStatic.GetUserInputWithForm(requested.FirstOrDefault());
                if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) break;
                requested.Clear();
                requested.Add(input.Item1.Trim());
            }
            return null;
        }
        


        //public static (string nPile, string ePile) SeachNameElement(HashSet<string> PossibleNamesFamilySymbol, List<string> elements)
        //{
        //    if (PossibleNamesFamilySymbol == null || elements == null) throw new ArgumentNullException("names");
        //    var requested = PossibleNamesFamilySymbol.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        //    var actual = elements.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        //    if (requested.Count == 0 || actual.Count == 0) return ("", null);
        //    for (int attempt = 0; attempt < 4; attempt++)
        //    {
        //        string bestRequest = null, bestActual = null; double maximum = 0;
        //        foreach (var n in requested)
        //            foreach (var e in actual)
        //            {
        //                if (StringSimilarity.Normalize(n) == StringSimilarity.Normalize(e)) return (n, e);
        //                double score = StringSimilarity.Calculate(n, e);
        //                if (score > maximum) { maximum = score; bestRequest = n; bestActual = e; }
        //            }
        //        if (bestActual != null && MessageBox.Show($"Точное имя не найдено. Использовать '{bestActual}'?", "Поиск семейства", MessageBoxButtons.YesNo) == DialogResult.Yes) return (bestRequest, bestActual);
        //        var input = HelperPrivateStatic.GetUserInputWithForm(requested.FirstOrDefault());
        //        if (!input.Item2 || string.IsNullOrWhiteSpace(input.Item1)) break;
        //        requested.Clear(); requested.Add(input.Item1.Trim());
        //    }
        //    return ("", null);
        //}

        /// <summary>
        /// Показывает окно выбора из кандидатов. Возвращает выбранный ElementType
        /// или null, если пользователь отменил выбор.
        /// </summary>
        private static ElementType ShowCandidateSelection(List<ElementType> candidates, string title)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = title;
                form.ClientSize = new System.Drawing.Size(500, 230);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                var label = new Label
                {
                    Left = 12,
                    Top = 10,
                    Width = 476,
                    Height = 20,
                    Text = "Выберите подходящий вариант (от наиболее вероятного к менее вероятному):"
                };

                var listBox = new ListBox
                {
                    Left = 12,
                    Top = 35,
                    Width = 476,
                    Height = 150,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f),
                    IntegralHeight = false
                };

                foreach (var t in candidates)
                {
                    // Формат: "Семейство: Типоразмер  (ID ...)"
                    listBox.Items.Add($"{t.FamilyName} | {t.Name}   (ID {t.Id.Value})");
                }
                listBox.SelectedIndex = 0;

                var okButton = new Button
                {
                    Text = "Выбрать",
                    Left = 312,
                    Top = 195,
                    Width = 85,
                    Height = 28,
                    DialogResult = DialogResult.OK
                };
                var cancelButton = new Button
                {
                    Text = "Отмена",
                    Left = 403,
                    Top = 195,
                    Width = 85,
                    Height = 28,
                    DialogResult = DialogResult.Cancel
                };

                // Двойной клик по элементу = «Выбрать»
                listBox.DoubleClick += (s, e) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                };

                form.Controls.Add(label);
                form.Controls.Add(listBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
                    return candidates[listBox.SelectedIndex];
            }
            return null;
        }
        /// <summary>
        /// Показывает окно выбора из строковых кандидатов. Возвращает выбранную строку
        /// или null, если пользователь отменил выбор.
        /// </summary>
        private static string ShowCandidateSelection(List<string> candidates, List<string> displays, string title)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = title;
                form.ClientSize = new System.Drawing.Size(500, 230);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;

                var label = new Label
                {
                    Left = 12,
                    Top = 10,
                    Width = 476,
                    Height = 20,
                    Text = "Выберите подходящий вариант (от наиболее вероятного к менее вероятному):"
                };

                var listBox = new ListBox
                {
                    Left = 12,
                    Top = 35,
                    Width = 476,
                    Height = 150,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f),
                    IntegralHeight = false
                };

                for (int i = 0; i < candidates.Count; i++)
                {
                    string text = (displays != null && i < displays.Count) ? displays[i] : candidates[i];
                    listBox.Items.Add(text);
                }
                listBox.SelectedIndex = 0;

                var okButton = new Button
                {
                    Text = "Выбрать",
                    Left = 312,
                    Top = 195,
                    Width = 85,
                    Height = 28,
                    DialogResult = DialogResult.OK
                };
                var cancelButton = new Button
                {
                    Text = "Отмена",
                    Left = 403,
                    Top = 195,
                    Width = 85,
                    Height = 28,
                    DialogResult = DialogResult.Cancel
                };

                listBox.DoubleClick += (s, e) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                };

                form.Controls.Add(label);
                form.Controls.Add(listBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
                    return candidates[listBox.SelectedIndex];
            }
            return null;
        }
    }
}