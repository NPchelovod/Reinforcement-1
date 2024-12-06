using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Reinforcement.CopySelectedSchedules
{
    public class ViewModelCopySelectedSchedules : INotifyPropertyChanged
    {
        public ViewModelCopySelectedSchedules() 
        {
            CopySchedulesCommand = new RelayCommand(CopySchedules, CanCopySchedules);
        }

        private string constrMark;
        private string viewDestination;

        public string ConstrMark
        {
            get { return constrMark; }
            set
            {
                constrMark = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ViewDestination
        {
            get { return viewDestination; }
            set
            {
                viewDestination = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public RelayCommand CopySchedulesCommand { get; set; }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }

        private bool CanCopySchedules(object param)
        {
            return !string.IsNullOrWhiteSpace(ConstrMark) && !string.IsNullOrWhiteSpace(ViewDestination);
        }

        public void CopySchedules()
        {
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            Selection sel = uidoc.Selection;
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Копирование спек"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    var viewId = sel.GetElementIds();
                    List<View> viewList = viewId
                        .Select(x => doc.GetElement(x))
                        .Cast<View>()
                        .ToList();
                    foreach (var view in viewList)
                    {
                        view.Duplicate(ViewDuplicateOption.Duplicate);
                        RaiseCloseRequest();
                    }

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
            }
        }
    }
}
