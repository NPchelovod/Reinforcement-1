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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Reinforcement.CopySelectedSchedules
{
    /// <summary>
    /// Логика взаимодействия для ViewCopySelectedSchedules.xaml
    /// </summary>
    public partial class ViewCopySelectedSchedules : Window
    {
        public ViewCopySelectedSchedules(ViewModelCopySelectedSchedules viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
