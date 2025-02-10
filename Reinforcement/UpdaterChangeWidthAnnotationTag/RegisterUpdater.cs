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
using Nice3point.Revit.Toolkit;
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
            var doc = Context.ActiveDocument;
            var uiapp = Context.UiApplication;
            var uidoc = Context.ActiveUiDocument;

            var parametersUpdater = new ChangeWidthAnnotationTag();

            if (!UpdaterRegistry.IsUpdaterRegistered(parametersUpdater.GetUpdaterId()))
            {
                UpdaterRegistry.RegisterUpdater(parametersUpdater, true);
                var updaterId = parametersUpdater.GetUpdaterId();

                var filter = new ElementCategoryFilter(BuiltInCategory.OST_GenericAnnotation);
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590069)));
                UpdaterRegistry.AddTrigger(updaterId, filter, Element.GetChangeTypeParameter(new ElementId(590070)));
                var window = new TransparentNotificationWindow("Авто длина аннотации вкл.", uidoc);
                //TaskDialog.Show("Updater", "Авто длина аннотации вкл.");
                window.Show();
            }
            else
            {
                UpdaterRegistry.RemoveAllTriggers(parametersUpdater.GetUpdaterId());
                UpdaterRegistry.UnregisterUpdater(parametersUpdater.GetUpdaterId());
                var window = new TransparentNotificationWindow("Авто длина аннотации выкл.", uidoc);
                window.Show();


                //TaskDialog.Show("Updater", "Авто длина аннотации выкл.");
            }
            return Result.Succeeded;
        }





    }
}
