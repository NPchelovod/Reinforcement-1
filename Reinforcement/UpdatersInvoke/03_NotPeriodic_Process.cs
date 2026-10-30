
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
namespace Reinforcement
{
    public partial class PeriodicHandler
    {
        public static void Execute(Document doc)
        {
            if (IsProcessingOwnChange)
            {
                return;
            }
            SnapShotElements snap = AnyChange.GetSnapShotElements(doc);
            if (snap == null) return;

            IsProcessingOwnChange = true;
            try
            {
                ProcessSnapshot(snap.DateTimeCreate, snap, snap.Doc);
                SecretComand(doc, snap); //секретная команда внутри группы действующая
            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors?.LogError(ex);

            }
            finally
            {
                IsProcessingOwnChange = false;
                // Фиксируем момент ЗАВЕРШЕНИЯ — от него отсчитывается cooldown в AnyChange
                Interlocked.Exchange(ref _lastExecuteTimestamp, Stopwatch.GetTimestamp());
            }
        }


        private static void SecretComand(Document doc, SnapShotElements snap)
        {
            //секретная команда внутри группы действующая
            if (!App.InPendingSecret) { return; }

            try
            {

                App.InPendingSecret = false;
                if (string.IsNullOrEmpty(App.OnGroupCurrent.Name))
                {
                    return;
                }
                var elIds = snap.ModifiedIdsProp;
                if (elIds.Count == 0) { return; }

                Element element = doc.GetElement(elIds.First());

                if (element == null) { return; }
                ElementId groupId = element.GroupId;
                if (groupId == ElementId.InvalidElementId) { return; }

                //лучше только выделенные, иначе если есть несколько групп на виде то ошибка будет
                List<Element> elements = snap.ModifiedIdsProp.Select(x => doc.GetElement(x)).ToList();// ArmLengthEquels.SelectOrAllElements(false, false);
                elements = elements.Where(el => el.GroupId == groupId).ToList();
                //надо найти имеющие туже группу

                //надо собрать все элементы которые принадлежат данной группе и находятся на виде
                DeleteDublicateFamily.ReplacedProcess(doc, elements, false, new HashSet<ElementId>(), false);

            }
            catch (Exception ex)
            {
                RevitAPI.AppErrors.LogError(ex);
            }


        }
    }
}
