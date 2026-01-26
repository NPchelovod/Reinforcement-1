using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Drawing.Text;
using System.Drawing;
using Reinforcement;

namespace Updaters
{
    [Transaction(TransactionMode.Manual)]

    public static class RegisterUpdater
    {
        public static AddInId addInId { get; set; }

        public static void Register()
        {
            var changedWidthUpdater = new ChangeWidthAnnotationTag();

            if (!UpdaterRegistry.IsUpdaterRegistered(changedWidthUpdater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(changedWidthUpdater, true);
                var updaterId = changedWidthUpdater.GetUpdaterId();

                var filter = new ElementCategoryFilter(BuiltInCategory.OST_GenericAnnotation);
                /*
                var all_id_control = new List<long>()
                {
                    590069, 590070, 10585865,10585866,
                    394975, 395004,1307668,1307697,3041016,3040699,576988,577017,591017,591046
                };
                
                foreach (var id in all_id_control)
                { 
                    UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(id)));
                }
                */

                var categoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_GenericAnnotation);
                var classFilter = new ElementClassFilter(typeof(FamilyInstance));

                var combinedFilter = new LogicalAndFilter(categoryFilter, classFilter);
                UpdaterRegistry.AddTrigger(updaterId, combinedFilter, Element.GetChangeTypeElementAddition());
                UpdaterRegistry.AddTrigger(updaterId, categoryFilter, Element.GetChangeTypeAny());
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(changedWidthUpdater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(changedWidthUpdater.GetUpdaterId());
            }

            var autoHoleFloorPoint = new AutoHoleFloorPoint();

            if (!UpdaterRegistry.IsUpdaterRegistered(autoHoleFloorPoint.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(autoHoleFloorPoint, true);
                var updaterId = autoHoleFloorPoint.GetUpdaterId();


                var categoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);
                var classFilter = new ElementClassFilter(typeof(FamilyInstance));

                var combinedFilter = new LogicalAndFilter(categoryFilter, classFilter);
                UpdaterRegistry.AddTrigger(updaterId, combinedFilter, Element.GetChangeTypeElementAddition());
                UpdaterRegistry.AddTrigger(updaterId, categoryFilter, Element.GetChangeTypeAny());
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(autoHoleFloorPoint.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(autoHoleFloorPoint.GetUpdaterId());
            }
        }
    }
}
