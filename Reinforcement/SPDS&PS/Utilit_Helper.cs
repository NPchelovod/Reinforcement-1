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
            => StringSimilarity.LongestCommonSubstring(s1, s2);

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
