using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Markup;

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
        public static string GetDatePluginText => $"Плагин от: {GetStringFromDate(App_Folders.DatePluginDll)}, ПК_{App_Folders.UserName_MachineName}";

        public static string GetStringFromDate(DateTime date)
        {
            return $"{date.ToLocalTime():d MMM yyyy, HH:mm}";
        }
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            chkFillAuthor.IsChecked = PeriodicHandler.RegWriterAvtor;
            //chkFillAuthorADSK.IsChecked = AutoFillNoteUpdater.regWriterAvtorPrim;
            chk_GroupPass.IsChecked = !AnyChangeClass.CorrectGroop;
            
           
            var buildDate = App_Folders.DatePluginDll.ToLocalTime(); //локальное время
            var version = App_Folders.VersionString;
            string status = !App_Folders.UserIsAvtor ? "Рядовой" : "Командир";
            txtPluginVersion.Text = GetDatePluginText+$", Пользователь: {status}";

            logErrorsShow.IsChecked = AppErrors.LookErrorsAdmin;
            chkUpdater.IsChecked = AnyChangeClass.AllUpdater;

            ControlVersion();
            OtherProp();
            sborStatAndErrors.IsChecked = StatUsers.SborStat;
        }

        private void ControlVersion()
        {
            var upData = App_History.NewVersion;
            if (upData != null && !string.IsNullOrEmpty(upData.Message))
            {
                txtNewVersion.Text =
                    $"Имеется новая {upData.GetMessegeVersion()}";
                txtNewVersion.Foreground = System.Windows.Media.Brushes.Red;   // есть обновление — красный
                txtNewVersion.Visibility = Visibility.Visible;
            }
            else
            {
                var ourV = App_History.OurVersion;
                if (UpdaterCheck.NewVersionData.HasUpdate)
                {
                    //из самого dll
                    txtNewVersion.Text = $"Имеется новая версия {GetStringFromDate(UpdaterCheck.NewVersionData.NewestSourceDateLocal)}, отличия от вашей неизвестны";
                    txtNewVersion.Foreground = System.Windows.Media.Brushes.Red;   // есть обновление — красный
                }
                else if (ourV != null && !string.IsNullOrEmpty(ourV.Message))
                {
                    txtNewVersion.Text =
                   $"Версия новее не найдена, но ваша {ourV.GetMessegeVersion()}";
                }
                else
                {
                    txtNewVersion.Text = $"Версия новее не найдена";
                }

                //txtNewVersion.Visibility = Visibility.Collapsed;
                txtNewVersion.Visibility = Visibility.Visible;
            }
        }
        private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // ваша существующая логика проверки, например:
            // UpdateChecker.CheckForUpdate();
            App_History.ReadData();
            UpdaterCheck.GetResult();
            ControlVersion();
        }

        private void OtherProp()
        {
            //другие характеристика например доступность диска share
            if(App_Folders.IsTempFolders)
            {
                txtShareDostup.Text = "Диск Share не доступен";
                txtShareDostup.Foreground = System.Windows.Media.Brushes.Red;
                txtShareDostup.Visibility = Visibility.Visible;
            }

            string answer =
            $"{GetDatePluginText}\r\n" +
            $"Путь хранения текущего плагина: {App_Folders.TargetPluginDir}\r\n"+
            $"Путь резервного хранения: {App_Folders.TempFolder}\r\n";
            txtData.Text = answer;
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
            StatUsers.SborStat = sborStatAndErrors.IsChecked == true;
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        //private void HistoryButton_Click(object sender, RoutedEventArgs e)
        //{
        //    var alVersions = App_History.UpdateAllVersion;
        //    if (alVersions == null) { return; }
        //    var listVersion =alVersions.UpdateStructVersion;
        //    if(listVersion == null || listVersion.Count==0) { return; };

        //    var sb = new System.Text.StringBuilder();
        //    sb.AppendLine("История версий плагина:");
        //    sb.AppendLine();

        //    foreach (var v in listVersion)
        //    {
        //        sb.AppendLine(v.GetMessegeVersion());
        //    }

        //    MessageBox.Show(sb.ToString(),
        //                    "История версий",
        //                    MessageBoxButton.OK, MessageBoxImage.Information);
        //}
        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var alVersions = App_History.UpdateAllVersion;
            if (alVersions == null) { return; }

            var listVersion = alVersions.UpdateStructVersion;
            if (listVersion == null || listVersion.Count == 0) { return; }

            var sb = new StringBuilder();
            sb.AppendLine("История версий плагина:");
            sb.AppendLine();

            int i = 1;
            foreach (var v in listVersion)
            {
                sb.AppendLine($"{i}. {v.GetMessegeVersion()}");
                sb.AppendLine();          // пустая строка между сообщениями
                i++;
            }

            var win = new HistoryWindow();
            win.SetText(sb.ToString());
            win.Owner = this;
            win.ShowDialog();
        }
        private void CopyTextDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtData.Text))
                {
                    MessageBox.Show("Поле пустое — нечего копировать.",
                                    "Копирование", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetText(txtData.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось скопировать в буфер обмена: {ex.Message}",
                                "Копирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

}