
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
        public static bool Execute(SnapShotElements snap)
        {
            if (IsProcessingOwnChange)
            {
                return false;
            }
           
            try
            {
                IsProcessingOwnChange = true;
                ProcessSnapshot(snap.DateTimeCreate, snap, snap.Doc);
                SecretComand(snap.Doc, snap); //секретная команда внутри группы действующая
            }
            catch (Exception ex)
            {
                AppErrors.LogError(ex);
                return false;
            }
            finally
            {
                IsProcessingOwnChange = false;
                
            }
            return true;
        }


        private static void SecretComand(Document doc, SnapShotElements snap)
        {
            //секретная команда внутри группы действующая
            if (!App.InPendingSecret) { return; }
            App.InPendingSecret = false;
            try
            {

                
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
                AppErrors.LogError(ex);
            }
            finally
            {
            }


        }
    }
}
