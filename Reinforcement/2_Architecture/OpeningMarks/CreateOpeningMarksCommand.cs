using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CreateOpeningMarksCommand : RevitCommandBase
    {
        private readonly IOpeningMarkService service = new OpeningMarkService();
        protected override Result ExecuteCore(UIDocument document)
        {
            var selected = document.Selection.GetElementIds();
            if (selected.Count == 0)
            { TaskDialog.Show("Марки проёмов", "Выберите проёмы на копии модели, затем повторите команду."); return Result.Cancelled; }
            var report = service.Create(document.Document, document.ActiveView, selected);
            var dialog = new TaskDialog("Марки проёмов")
            {
                MainInstruction = report.FormatSummary(),
                MainContent = "Использованы существующие значения параметра «Марка». Геометрия проёмов не изменялась.",
                ExpandedContent = report.FormatDetails(), CommonButtons = TaskDialogCommonButtons.Ok
            };
            dialog.Show(); return Result.Succeeded;
        }
    }
}
