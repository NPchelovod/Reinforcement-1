using Autodesk.Revit.DB;
using Reinforcement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
namespace Reinforcement
{
    public partial class CalculateArmWalls : Window, INotifyPropertyChanged
    {
        private readonly Document Doc;

        public CalculateArmWalls(Document doc)
        {
            Doc = doc;
            LoadFromConstants();      // 1) сначала заполняем поля
            InitializeComponent();
            DataContext = this;

            Loaded += Window_Loaded;


        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var wa = SystemParameters.WorkArea;

            // не вылезаем за рабочую область экрана
            MaxHeight = wa.Height - 40;
            MaxWidth = wa.Width - 40;

            // фиксируем текущий (уже просчитанный) размер и отпускаем SizeToContent,
            // иначе ресайз мышью будет «отпружинивать» обратно к размеру контента
            Width = ActualWidth;
            Height = ActualHeight;
            SizeToContent = SizeToContent.Manual;
        }
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //LoadFromConstants();   // начальные значения из ArmElementConstants
        }
        // === кнопка "Выполнить" ===
        private void OnRunClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Calculate() только читает Revit и пишет в статические словари.
                // Если FillParametersInProject == true и внутри идёт запись в стены —
                // нужна транзакция.
               Calculate();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка расчёта",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === кнопка "Закрыть" ===
        private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

        // === перетаскивание окна за Border ===
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();


        /// <summary>
        /// Читает настройки из ArmElementConstants и раскладывает их по свойствам окна.
        /// ВАЖНО: константы хранятся в МЕТРАХ (кроме счётчиков-штук), а поля WPF — в МИЛЛИМЕТРАХ,
        ///        поэтому длины конвертируются ×1000, а счётчики (шт.) — без конверсии.
        /// Вызывать до DataContext = this (или до первого чтения свойств UI).
        /// </summary>
        public void LoadFromConstants()
        {
            // ============================================================
            // КИРПИЧ
            // ============================================================
            // Шаг армирования на этаж, мм  ← Wall_Arm_Row_Step_m (м)
            StepArmOfRowsInBricks =
                ArmElementConstants.GetConstant(EWallTypes.AR_Brick,
                    EValuesExportData.Wall_Arm_Row_Step_m) * 1000.0;

            // Шаг деталей (крепление к перекрытию), мм ← Wall_Detailed_Roof_Step_m (м)
            StepOfTopJointsInBricks =
                ArmElementConstants.GetConstant(EWallTypes.AR_Brick,
                    EValuesExportData.Wall_Detailed_Roof_Step_m) * 1000.0;

            // Шаг деталей (крепление по бокам), мм ← Wall_Detailed_Row_Step_m (м)
            StepOfHorJointsInBricks =
                ArmElementConstants.GetConstant(EWallTypes.AR_Brick,
                    EValuesExportData.Wall_Detailed_Row_Step_m) * 1000.0;

            // Кол-во доп. рядов арматуры у потолка, шт ← Wall_Arm_DopTop_Count
            // Конверсия НЕ нужна: это счётчик, а не длина.
            CountDopArmInBricks =
                ArmElementConstants.GetConstant(EWallTypes.AR_Brick,
                    EValuesExportData.Wall_Arm_DopTop_Count);

            // ============================================================
            // ГАЗОБЕТОН
            // ============================================================
            StepArmOfRowsInConcrete =
                ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete,
                    EValuesExportData.Wall_Arm_Row_Step_m) * 1000.0;

