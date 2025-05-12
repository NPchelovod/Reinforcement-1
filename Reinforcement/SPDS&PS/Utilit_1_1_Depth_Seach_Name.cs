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
    internal class Utilit_1_1_Depth_Seach_Name
    {
         public static ElementType GetResult(string FamName, IList<Element> symbols)
        {
            
            ElementType symbol = null;

            bool contol_proxod = false;
            foreach (var element in symbols)
            {
                ElementType elemType = element as ElementType;
                if (elemType.FamilyName == FamName)
                {

                    symbol = element as FamilySymbol;
                    contol_proxod = true;
                    break;
                }
            }

            int LongestCommonSubstring(string s1, string s2)
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

            if (contol_proxod == false)
            {
                // ищем максимальную подстроку в строке для этого удаляем все символы чипушные

                var simvol_del = new List<string>() { ".", ",", "_", "-", "/",";",":", " " };
                string FamName2 = FamName.ToLower();
                foreach (var simvol in simvol_del)
                {
                    // приводим строку к виду
                    FamName2=FamName2.Replace(simvol, "");
                }

                int simvol_sovpad = 0;
                string name_sovpad = "";
                string FamName_sravn = "";
                foreach (var element in symbols)
                {
                    ElementType elemType = element as ElementType;
                    string potenc_name_sovpad = elemType.FamilyName;
                    FamName_sravn = elemType.FamilyName.ToLower();

                    foreach (var simvol in simvol_del)
                    {
                        // приводим строку к виду
                        FamName_sravn = FamName_sravn.Replace(simvol, "");
                    }
                    // количество пересечений
                    int count = LongestCommonSubstring(FamName2, FamName_sravn);//FamName2.Zip(FamName_sravn, (c1, c2) => c1 == c2).Count(match => match);
                    if (count > simvol_sovpad)
                    {
                        simvol_sovpad = count;
                        name_sovpad = potenc_name_sovpad;
                    }
                }

                if (simvol_sovpad > 3 && simvol_sovpad > Convert.ToInt32(0.7 * FamName2.Count()))
                {
                    foreach (var element in symbols)
                    {
                        ElementType elemType = element as ElementType;
                        if (elemType.FamilyName == name_sovpad)
                        {

                            symbol = element as FamilySymbol;
                            contol_proxod = true;
                            TaskDialog.Show("Не найдено точное совпадение имени семейства", $"Нашёл аналог {FamName} : {name_sovpad}");
                            
                            break;
                        }
                    }
                }

            }

            return symbol;


        }

    }
}
