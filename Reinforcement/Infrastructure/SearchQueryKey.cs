using System;
using System.Collections.Generic;
using System.Linq;
namespace Reinforcement
{
    internal static class SearchQueryKey
    {
        public static string Names(IEnumerable<string> names)
        {
            return string.Concat(names.Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(StringSimilarity.Normalize).Distinct().OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => n.Length + ":" + n));
        }
        public static string Build(IEnumerable<string> families, IEnumerable<string> types, int mode)
            => mode + "|" + Names(families) + "|" + Names(types);
    }
}
