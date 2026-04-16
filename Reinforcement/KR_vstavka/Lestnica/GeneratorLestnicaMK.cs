using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class GeneratorLestnicaMK : IExternalCommand
    {
        public Result Execute(
           ExternalCommandData commandData,
           ref string message,
           ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;

            Document doc = uidoc.Document;
            // Условие: находимся на виде (активный вид существует)
            View activeView = doc.ActiveView;
            if (activeView == null)
            {
                TaskDialog.Show("Ошибка", "Активный вид не найден. Откройте вид (план, фасад, 3D) и повторите попытку.");
                return Result.Failed;
            }

            // Создаём ViewModel
            var viewModel = new StairParametersViewModel();
            // Создаём и показываем окно
            var window = new StairParametersWindow(viewModel);

            //try
            {
                // Для модальности относительно Revit
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                bool? result = window.ShowDialog();
                if (result == true)
                {
                    // Создание уже выполняется в команде ViewModel, но можно и здесь обработать
                    return Result.Succeeded;
                }
                return Result.Succeeded;
            }
            //catch (Exception ex)
            {
                return Result.Failed;
            }
        }
    }
}
