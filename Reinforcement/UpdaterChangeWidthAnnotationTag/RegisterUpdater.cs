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

namespace UpdaterChangeWidthAnnotationTag
{
    [Transaction(TransactionMode.Manual)]

    public static class RegisterUpdater
    {
        public static AddInId addInId { get; set; }

        public static void Register()
        {
            var parametersUpdater = new ChangeWidthAnnotationTag();

            if (!UpdaterRegistry.IsUpdaterRegistered(parametersUpdater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(parametersUpdater, true);
                var updaterId = parametersUpdater.GetUpdaterId();

                var filter = new ElementCategoryFilter(BuiltInCategory.OST_GenericAnnotation);
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590069)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590070)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(10585865)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(10585866)));
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(parametersUpdater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(parametersUpdater.GetUpdaterId());
            }
        }
    }
}
