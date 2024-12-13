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
        public TransparentNotificationWindow(string message, UIDocument uidoc)
        {
            InitializeComponent();

            NotificationBlock.Text = message;
            var app = uidoc.Application;
            
            //Get drawing area coordinates
            var windowSize = app.DrawingAreaExtents;
            Left = windowSize.Left;
            Top = windowSize.Top + 22;

            //Left = SystemParameters.PrimaryScreenWidth - Width - 25;
            //Top = SystemParameters.PrimaryScreenHeight - Height - 80;

        }
        private void CloseButton_Click (object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
