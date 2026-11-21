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
            LoadFromConstants();          // до InitializeComponent — иначе UI прочитает нули
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
    
        // ==================== ВВОД (всё в мм) ====================
        public double NumberOfRowsInBricks { get; set; }
        public double StepOfTopJointsInBricks { get; set; }          // мм
        public double NumberOfSideJointsInBricks { get; set; }
        public double RowsOfReductionReinforcementForBricksWindow { get; set; }
        public double RowsOfReductionReinforcementForBricksDoor { get; set; }
        public double RowsOfReductionReinforcementForBricksCurtains { get; set; }

        public double NumberOfRowsInGasConcrete { get; set; }
        public double StepOfTopJointsInGasConcrete { get; set; }     // мм
        public double NumberOfSideJointsInGasConcrete { get; set; }
        public double RowsOfReductionReinforcementForGasConcreteWindow { get; set; }
        public double RowsOfReductionReinforcementForGasConcreteDoor { get; set; }
        public double RowsOfReductionReinforcementForGasConcreteCurtains { get; set; }

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
        public void LoadFromConstants()
        {
            // кирпич
            NumberOfRowsInBricks = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.ArmRow);
            StepOfTopJointsInBricks = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.StepDetailsMM);
            NumberOfSideJointsInBricks = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.DetailsWallRow);
            RowsOfReductionReinforcementForBricksWindow = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.ArmRowsCutByWindow);
            RowsOfReductionReinforcementForBricksDoor = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.ArmRowsCutByDoor);
            RowsOfReductionReinforcementForBricksCurtains = ArmElementConstants.GetConstant(EWallTypes.AR_Brick, EArmWall.ArmRowsCutByCurtain);

            // газобетон
            NumberOfRowsInGasConcrete = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.ArmRow);
            StepOfTopJointsInGasConcrete = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.StepDetailsMM);
            NumberOfSideJointsInGasConcrete = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.DetailsWallRow);
            RowsOfReductionReinforcementForGasConcreteWindow = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.ArmRowsCutByWindow);
            RowsOfReductionReinforcementForGasConcreteDoor = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.ArmRowsCutByDoor);
            RowsOfReductionReinforcementForGasConcreteCurtains = ArmElementConstants.GetConstant(EWallTypes.AR_Aerocrete, EArmWall.ArmRowsCutByCurtain);

            // настройки заполнения
            ReadParametersFromWalls = ArmElementConstants.GetParamFromECPropElement;
            SkipFilledParameters = ArmElementConstants.GetParamOnlyNonZero;
            FillParametersInProject = ArmElementConstants.FillParametersInProject;
        }

        // ==================== ЗАПИСЬ ПОЛЯ ОКНА → В КОНСТАНТЫ ====================
        public void SaveToConstants()
        {
            SaveConstantsFor(EWallTypes.AR_Brick,
                NumberOfRowsInBricks,
                StepOfTopJointsInBricks,             // уже мм — конверсия не нужна
                NumberOfSideJointsInBricks,
                RowsOfReductionReinforcementForBricksWindow,
                RowsOfReductionReinforcementForBricksDoor,
                RowsOfReductionReinforcementForBricksCurtains);

            SaveConstantsFor(EWallTypes.AR_Aerocrete,
                NumberOfRowsInGasConcrete,
                StepOfTopJointsInGasConcrete,
                NumberOfSideJointsInGasConcrete,
                RowsOfReductionReinforcementForGasConcreteWindow,
                RowsOfReductionReinforcementForGasConcreteDoor,
                RowsOfReductionReinforcementForGasConcreteCurtains);

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
        private static void SaveConstantsFor(EWallTypes type,
            double rows, double stepMm, double side,
            double win, double door, double curt)
        {
            ArmElementConstants.SetConstantsParameter(type, EArmWall.ArmRow, rows);
            ArmElementConstants.SetConstantsParameter(type, EArmWall.StepDetailsMM, stepMm); // уже мм
            ArmElementConstants.SetConstantsParameter(type, EArmWall.DetailsWallRow, side);
            ArmElementConstants.SetConstantsParameter(type, EArmWall.ArmRowsCutByWindow, win);
            ArmElementConstants.SetConstantsParameter(type, EArmWall.ArmRowsCutByDoor, door);
            ArmElementConstants.SetConstantsParameter(type, EArmWall.ArmRowsCutByCurtain, curt);
        }

        // ==================== Пример использования в Calculate ====================
        public void Calculate()
        {
            SaveToConstants();  // введённые данные → константы (всё в мм)

            var walls = ArmWalls.ReadAndGet(Doc);

            var answerBrick = new RezaltCalc(walls.Where(x => x.EWallTypes == EWallTypes.AR_Brick).ToList());
            var answerAer = new RezaltCalc(walls.Where(x => x.EWallTypes == EWallTypes.AR_Aerocrete).ToList());

            // ---------- Кирпич ----------
            ReinforcementInBricks = answerBrick[EArmWall.ArmAllM];
            ReinforcementInBricksM2 = answerBrick[EArmWall.ArmAllM2];
            LengthOfVilatermInBricks = answerBrick[EArmWall.WallLengthMM] / 1000.0 * 2; // пример: периметр в м; уточните формулу
            NumberOfJointsInBricks = (int)answerBrick[EArmWall.DetailsRoofInt];
            TotalNumberOfSideJointsInBricks = (int)answerBrick[EArmWall.DetailsWallsInt];

            // ---------- Газобетон ----------
            ReinforcementInGasConcrete = answerAer[EArmWall.ArmAllM];
            ReinforcementInGasConcreteM2 = answerAer[EArmWall.ArmAllM2];
            LengthOfVilatermInGasConcrete = answerAer[EArmWall.WallLengthMM] / 1000.0 * 2;
            NumberOfJointsInGasConcrete = (int)answerAer[EArmWall.DetailsRoofInt];
            TotalNumberOfSideJointsInGasConcrete = (int)answerAer[EArmWall.DetailsWallsInt];
        }


        private class RezaltCalc
        {
            public Dictionary<EArmWall, double> AnswerDict=new Dictionary<EArmWall, double>();
            public RezaltCalc(List<ECWall> eCWalls) 
            {
                foreach (var sc in ArmExport.SummedColumns)
                {
                    AnswerDict[sc] = 0;
                }

                //тут выполняем расчет и заполняем ответ
                foreach (ECWall wall in eCWalls)
                {
                    ECArmWallElement eCArmWallElement = wall.ECArmWallElement;
                    if (eCArmWallElement == null) {continue;}
                    foreach (var sc in ArmExport.SummedColumns)
                    {
                        AnswerDict[sc] += eCArmWallElement[sc];
                    }
                }
            }
            public double this[EArmWall eArmWall]
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