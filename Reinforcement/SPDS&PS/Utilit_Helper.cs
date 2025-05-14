using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    internal class Utilit_Helper
    {

        // поиск максимального пересечения строки с подстрокой
         public static int LongestCommonSubstring(string s1, string s2)
        {
            int maxLength = 0;
            int[,] dp = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                        maxLength = Math.Max(maxLength, dp[i, j]);
                    }
                }
            }
            return maxLength;
        }

        public static string unific_sravn_string(string FamName)
        {
            // удаление символов этих и к нижнему регистру перевод
            var simvol_del = new List<string>() { ".", ",", "_", "-", "/", ";", ":","*","^", " " };
            FamName = FamName.ToLower();
            foreach (var simvol in simvol_del)
            {
                // приводим строку к виду
                FamName = FamName.Replace(simvol, "");
            }
            return FamName;
        }

    }
}
