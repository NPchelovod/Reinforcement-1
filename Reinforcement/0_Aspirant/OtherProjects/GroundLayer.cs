using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Reinforcement.Aspirant
{
    /// <summary>Один грунтовый слой: толщина (мм) и наименование.</summary>
    public class GroundLayer : INotifyPropertyChanged
    {
        private string _name = "";
        private double _thicknessM;

        /// <summary>Наименование грунта (→ ЕС_Имя).</summary>
        public string Name
        {
            get => _name;
            set { _name = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Толщина слоя в метрах.</summary>
        public double ThicknessM
        {
            get => _thicknessM;
            set { _thicknessM = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}