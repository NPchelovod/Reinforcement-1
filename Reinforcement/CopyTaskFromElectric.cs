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
            TransparentNotificationWindow.ShowNotification("Выберите связанную модель ЭЛ", uidoc, 5);

            Reference selection;
            try
            {
                selection = sel.PickObject(ObjectType.Element, selFilter);
            }
            catch
            {
                TaskDialog.Show("Ошибка", "Не выбрана связанная модель");
                return Result.Cancelled;
            }

            RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
            Document linkedDoc = linkedModel.GetLinkDocument();

            //проверить есть ли в проекте семейства короба эл
            var checkFamily = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Where(x => x.Name == "Короб ЭЛ_Квадратный" || x.Name == "Короб ЭЛ_Круглый")
                .ToList();
            if (checkFamily.Count != 2)
            {
                TaskDialog.Show("Ошибка", "Загрузите семейства в проект: \"Короб ЭЛ_Квадратный\" и \"Короб ЭЛ_Квадратный\"");
            }
            var symbolSquare = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First(x => x.FamilyName == "Короб ЭЛ_Квадратный");
            var symbolCircle = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .First(x => x.FamilyName == "Короб ЭЛ_Круглый");
            var linkedViews = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => x.Name.ToLower().Contains("этаж_основ") || x.Name.ToLower().Contains("разрез 1"))
                .Select(x => x.Id)
                .ToList();
            var linkedSectionView = new FilteredElementCollector(linkedDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(x => x.Name.ToLower().Contains("разрез 1"))
                .Select(x => x.Id)
                .ToList();
            var listBoxes = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .ToList();
            /*var detailLines = new FilteredElementCollector(linkedDoc, linkedViews.Id)
                .OfClass(typeof(CurveElement))
                .ToElementIds()
                .ToList();*/


            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Копирование задания ЭЛ"))
                {
                    t.Start();
                    //activate family symbol
                    if (!symbolSquare.IsActive)
                        symbolSquare.Activate();
                    if (!symbolCircle.IsActive)
                        symbolCircle.Activate();
                    //Transform transform = linkedModel.GetTransform();
                    CopyPasteOptions copyPasteOptions = new CopyPasteOptions();
                    copyPasteOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    /*            ElementTransformUtils.CopyElements(
                                    linkedDoc,
                                    linkedSectionView,
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
                );*/

                    //Вставляем семейства коробов
                    foreach (var box in listBoxes)
                    {
                        XYZ coord = ((LocationPoint)box.Location).Point;
                        XYZ orientation = box.HandOrientation;                        
                        ElementCategoryFilter wallAndFloorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
                        ElementCategoryFilter floorFilter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
                        LogicalOrFilter filter = new LogicalOrFilter(wallAndFloorFilter, floorFilter);
                        ReferenceIntersector intersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, doc.ActiveView as View3D);
                        
                        List<XYZ> directions = new List<XYZ> {XYZ.BasisX,-XYZ.BasisX,XYZ.BasisY,-XYZ.BasisY,XYZ.BasisZ,-XYZ.BasisZ};
                        ReferenceWithContext bestRwc = null;
                        double minProximity = double.MaxValue;
                        //стреляем лучи и находим ближайшую грань стены/перекрытия
                        foreach (XYZ dir in directions)
                        {
                            ReferenceWithContext rwc = intersector.FindNearest(coord, dir);
                            if (rwc == null) continue;

                            if (rwc.Proximity < minProximity)
                            {
                                bestRwc = rwc;
                                minProximity = rwc.Proximity;
                            }
                        }
                        if (bestRwc != null)
                        {
                            Reference reference = bestRwc.GetReference();
                            Element wall = doc.GetElement(reference.ElementId);
                            // можно создавать экземпляр семейства

                            if (box.Symbol.FamilyName == "Короб ЭЛ_Квадратный")
                            {
                                doc.Create.NewFamilyInstance(reference, coord, orientation, symbolSquare);
                            }
                            else if (box.Symbol.FamilyName == "Короб ЭЛ_Круглый")
                            {
                                doc.Create.NewFamilyInstance(reference, coord, orientation, symbolCircle);
                            }
                        }
                    }

                    //Копируем виды из связанной модели ЭЛ
                    var newViews = ElementTransformUtils.CopyElements(
                        linkedDoc,
                        linkedViews,
                        doc,
                        Transform.Identity,
                        copyPasteOptions
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
