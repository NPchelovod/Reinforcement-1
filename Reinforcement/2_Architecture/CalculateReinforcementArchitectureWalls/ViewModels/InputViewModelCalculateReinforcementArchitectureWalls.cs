//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
//using Reinforcement;
//using Reinforcement.CopySelectedSchedules;
//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Input;

//namespace Reinforcement
//{
//    public class InputViewModelCalculateReinforcementArchitectureWalls : INotifyPropertyChanged
//    {
//        private string _someValue;

//        public string SomeValue
//        {
//            get => _someValue;
//            set
//            {
//                _someValue = value;
//                OnPropertyChanged(nameof(SomeValue));
//            }
//        }

//        public ICommand OkCommand { get; }
//        public ICommand CancelCommand { get; }

//        public event Action<bool?> CloseRequested;

//        public InputViewModelCalculateReinforcementArchitectureWalls()
//        {
//            OkCommand = new RelayCommand(_ => CloseRequested?.Invoke(true));
//            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
//        }

//        public object InputData => new { SomeValue };

//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//    }
//}