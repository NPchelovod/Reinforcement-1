using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Reinforcement
{
    public partial class PileSettingsWindow2 : Window
    {
        // Свойства (без изменений)
        public double SectorStep { get; set; }
        public double SectorStepPile { get; set; }
        public double SectorStepZ { get; set; }
        public int PredelGroup { get; set; }
        public bool UstanNumPile { get; set; }
        public bool BoolNumPileIandex { get; set; } = true;
        public bool UstanUGO { get; set; }
        public bool GroupPiles { get; set; } = true;
        public bool SetNumComment { get; set; }
        public bool DoNotChangeUGOIfExists { get; set; }
        public string SortCode { get; set; }
        public string SortCodeUGO { get; set; }
        public int FoundPilesCount { get; set; }
        public bool ContinueExecution { get; set; }
        public bool AdjustPilePositions { get; set; }
        public double MinDistanceBetweenPiles { get; set; }
        public double CoordinateRoundingStep { get; set; }
        public bool RecreateAllPiles { get; set; }
        public bool RotorPiles { get; set; } = false;
        public bool ReloadUGO { get; set; }
        public int MarkStart { get; set; } = 1;
        public string MarkPrefix = "";
        public string MarkPostfix = "";
        public ExternalCommandData CommandData { get; set; }
        public PileSettingsWindow2(ExternalCommandData commandData)
        {
            InitializeComponent();

            CommandData = commandData;
            SeachPiles();

            nameFamilies.Text = String.Join( ",", FamilyPiles);

            // Заполняем поля текущими значениями
            adjustPositionsCheckBox.IsChecked = AdjustPilePositions;
            recreateAllPilesCheckBox.IsChecked = RecreateAllPiles;
            rotatePilesCheckBox.IsChecked = RotorPiles;
            minDistanceTextBox.Text = MinDistanceBetweenPiles.ToString();
            coordinateRoundingTextBox.Text = CoordinateRoundingStep.ToString();

            ustanNumPileCheckBox.IsChecked = UstanNumPile;
            markStartTextBox.Text = MarkStart.ToString();
            markPrefixTextBox.Text = MarkPrefix;
            markPostfixTextBox.Text = MarkPostfix;

            boolNumPileIandex.IsChecked = BoolNumPileIandex;


            sectorStepTextBox.Text = currentSectorStep.ToString();
            sectorStepPileTextBox.Text = currentSectorStepPile.ToString();
            sectorStepZTextBox.Text = currentSectorStepZ.ToString();
            predelGroupTextBox.Text = currentPredelGroup.ToString();
            sortCodeTextBox.Text = currentSortCode;
            sortCodeUGOTextBox.Text = currentSortCodeUGO;

            adjustPositionsCheckBox.IsChecked = currentAdjustPilePositions;
            minDistanceTextBox.Text = currentMinDistanceBetweenPiles.ToString();
            coordinateRoundingTextBox.Text = currentCoordinateRoundingStep.ToString();
            recreateAllPilesCheckBox.IsChecked = currentRecreateAllPiles;

            ustanNumPileCheckBox.IsChecked = currentUstanNumPile;
            ustanUGOCheckBox.IsChecked = currentUstanUGO;
            GroupPilesCheckBox.IsChecked = GroupPiles;
            setNumCommentCheckBox.IsChecked = currentCommentCheckBox;
           
            markStartTextBox.Text = markStart.ToString();
            
            reloadUGOCheckBox.IsChecked = ReloadUGO;
            boolNumPileIandex.IsChecked = BoolNumPileIandex;

            sectorStepTextBox.Focus();
            sectorStepTextBox.SelectAll();
        }

        public static HashSet<Element> Seacher = new HashSet<Element>();
        private void SeachPiles()
        {
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            
            // 1. Находим сваи
            Seacher = HelperSeachAllElements.SeachSelectElements(CommandData);
            if (Seacher.Count < 3)//значит мы специально не выделяли
            {
                Seacher = HelperSeachAllElements.SeachAllElements(FamilyPiles , CommandData, true);
            }
            pilesCountText.Text = $"Найдено свай/объектов (на виде или были выделены более 3 шт): {Seacher.Count} шт.";
        }
        private  HashSet<string> FamilyPiles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };
        private void SeachButton_Click(object sender, RoutedEventArgs e)
        {
            FamilyPiles = new HashSet<string>(pilesCountText.Text.Split(','));
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateNumber(sectorStepTextBox.Text, "Шаг группировки", out double sectorStep, 0))
                return;
            if (!ValidateNumber(sectorStepPileTextBox.Text, "Шаг округления сваи", out double sectorStepPile, 1))
                return;
            if (!ValidateNumber(sectorStepZTextBox.Text, "Шаг по высоте", out double sectorStepZ, 0))
                return;
            if (!ValidateInteger(predelGroupTextBox.Text, "Лимит группы", out int predelGroup, 0))
                return;
            if (!ValidateNumber(markStartTextBox.Text, "Старт марки", out double markStart, 0))
                return;
            if (!ValidateNumber(minDistanceTextBox.Text, "Минимальная дистанция", out double minDistance, 0))
                return;
            if (!ValidateNumber(coordinateRoundingTextBox.Text, "Шаг округления координат", out double roundingStep, 0))
                return;
            if (!IsValidSortCode(sortCodeTextBox.Text, "свай", new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }))
                return;
            if (!IsValidSortCode(sortCodeUGOTextBox.Text, "УГО", new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }))
                return;

            GroupPiles = GroupPilesCheckBox.IsChecked ?? false;
            SectorStep = sectorStep;
            SectorStepPile = sectorStepPile;
            SectorStepZ = sectorStepZ;
            PredelGroup = predelGroup;
            SortCode = sortCodeTextBox.Text;
            SortCodeUGO = sortCodeUGOTextBox.Text;

            AdjustPilePositions = adjustPositionsCheckBox.IsChecked ?? false;
            RecreateAllPiles = recreateAllPilesCheckBox.IsChecked ?? false;
            RotorPiles = rotatePilesCheckBox.IsChecked ?? false;
            ReloadUGO = reloadUGOCheckBox.IsChecked ?? false;
            MinDistanceBetweenPiles = minDistance;
            MarkStart = (int)markStart;
            CoordinateRoundingStep = roundingStep;

            UstanNumPile = ustanNumPileCheckBox.IsChecked ?? false;
            BoolNumPileIandex = boolNumPileIandex.IsChecked ?? false;
            UstanUGO = ustanUGOCheckBox.IsChecked ?? false;
            SetNumComment = setNumCommentCheckBox.IsChecked ?? false;
           

            ContinueExecution = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ContinueExecution = false;
            DialogResult = false;
            Close();
        }

        // Вспомогательные методы валидации – идентичны исходным
        private bool ValidateNumber(string text, string fieldName, out double value, double minValue = 0)
        {
            if (!double.TryParse(text, out value) || value <= minValue)
            {
                MessageBox.Show(
                    minValue == 0
                        ? $"{fieldName} должен быть положительным числом!"
                        : $"{fieldName} должен быть числом больше {minValue}!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private bool ValidateInteger(string text, string fieldName, out int value, int minValue = 0)
        {
            if (!int.TryParse(text, out value) || value < minValue)
            {
                MessageBox.Show(
                    minValue == 0
                        ? $"{fieldName} должен быть неотрицательным целым числом!"
                        : $"{fieldName} должен быть целым числом не менее {minValue}!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private bool IsValidSortCode(string code, string codeType, char[] allowedDigits)
        {
            if (string.IsNullOrEmpty(code))
                return true;
            foreach (char c in code)
            {
                if (!allowedDigits.Contains(c))
                {
                    MessageBox.Show(
                        $"Код сортировки {codeType} содержит недопустимый символ '{c}'.\nДопустимы только цифры: {string.Join(", ", allowedDigits)}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            if (code.Distinct().Count() != code.Length)
            {
                MessageBox.Show(
                    $"Код сортировки {codeType} содержит повторяющиеся цифры.\nКаждая цифра должна быть уникальной.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }
    }
}