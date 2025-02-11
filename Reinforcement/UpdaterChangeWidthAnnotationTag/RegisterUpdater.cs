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

    public class RegisterUpdater : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (RevitAPI.UiApplication == null)
            {
                RevitAPI.Initialize(commandData);
            }
            var doc = RevitAPI.Document;
            var uiapp = RevitAPI.UiApplication;
            var uidoc = RevitAPI.UiDocument;

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
                TransparentNotificationWindow.ShowNotification("Авто длина аннотации вкл.", uidoc, 3);
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(parametersUpdater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(parametersUpdater.GetUpdaterId());
                TransparentNotificationWindow.ShowNotification("Авто длина аннотации выкл.", uidoc, 3);
            }
            return Result.Succeeded;
        }





    }
}
