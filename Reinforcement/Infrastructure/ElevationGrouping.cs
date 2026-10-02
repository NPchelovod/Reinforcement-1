using System;
using System.Collections.Generic;
using System.Linq;
namespace Reinforcement
{
    internal static class ElevationGrouping
    {
        public static int FindKey(IEnumerable<int> keys, int elevation, int tolerance)
        {
            if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            var matches = keys.Where(key => key == elevation || Math.Abs((long)key - elevation) < tolerance)
                .OrderBy(key => Math.Abs((long)key - elevation)).ThenBy(key => key).ToList();
            return matches.Count == 0 ? elevation : matches[0];
        }
    }
}
