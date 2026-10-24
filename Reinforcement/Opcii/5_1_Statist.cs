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
                if (!RevitAPI.RequestPassword())
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
               RevitAPI.AppErrors.LogError(ex);
            }

            return Result.Succeeded;
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
