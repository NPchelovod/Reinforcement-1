using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

using Autodesk.Revit.UI;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

namespace Reinforcement
{
    public class HelperSeachLookup
    {
        

        public static (List<Element>, string parameter, string value) GetElements(string parameter, string value, ExternalCommandData commandData)
        {
            //например parameter = "Тип системы", AsValueString() == "Вентканал"
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            Autodesk.Revit.DB.View activeView = doc.ActiveView;

            var answer = new List<Element>();
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды на котором должны быть видны все необходимые элементы");
                return (answer, parameter, value);

            }

            int iter = -1;
            while (iter<4)
            {
                iter++;

                answer = new FilteredElementCollector(doc, activeView.Id)
              .OfClass(typeof(FamilyInstance))
              .Where(it =>
              {
                  // Проверяем, существует ли параметр
                  Parameter foundParam = it.LookupParameter(parameter);
                  // Если параметр существует и его строковое значение равно искомому, включаем элемент в результат
                  return foundParam != null && foundParam.AsValueString() == value;
              }).ToList();

                if(answer.Count>0)
                {
                    break;
                }

                int iter2 = -1;
                bool proxod = true;

                while (iter2 < 4)
                {
                    var input = GetUserInputWithForm(parameter,value);

                    if (!input.Item3) { proxod = false; break; }

                    if ( input.Item2.Count() > 1)
                    {
                        if (input.Item1.Count() > 1)
                        { parameter = input.Item1; }
                        value = input.Item2;
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                if (!proxod)
                {
                    break;
                }

            }
            
            return (answer, parameter,value);
        }

        public static (string parameter, string value, bool) GetUserInputWithForm(string parameter = "", string value="")
        {
            string userInput1 = parameter;
            string userInput2 = value;
            bool ok = false;

            using (var form = new Form())
            {
                form.Text = "Введите параметры";
                form.Size = new System.Drawing.Size(350, 200);
                form.StartPosition = FormStartPosition.CenterScreen;

                // Первое поле ввода
                var label1 = new Label { Text = "Параметр:", Location = new System.Drawing.Point(20, 20), Width = 100 };
                var textBox1 = new System.Windows.Forms.TextBox
                {
                    Text = parameter,
                    Location = new System.Drawing.Point(120, 20),
                    Size = new System.Drawing.Size(180, 20)
                };

                // Второе поле ввода
                var label2 = new Label { Text = "Значение параметра:", Location = new System.Drawing.Point(20, 50), Width = 100 };
                var textBox2 = new System.Windows.Forms.TextBox
                {
                    Text = value,
                    Location = new System.Drawing.Point(120, 50),
                    Size = new System.Drawing.Size(180, 20)
                };

                // Кнопки
                var okButton = new Button
                {
                    Text = "OK",
                    Location = new System.Drawing.Point(120, 90),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Отмена",
                    Location = new System.Drawing.Point(220, 90),
                    DialogResult = DialogResult.Cancel
                };

                // Добавление элементов на форму
                form.Controls.AddRange(new System.Windows.Forms.Control[] { label1, textBox1, label2, textBox2, okButton, cancelButton });

                // Назначение кнопки по умолчанию
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    userInput1 = textBox1.Text;
                    userInput2 = textBox2.Text;
                    ok = true;
                }
            }

            return (userInput1, userInput2, ok);
        }
    }
}
