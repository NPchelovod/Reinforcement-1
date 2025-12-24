using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public partial class PileDWGSettingsWindow : Window
    {
        public bool AdjustPilePositions { get; set; }
        public double RoundingStep { get; set; }
        public double MinDistanceBetweenPiles { get; set; }
        public bool ContinueExecution { get; set; }

        public PileDWGSettingsWindow(bool currentAdjustPilePositions,
            double currentRoundingStep, double currentMinDistanceBetweenPiles)
        {
            InitializeComponent();

            AdjustPilePositions = currentAdjustPilePositions;
            RoundingStep = currentRoundingStep;
            MinDistanceBetweenPiles = currentMinDistanceBetweenPiles;
            ContinueExecution = false;

            // Заполняем поля текущими значениями
            adjustPositionsCheckBox.IsChecked = currentAdjustPilePositions;
            roundingStepTextBox.Text = currentRoundingStep.ToString("F0");
            minDistanceTextBox.Text = currentMinDistanceBetweenPiles.ToString("F0");
        }

        private void InitializeComponent()
        {
            this.Width = 500;
            this.Height = 400;
            this.Title = "Настройки расстановки свай по DWG";
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
                Text = "Параметры расстановки свай",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(titleText);

            // Галочка для корректировки положений свай
            var adjustPositionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };
            var adjustPositionsLabel = new TextBlock
            {
                Text = "Корректировать положения свай:",
                FontSize = 12,
                Width = 220,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            adjustPositionsCheckBox = new CheckBox
            {
                IsChecked = AdjustPilePositions,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Автоматически корректировать положения свай при пересечениях"
            };
            adjustPositionsPanel.Children.Add(adjustPositionsLabel);
            adjustPositionsPanel.Children.Add(adjustPositionsCheckBox);
            mainStackPanel.Children.Add(adjustPositionsPanel);

            // Разделитель
            var separator = new Separator
            {
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(separator);

            // Поле для шага округления
            var roundingStepPanel = CreateNumberInputPanel(
                "Шаг округления координат (мм):",
                RoundingStep.ToString("F0"),
                out roundingStepTextBox,
                "Координаты свай будут округляться кратно этому числу (например, 25)"
            );
            mainStackPanel.Children.Add(roundingStepPanel);

            // Поле для минимальной дистанции
            var minDistancePanel = CreateNumberInputPanel(
                "Минимальная дистанция между сваями (мм):",
                MinDistanceBetweenPiles.ToString("F0"),
                out minDistanceTextBox,
                "Минимальное расстояние между центрами свай. При меньшем расстоянии будет выполнена корректировка"
            );
            mainStackPanel.Children.Add(minDistancePanel);

            // Подсказки
            var hintsText = new TextBlock
            {
                Text = "Подсказки:\n" +
                       "• Корректировать положения свай: если включено, сваи, расположенные ближе минимальной дистанции, будут автоматически смещены\n" +
                       "• Шаг округления координат: координаты из DWG будут округляться до ближайшего кратного значения\n" +
                       "• Минимальная дистанция между сваями: расстояние, меньше которого считается пересечением (рекомендуется 900 мм для стандартных свай)",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 20, 0, 20),
                Padding = new Thickness(10),
            };
            mainStackPanel.Children.Add(hintsText);

            // Кнопки
            var buttonPanel = CreateButtonPanel();
            mainStackPanel.Children.Add(buttonPanel);

            // Устанавливаем фокус
            roundingStepTextBox.Focus();
            roundingStepTextBox.SelectAll();

            this.Content = mainStackPanel;
        }

        // Вспомогательный метод для создания панели с числовым вводом
        private StackPanel CreateNumberInputPanel(string label, string defaultValue, out TextBox textBox, string tooltip)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var labelControl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Width = 220,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };

            textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 12,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip
            };

            panel.Children.Add(labelControl);
            panel.Children.Add(textBox);

            return panel;
        }

        // Метод для создания панели с кнопками
        private StackPanel CreateButtonPanel()
        {
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var okButton = new Button
            {
                Content = "Продолжить",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5),
                FontWeight = FontWeights.Bold,
                IsDefault = true
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            return buttonPanel;
        }

        private CheckBox adjustPositionsCheckBox;
        private TextBox roundingStepTextBox;
        private TextBox minDistanceTextBox;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация введенных данных
            if (!ValidateNumber(roundingStepTextBox.Text, "Шаг округления", out double roundingStep, 0))
                return;

            if (!ValidateNumber(minDistanceTextBox.Text, "Минимальная дистанция", out double minDistance, 0))
                return;

            AdjustPilePositions = adjustPositionsCheckBox.IsChecked ?? false;
            RoundingStep = roundingStep;
            MinDistanceBetweenPiles = minDistance;

            ContinueExecution = true;
            this.DialogResult = true;
            this.Close();
        }

        private bool ValidateNumber(string text, string fieldName, out double value, double minValue = 0)
        {
            if (!double.TryParse(text, out value) || value <= minValue)
            {
                string message = minValue == 0 ?
                    $"{fieldName} должен быть положительным числом!" :
                    $"{fieldName} должен быть числом больше {minValue}!";

                MessageBox.Show(message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (fieldName.Contains("округления"))
                    roundingStepTextBox.Focus();
                else if (fieldName.Contains("дистанция"))
                    minDistanceTextBox.Focus();

                return false;
            }
            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ContinueExecution = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}