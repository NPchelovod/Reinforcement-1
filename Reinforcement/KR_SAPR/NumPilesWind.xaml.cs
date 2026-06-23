using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static System.Net.Mime.MediaTypeNames;

namespace Reinforcement
{
    public partial class PileSettingsWindow2 : Window
    {
        // Свойства (без изменений)
        public static double SectorStep { get; set; } = 1051;//"Шаг группировки свай в КУСТ (мм):"
        public static double SectorStepPile { get; set; } = 510;//"Шаг округления рядов свай (мм):"
        public static double SectorStepZ { get; set; } = 10;
        public static int PredelGroup { get; set; } = 150;
        public static bool UstanNumPile { get; set; } = true;
        public static bool BoolNumPileIandex { get; set; } = true;
        public bool UstanUGO { get; set; }
        public bool GroupPiles { get; set; } = true;
        public bool SetNumComment { get; set; }
        
        public static string SortCode { get; set; } ="1403859";
        public static string SortCodeUGO { get; set; } = "123";
        
        
        public bool AdjustPilePositions { get; set; }//"Корректировать положения свай от соседей 3*d:"
        public bool AdjustPositionsRound { get; set; }//"Корректировать положения свай кратно шагу округл координат:"
        public bool AdjustPositionsRoundZ { get; set; }
        public static double MinDistanceBetweenPiles { get; set; } = 900;
        public static double CoordinateRoundingStep { get; set; } = 25;
        public bool RecreateAllPiles { get; set; }
        public bool RotorPiles { get; set; } = false;
        public bool ReloadUGO { get; set; }
        public int MarkStart { get; set; } = 1;
        public string MarkPrefix = "";
        public string MarkPostfix = "";

        public bool ContinueExecution { get; set; }
        public ExternalCommandData CommandData { get; set; }
        public PileSettingsWindow2(ExternalCommandData commandData)
        {
            InitializeComponent();

            CommandData = commandData;
            SeachPiles();

            WriterData();


        }
        private void WriterData()
        {
            nameFamilies.Text = String.Join(",", FamilyPiles);

            // Заполняем поля текущими значениями
            adjustPositionsCheckBox.IsChecked = AdjustPilePositions;
            adjustPositionsRoundCheckBox.IsChecked = AdjustPositionsRound;

            recreateAllPilesCheckBox.IsChecked = RecreateAllPiles;
            rotatePilesCheckBox.IsChecked = RotorPiles;
            minDistanceTextBox.Text = MinDistanceBetweenPiles.ToString();
            coordinateRoundingTextBox.Text = CoordinateRoundingStep.ToString();

            ustanNumPileCheckBox.IsChecked = UstanNumPile;
            markStartTextBox.Text = MarkStart.ToString();
            markPrefixTextBox.Text = MarkPrefix;
            markPostfixTextBox.Text = MarkPostfix;

            boolNumPileIandex.IsChecked = BoolNumPileIandex;
            GroupPilesCheckBox.IsChecked = GroupPiles;
            setNumCommentCheckBox.IsChecked = SetNumComment;


            sectorStepTextBox.Text = SectorStep.ToString();
            sectorStepPileTextBox.Text = SectorStepPile.ToString();
            sectorStepZTextBox.Text = SectorStepZ.ToString();
            predelGroupTextBox.Text = PredelGroup.ToString();


            ustanUGOCheckBox.IsChecked = UstanUGO;
            sortCodeTextBox.Text = SortCode.ToString();
            
            sortCodeUGOTextBox.Text = SortCodeUGO.ToString();
            reloadUGOCheckBox.IsChecked = ReloadUGO;
        }

        public static HashSet<Element> Seacher = new HashSet<Element>();
        public int FoundPilesCount => Seacher.Count;
        private void SeachPiles()
        {
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            
            // 1. Находим сваи
            Seacher = HelperSeachAllElements.SeachSelectElements(CommandData);
            if (FoundPilesCount < 3)//значит мы специально не выделяли
            {
                Seacher = HelperSeachAllElements.SeachAllElements(FamilyPiles , CommandData, true);
            }
            pilesCountText.Text = $"Найдено свай/объектов (на виде или были выделены более 3 шт): {FoundPilesCount} шт.";
        }
        private  HashSet<string> FamilyPiles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };
        private void SeachButton_Click(object sender, RoutedEventArgs e)
        {
            FamilyPiles = new HashSet<string>(nameFamilies.Text.Split(','));
            SeachPiles();
            ReadData();
        }
        private void f_Click(object sender, RoutedEventArgs e)
        {

        }
        private void ReadData()
        {
            AdjustPilePositions = adjustPositionsCheckBox.IsChecked ?? false;
            AdjustPositionsRound = adjustPositionsRoundCheckBox.IsChecked ?? false;
            AdjustPositionsRoundZ = adjustPositionsRoundZCheckBox.IsChecked ?? false;
            RecreateAllPiles = recreateAllPilesCheckBox.IsChecked ?? false;

            RotorPiles = rotatePilesCheckBox.IsChecked ?? false;
            if(!ValidateNumber(minDistanceTextBox.Text, out double value, 0))
            {
                MinDistanceBetweenPiles = value;
            }
            if (!ValidateNumber(coordinateRoundingTextBox.Text, out value, 0))
            {
                CoordinateRoundingStep = value;
            }

            UstanNumPile = ustanNumPileCheckBox.IsChecked ?? false;

            if (!ValidateNumber(markStartTextBox.Text, out value, 0))
            {
                MarkStart = (int) value;
            }

            MarkPrefix = markPrefixTextBox.Text;
            MarkPostfix = markPostfixTextBox.Text;

            BoolNumPileIandex = boolNumPileIandex.IsChecked ?? false;
            GroupPiles = GroupPilesCheckBox.IsChecked ?? false;
            SetNumComment = setNumCommentCheckBox.IsChecked ?? false;



            if (!ValidateNumber(sectorStepTextBox.Text, out value, 0))
            {
                SectorStep = value;
            }
            if (!ValidateNumber(sectorStepPileTextBox.Text, out value, 0))
            {
                SectorStepPile = value;
            }
            if (!ValidateNumber(sectorStepZTextBox.Text, out value, 0))
            {
                SectorStepZ = value;
            }
            if (!ValidateNumber(predelGroupTextBox.Text, out value, 0))
            {
                PredelGroup = (int)value;
            }
            SortCode = sortCodeTextBox.Text;    
            UstanUGO = ustanUGOCheckBox.IsChecked ?? false;
            SortCodeUGO = sortCodeUGOTextBox.Text;
            ReloadUGO = reloadUGOCheckBox.IsChecked ?? false;
            CorrectData();
            WriterData();
        }





        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ReadData();

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
        public void CorrectData()
        {
     
            if (SectorStepPile < 1)
            {
                SectorStepPile = 1;
            }
            if (SectorStep < 1)
            {
                SectorStep = 1;
            }
            if (SectorStepZ < 1)
            {
                SectorStepZ = 1;
            }
            if (PredelGroup < 1)
            {
                PredelGroup = 1;
            }
            if(CoordinateRoundingStep<1)
            {
                CoordinateRoundingStep = 1;
            }
        }
        // Вспомогательные методы валидации – идентичны исходным
        private bool ValidateNumber(string text, out double value, double minValue = 0)
        {
            if (!double.TryParse(text, out value) || value <= minValue)
            {
                //MessageBox.Show(
                //    minValue == 0
                //        ? $"{fieldName} должен быть положительным числом!"
                //        : $"{fieldName} должен быть числом больше {minValue}!",
                //    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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