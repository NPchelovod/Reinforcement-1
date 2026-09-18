using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Updaters;

namespace Reinforcement
{
    public enum ReportKind
    {
        UsersPerFilePerDay,   // дни × файлы, число пользователей
        ClicksPerFilePerDay,  // дни × файлы, число кликов
        UsersPerDay,          // дни × пользователи
        FilesPerUser          // пользователи × файлы
    }
    public enum TextReportKind
    {
        Summary,    // общая сводка
        ByUsers,    // по пользователям
        ByFiles,    // по файлам
        ByDays      // по дням
    }
    [Transaction(TransactionMode.Manual)]
    public class Statist : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);

            try
            {
                if (!RequestPassword())
                {
                    message = "Неверный пароль или отмена.";
                    return Result.Cancelled;
                }

                
                var SettingsWindow = new StatistWindow();
                //Фикс — обязательно задавать Owner через HWND Revit:
                var helper = new System.Windows.Interop.WindowInteropHelper(SettingsWindow);
                helper.Owner = RevitAPI.UiApplication.MainWindowHandle;
                bool? resultW = SettingsWindow.ShowDialog();
                if (resultW != true)
                {
                    return Result.Cancelled;
                }
            }
            catch (Exception ex) 
            {
                App_Apdater_1.LookUsers.LogError(ex);
            }

            return Result.Succeeded;
        }

        public const string CorrectPasswordHash = "kerulen"; // например, SHA256 от пароля

        public static bool vernPassword=false;

        public static bool RequestPassword()
        {
            if (vernPassword) { return vernPassword; }//уже верный пароль

            using (var form = new PasswordForm())
            {
                var revitHandle = RevitAPI.UiApplication.MainWindowHandle;
                var owner = new RevitWindowHandle(revitHandle);

                var result = form.ShowDialog(owner);

                if (result != DialogResult.OK)
                    return false;
                vernPassword = IsPasswordCorrect(form.EnteredPassword);
                // Пароль верный — продолжаем
                TaskDialog.Show("OK", "Пароль принят.");
                return vernPassword;
            }
        }

        private static bool IsPasswordCorrect(string entered)
        {
            if (string.IsNullOrEmpty(entered))
                return false;

            // Простейший вариант — сравнение с константой.
            // Для продакшена используйте хеш (см. ниже).
            return entered == CorrectPasswordHash;
        }
    }
    internal class RevitWindowHandle : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }

        public RevitWindowHandle(IntPtr handle)
        {
            Handle = handle;
        }
    }


}
