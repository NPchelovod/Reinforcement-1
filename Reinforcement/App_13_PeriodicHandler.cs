using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement;
using System.Threading;

public class PeriodicHandler : IExternalEventHandler
{
    public static bool ChangeModelFromDoc = false;
    public static bool ChangeModelFromInvoke = false;
    public static readonly string TransactionName = RevitAPI.GetTranzactionName("Отложенное действие");
    public void Execute(UIApplication app)
    {
        if (!ChangeModelFromDoc) { return; }

        UIDocument uidoc = app.ActiveUIDocument;
        if (uidoc == null) return;
        Document doc = uidoc.Document;
        if (doc == null || doc.IsReadOnly) return;

        // Здесь ваши отложенные действия.
        // Можно открыть транзакцию — это законно в контексте ExternalEvent.Execute.
        using (Transaction t = new Transaction(doc, TransactionName))
        {
            t.Start();
            try
            {
                // Например: запись автора, проверка модели, обновление параметров...
                t.Commit();
            }
            catch
            {
                t.RollBack();
                throw;
            }
        }
    }

    public string GetName() => "PeriodicHandler";
}