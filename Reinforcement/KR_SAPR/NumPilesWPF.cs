using System;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public partial class PileSettingsWindow : Window
    {
        public double SectorStep { get; set; }
        public double SectorStepPile { get; set; }
        public double SectorStepZ { get; set; }
        public int PredelGroup { get; set; }
        public bool UstanNumPile { get; set; }
        public bool UstanUGO { get; set; }
        public bool ReturnCoord { get; set; }
        public int FoundPilesCount { get; set; }
        public bool ContinueExecution { get; set; }

        public PileSettingsWindow(int foundPilesCount, double currentSectorStep,
            double currentSectorStepPile, double currentSectorStepZ, int currentPredelGroup,
            bool currentUstanNumPile, bool currentUstanUGO, bool currentReturnCoord)
        {
            InitializeComponent();
            FoundPilesCount = foundPilesCount;
            SectorStep = currentSectorStep;
            SectorStepPile = currentSectorStepPile;
            SectorStepZ = currentSectorStepZ;
            PredelGroup = currentPredelGroup;
            UstanNumPile = currentUstanNumPile;
            UstanUGO = currentUstanUGO;
            ReturnCoord = currentReturnCoord;
            ContinueExecution = false;

            // Заполняем поля текущими значениями
            pilesCountText.Text = $"Найдено свай: {foundPilesCount}";
            sectorStepTextBox.Text = currentSectorStep.ToString();
            sectorStepPileTextBox.Text = currentSectorStepPile.ToString();
            sectorStepZTextBox.Text = currentSectorStepZ.ToString();
            predelGroupTextBox.Text = currentPredelGroup.ToString();

            ustanNumPileCheckBox.IsChecked = currentUstanNumPile;
            ustanUGOCheckBox.IsChecked = currentUstanUGO;
            returnCoordCheckBox.IsChecked = currentReturnCoord;
        }

        private void InitializeComponent()
        {
            this.Width = 450;
            this.Height = 620; // Увеличили высоту из-за новых полей
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
                Margin = new Thickness(0, 0, 0, 20),
                FontWeight = FontWeights.Bold
            };
            mainStackPanel.Children.Add(pilesCountText);

            // === ГАЛОЧКИ НАСТРОЕК (сначала) ===

            // Галочка для установки номеров свай
            var ustanNumPilePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var ustanNumPileLabel = new TextBlock
            {
                Text = "Нумеровать сваи:",
                FontSize = 12,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            ustanNumPileCheckBox = new CheckBox
            {
                IsChecked = UstanNumPile,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Установить марки (номера) для свай"
            };
            ustanNumPilePanel.Children.Add(ustanNumPileLabel);
            ustanNumPilePanel.Children.Add(ustanNumPileCheckBox);
            mainStackPanel.Children.Add(ustanNumPilePanel);

            // Галочка для установки УГО
            var ustanUGOPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var ustanUGOLabel = new TextBlock
            {
                Text = "Установить УГО:",
                FontSize = 12,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            ustanUGOCheckBox = new CheckBox
            {
                IsChecked = UstanUGO,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Установить УГО для групп свай"
            };
            ustanUGOPanel.Children.Add(ustanUGOLabel);
            ustanUGOPanel.Children.Add(ustanUGOCheckBox);
            mainStackPanel.Children.Add(ustanUGOPanel);

            // Галочка для обратной сортировки
            var returnCoordPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 20)
            };
            var returnCoordLabel = new TextBlock
            {
                Text = "Обратная сортировка:",
                FontSize = 12,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            returnCoordCheckBox = new CheckBox
            {
                IsChecked = ReturnCoord,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Сначала по X, потом по Y (обычно: сначала по Y, потом по X)"
            };
            returnCoordPanel.Children.Add(returnCoordLabel);
            returnCoordPanel.Children.Add(returnCoordCheckBox);
            mainStackPanel.Children.Add(returnCoordPanel);

            // Разделитель
            var separator = new Separator
            {
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStackPanel.Children.Add(separator);

            // === ЧИСЛОВЫЕ ПАРАМЕТРЫ ===

            // Поле для sectorStep
            var sectorStepPanel = CreateNumberInputPanel(
                "Шаг группировки X,Y (мм):",
                SectorStep.ToString("F0"),
                out sectorStepTextBox,
                "Расстояние для группировки свай по осям X и Y (рекомендуется 900-1500 мм)"
            );
            mainStackPanel.Children.Add(sectorStepPanel);

            // Поле для sectorStepPile
            var sectorStepPilePanel = CreateNumberInputPanel(
                "Шаг округления сваи (мм):",
                SectorStepPile.ToString("F0"),
                out sectorStepPileTextBox,
                "Точность определения координат свай (например, 50 мм для округления)"
            );
            mainStackPanel.Children.Add(sectorStepPilePanel);

            // Поле для sectorStepZ
            var sectorStepZPanel = CreateNumberInputPanel(
                "Шаг по высоте Z (мм):",
                SectorStepZ.ToString("F0"),
                out sectorStepZTextBox,
                "Для группировки по оси Z (УГО)"
            );
            mainStackPanel.Children.Add(sectorStepZPanel);

            // Поле для predelGroup
            var predelGroupPanel = CreateNumberInputPanel(
                "Лимит группы:",
                PredelGroup.ToString(),
                out predelGroupTextBox,
                "Максимальное количество свай в одной группе. 0 = без лимита"
            );
            mainStackPanel.Children.Add(predelGroupPanel);

            // Подсказки
            var hintsText = new TextBlock
            {
                Text = "Подсказки:\n" +
                       "• Шаг группировки X,Y: расстояние между соседними сваями для группировки в КУСТ и поиска соседей\n" +
                       "• Шаг округления сваи: точность расположения свай в 1 ряд\n" +
                       "• Шаг по высоте Z: для группировки свай по УГО\n" +
                       "• Лимит группы: максимальное количество свай в КУСТе для нумерации в КУСТе\n" +
                       "• Нумеровать сваи: установит марки свай (1, 2...)\n" +
                       "• Установить УГО: установит обозначения для групп\n" +
                       "• Обратная сортировка: меняет порядок осей сортировки свай",
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
            sectorStepTextBox.Focus();
            sectorStepTextBox.SelectAll();

            this.Content = mainStackPanel;
        }

        // Вспомогательный метод для создания панели с числовым вводом
        private StackPanel CreateNumberInputPanel(string label, string defaultValue, out TextBox textBox, string tooltip)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var labelControl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center
            };

            textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 12,
                Width = 120,
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
                IsDefault = true // Enter активирует эту кнопку
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 120,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true // Esc закрывает окно
            };
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            return buttonPanel;
        }

        private TextBlock pilesCountText;
        private TextBox sectorStepTextBox;
        private TextBox sectorStepPileTextBox;
        private TextBox sectorStepZTextBox;
        private TextBox predelGroupTextBox;

        private CheckBox ustanNumPileCheckBox;
        private CheckBox ustanUGOCheckBox;
        private CheckBox returnCoordCheckBox;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация введенных данных
            if (!ValidateNumber(sectorStepTextBox.Text, "Шаг группировки", out double sectorStep, 0))
                return;

            if (!ValidateNumber(sectorStepPileTextBox.Text, "Шаг округления сваи", out double sectorStepPile, 1))
                return;

            if (!ValidateNumber(sectorStepZTextBox.Text, "Шаг по высоте", out double sectorStepZ, 0))
                return;

            if (!ValidateInteger(predelGroupTextBox.Text, "Лимит группы", out int predelGroup, 0))
                return;

            SectorStep = sectorStep;
            SectorStepPile = sectorStepPile;
            SectorStepZ = sectorStepZ;
            PredelGroup = predelGroup;
            UstanNumPile = ustanNumPileCheckBox.IsChecked ?? false;
            UstanUGO = ustanUGOCheckBox.IsChecked ?? false;
            ReturnCoord = returnCoordCheckBox.IsChecked ?? false;

            // Проверка: хотя бы одна опция должна быть включена
            if (!UstanNumPile && !UstanUGO)
            {
                MessageBox.Show("Не выбрано ни одной опции!\nВыберите хотя бы 'Нумеровать сваи' или 'Установить УГО'",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                if (fieldName.Contains("группировки"))
                    sectorStepTextBox.Focus();
                else if (fieldName.Contains("округления"))
                    sectorStepPileTextBox.Focus();
                else if (fieldName.Contains("высоте"))
                    sectorStepZTextBox.Focus();

                return false;
            }
            return true;
        }

        private bool ValidateInteger(string text, string fieldName, out int value, int minValue = 0)
        {
            if (!int.TryParse(text, out value) || value < minValue)
            {
                string message = minValue == 0 ?
                    $"{fieldName} должен быть неотрицательным целым числом!" :
                    $"{fieldName} должен быть целым числом не менее {minValue}!";

                MessageBox.Show(message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                predelGroupTextBox.Focus();
                predelGroupTextBox.SelectAll();
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