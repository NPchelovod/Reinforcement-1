using System;
using System.Drawing;
using System.Windows.Forms;

namespace Reinforcement
{
    public class PasswordForm : Form
    {
        private TextBox _txtPassword;
        private Button _btnOk;
        private Button _btnCancel;

        public string EnteredPassword { get; private set; }

        public PasswordForm()
        {
            Text = "Введите пароль";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(300, 100);

            var lbl = new Label
            {
                Text = "Пароль:",
                Location = new Point(10, 15),
                AutoSize = true
            };

            _txtPassword = new TextBox
            {
                Location = new Point(10, 35),
                Width = 280,
                UseSystemPasswordChar = true   // символы маскируются
            };

            _btnOk = new Button
            {
                Text = "ОК",
                DialogResult = DialogResult.OK,
                Location = new Point(130, 65),
                Width = 75
            };

            _btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new Point(215, 65),
                Width = 75
            };

            Controls.Add(lbl);
            Controls.Add(_txtPassword);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtPassword.Focus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Сохраняем пароль перед закрытием
            if (DialogResult == DialogResult.OK)
                EnteredPassword = _txtPassword.Text;

            base.OnFormClosing(e);
        }
    }
}