using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Reinforcement
{
    /// <summary>
    /// Логика взаимодействия для TransparentNotificationWindow.xaml
    /// </summary>
    public partial class TransparentNotificationWindow : Window
    {
        /// <summary>
        /// 1. Текст сообщения.<para/>
        /// 2. Текущий документ Revit.<para/>
        /// 3. Время в секундах до закрытия. При значении 0 окно всегд открыто
        /// </summary>
        /// <param name="message"></param>
        /// <param name="uidoc"></param>
        /// <param name="timer"></param>
        public TransparentNotificationWindow(string message, UIDocument uidoc, int timer)
        {
            InitializeComponent();

            if (timer > 0)
            {
                var timerThread = new System.Timers.Timer(timer * 1000);// Создаем таймер (секунды -> миллисекунды)
                timerThread.Elapsed += (s, e) => CloseWindow();// При срабатывании таймера вызываем метод CloseWindow()
                timerThread.AutoReset = false; // Таймер срабатывает только один раз
                timerThread.AutoReset = false;// Таймер срабатывает только один раз
                timerThread.Start();// Запускаем таймер
            }

            NotificationBlock.Text = message;
            Loaded += (s, e) =>
            {
                UpdateLayout();

                var app = uidoc.Application;

                //Get drawing area coordinates
                var windowSize = app.DrawingAreaExtents;

                Measure(new Size(Width, double.PositiveInfinity));
                double desiredHeight = DesiredSize.Height;

                Left = windowSize.Right - Width - 22;
                Top = windowSize.Bottom - desiredHeight -20;
            };

        }
        /// <summary>
        /// Показывает уведомление.
        /// </summary>
        public static void ShowNotification(string message, UIDocument uidoc, int timer)
        {
            // Если окно уже открыто — закрыть его перед созданием нового
            _currentWindow?.Close();

            _currentWindow = new TransparentNotificationWindow(message, uidoc, timer);
            _currentWindow.Show();
        }
        private static TransparentNotificationWindow _currentWindow;

        private void CloseWindow()
        {
            Dispatcher.Invoke(() =>
            {
                _currentWindow = null;
                Close();
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _currentWindow = null;
        }
        private void CloseButton_Click (object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
