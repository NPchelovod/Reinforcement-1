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
using Autodesk.Revit.DB;

namespace Reinforcement
{
    /// <summary>
    /// Логика взаимодействия для MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {

        public MainView(ViewModelCreateSchedules createSchedules)
        {
            InitializeComponent();
            DataContext = createSchedules;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            (DataContext as ViewModelCreateSchedules).SchedulesDuplication();
            Close();
        }
        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }


    }
}

