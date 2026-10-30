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
        public static string GetDatePluginText => $"Плагин от: {App_Folders.DatePluginDll.ToLocalTime():d MMM yyyy, HH:mm}";
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            chkFillAuthor.IsChecked = PeriodicHandler.RegWriterAvtor;
            //chkFillAuthorADSK.IsChecked = AutoFillNoteUpdater.regWriterAvtorPrim;
            chk_GroupPass.IsChecked = !AnyChangeClass.CorrectGroop;
            
           
            var buildDate = App_Folders.DatePluginDll.ToLocalTime(); //локальное время
            var version = App_Folders.VersionString; 
            txtPluginVersion.Text = GetDatePluginText+$" V ({version})";

            logErrorsShow.IsChecked = AppErrors.LookErrorsAdmin;
            chkUpdater.IsChecked = AnyChangeClass.AllUpdater;
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем значения
            // AddinSettings.FillAuthor = chkFillAuthor.IsChecked == true;

            // При необходимости можно вызвать сохранение в файл/реестр
            //AddinSettings.Save();

            PeriodicHandler.RegWriterAvtor = chkFillAuthor.IsChecked == true;

            //AutoFillNoteUpdater.regWriterAvtorPrim = chkFillAuthorADSK.IsChecked == true;

            AnyChangeClass.CorrectGroop = chk_GroupPass.IsChecked != true;


            AnyChangeClass.AllUpdater=chkUpdater.IsChecked == true;


            AppErrors.LookErrorsAdmin= logErrorsShow.IsChecked == true;



            if (AnyChangeClass.AllUpdater && rePodpiska.IsChecked == true)
            {
                App.InitializedInvoker(RevitAPI.UiApplication);//переподписываеся
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