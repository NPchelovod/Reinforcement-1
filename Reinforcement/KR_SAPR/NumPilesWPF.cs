using System;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public partial class PileSettingsWindow : Window
    {
        public double SectorStep { get; set; }
        public double SectorStepZ { get; set; }
        public int PredelGroup { get; set; }
        public int FoundPilesCount { get; set; }
        public bool ContinueExecution { get; set; }

        public PileSettingsWindow(int foundPilesCount, double currentSectorStep, double currentSectorStepZ, int currentPredelGroup)
        {
            InitializeComponent();
            FoundPilesCount = foundPilesCount;
            SectorStep = currentSectorStep;
            SectorStepZ = currentSectorStepZ;
            PredelGroup = currentPredelGroup;
            ContinueExecution = false;

            // Заполняем поля текущими значениями
            pilesCountText.Text = $"Найдено свай: {foundPilesCount}";
            sectorStepTextBox.Text = currentSectorStep.ToString();
            sectorStepZTextBox.Text = currentSectorStepZ.ToString();
            predelGroupTextBox.Text = currentPredelGroup.ToString();
        }

        private void InitializeComponent()
        {
            this.Width = 400;
            this.Height = 450;
            this.Title = "Настройки нумерации свай";
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;

            var mainStackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical
            };

            // Заголовок
            var titleText = new TextBlock
            {
                Text = "Параметры группировки свай",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(titleText);

            // Информация о найденных сваях
            pilesCountText = new TextBlock
            {
                Text = $"Найдено свай: {FoundPilesCount}",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(pilesCountText);

            // Поле для sectorStep
            var sectorStepPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var sectorStepLabel = new TextBlock
            {
                Text = "Шаг сектора (мм):",
                FontSize = 12,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };
            sectorStepTextBox = new TextBox
            {
                Text = SectorStep.ToString(),
                FontSize = 12,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            sectorStepPanel.Children.Add(sectorStepLabel);
            sectorStepPanel.Children.Add(sectorStepTextBox);
            mainStackPanel.Children.Add(sectorStepPanel);

            // Поле для sectorStepZ
            var sectorStepZPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var sectorStepZLabel = new TextBlock
            {
                Text = "Шаг по высоте (мм):",
                FontSize = 12,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };
            sectorStepZTextBox = new TextBox
            {
                Text = SectorStepZ.ToString(),
                FontSize = 12,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            sectorStepZPanel.Children.Add(sectorStepZLabel);
            sectorStepZPanel.Children.Add(sectorStepZTextBox);
            mainStackPanel.Children.Add(sectorStepZPanel);

            // Поле для predelGroup
            var predelGroupPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            var predelGroupLabel = new TextBlock
            {
                Text = "Лимит группы:",
                FontSize = 12,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            };
            predelGroupTextBox = new TextBox
            {
                Text = PredelGroup.ToString(),
                FontSize = 12,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            predelGroupPanel.Children.Add(predelGroupLabel);
            predelGroupPanel.Children.Add(predelGroupTextBox);
            mainStackPanel.Children.Add(predelGroupPanel);

            // Подсказки
            var hintsText = new TextBlock
            {
                Text = "Подсказки:\n• Шаг сектора: расстояние для группировки свай по осям X и Y равен шагу свай 900 или больше для поиска пар >1300\n• Шаг по высоте: для группировки по оси Z УГО\n• Лимит группы: максимальное количество свай в одной группе которую нумеруем (например кусты свай по 4 сваи) (0 = без лимита - любой куст)",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(hintsText);

            // Кнопки
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var okButton = new Button
            {
                Content = "Продолжить",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5)
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5)
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            mainStackPanel.Children.Add(buttonPanel);

            this.Content = mainStackPanel;
        }

        private TextBlock pilesCountText;
        private TextBox sectorStepTextBox;
        private TextBox sectorStepZTextBox;
        private TextBox predelGroupTextBox;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация введенных данных
            if (!double.TryParse(sectorStepTextBox.Text, out double sectorStep) || sectorStep <= 0)
            {
                MessageBox.Show("Шаг сектора должен быть положительным числом!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!double.TryParse(sectorStepZTextBox.Text, out double sectorStepZ) || sectorStepZ <= 0)
            {
                MessageBox.Show("Шаг по высоте должен быть положительным числом!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(predelGroupTextBox.Text, out int predelGroup) || predelGroup < 0)
            {
                MessageBox.Show("Лимит группы должен быть неотрицательным целым числом!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SectorStep = sectorStep;
            SectorStepZ = sectorStepZ;
            PredelGroup = predelGroup;
            ContinueExecution = true;

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ContinueExecution = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}