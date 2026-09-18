using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Reinforcement
{
    public partial class LookUsers
    {
        private static DateTime _lastDialogShown = DateTime.MinValue;
        private static readonly object _dialogLock = new object();
        private const int DialogCooldownMs = 800;   // не чаще 1 окна в 0.8 сек
        private static int _lastDialogHash;
        /// <summary>
        /// Показывает окно с текстом ошибки поверх Revit.
        /// Безопасен для вызова из любого потока.
        /// </summary>
        private static bool ShowErrorDialog(string text)
        {
            bool disable = false;

            try
            {
                Action show = () =>
                {
                    try
                    {
                        var w = new ErrorAdminWindow(text);

                        var hwnd = RevitAPI.UiApplication?.MainWindowHandle ?? IntPtr.Zero;
                        if (hwnd != IntPtr.Zero)
                            new System.Windows.Interop.WindowInteropHelper(w).Owner = hwnd;

                        w.ShowDialog();

                        if (w.DisableRequested)
                            disable = true;
                    }
                    catch
                    {
                        // Fallback: нативный TaskDialog
                        try
                        {
                            new Autodesk.Revit.UI.TaskDialog("Ошибка Revit (админ-режим)")
                            {
                                MainInstruction = "Зафиксирована ошибка",
                                MainContent = text,
                                CommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons.Ok
                            }.Show();
                        }
                        catch
                        {
                            try
                            {
                                System.Windows.MessageBox.Show(
                                    text, "Ошибка Revit",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Error);
                            }
                            catch { }
                        }
                    }
                };

                // Уходим в UI-поток Revit
                var app = System.Windows.Application.Current;
                if (app == null)
                    app = new System.Windows.Application();

                if (app.Dispatcher.CheckAccess())
                    show();
                else
                    app.Dispatcher.Invoke(show);
            }
            catch
            {
                // Молча — не даём LogError самому упасть
            }

            return disable;
        }
        /// <summary>
        /// Троттлинг: возвращает true, если с момента последнего показа
        /// прошло не меньше DialogCooldownMs миллисекунд.
        /// Потокобезопасен.
        /// </summary>
        private static bool ShouldShowDialog(string text)
        {
            int hash = text?.GetHashCode() ?? 0;
            lock (_dialogLock)
            {
                var now = DateTime.UtcNow;

                // базовый кулдаун
                if ((now - _lastDialogShown).TotalMilliseconds < DialogCooldownMs)
                    return false;

                // ту же самую ошибку не показываем повторно в течение 5 сек
                if (hash == _lastDialogHash &&
                    (now - _lastDialogShown).TotalSeconds < 5)
                    return false;

                _lastDialogShown = now;
                _lastDialogHash = hash;
                return true;
            }
        }
    
    }
}