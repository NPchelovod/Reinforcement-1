using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class OVParameters : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {


            ExecuteLogic(commandData);
            return Result.Succeeded;
        }
        //настройка параметров поиска
        public void ExecuteLogic(ExternalCommandData commandData)
        {
            
            bool ok = false;

            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = "Введите параметры";
                form.Size = new System.Drawing.Size(350, 200);
                form.StartPosition = FormStartPosition.CenterScreen;

                // Первое поле ввода
                var label1 = new Label
                {
                    Text = "Параметр:",
                    Location = new System.Drawing.Point(20, 20),
                    Width = 100
                };
                var textBox1 = new System.Windows.Forms.TextBox
                {
                    Text = GetDataAllOV.parameter,
                    Location = new System.Drawing.Point(120, 20),
                    Size = new System.Drawing.Size(180, 20)
                };

                // Второе поле ввода
                var label2 = new Label
                {
                    Text = "Значение параметра:",
                    Location = new System.Drawing.Point(20, 50),
                    Width = 100
                };
                var textBox2 = new System.Windows.Forms.TextBox
                {
                    Text = GetDataAllOV.valueParameter,
                    Location = new System.Drawing.Point(120, 50),
                    Size = new System.Drawing.Size(180, 20)
                };

                // Ширина
                var label3 = new Label
                {
                    Text = "Имя Ширина",
                    Location = new System.Drawing.Point(20, 80),
                    Width = 100
                };
                var textBox3 = new System.Windows.Forms.TextBox
                {
                    Text = GetAllSizeOV.swidth,
                    Location = new System.Drawing.Point(120, 80),
                    Size = new System.Drawing.Size(180, 20)
                };

                // Высота
                var label4 = new Label
                {
                    Text = "Имя Длина",
                    Location = new System.Drawing.Point(20, 110),
                    Width = 100
                };
                var textBox4 = new System.Windows.Forms.TextBox
                {
                    Text = GetAllSizeOV.sheight,
                    Location = new System.Drawing.Point(120, 110),
                    Size = new System.Drawing.Size(180, 20)
                };




                // Кнопки
                var okButton = new Button
                {
                    Text = "OK",
                    Location = new System.Drawing.Point(120, 140),
                    DialogResult = DialogResult.OK
                };

                var cancelButton = new Button
                {
                    Text = "Отмена",
                    Location = new System.Drawing.Point(220, 140),
                    DialogResult = DialogResult.Cancel
                };


                // Добавление элементов на форму
                form.Controls.AddRange(new System.Windows.Forms.Control[] { label1, textBox1, label2, textBox2,label3, textBox3,label4, textBox4, okButton, cancelButton
});

                // Назначение кнопки по умолчанию
                form.AcceptButton = okButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    GetDataAllOV.parameter = textBox1.Text;
                    GetDataAllOV.valueParameter = textBox2.Text;

                    GetDataAllOV.namesLookupParameterDouble.Remove(GetAllSizeOV.swidth);
                    GetDataAllOV.namesLookupParameterDouble.Remove(GetAllSizeOV.sheight);

                    GetAllSizeOV.swidth = textBox3.Text;
                    GetAllSizeOV.sheight = textBox4.Text;

                    GetDataAllOV.namesLookupParameterDouble.Add(GetAllSizeOV.swidth);
                    GetDataAllOV.namesLookupParameterDouble.Add(GetAllSizeOV.sheight);
                    ok = true;
                }
            }
        }
    }
}
