using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement.Worksets
{
    [Transaction(TransactionMode.Manual)]
    public class CmdWorksetManager : IExternalCommand
    {
        private static WorksetManagerWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var handler = new WorksetAssignHandler();
            var externalEvent = ExternalEvent.Create(handler);

            var vm = new WorksetManagerViewModel(externalEvent, handler);
            _window = new WorksetManagerWindow { DataContext = vm };
            _window.Show(); // modeless

            return Result.Succeeded;
        }
    }
}