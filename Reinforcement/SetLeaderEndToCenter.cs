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

    public class SetLeaderEndToCenter : IExternalCommand
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

            Reference sel = uidoc.Selection.PickObject(ObjectType.Element);
            Element element = doc.GetElement(sel);
            IList<ElementId> dependentElements = element.GetDependentElements(null);
            ElementId familyCircle = dependentElements.FirstOrDefault(x => doc.GetElement(x).Name == "Точка");
            ElementId independentTagId = dependentElements.FirstOrDefault(x => doc.GetElement(x) is IndependentTag);

            LocationPoint locationPoint = doc.GetElement(familyCircle).Location as LocationPoint;
            var xPt = locationPoint.Point.X;
            var yPt = locationPoint.Point.Y;
            var zPt = locationPoint.Point.Z;
            XYZ newLeaderPosition = new XYZ(xPt, yPt, zPt);
            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Положение выносной линии"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    IndependentTag independentTag = doc.GetElement(independentTagId) as IndependentTag;
                    independentTag.LeaderEnd = newLeaderPosition;
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
