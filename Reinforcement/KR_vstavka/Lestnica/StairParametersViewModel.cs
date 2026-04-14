using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Reinforcement.CopySelectedSchedules;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public enum LestnicaData
    { 
        Kolonn, // колонна для лестницы
        BeamZ,//балка укрепляющая колонны
        BeamSupport,//балка под косоурами к колонне приварена
        BeamCosour,//балка косоура
        Plate,//пластины
        Ygolok,
        Peril,
        Ograd,
    }

    public class StairParametersViewModel : INotifyPropertyChanged
    {
        //private string _guideColumnFamily = "Балка_25Ш1";
        //private string _stringerBeamFamily = "Косоур";
        //private string _supportBeamFamily = "Поддерживающая балка";
        //private string _plateFamily = "Пластина";
        private double _plateThickness = 10.0;
        private double _stairWidth = 1200.0;
        private double _stairHeight = 1500.0;
        private double _stairLength = 2700;
        private double _stairNum = 2;
        private double _stupenNum = 10;

        public Dictionary<LestnicaData, (string name, Element element)> NamesFamilies = new Dictionary<LestnicaData, (string name, Element element)>()
        {
            {
                LestnicaData.Kolonn, ("Балка_25Ш1", null)
            },
            {
                LestnicaData.BeamZ, ("Балка_100х100х5мм", null)
            },
            {
                LestnicaData.BeamSupport, ("Балка_22П", null)
            },
            {
                LestnicaData.BeamCosour, ("Балка_16П", null)
            },
            {
                LestnicaData.Plate, ("Пластина", null)
            },
            {
                LestnicaData.Ygolok, ("Балка_50x5мм", null)
            },
            {
                LestnicaData.Peril, ("Балка_50х50х3мм", null)
            },
            {
                LestnicaData.Ograd, ("Балка_25Ш1", null)
            }

        };

        public string GuideColumnFamily
        {
            get => NamesFamilies[LestnicaData.Kolonn].name;
            set { NamesFamilies[LestnicaData.Kolonn] = (value,null); OnPropertyChanged(); }
        }

        public string StringerBeamFamily
        {
            get => NamesFamilies[LestnicaData.BeamCosour].name;
            set { NamesFamilies[LestnicaData.BeamCosour] = (value, null); OnPropertyChanged(); }
        }

        public string SupportBeamFamily
        {
            get => NamesFamilies[LestnicaData.BeamSupport].name;
            set { NamesFamilies[LestnicaData.BeamSupport] = (value, null); OnPropertyChanged(); }
        }

        public string PlateFamily
        {
            get => NamesFamilies[LestnicaData.Plate].name;
            set { NamesFamilies[LestnicaData.Plate] = (value, null); OnPropertyChanged(); }
        }

        public double PlateThickness
        {
            get => _plateThickness;
            set { _plateThickness = value; OnPropertyChanged(); }
        }

        public double StairWidth
        {
            get => _stairWidth;
            set { _stairWidth = value; OnPropertyChanged(); }
        }

        public double StairHeight
        {
            get => _stairHeight;
            set { _stairHeight = value; OnPropertyChanged(); }
        }

        public double StairLength
        {
            get => _stairLength;
            set { _stairLength = value; OnPropertyChanged(); }
        }
        public double StairNum
        {
            get => _stairNum;
            set { _stairNum = value; OnPropertyChanged(); }
        }


        public double StupenNum
        {
            get => _stupenNum;
            set { _stupenNum = value; OnPropertyChanged(); }
        }





        public ICommand CreateCommand { get; }

        public StairParametersViewModel()
        {
            CreateCommand = new RelayCommand(OnCreate);
        }

        private void OnCreate()
        {
            // Здесь будет вызов Revit API для создания конструкций
            // Например, вызвать статический метод, передав параметры
            StairCreator.CreateStairComponents(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    // Простая реализация ICommand
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
