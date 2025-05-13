using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Reinforcement.LinearCopy
{
    /// <summary>
    /// Логика взаимодействия для MainViewLinearCopyElement.xaml
    /// </summary>
    public partial class MainViewLinearCopyElement : Window
    {
        public MainViewLinearCopyElement()
        {
            InitializeComponent();
        }
        private void Click_Select(object sender, RoutedEventArgs e)
        {
            Hide();
            LinearCopyElement.copyStep = textName.Text;
            Close();

        }
        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            LinearCopyElement.copyStep = "stop";
            Close();
        }
    }
}
