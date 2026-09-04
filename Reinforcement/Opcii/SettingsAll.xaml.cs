using System;
using System.Windows;

namespace Reinforcement
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            this.Loaded += SettingsWindow_Loaded;
            //Initial();
            // Загружаем текущие настройки в элементы управления
            //chkFillAuthor.IsChecked = AddinSettings.FillAuthor;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            chkFillAuthor.IsChecked = AutoFillNoteUpdater.regWriterAvtor;
            //chkFillAuthorADSK.IsChecked = AutoFillNoteUpdater.regWriterAvtorPrim;
            chk_GroupPass.IsChecked = !AutoFillNoteUpdater.correctGroup;
            AutoFillNoteUpdater.regWriterAvtorPrim = AnyChange.AllUpdater;

            var buildDate = App.TargetLatestTime;
            var version = App.Version;
            txtPluginVersion.Text = $"Плагин от: {buildDate:dd.MM.yyyy HH:mm}  (v{version}_1655)";
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


            if (AnyChange.AllUpdater && rePodpiska.IsChecked == true)
            {
                AnyChange.PodpiskaAll();//переподписываеся
            }


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