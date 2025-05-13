using Autodesk.Revit.DB;
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

namespace Reinforcement
{
    /// <summary>
    /// Логика взаимодействия для MainViewCreateViewPlan.xaml
    /// </summary>
    public partial class MainViewCreateViewPlan : Window
    {
        public MainViewCreateViewPlan(ViewModelCreateViewPlan Levels)
        {
            InitializeComponent();
            DataContext = Levels;
        }

        
        private void Click_Create(object sender, RoutedEventArgs e)
        {
            Hide();
            (DataContext as ViewModelCreateViewPlan).CreateViewPlan();            
            Close();
        }
        private void Click_Cancel(object sender, RoutedEventArgs e)
        {          
            Close();                  
        }

    }
}
