using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace Reinforcement
{
    internal class HelperPrivateStatic
    {
        //класс для помощи помощникам

        // Метод для вычисления схожести строк
        public static double CalculateSimilarity(string s1, string s2)
        {
            string normalized1 = NormalizeString(s1);
            string normalized2 = NormalizeString(s2);

            int lcsLength = LongestCommonSubstring(normalized1, normalized2);
            int maxLength = Math.Max(normalized1.Length, normalized2.Length);

            return maxLength > 0 ? (double)lcsLength / maxLength : 0;
        }

        // Метод нормализации строки
        public static string NormalizeString(string input)
        {
            return string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        }

        // Ваш метод поиска наибольшей общей подстроки
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
    
    
    

        public static (string,bool) GetUserInputWithForm(string familyName="")
        {
            string userInput = ""; // Variable to store the result
            bool ok=false;
            using (Form form = new Form())
            {
                form.Text = $" Можете ввести имя искомого типоразмера семейства или прервать:";

                form.Size = new System.Drawing.Size(300, 150);

                TextBox textBox = new TextBox();
                textBox.Location = new System.Drawing.Point(20, 20);
                textBox.Size = new System.Drawing.Size(240, 20);
                form.Controls.Add(textBox);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Location = new System.Drawing.Point(20, 50);
                okButton.DialogResult = DialogResult.OK;
                form.Controls.Add(okButton);

                // Show the dialog and check if the user clicked OK
                if (form.ShowDialog() == DialogResult.OK)
                {
                    userInput = textBox.Text;
                    ok = true;
                }

            }

            return (userInput, ok);
        }
    }
}
