using System;
using System.Linq;
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
        public bool DoNotRenumberNumberedPiles { get; set; }
        public bool DoNotChangeUGOIfExists { get; set; }
        public string SortCode { get; set; }
        public string SortCodeUGO { get; set; }
        public int FoundPilesCount { get; set; }
        public bool ContinueExecution { get; set; }

        // Новые свойства для корректировки координат
        public bool AdjustPilePositions { get; set; }
        public double MinDistanceBetweenPiles { get; set; }
        public double CoordinateRoundingStep { get; set; }

        // Новая опция - пересоздать все сваи
        public bool RecreateAllPiles { get; set; }

        public PileSettingsWindow(int foundPilesCount, double currentSectorStep,
            double currentSectorStepPile, double currentSectorStepZ, int currentPredelGroup,
            bool currentUstanNumPile, bool currentUstanUGO, bool currentDoNotRenumberNumberedPiles,
            bool currentDoNotChangeUGOIfExists, string currentSortCode, string currentSortCodeUGO,
            bool currentAdjustPilePositions = false, double currentMinDistanceBetweenPiles = 900,
            double currentCoordinateRoundingStep = 25, bool currentRecreateAllPiles = false)
        {
            InitializeComponent();
            FoundPilesCount = foundPilesCount;
            SectorStep = currentSectorStep;
            SectorStepPile = currentSectorStepPile;
            SectorStepZ = currentSectorStepZ;
            PredelGroup = currentPredelGroup;
            UstanNumPile = currentUstanNumPile;
            UstanUGO = currentUstanUGO;
            DoNotRenumberNumberedPiles = currentDoNotRenumberNumberedPiles;
            DoNotChangeUGOIfExists = currentDoNotChangeUGOIfExists;
            SortCode = currentSortCode;
            SortCodeUGO = currentSortCodeUGO;

            // Новые параметры
            AdjustPilePositions = currentAdjustPilePositions;
            MinDistanceBetweenPiles = currentMinDistanceBetweenPiles;
            CoordinateRoundingStep = currentCoordinateRoundingStep;
            RecreateAllPiles = currentRecreateAllPiles;

            ContinueExecution = false;

            // Заполняем поля текущими значениями
            pilesCountText.Text = $"Найдено свай: {foundPilesCount}";
            sectorStepTextBox.Text = currentSectorStep.ToString();
            sectorStepPileTextBox.Text = currentSectorStepPile.ToString();
            sectorStepZTextBox.Text = currentSectorStepZ.ToString();
            predelGroupTextBox.Text = currentPredelGroup.ToString();
            sortCodeTextBox.Text = currentSortCode;
            sortCodeUGOTextBox.Text = currentSortCodeUGO;

            // Новые поля
            adjustPositionsCheckBox.IsChecked = currentAdjustPilePositions;
            minDistanceTextBox.Text = currentMinDistanceBetweenPiles.ToString();
            coordinateRoundingTextBox.Text = currentCoordinateRoundingStep.ToString();
            recreateAllPilesCheckBox.IsChecked = currentRecreateAllPiles;

            ustanNumPileCheckBox.IsChecked = currentUstanNumPile;
            ustanUGOCheckBox.IsChecked = currentUstanUGO;
            doNotRenumberCheckBox.IsChecked = currentDoNotRenumberNumberedPiles;
            doNotChangeUGOIfExistsCheckBox.IsChecked = currentDoNotChangeUGOIfExists;
        }

        private void InitializeComponent()
        {
            this.Width = 650;
            this.Height = 950; // Увеличили высоту для нового элемента
            this.MinWidth = 600;
            this.MinHeight = 700;
            this.MaxHeight = 1200;
            this.Title = "Настройки нумерации и корректировки свай";
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.CanResizeWithGrip;
            this.SizeToContent = SizeToContent.Manual;

            // Создаем основной контейнер с прокруткой
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0)
            };

            var mainStackPanel = new StackPanel
            {
                Margin = new Thickness(20, 10, 20, 20),
                Orientation = Orientation.Vertical
            };

            // Заголовок
            var titleText = new TextBlock
            {
                Text = "Параметры группировки и корректировки свай",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap
            };
            mainStackPanel.Children.Add(titleText);

            // Информация о найденных сваях
            pilesCountText = new TextBlock
            {
                Text = $"Найдено свай: {FoundPilesCount}",
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 20),
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.DarkGreen
            };
            mainStackPanel.Children.Add(pilesCountText);

            // === РАЗДЕЛ: КОРРЕКТИРОВКА КООРДИНАТ СВАЙ ===
            var correctionTitle = CreateSectionTitle("Корректировка координат свай");
            mainStackPanel.Children.Add(correctionTitle);

            // Галочка для корректировки положений свай
            var adjustPositionsPanel = CreateCheckBoxPanel(
                "Корректировать положения свай:",
                AdjustPilePositions,
                out adjustPositionsCheckBox,
                "Автоматически корректировать положения свай при пересечениях"
            );
            mainStackPanel.Children.Add(adjustPositionsPanel);

            // Галочка для пересоздания всех свай
            var recreateAllPilesPanel = CreateCheckBoxPanel(
                "Пересоздать все сваи:",
                RecreateAllPiles,
                out recreateAllPilesCheckBox,
                "Удалить все найденные сваи и создать их заново с учетом новых координат и параметров"
            );
            mainStackPanel.Children.Add(recreateAllPilesPanel);

            // Поле для минимальной дистанции
            var minDistancePanel = CreateNumberInputPanel(
                "Минимальная дистанция между сваями (мм):",
                MinDistanceBetweenPiles.ToString("F0"),
                out minDistanceTextBox,
                "Минимальное расстояние между центрами свай. При меньшем расстоянии будет выполнена корректировка",
                240
            );
            mainStackPanel.Children.Add(minDistancePanel);

            // Поле для шага округления координат
            var coordinateRoundingPanel = CreateNumberInputPanel(
                "Шаг округления координат (мм):",
                CoordinateRoundingStep.ToString("F0"),
                out coordinateRoundingTextBox,
                "Координаты свай будут округляться кратно этому числу",
                240
            );
            mainStackPanel.Children.Add(coordinateRoundingPanel);

            // Разделитель
            mainStackPanel.Children.Add(CreateSeparator());

            // === РАЗДЕЛ: ОСНОВНЫЕ НАСТРОЙКИ ===
            var mainSettingsTitle = CreateSectionTitle("Основные настройки нумерации");
            mainStackPanel.Children.Add(mainSettingsTitle);

            // Галочка для установки номеров свай
            var ustanNumPilePanel = CreateCheckBoxPanel(
                "Нумеровать сваи:",
                UstanNumPile,
                out ustanNumPileCheckBox,
                "Установить марки (номера) для свай"
            );
            mainStackPanel.Children.Add(ustanNumPilePanel);

            // Галочка для установки УГО
            var ustanUGOPanel = CreateCheckBoxPanel(
                "Установить УГО:",
                UstanUGO,
                out ustanUGOCheckBox,
                "Установить УГО для групп свай"
            );
            mainStackPanel.Children.Add(ustanUGOPanel);

            // Галочка - Не перенумеровывать нумерованные сваи
            var doNotRenumberPanel = CreateCheckBoxPanel(
                "Не перенумеровывать нумерованные сваи:",
                DoNotRenumberNumberedPiles,
                out doNotRenumberCheckBox,
                "Если свая уже имеет маркировку (номер), не изменять его"
            );
            mainStackPanel.Children.Add(doNotRenumberPanel);

            // Галочка - Не менять УГО если он есть
            var doNotChangeUGOIfExistsPanel = CreateCheckBoxPanel(
                "Не менять УГО если он есть:",
                DoNotChangeUGOIfExists,
                out doNotChangeUGOIfExistsCheckBox,
                "Если у сваи уже установлено УГО, не изменять его"
            );
            mainStackPanel.Children.Add(doNotChangeUGOIfExistsPanel);

            // Разделитель
            mainStackPanel.Children.Add(CreateSeparator());

            // === ЧИСЛОВЫЕ ПАРАМЕТРЫ ===

            // Поле для sectorStep
            var sectorStepPanel = CreateNumberInputPanel(
                "Шаг группировки свай в КУСТ (мм):",
                SectorStep.ToString("F0"),
                out sectorStepTextBox,
                "Расстояние для группировки свай в один куст и дальнейшая его сортировка в кусте (200-1500)",
                240
            );
            mainStackPanel.Children.Add(sectorStepPanel);

            // Поле для sectorStepPile
            var sectorStepPilePanel = CreateNumberInputPanel(
                "Шаг рядов свай (мм):",
                SectorStepPile.ToString("F0"),
                out sectorStepPileTextBox,
                "По рядам мы нумеруем, поэтому с данной погрешностью сваи будут считаться в одном ряду (450-1500)",
                240
            );
            mainStackPanel.Children.Add(sectorStepPilePanel);

            // Поле для sectorStepZ
            var sectorStepZPanel = CreateNumberInputPanel(
                "Шаг по высоте Z (мм):",
                SectorStepZ.ToString("F0"),
                out sectorStepZTextBox,
                "Для группировки по оси Z (УГО)",
                240
            );
            mainStackPanel.Children.Add(sectorStepZPanel);

            // Поле для predelGroup
            var predelGroupPanel = CreateNumberInputPanel(
                "Лимит свай в КУСТе (шт):",
                PredelGroup.ToString(),
                out predelGroupTextBox,
                "Максимальное количество свай в одной группе. 0 = без лимита, 1 = без кустов",
                240
            );
            mainStackPanel.Children.Add(predelGroupPanel);

            // Поле для кода сортировки свай
            var sortCodePanel = CreateTextInputPanel(
                "Код сортировки свай:",
                SortCode,
                out sortCodeTextBox,
                "Введите код для пользовательской сортировки (например, 134)",
                240
            );
            mainStackPanel.Children.Add(sortCodePanel);

            // Поле для кода сортировки уго
            var sortCodeUGOPanel = CreateTextInputPanel(
                "Код сортировки УГО:",
                SortCodeUGO,
                out sortCodeUGOTextBox,
                "Введите код для пользовательской сортировки (например, 123)",
                240
            );
            mainStackPanel.Children.Add(sortCodeUGOPanel);

            // Разделитель перед подсказками
            mainStackPanel.Children.Add(CreateSeparator());

            // Подсказки (обновленные с новыми опциями) - в раскрывающемся элементе
            var hintsExpander = new Expander
            {
                Header = "📚 Подсказки по настройкам",
                IsExpanded = false,
                Margin = new Thickness(0, 15, 0, 15),
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                Padding = new Thickness(5)
            };

            var hintsText = new TextBlock
            {
                Text = GetHintsText(),
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5),
                LineHeight = 16
            };

            hintsExpander.Content = hintsText;
            mainStackPanel.Children.Add(hintsExpander);

            // Кнопки
            var buttonPanel = CreateButtonPanel();
            mainStackPanel.Children.Add(buttonPanel);

            // Устанавливаем контент в ScrollViewer
            scrollViewer.Content = mainStackPanel;
            this.Content = scrollViewer;

            // Устанавливаем фокус
            sectorStepTextBox.Focus();
            sectorStepTextBox.SelectAll();
        }

        private string GetHintsText()
        {
            return @"• Корректировать положения свай: если включено, сваи, расположенные ближе минимальной дистанции, будут автоматически смещены
• Пересоздать все сваи: удалить все найденные сваи и создать их заново с учетом новых координат и параметров (может ускорить процесс при большом количестве изменений)
• Минимальная дистанция между сваями: расстояние, меньше которого считается пересечением (рекомендуется 900 мм для стандартных свай)
• Шаг округления координат: координаты свай будут округляться до ближайшего кратного значения
• Нумеровать сваи: установит марки свай (1, 2...)
• Установить УГО: установит графическое обозначение сваям
• Не перенумеровывать нумерованные сваи: если свая уже имеет маркировку, не изменять ее
• Не менять УГО если он есть: если у сваи уже установлено УГО, не изменять его
• Шаг группировки свай в КУСТ: расстояние между соседними сваями для группировки в КУСТ и поиска соседей (чуть больше шага свай или 100 для игнора)
• Шаг рядов свай: точность расположения свай в 1 ряд
• Шаг по высоте Z: для группировки свай по УГО
• Лимит свай в КУСТе: максимальное количество свай в КУСТе для нумерации в КУСТе (1 - без кустов, 0 - без лимита)
• Код сортировки свай: пользовательский код для порядка сортировки свай (1346)
  1 - сортировка по Y затем по X
  2 - наоборот, сортировка по X затем по Y
  3 - по типу сваи
  4 - по количеству свай в типе
  6 - вместо сортировки куста к левому верхнему углу использовать центр куста
  7 - сортировка сверху вниз
• Код сортировки УГО:
  1 - сортировка по типу
  2 - по количеству свай в типе
  3 - по убыванию Z
  4 - по возрастанию Z";
        }

        // Вспомогательный метод для создания заголовка раздела
        private TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 12),
                Foreground = System.Windows.Media.Brushes.DarkBlue
            };
        }

        // Вспомогательный метод для создания разделителя
        private Separator CreateSeparator()
        {
            return new Separator
            {
                Margin = new Thickness(0, 10, 0, 15),
                Height = 1,
                Background = System.Windows.Media.Brushes.LightGray
            };
        }

        // Вспомогательный метод для создания панели с чекбоксом
        private StackPanel CreateCheckBoxPanel(string label, bool isChecked, out CheckBox checkBox, string tooltip)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var labelControl = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Width = 320,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            checkBox = new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = tooltip,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            panel.Children.Add(labelControl);
            panel.Children.Add(checkBox);

            return panel;
        }

        // Вспомогательный метод для создания панели с числовым вводом
        private StackPanel CreateNumberInputPanel(string label, string defaultValue, out TextBox textBox, string tooltip, double labelWidth = 240)
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
                Width = labelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 12,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip,
                Margin = new Thickness(5, 0, 0, 0)
            };

            panel.Children.Add(labelControl);
            panel.Children.Add(textBox);

            return panel;
        }

        // Вспомогательный метод для создания панели с текстовым вводом
        private StackPanel CreateTextInputPanel(string label, string defaultValue, out TextBox textBox, string tooltip, double labelWidth = 240)
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
                Width = labelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 12,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip,
                Margin = new Thickness(5, 0, 0, 0)
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
                Margin = new Thickness(0, 20, 0, 10)
            };

            var okButton = new Button
            {
                Content = "✅ Продолжить",
                Width = 150,
                Height = 35,
                Margin = new Thickness(10, 5, 10, 5),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                IsDefault = true
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Content = "❌ Отмена",
                Width = 150,
                Height = 35,
                Margin = new Thickness(10, 5, 10, 5),
                FontSize = 13,
                IsCancel = true
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
        private TextBox sortCodeTextBox;
        private TextBox sortCodeUGOTextBox;

        // Новые поля
        private CheckBox adjustPositionsCheckBox;
        private CheckBox recreateAllPilesCheckBox;
        private TextBox minDistanceTextBox;
        private TextBox coordinateRoundingTextBox;

        private CheckBox ustanNumPileCheckBox;
        private CheckBox ustanUGOCheckBox;
        private CheckBox doNotRenumberCheckBox;
        private CheckBox doNotChangeUGOIfExistsCheckBox;

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

            // Валидация новых параметров
            if (!ValidateNumber(minDistanceTextBox.Text, "Минимальная дистанция", out double minDistance, 0))
                return;

            if (!ValidateNumber(coordinateRoundingTextBox.Text, "Шаг округления координат", out double roundingStep, 0))
                return;

            // Проверка кода сортировки свай
            if (!IsValidSortCode(sortCodeTextBox.Text, "свай", new char[] { '1', '2', '3', '4', '5', '6', '7', '8' }))
                return;

            // Проверка кода сортировки УГО
            if (!IsValidSortCode(sortCodeUGOTextBox.Text, "УГО", new char[] { '1', '2', '3', '4', '5', '6' }))
                return;

            SectorStep = sectorStep;
            SectorStepPile = sectorStepPile;
            SectorStepZ = sectorStepZ;
            PredelGroup = predelGroup;
            SortCode = sortCodeTextBox.Text;
            SortCodeUGO = sortCodeUGOTextBox.Text;

            // Новые параметры
            AdjustPilePositions = adjustPositionsCheckBox.IsChecked ?? false;
            RecreateAllPiles = recreateAllPilesCheckBox.IsChecked ?? false;
            MinDistanceBetweenPiles = minDistance;
            CoordinateRoundingStep = roundingStep;

            UstanNumPile = ustanNumPileCheckBox.IsChecked ?? false;
            UstanUGO = ustanUGOCheckBox.IsChecked ?? false;
            DoNotRenumberNumberedPiles = doNotRenumberCheckBox.IsChecked ?? false;
            DoNotChangeUGOIfExists = doNotChangeUGOIfExistsCheckBox.IsChecked ?? false;

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
                else if (fieldName.Contains("округления сваи"))
                    sectorStepPileTextBox.Focus();
                else if (fieldName.Contains("высоте"))
                    sectorStepZTextBox.Focus();
                else if (fieldName.Contains("дистанция"))
                    minDistanceTextBox.Focus();
                else if (fieldName.Contains("округления координат"))
                    coordinateRoundingTextBox.Focus();

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

        // Вспомогательный метод
        private bool IsValidSortCode(string code, string codeType, char[] allowedDigits)
        {
            if (string.IsNullOrEmpty(code))
                return true; // Пустой код допустим

            // Проверяем, что все символы - цифры и находятся в allowedDigits
            foreach (char c in code)
            {
                if (!allowedDigits.Contains(c))
                {
                    MessageBox.Show($"Код сортировки {codeType} содержит недопустимый символ '{c}'.\nДопустимы только цифры: {string.Join(", ", allowedDigits)}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            // Проверяем на повторяющиеся цифры
            if (code.Distinct().Count() != code.Length)
            {
                MessageBox.Show($"Код сортировки {codeType} содержит повторяющиеся цифры.\nКаждая цифра должна быть уникальной.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }
    }
}