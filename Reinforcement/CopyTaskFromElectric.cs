using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class CopyTaskFromElectric : IExternalCommand
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
            UIApplication uiapp = RevitAPI.UiApplication;
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            Selection sel = uidoc.Selection;

            ISelectionFilter selFilter = new SelectionFilter();
            TransparentNotificationWindow.ShowNotification("Выберите связанную модель ЭЛ", uidoc, 10);
            RevitLinkInstance linkedModel = doc.GetElement(sel.PickObject(ObjectType.Element, selFilter).ElementId) as RevitLinkInstance;
            Document linkedDoc = linkedModel.GetLinkDocument();

            var linkedView = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(x => x.Name.ToLower().Contains("2_этаж"));
            var linkedSectionView = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => x.Name.ToLower().Contains("разрез 1"))
                .Select(x => x.Id)
                .ToList();
            var listBoxes = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .ToElementIds()
                .ToList();

            var detailLines = new FilteredElementCollector(linkedDoc, linkedView.Id)
                .OfClass(typeof(CurveElement))
                .ToElementIds()
                .ToList();

            
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();
                    Transform transform = linkedModel.GetTransform();
                    CopyPasteOptions copyPasteOptions = new CopyPasteOptions();
                    copyPasteOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    ElementTransformUtils.CopyElements(
                        linkedDoc,
                        linkedSectionView,
                        doc,
                        transform,
                        copyPasteOptions
                        );
                   ElementTransformUtils.CopyElements(
                        linkedDoc,
                        listBoxes,
                        doc,
                        transform,
                        copyPasteOptions
                        );

                    ElementTransformUtils.CopyElements(
                        linkedView,
                        detailLines,
                        doc.ActiveView,
                        Transform.Identity,
                        new CopyPasteOptions()
                        );
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                if (element is RevitLinkInstance)
                {
                    return true;
                }
                return false;
            }

            public bool AllowReference(Reference reference, XYZ point)
            {
                return false;
            }

        }
        public class DuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                // Использовать уже существующий тип
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }

    }
}
