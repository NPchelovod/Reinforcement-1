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

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class ChangeFamilyType : IExternalCommand
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

            string newName = "ЕС_А_Выноска_КлассСтали_СтадияП";
            string newTypeName = "";

            FilteredElementCollector collection = new FilteredElementCollector(doc);

            var newTypes = collection.OfClass(typeof(Family))
                .Where(x => x.Name == newName)
                .Cast<Family>()
                .FirstOrDefault()
                .GetFamilySymbolIds()
                .Select(x => doc.GetElement(x));


            Selection sel = uidoc.Selection;
            var elementsId = sel.GetElementIds();


            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    foreach (var elementId in elementsId)
                    {
                        var element = doc.GetElement(elementId);
                        newTypeName = element.Name;
                        var newTypeId = newTypes.Where(x => x.Name == newTypeName).FirstOrDefault().Id;

                        /*
                        var param1 = element.LookupParameter("Текст верх").AsValueString();
                        var param2 = element.LookupParameter("Текст низ").AsValueString();
                        var param3 = element.LookupParameter("Ширина полки").AsDouble();
                        */

                        element.ChangeTypeId(newTypeId);

                        /*
                        element.LookupParameter("Текст верх").Set(param1);
                        element.LookupParameter("Текст низ").Set(param2);
                        element.LookupParameter("Ширина полки").Set(param3);
                        */
                    }
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
    }
}
