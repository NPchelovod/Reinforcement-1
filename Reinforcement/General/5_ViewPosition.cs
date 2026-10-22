using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace Reinforcement
{
    /// <summary>
    /// Одиночный слот буфера: SectionBox + ViewOrientation3D одного 3D вида.
    /// Только в памяти процесса. Сбрасывается при перезапуске Revit.
    /// </summary>
    public static class ViewPositionBuffer
    {
        public static bool IsValid;

        public static string SourceDocTitle;
        public static string SourceViewName;
        public static int SourceViewId;
        public static Document SourceDocument;
        public static DateTime SavedAt;
        public static string SavedBy;

        public static bool HasTarget;
        public static string TargetDocTitle;
        public static Document TargetDocument;
        public static int TargetViewId;

        public static bool HasSectionBox;
        public static bool IsSectionBoxActive;
        public static double[] Min;
        public static double[] Max;
        public static double[] TOrigin;
        public static double[] TBasisX;
        public static double[] TBasisY;
        public static double[] TBasisZ;

        public static double[] Eye;
        public static double[] Up;
        public static double[] Forward;

        public static void Clear()
        {
            IsValid = false;
            HasTarget = false;
            TargetDocument = null;
            TargetViewId = 0;
            TargetDocTitle = null;
        }

        public static double[] ToArray(XYZ p)
            => p == null ? null : new[] { p.X, p.Y, p.Z };

        public static XYZ FromArray(double[] a)
            => (a == null || a.Length != 3) ? null : new XYZ(a[0], a[1], a[2]);
    }

    // === Копирование позиции ===
    [Transaction(TransactionMode.Manual)]
    public class ViewPositionCopy : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Буфер позиции",
                    "Откройте ортогональный 3D вид (перспективный не поддерживается).");
                return Result.Cancelled;
            }

            if (!ViewPositionOperations.TryCapture(view3D,  null, out string error))
            {
                TaskDialog.Show("Буфер позиции", "Не удалось сохранить позицию:\n" + error);
                return Result.Failed;
            }

            string boxInfo = !ViewPositionBuffer.HasSectionBox
                ? "отсутствуют"
                : (ViewPositionBuffer.IsSectionBoxActive ? "включены" : "выключены");

            TaskDialog.Show("Буфер позиции",
                $"Позиция скопирована.\n\n" +
                $"Вид: {view3D.Name}\n" +
                $"Документ: {doc.Title}\n" +
                $"Границы 3D: {boxInfo}\n" +
                $"Ориентация: {(ViewPositionBuffer.Eye != null ? "сохранена" : "нет данных")}\n" +
                $"Буфер заменит предыдущее содержимое.");

            return Result.Succeeded;
        }
    }

    // === Применение позиции ===
    [Transaction(TransactionMode.Manual)]
    public class ViewPositionPaste : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            if (!ViewPositionBuffer.IsValid)
            {
                TaskDialog.Show("Позиционировать",
                    "Буфер пуст. Сначала нажмите «Буфер позиции» на нужном 3D виде.");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Позиционировать",
                    "Откройте ортогональный 3D вид (перспективный не поддерживается).");
                return Result.Cancelled;
            }

            bool sameView = (int)view3D.Id.Value == ViewPositionBuffer.SourceViewId
                            && ReferenceEquals(doc, ViewPositionBuffer.SourceDocument);
            if (sameView)
            {
                var td = new TaskDialog("Позиционировать")
                {
                    MainInstruction = "Текущий вид — тот же, с которого копировали.",
                    MainContent = "Продолжить и применить позицию к нему же?",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (td.Show() != TaskDialogResult.Yes) return Result.Cancelled;
            }

            if (!ViewPositionOperations.TryApply(view3D, out string error))
            {
                TaskDialog.Show("Позиционировать", "Не удалось применить позицию:\n" + error);
                return Result.Failed;
            }

            // Только теперь фиксируем Target — применение прошло успешно,
            // и именно в этот вид имеет смысл продолжать синхронизацию.
            ViewPositionBuffer.TargetDocument = doc;
            ViewPositionBuffer.TargetDocTitle = doc.Title;
            ViewPositionBuffer.TargetViewId = (int)view3D.Id.Value;
            ViewPositionBuffer.HasTarget = true;

            TaskDialog.Show("Позиционировать",
                $"Позиция применена.\n\n" +
                $"Источник: {ViewPositionBuffer.SourceViewName}\n" +
                $"Документ: {ViewPositionBuffer.SourceDocTitle}\n" +
                $"Сохранено: {ViewPositionBuffer.SavedAt:yyyy-MM-dd HH:mm} ({ViewPositionBuffer.SavedBy})\n\n" +
                $"Вид «{view3D.Name}» зафиксирован как цель для синхронизации.");

            return Result.Succeeded;
        }

        
        
    }
    public static class ViewPositionOperations
    {
        // ---------- CAPTURE ----------
        public static bool TryCapture(Document doc, int viewId, out string error,
                                      int? targetViewId = null)
        {
            error = null;
            if (doc == null) { error = "Документ не задан."; return false; }
            var view = doc.GetElement(new ElementId(viewId)) as View3D;
            if (view == null) { error = $"Вид с Id {viewId} не найден или это не 3D-вид."; return false; }
            return TryCapture(view, targetViewId, out error);
        }

        public static bool TryCapture(View3D view, int? targetViewId, out string error)
        {
            error = null;
            if (view == null) { error = "3D-вид не задан."; return false; }
            if (view.IsPerspective) { error = "Перспективный вид не поддерживается."; return false; }

            try
            {
                BoundingBoxXYZ box = view.GetSectionBox();
                ViewPositionBuffer.HasSectionBox = box != null;
                ViewPositionBuffer.IsSectionBoxActive = view.IsSectionBoxActive;

                if (box != null)
                {
                    ViewPositionBuffer.Min = ViewPositionBuffer.ToArray(box.Min);
                    ViewPositionBuffer.Max = ViewPositionBuffer.ToArray(box.Max);
                    ViewPositionBuffer.TOrigin = ViewPositionBuffer.ToArray(box.Transform?.Origin);
                    ViewPositionBuffer.TBasisX = ViewPositionBuffer.ToArray(box.Transform?.BasisX);
                    ViewPositionBuffer.TBasisY = ViewPositionBuffer.ToArray(box.Transform?.BasisY);
                    ViewPositionBuffer.TBasisZ = ViewPositionBuffer.ToArray(box.Transform?.BasisZ);
                }
                else
                {
                    ViewPositionBuffer.Min = ViewPositionBuffer.Max = null;
                    ViewPositionBuffer.TOrigin = ViewPositionBuffer.TBasisX =
                    ViewPositionBuffer.TBasisY = ViewPositionBuffer.TBasisZ = null;
                }

                ViewOrientation3D orientation = view.GetOrientation();
                if (orientation != null)
                {
                    ViewPositionBuffer.Eye = ViewPositionBuffer.ToArray(orientation.EyePosition);
                    ViewPositionBuffer.Up = ViewPositionBuffer.ToArray(orientation.UpDirection);
                    ViewPositionBuffer.Forward = ViewPositionBuffer.ToArray(orientation.ForwardDirection);
                }
                else
                {
                    ViewPositionBuffer.Eye = ViewPositionBuffer.Up = ViewPositionBuffer.Forward = null;
                }

                Document doc = view.Document;
                ViewPositionBuffer.SourceDocTitle = doc.Title;
                ViewPositionBuffer.SourceDocument = doc;
                ViewPositionBuffer.SourceViewName = view.Name;
                ViewPositionBuffer.SourceViewId = (int)view.Id.Value;
                ViewPositionBuffer.SavedAt = DateTime.Now;
                ViewPositionBuffer.SavedBy = Environment.UserName;

                // Если явно передан targetViewId — фиксируем цель.
                // Если нет — оставляем уже установленную цель (если была),
                // чтобы Copy не сбрасывал цель, заданную через Paste.
                if (targetViewId.HasValue)
                {
                    ViewPositionBuffer.HasTarget = true;
                    ViewPositionBuffer.TargetViewId = targetViewId.Value;
                    ViewPositionBuffer.TargetDocTitle = doc.Title;
                    ViewPositionBuffer.TargetDocument = doc;
                }

                ViewPositionBuffer.IsValid = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ---------- APPLY ----------
        public static bool TryApply(Document doc, int viewId, out string error,
                                    bool useOwnTransaction = true,
                                    Transaction externalTransaction = null)
        {
            error = null;
            if (doc == null) { error = "Документ не задан."; return false; }
            var view = doc.GetElement(new ElementId(viewId)) as View3D;
            if (view == null) { error = $"Вид с Id {viewId} не найден или это не 3D-вид."; return false; }
            return TryApply(view, out error, useOwnTransaction, externalTransaction);
        }

        public static bool TryApply(View3D view, out string error,
                                    bool useOwnTransaction = true,
                                    Transaction externalTransaction = null)
        {
            error = null;
            if (view == null) { error = "3D-вид не задан."; return false; }
            if (view.IsPerspective) { error = "Перспективный вид не поддерживается."; return false; }
            if (!ViewPositionBuffer.IsValid)
            {
                error = "Буфер пуст. Сначала снимок позиции не сделан.";
                return false;
            }

            // Orientation
            ViewOrientation3D orientation = null;
            XYZ eye = ViewPositionBuffer.FromArray(ViewPositionBuffer.Eye);
            XYZ up = ViewPositionBuffer.FromArray(ViewPositionBuffer.Up);
            XYZ forward = ViewPositionBuffer.FromArray(ViewPositionBuffer.Forward);
            if (eye != null && up != null && forward != null)
            {
                try { orientation = new ViewOrientation3D(eye, up, forward); }
                catch { orientation = null; }
            }

            // Box
            BoundingBoxXYZ box = null;
            if (ViewPositionBuffer.HasSectionBox)
            {
                XYZ min = ViewPositionBuffer.FromArray(ViewPositionBuffer.Min);
                XYZ max = ViewPositionBuffer.FromArray(ViewPositionBuffer.Max);
                if (min != null && max != null)
                {
                    box = new BoundingBoxXYZ { Min = min, Max = max };
                    XYZ tO = ViewPositionBuffer.FromArray(ViewPositionBuffer.TOrigin);
                    XYZ tX = ViewPositionBuffer.FromArray(ViewPositionBuffer.TBasisX);
                    XYZ tY = ViewPositionBuffer.FromArray(ViewPositionBuffer.TBasisY);
                    XYZ tZ = ViewPositionBuffer.FromArray(ViewPositionBuffer.TBasisZ);
                    if (tO != null && tX != null && tY != null && tZ != null)
                    {
                        Transform t = Transform.Identity;
                        t.Origin = tO; t.BasisX = tX; t.BasisY = tY; t.BasisZ = tZ;
                        box.Transform = t;
                    }
                }
            }

            Transaction trans = externalTransaction;
            bool ownTransaction = false;

            try
            {
                if (trans == null && !view.Document.IsModifiable)
                {
                    if (!useOwnTransaction)
                    {
                        error = "Транзакция не передана, а useOwnTransaction=false.";
                        return false;
                    }
                    trans = new Transaction(view.Document, "Применение позиции 3D вида");
                    trans.Start();
                    ownTransaction = true;
                }

                if (orientation != null)
                    view.SetOrientation(orientation);

                if (box != null)
                {
                    view.SetSectionBox(box);
                    view.IsSectionBoxActive = ViewPositionBuffer.IsSectionBoxActive;
                }

                if (ownTransaction) trans.Commit();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                try { if (ownTransaction && trans != null && trans.HasStarted()) trans.RollBack(); }
                catch { /* ignore */ }
                return false;
            }
        }

        // ---------- SYNC ----------
        public static bool TrySync(Document sourceDoc, int sourceViewId,
                                   Document targetDoc, int targetViewId,
                                   out string error)
        {
            if (!TryCapture(sourceDoc, sourceViewId, out error, targetViewId)) return false;
            return TryApply(targetDoc, targetViewId, out error);
        }

        /// <summary>
        /// Синхронизация по текущему содержимому буфера:
        /// SourceDocument/SourceViewId → TargetDocument/TargetViewId.
        /// Удобно для фонового цикла раз в N секунд.
        /// </summary>
        public static bool TrySyncFromBuffer(out string error)
        {
            error = null;
            if (!ViewPositionBuffer.IsValid) { error = "Буфер пуст."; return false; }
            if (!ViewPositionBuffer.HasTarget) { error = "Целевой вид не задан (нажмите «Позиционировать»)."; return false; }
            if (ViewPositionBuffer.SourceDocument == null) { error = "SourceDocument потерян (документ закрыт?)."; return false; }
            if (ViewPositionBuffer.TargetDocument == null) { error = "TargetDocument потерян (документ закрыт?)."; return false; }

            // Тот же документ и тот же вид — синхронизация бессмысленна.
            if (ReferenceEquals(ViewPositionBuffer.SourceDocument, ViewPositionBuffer.TargetDocument)
                && ViewPositionBuffer.SourceViewId == ViewPositionBuffer.TargetViewId)
            {
                error = "Источник и цель — один и тот же вид.";
                return false;
            }

            return TrySync(ViewPositionBuffer.SourceDocument, ViewPositionBuffer.SourceViewId,
                           ViewPositionBuffer.TargetDocument, ViewPositionBuffer.TargetViewId,
                           out error);
        }
    }
}