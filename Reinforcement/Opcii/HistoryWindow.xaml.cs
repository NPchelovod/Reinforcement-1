using System;
using System.Windows;
using System.Text;

namespace Reinforcement
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
        }

        public void SetText(string text)
        {
            txtHistory.Text = text;
        }
    }
}
