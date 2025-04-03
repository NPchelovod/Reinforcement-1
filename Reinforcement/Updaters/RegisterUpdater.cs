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
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590069)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590070)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(10585865)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(10585866)));
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

                var filter = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);
                //var filter1 = new ElementClassFilter(typeof(FamilyInstance));
                
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeAny());
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeElementAddition());
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(autoHoleFloorPoint.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(autoHoleFloorPoint.GetUpdaterId());
            }
        }
    }
}
