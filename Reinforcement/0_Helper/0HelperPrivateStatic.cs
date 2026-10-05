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
        public static double CalculateSimilarity(string s1, string s2) => StringSimilarity.Calculate(s1, s2);

        // Метод нормализации строки
        public static string NormalizeString(string input) => StringSimilarity.Normalize(input);

        // Ваш метод поиска наибольшей общей подстроки
        public static int LongestCommonSubstring(string s1, string s2) => StringSimilarity.LongestCommonSubstring(s1, s2);
    
    
    

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
                textBox.Text = familyName ?? string.Empty;
                form.Controls.Add(textBox);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Location = new System.Drawing.Point(20, 50);
                okButton.DialogResult = DialogResult.OK;
                form.Controls.Add(okButton);
                form.AcceptButton = okButton;
                form.StartPosition = FormStartPosition.CenterScreen;

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
