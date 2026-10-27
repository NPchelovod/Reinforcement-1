using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace Reinforcement
{
    public enum ETypeChangeEl
    {
        None = 0,
        AddedElements,
        ModifiedElements,
        GeomModifiedElements,
        DeleteModifiedElements,
    }
    public class PeriodicHandler : IExternalEventHandler
    {
       
        public static readonly string TransactionName =
            RevitAPI.GetTranzactionName("Отложенное действие");

        public static volatile bool IsProcessingOwnChange = false; // что вошли в редацию
        

        public void Execute(UIApplication app)
        {
            if (IsProcessingOwnChange || !AnyChange.IsExistElementsChange) return;//уже тут 
           
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
    public static partial class AnyChange
    {
        //уже обработанные элементы
        //private static HashSet<ElementId> ElementIdsIsProcessing = new HashSet<ElementId>();

        //private static ExternalEvent _exEvent;
        //private static Timer _debounceTimer;
        //private static string _ownTxName;
        //private static readonly object _lock = new object();

        public static void ProcessSnapshot()
        {
            if (IsProcessingOwnChange||!_existElementsChange) { return; }
            IsProcessingOwnChange=true;

            try
            {
                processSnapshot();
            }
            catch(Exception ex) 
            {
                RevitAPI.AppErrors.LogError(ex);
            }
            finally
            {

            }
            IsProcessingOwnChange=false;
        }

        private static void processSnapshot()
        {
            ElementIdsIsProcessing.Clear();//которые меняли в прошлый раз

            //и все элементы мы обрабатываем
            foreach (Element el in snapshotModificationElements.AddedElements)
            {
                ProcessElement(el, ETypeChangeEl.AddedElements);
            }
            foreach (Element el in snapshotModificationElements.ModifiedElements)
            {
                //тут нет элементов из AddedElements
                ProcessElement(el, ETypeChangeEl.ModifiedElements);
            }
            foreach (Element el in snapshotModificationElements.GeomModifiedElements)
            {
                //тут могут быть элементы из AddedElements и или ModifiedElements
                ProcessElement(el, ETypeChangeEl.ModifiedElements);
            }

        }

        public static void ProcessElement(Element el, ETypeChangeEl eTypeChangeEl)
        {
            if (el == null) { return; }
            BuiltInCategory bic = el.Category.BuiltInCategory;

            switch (bic)
            {
                case (BuiltInCategory.OST_GenericAnnotation):
                    break;
                case (BuiltInCategory.OST_GenericModel):
                    break;
                default: 
                    break;
            }

        }




    }
}
