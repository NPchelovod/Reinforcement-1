using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;

namespace Reinforcement
{
    public static class UpdaterCheck
    {
        public static void GetResult(bool forceShow = false)
        {
            if (existShow) return;//уже быд
            if (!UpdaterPlagina.UpdaterIsStart) return;
            if (App.OnGroupCurrent.InGroop) return;
            
            CheckAndNotify(forceShow);
        }

        // Cooldown — не показывать диалог чаще, чем раз в 2 часа
        private static DateTime _lastNotifiedTime = DateTime.MinValue;
        private static readonly TimeSpan NotifyCooldown = TimeSpan.FromHours(2);

        public struct CheckResult
        {
            public DateTime CurrentDateLocal;
            public DateTime NewestSourceDateLocal;
            public string NewestSourcePath;

            public bool HasUpdate =>
                CurrentDateLocal != DateTime.MinValue &&
                NewestSourceDateLocal != DateTime.MinValue &&
                NewestSourceDateLocal > CurrentDateLocal;
        }

        /// <summary>Только сравнение дат. Без UI.</summary>
        public static CheckResult CheckForUpdates()
        {
            if (!App_Folders.isInitialize)
                App_Folders.Initializator();

            var r = new CheckResult
            {
                CurrentDateLocal = App_Folders.DatePluginDll,
                NewestSourceDateLocal = DateTime.MinValue,
                NewestSourcePath = null
            };

            if (r.CurrentDateLocal == DateTime.MinValue)
            {
                try { r.CurrentDateLocal = App_Folders.GetLatestFileTime(App_Folders.TargetPluginDir); }
                catch (Exception ex) { AppErrors.LogError(ex); }
            }

            TryEvaluateSource(App_Folders.SourcePluginDir, ref r);

            if (App_Folders.UserIsAvtor)
                TryEvaluateSource(App_Folders.SourcePluginDirAvtor, ref r);

            return r;
        }

        private static void TryEvaluateSource(string sourceDir, ref CheckResult r)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceDir)) return;
                if (!FastDirectoryChecker.DirectoryExistsFast(sourceDir)) return;

                var d = GetNewestDllExeLocal(sourceDir);
                if (d > r.NewestSourceDateLocal)
                {
                    r.NewestSourceDateLocal = d;
                    r.NewestSourcePath = sourceDir;
                }
            }
            catch (Exception ex) { AppErrors.LogError(ex); }
        }

        /// <summary>
        /// Локальное время — та же шкала, что и App_Folders.GetLatestFileTime
        /// и App_Folders.GetDateTimeFolder. Сравнивать UTC с локальным нельзя.
        /// </summary>
        private static DateTime GetNewestDllExeLocal(string dir)
        {
            try
            {
                return new[] { "*.dll", "*.exe" }
                    .SelectMany(m => Directory.EnumerateFiles(dir, m, SearchOption.AllDirectories))
                    .Select(File.GetLastWriteTime)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();
            }
            catch (Exception ex) { AppErrors.LogError(ex); return DateTime.MinValue; }
        }

        /// <summary>
        /// Сравнить и при наличии обновления показать диалог.
        /// forceShow = true — показать даже если cooldown не истёк (для ручной кнопки).
        /// </summary>
        private static bool CheckAndNotify(bool forceShow = false)
        {
            try
            {
                var r = CheckForUpdates();
                if (!r.HasUpdate) return false;

                if (!forceShow && (DateTime.Now - _lastNotifiedTime) < NotifyCooldown)
                    return false;

                if(!forceShow && _lastNotifiedTime ==DateTime.MinValue)
                {
                    _lastNotifiedTime = DateTime.Now;
                    return false;
                }

                _lastNotifiedTime = DateTime.Now;
                ShowUpdateDialog(r);
                return true;
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return false;
            }
        }
        private static bool existShow = false;
        private static void ShowUpdateDialog(CheckResult r)
        {
            existShow= true;
            var td = new TaskDialog("Обновление плагина");
            td.MainInstruction = "Доступно свежее обновление плагина";
            td.MainContent =
                $"Есть свежее обновление от {r.NewestSourceDateLocal:yyyy-MM-dd HH:mm},\n" +
                $"в то время как ваш плагин от {r.CurrentDateLocal:yyyy-MM-dd HH:mm}.\n\n" +
                "Закройте Revit для автоматического обновления.";
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Закрыть Revit сейчас",
                "При следующем запуске плагин обновится автоматически");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2,
                "Позже",
                "Напомнить при следующей проверке");
            td.CommonButtons = TaskDialogCommonButtons.None;
            td.DefaultButton = TaskDialogResult.CommandLink2;

            var res = td.Show();

            if (res == TaskDialogResult.CommandLink1)
            {
                try
                {
                    if (!UpdaterPlagina.UpdaterIsStart)
                        UpdaterPlagina.StartUpdateENS();
                }
                catch (Exception ex) { AppErrors.LogError(ex); }

                try
                {
                    var uiApp = RevitAPI.UiDocument?.Application;
                    if (uiApp != null)
                    {
                        var cmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit);
                        if (cmdId != null) uiApp.PostCommand(cmdId);
                    }
                }
                catch (Exception ex) { AppErrors.LogError(ex); }
            }
        }
    }
}