            StepOfTopJointsInGasConcrete =
                ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete,
                    EValuesExportData.Wall_Detailed_Roof_Step_m) * 1000.0;

            StepOfHorJointsInGasConcrete =
                ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete,
                    EValuesExportData.Wall_Detailed_Row_Step_m) * 1000.0;

            // ============================================================
            // НАСТРОЙКИ ЗАПОЛНЕНИЯ ПАРАМЕТРОВ
            // ============================================================
            ReadParametersFromWalls = ArmElementConstants.GetParamFromECPropElement;
            SkipFilledParameters = ArmElementConstants.GetParamOnlyNonZero;
            FillParametersInProject = ArmElementConstants.FillParametersInProject;

            // ============================================================
            // СТРАХОВКА: если какая-то константа не нашлась (вернулось 0),
            // подставляем разумный дефолт, чтобы поле ввода не выглядело пустым.
            // Это чисто косметика — реальные значения всё равно приходят из ArmElementConstants.
            // ============================================================
            if (StepArmOfRowsInBricks <= 0) StepArmOfRowsInBricks = 600.0;    // 0.600 м
            if (StepOfTopJointsInBricks <= 0) StepOfTopJointsInBricks = 1500.0;   // 1.500 м
            if (StepOfHorJointsInBricks <= 0) StepOfHorJointsInBricks = 600.0;    // 0.600 м
            if (CountDopArmInBricks <= 0) CountDopArmInBricks = 3.0;      // шт

            if (StepArmOfRowsInConcrete <= 0) StepArmOfRowsInConcrete = 750.0;  // 0.750 м
            if (StepOfTopJointsInGasConcrete <= 0) StepOfTopJointsInGasConcrete = 3000.0; // 3.000 м
            if (StepOfHorJointsInGasConcrete <= 0) StepOfHorJointsInGasConcrete = 1250.0; // 1.250 м
        }
        // ==================== ВВОД ====================

        // --- Кирпич (все длины в мм, счётчики в шт.) ---
        public double StepArmOfRowsInBricks { get; set; }   // → Wall_Arm_Row_Step_m
        public double StepOfTopJointsInBricks { get; set; }   // → Wall_Detailed_Roof_Step_m
        public double StepOfHorJointsInBricks { get; set; }   // → Wall_Detailed_Row_Step_m
        public double CountDopArmInBricks { get; set; }   // → Wall_Arm_DopTop_Count

        // --- Газобетон ---
        public double StepArmOfRowsInConcrete { get; set; }   // → Wall_Arm_Row_Step_m
        public double StepOfTopJointsInGasConcrete { get; set; }   // → Wall_Detailed_Roof_Step_m
        public double StepOfHorJointsInGasConcrete { get; set; }   // → Wall_Detailed_Row_Step_m

        // --- Настройки заполнения ---
        public bool ReadParametersFromWalls { get; set; }
        public bool FillParametersInProject { get; set; }
        public bool SkipFilledParameters { get; set; }


        // ==================== РЕЗУЛЬТАТЫ (м и шт.) ====================
        private double _reinforcementInBricks;
        public double ReinforcementInBricks { get => _reinforcementInBricks; private set { _reinforcementInBricks = value; OnPropertyChanged(); } }

        private double _lengthOfVilatermInBricks;
        public double LengthOfVilatermInBricks { get => _lengthOfVilatermInBricks; private set { _lengthOfVilatermInBricks = value; OnPropertyChanged(); } }

        private int _numberOfJointsInBricks;
        public int NumberOfJointsInBricks { get => _numberOfJointsInBricks; private set { _numberOfJointsInBricks = value; OnPropertyChanged(); } }

        private int _totalNumberOfSideJointsInBricks;
        public int TotalNumberOfSideJointsInBricks { get => _totalNumberOfSideJointsInBricks; private set { _totalNumberOfSideJointsInBricks = value; OnPropertyChanged(); } }

        private double _reinforcementInGasConcrete;
        public double ReinforcementInGasConcrete { get => _reinforcementInGasConcrete; private set { _reinforcementInGasConcrete = value; OnPropertyChanged(); } }

        private double _lengthOfVilatermInGasConcrete;
        public double LengthOfVilatermInGasConcrete { get => _lengthOfVilatermInGasConcrete; private set { _lengthOfVilatermInGasConcrete = value; OnPropertyChanged(); } }

        private int _numberOfJointsInGasConcrete;
        public int NumberOfJointsInGasConcrete { get => _numberOfJointsInGasConcrete; private set { _numberOfJointsInGasConcrete = value; OnPropertyChanged(); } }

        private int _totalNumberOfSideJointsInGasConcrete;
        public int TotalNumberOfSideJointsInGasConcrete { get => _totalNumberOfSideJointsInGasConcrete; private set { _totalNumberOfSideJointsInGasConcrete = value; OnPropertyChanged(); } }

        private double _reinforcementInBricksM2;
        public double ReinforcementInBricksM2
        {
            get => _reinforcementInBricksM2;
            private set { _reinforcementInBricksM2 = value; OnPropertyChanged(); }
        }

        private double _reinforcementInGasConcreteM2;
        public double ReinforcementInGasConcreteM2
        {
            get => _reinforcementInGasConcreteM2;
            private set { _reinforcementInGasConcreteM2 = value; OnPropertyChanged(); }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ==================== ЧТЕНИЕ ИЗ КОНСТАНТ → В ПОЛЯ ОКНА ====================
        public void SaveToConstants()
        {
            // ==================== Кирпич ====================
            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Brick,
                EValuesExportData.Wall_Arm_Row_Step_m, StepArmOfRowsInBricks / 1000.0);

            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Brick,
                EValuesExportData.Wall_Detailed_Roof_Step_m, StepOfTopJointsInBricks / 1000.0);

            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Brick,
                EValuesExportData.Wall_Detailed_Row_Step_m, StepOfHorJointsInBricks / 1000.0);

            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Brick,
                EValuesExportData.Wall_Arm_DopTop_Count, CountDopArmInBricks);   // шт, без конверсии

            // ==================== Газобетон ====================
            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Aerocrete,
                EValuesExportData.Wall_Arm_Row_Step_m, StepArmOfRowsInConcrete / 1000.0);

            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Aerocrete,
                EValuesExportData.Wall_Detailed_Roof_Step_m, StepOfTopJointsInGasConcrete / 1000.0);

            ArmElementConstants.SetConstantsParameter(EWallTypes.AR_Aerocrete,
                EValuesExportData.Wall_Detailed_Row_Step_m, StepOfHorJointsInGasConcrete / 1000.0);

            // ==================== Настройки ====================
            ArmElementConstants.GetParamFromECPropElement = ReadParametersFromWalls;
            ArmElementConstants.GetParamOnlyNonZero = SkipFilledParameters;
            ArmElementConstants.FillParametersInProject = FillParametersInProject;
        }
       
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
     => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            DragMove();
        }


        // ==================== Пример использования в Calculate ====================
        public void Calculate()
        {
            SaveToConstants();

            var walls = ArmWalls.ReadAndGet(Doc);

            var answerBrick = new RezaltSumCalc(
                walls.Where(x => x.EWallTypes == EWallTypes.AR_Brick && x.ECArmWallElement != null)
                     .Select(x => (IExportStringDoubleData)x.ECArmWallElement).ToList());

            var answerAer = new RezaltSumCalc(
                walls.Where(x => x.EWallTypes == EWallTypes.AR_Aerocrete && x.ECArmWallElement != null)
                     .Select(x => (IExportStringDoubleData)x.ECArmWallElement).ToList());

            // ---------- Кирпич ----------
            ReinforcementInBricks = answerBrick[EValuesExportData.Wall_Arm_Itog_m];
            ReinforcementInBricksM2 = answerBrick[EValuesExportData.Wall_Arm_Itog_m2];

            LengthOfVilatermInBricks = answerBrick[EValuesExportData.Wall_Arm_Vilaterm_Itog_m] * 2;

            NumberOfJointsInBricks = (int)answerBrick[EValuesExportData.Wall_Datailed_Roof_Count];
            TotalNumberOfSideJointsInBricks = (int)answerBrick[EValuesExportData.Wall_Datailed_Hor_Count];

            // ---------- Газобетон ----------
            ReinforcementInGasConcrete = answerAer[EValuesExportData.Wall_Arm_Itog_m];
            ReinforcementInGasConcreteM2 = answerAer[EValuesExportData.Wall_Arm_Itog_m2];

            // Wall_Length_m уже в метрах — деление на 1000 не нужно
            LengthOfVilatermInGasConcrete = answerAer[EValuesExportData.Wall_Length_m] * 2;

            NumberOfJointsInGasConcrete = (int)answerAer[EValuesExportData.Wall_Datailed_Roof_Count];
            TotalNumberOfSideJointsInGasConcrete = (int)answerAer[EValuesExportData.Wall_Datailed_Hor_Count];
        }

        /// <summary>
        /// Позволяет просуммировать N колво данных
        /// </summary>
        private class RezaltSumCalc
        {
            public Dictionary<EValuesExportData, double> AnswerDict=new Dictionary<EValuesExportData, double>();
            public RezaltSumCalc(List<IExportStringDoubleData> eCArms) 
            {
                
                //тут выполняем расчет и заполняем ответ
                foreach (var arm in eCArms)
                {
                    var calcData = arm.GetCalcData();
                    if (calcData == null || calcData.Count==0) {continue;}
                   
                    foreach (var sc in calcData)
                    {
                        if (sc != null && sc.EValuesExportData != EValuesExportData.None && sc.ExportDoubleOrString)
                        {
                            AnswerDict.TryGetValue(sc.EValuesExportData, out double value);
                            AnswerDict[sc.EValuesExportData] = value+sc.ValueDouble;
                        }
                    }
                }
            }
            public double this[EValuesExportData eArmWall]
            {
                get
                {
                    AnswerDict.TryGetValue(eArmWall, out double result);
                    return result;
                }
            }



        }


    }
}