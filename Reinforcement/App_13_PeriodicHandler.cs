using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement;
using System;

public class PeriodicHandler : IExternalEventHandler
{
    public static bool IsPeriodicControl = false;

    public static volatile bool IsProcessingOwnChange = false;
    public static volatile bool WorkPending = false;
    public static readonly string TransactionName =
        RevitAPI.GetTranzactionName("Отложенное действие");

    public void Execute(UIApplication app)
    {
        if (!IsPeriodicControl) return;
        if (!WorkPending) return;

        UIDocument uidoc = app.ActiveUIDocument;
        if (uidoc == null) return;
        Document doc = uidoc.Document;
        if (doc == null || doc.IsReadOnly) return;

        // Защита от повторного входа: если другая транзакция уже открыта,
        // откладываем до следующего тика.
        if (doc.IsModifiable) return;

        // Сбрасываем ДО работы, чтобы новые изменения снова взвели флаг
        WorkPending = false;

        var pending = App.TakePendingSnapshots();
        if (pending == null || pending.Count == 0) return;
        IsProcessingOwnChange = true;   // ⬅️ IUpdater будет игнорировать наши изменения
        try
        {
            using (var t = new Transaction(doc, TransactionName))
            {
                t.Start();
                try
                {
                    foreach (var snap in pending) ProcessSnapshot(snap, doc);
                    t.Commit();
                }
                catch
                {
                    if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                    throw;
                }
            }
        }
        finally
        {
            IsProcessingOwnChange = false;
        }
    }
    public string GetName() => "PeriodicHandler";
    private void ProcessSnapshot(SnapshotModificationElements snapshot, Document doc)
    {
        // ⚠️ Вся ваша «разветвлённая логика» — здесь.
        // Транзакция названа TransactionName, поэтому DocChanged её отфильтрует
        // по имени и рекурсии не будет.
        foreach (var el in snapshot.GeomModifiedElements)
        {
            // например:
            // var p = el.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            // if (p != null && !p.IsReadOnly) p.Set("geom-changed");
        }
    }

    
}