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

namespace Reinforcement._2_Architecture.CalculateReinforcementArchitectureWalls.View
{
    /// <summary>
    /// Логика взаимодействия для CalculateReinforcementArchitectureWallsView.xaml
    /// </summary>
    public partial class CalculateReinforcementArchitectureWallsView : Window
    {
        public CalculateReinforcementArchitectureWallsView()
        {
            InitializeComponent();
        }

        private void OnRunClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is CalculateReinforcementArchitectureWallsViewModel viewModel)
            {
                viewModel.Calculate();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
