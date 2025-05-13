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
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class MainViewReinforcementList : Window
    {
        public MainViewReinforcementList()
        {
            InitializeComponent();
        }
        private void Click_Select(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Hide();
            ReinforcementList.ElementMark = elementMark.Text;
            ReinforcementList.ConstrMark = constrMark.Text;
            Close();
        }
        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
