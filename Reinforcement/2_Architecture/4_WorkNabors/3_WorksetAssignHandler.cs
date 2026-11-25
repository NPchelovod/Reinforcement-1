using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace Reinforcement.Worksets
{
    public class WorksetAssignHandler : IExternalEventHandler
    {
        public List<WorksetRule> Rules { get; set; }
        public bool OnlyVisibleInActiveView { get; set; }
        public bool OnlySelected { get; set; }
        public bool CreateMissingWorksets { get; set; }

        public string LastMessage { get; private set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                LastMessage = "Нет активного документа.";
                TaskDialog.Show("Рабочие наборы", LastMessage);
                return;
            }

            WorksetAssignService.Assign(
                doc,
                Rules ?? new List<WorksetRule>(),
                OnlyVisibleInActiveView,
                OnlySelected,
                CreateMissingWorksets,
                out int changed,
                out int skipped,
                out List<string> log);

            LastMessage = string.Join("\n", log);
            TaskDialog.Show("Рабочие наборы", LastMessage);
        }

        public string GetName() => "WorksetAssignHandler";
    }
}