using Autodesk.Revit.DB;
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

namespace Reinforcement
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class MainViewCreateViewPlan : Window
    {
        public MainViewCreateViewPlan(List<Level> levels)
        {
            InitializeComponent();
            DataContext = levels;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Hide();
           // (DataContext as CreateViewPlan).Levels;
            Close();
        }

    }
}
