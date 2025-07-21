using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
    public class CopyElectricFromTask : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Selection sel = uidoc.Selection;
            View activeView = doc.ActiveView;

            RevitAPI.Initialize(commandData);

            // Проверка активного вида
            if (!(activeView is View3D))
            {
                TaskDialog.Show("Ошибка", "Необходимо активировать 3D вид перед выполнением команды");
                return Result.Failed;
            }

            // Выбор связанной модели
            ISelectionFilter selFilter = new SelectionFilter();
            TransparentNotificationWindow.ShowNotification("Выберите связанную модель Задания", uidoc, 5);

            Reference selection;
            try
            {
                selection = sel.PickObject(ObjectType.Element, selFilter);
            }
            catch
            {
                return Result.Cancelled;
            }

            RevitLinkInstance linkedModel = doc.GetElement(selection.ElementId) as RevitLinkInstance;
            Document linkedDoc = linkedModel.GetLinkDocument();

            // Получение типоразмера
            string lightingSymbolName = "160х40_12Вт_ip54";
            var lightingSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(x => x.FamilyName.Equals(lightingSymbolName));

            // Получение элементов для копирования
            string taskSymbolName = "КУ1301";
            XYZ taskSlabXYZ = new XYZ(1, 0, 0);
            var elementsToCopy = new FilteredElementCollector(linkedDoc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_ConduitFitting)
                .Cast<FamilyInstance>()
                .Where(x => x.Symbol.Name == taskSymbolName)
                .Where(x => x.FacingOrientation == taskSlabXYZ)
                .ToList();

            //var excludedElements = new FilteredElementCollector(linkedDoc)
            //    .WhereElementIsNotElementType()
            //    .OfCategory(BuiltInCategory.OST_ConduitFitting)
            //    .Cast<FamilyInstance>()
            //    .Where(x => x.Host == null || x.HostFace == null)
            //    .ToList();

            try
            {
                using (Transaction t = new Transaction(doc, "Копирование из задания в светильники"))
                {
                    t.Start();

                    // Настройки копирования
                    var copyOptions = new CopyPasteOptions();
                    copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

                    //// Копирование элементов
                    foreach (var element in elementsToCopy)
                    {
                        var location = element.Location as LocationPoint;
                        if (location == null) continue;

                        XYZ point = location.Point;

                        // Создание экземпляра семейства
                        doc.Create.NewFamilyInstance(point, lightingSymbol, StructuralType.NonStructural);
                    }
                                                                      
                    t.Commit();

                    // Вывод информации о не скопированных элементах
                    //if (excludedElements.Count > 0)
                    //{
                    //    TaskDialog.Show("Предупреждение",
                    //        $"Не скопировано элементов: {excludedElements.Count}\n" +
                    //        $"ID элементов: {string.Join(", ", excludedElements.Select(x => x.Id.ToString()))}");
                    //}
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        // Фильтр выбора (только связи)
        public class SelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is RevitLinkInstance;
            public bool AllowReference(Reference reference, XYZ point) => false;
        }

        // Обработчик дублирования типов
        public class DuplicateTypeHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
