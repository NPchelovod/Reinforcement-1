using System.Windows;

namespace Reinforcement
{
    public partial class ErrorAdminWindow : Window
    {
        /// <summary>
        /// Пользователь нажал «Отключить вывод ошибок».
        /// </summary>
        public bool DisableRequested { get; private set; }

        public ErrorAdminWindow(string text)
        {
            InitializeComponent();
            ErrorText.Text = text ?? string.Empty;
        }

        private void Disable_Click(object sender, RoutedEventArgs e)
        {
            DisableRequested = true;
            Close();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(ErrorText.Text ?? ""); }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}