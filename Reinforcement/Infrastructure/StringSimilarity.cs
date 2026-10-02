using System;
using System.Linq;
namespace Reinforcement
{
    internal static class StringSimilarity
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty :
                string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        }
        public static double Calculate(string left, string right)
        {
            left = Normalize(left); right = Normalize(right);
            int length = Math.Max(left.Length, right.Length);
            return length == 0 ? 0 : (double)LongestCommonSubstring(left, right) / length;
        }
        // Same substring metric as the original, O(min(m,n)) auxiliary memory.
        public static int LongestCommonSubstring(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return 0;
            if (left.Length < right.Length) { string swap = left; left = right; right = swap; }
            var row = new int[right.Length + 1];
            int maximum = 0;
            for (int i = 0; i < left.Length; i++)
                for (int j = right.Length; j > 0; j--)
                {
                    row[j] = left[i] == right[j - 1] ? row[j - 1] + 1 : 0;
                    maximum = Math.Max(maximum, row[j]);
                }
            return maximum;
        }
    }
}
