using System.Windows;

namespace Reinforcement
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Initial();
            // Загружаем текущие настройки в элементы управления
            //chkFillAuthor.IsChecked = AddinSettings.FillAuthor;
        }

        private void Initial()
        {
            chkFillAuthor.IsChecked = AutoFillNoteUpdater.regWriterAvtor;
            //chkFillAuthorADSK.IsChecked = AutoFillNoteUpdater.regWriterAvtorPrim;
            chk_GroupPass.IsChecked = !AutoFillNoteUpdater.correctGroup;
            AutoFillNoteUpdater.regWriterAvtorPrim = AnyChange.AllUpdater;
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем значения
            // AddinSettings.FillAuthor = chkFillAuthor.IsChecked == true;

            // При необходимости можно вызвать сохранение в файл/реестр
            //AddinSettings.Save();

            AutoFillNoteUpdater.regWriterAvtor= chkFillAuthor.IsChecked == true;

            //AutoFillNoteUpdater.regWriterAvtorPrim = chkFillAuthorADSK.IsChecked == true;

            AutoFillNoteUpdater.correctGroup= chk_GroupPass.IsChecked != true;


            AnyChange.AllUpdater=chkUpdater.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}