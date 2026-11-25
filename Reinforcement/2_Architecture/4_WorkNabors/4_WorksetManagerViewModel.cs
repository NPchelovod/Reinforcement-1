using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement.Worksets
{
    public class WorksetManagerViewModel : INotifyPropertyChanged
    {
        private readonly ExternalEvent _externalEvent;
        private readonly WorksetAssignHandler _handler;

        public ObservableCollection<WorksetRule> ArchRules { get; } = new ObservableCollection<WorksetRule>();
        public ObservableCollection<WorksetRule> LinkRules { get; } = new ObservableCollection<WorksetRule>();

        private bool _onlyVisible = true;
        public bool OnlyVisible
        {
            get => _onlyVisible;
            set { _onlyVisible = value; OnPropertyChanged(); }
        }

        private bool _onlySelected;
        public bool OnlySelected
        {
            get => _onlySelected;
            set { _onlySelected = value; OnPropertyChanged(); }
        }

        private bool _createMissing = true;
        public bool CreateMissing
        {
            get => _createMissing;
            set { _createMissing = value; OnPropertyChanged(); }
        }

        public ICommand AssignCommand { get; }

        public WorksetManagerViewModel(ExternalEvent externalEvent, WorksetAssignHandler handler)
        {
            _externalEvent = externalEvent;
            _handler = handler;
            AssignCommand = new RelayCommand(_ => RaiseAssign());

            BuildDefaultArchRules();
            BuildDefaultLinkRules();
        }

        private void RaiseAssign()
        {
            _handler.Rules = new List<WorksetRule>();
            foreach (var r in ArchRules) _handler.Rules.Add(r);
            foreach (var r in LinkRules) _handler.Rules.Add(r);

            _handler.OnlyVisibleInActiveView = OnlyVisible && !OnlySelected;
            _handler.OnlySelected = OnlySelected;
            _handler.CreateMissingWorksets = CreateMissing;

            _externalEvent.Raise();
        }

        // ------------------------------------------------------------
        // Правила АР
        // ------------------------------------------------------------
        private void BuildDefaultArchRules()
        {
            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Витражи",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_CurtainWallPanels,
                    BuiltInCategory.OST_CurtainWallMullions
                },
            });
            // Вентфасад — стены с таким типом тоже относятся к наружным
            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Наружные стены (Фасад)",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls },
                TypeNameContains = "Вентфасад",
                TypeNameExcludes = "Бетон",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Наружные стены (Фасад)",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls },
                TypeNameContains = "Наружные",
                TypeNameExcludes = "Бетон",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Монолит",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls },
                TypeNameContains = "Бетон",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Внутренние стены и перегородки",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Walls },
                TypeNameExcludes = "Наружные",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Двери",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Doors },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Окна",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Windows },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Кровля",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Roofs,
                    BuiltInCategory.OST_RoofSoffit,
                    BuiltInCategory.OST_EdgeSlab
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Лестницы",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Stairs,
                    BuiltInCategory.OST_StairsRailing,
                    BuiltInCategory.OST_StairsRuns,
                    BuiltInCategory.OST_StairsLandings,
                    BuiltInCategory.OST_StairsSupports
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Машиноместа",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Parking },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Мебель",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Furniture,
                    BuiltInCategory.OST_FurnitureSystems
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Оборудование",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_MechanicalEquipment,
                    BuiltInCategory.OST_ElectricalEquipment,
                    BuiltInCategory.OST_SpecialityEquipment
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Ограждение",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Railings,        // обычные ограждения
                    BuiltInCategory.OST_StairsRailing,   // лестничные ограждения
                    BuiltInCategory.OST_Ramps            // пандусы
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Отверстия",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_SWallRectOpening,
                    BuiltInCategory.OST_ShaftOpening
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_ПБ/ОДИ",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_FireAlarmDevices,
                    BuiltInCategory.OST_SecurityDevices,
                    BuiltInCategory.OST_LightingFixtures
                },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Перемычки",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_StructuralFraming },
                TypeNameContains = "Перемычка",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Полы",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Floors },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Помещения",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_RoomSeparationLines },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Потолки",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Ceilings },
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Сантехника непечатная",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_PlumbingFixtures },
                TypeNameContains = "непечатн",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Сантехприборы и электроприборы",
                Categories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_PlumbingFixtures,
                    BuiltInCategory.OST_ElectricalFixtures,
                    BuiltInCategory.OST_LightingFixtures
                },
                TypeNameExcludes = "непечатн",
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Формы",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_GenericModel },
            });

            // Импортированные DWG (не связи!) — по классу, а не по категории
            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_Чертежи автокад",
                MatchImportInstanceOnly = true,
            });

            ArchRules.Add(new WorksetRule
            {
                WorksetName = "АР_двери непечатные",
                Categories = new List<BuiltInCategory> { BuiltInCategory.OST_Doors },
                TypeNameContains = "непечатн",
            });
        }

        // ------------------------------------------------------------
        // Правила связей (!Связь_ВК, !Связь_КР, !Связь_ОВ, !Связь_ЭЛ)
        // ------------------------------------------------------------
        private void BuildDefaultLinkRules()
        {
            foreach (var code in new[] { "ВК", "КР", "ОВ", "ЭЛ" })
            {
                LinkRules.Add(new WorksetRule
                {
                    WorksetName = $"!Связь_{code}",
                    Categories = new List<BuiltInCategory> { BuiltInCategory.OST_RvtLinks },
                    LinkNameContains = code,
                });
            }
        }

        // ------------------------------------------------------------
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}