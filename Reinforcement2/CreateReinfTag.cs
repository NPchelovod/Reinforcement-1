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

    public class CreateReinfTag : IExternalCommand
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
            try //ловим ошибку
            {
                Selection sel = uidoc.Selection;
                var reference = sel.PickObject(ObjectType.Element);
                var elementId = reference.ElementId;
                var element = doc.GetElement(elementId);
                FamilyInstance family= element as FamilyInstance;
                var famName = family.Symbol.FamilyName;
                var category = family.Category;
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    switch (famName)
                    {
                        case "ЕС_А-21 - П-стержень":
                            var leaderEndPoint = sel.PickPoint();
                            var point = sel.PickPoint();
                            var tag = IndependentTag.Create(doc, doc.ActiveView.Id, reference, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, point);
                            tag.LeaderEndCondition = LeaderEndCondition.Free;
                            tag.TagHeadPosition = point;
                           // tag.LeaderEnd = leaderEndPoint; //в 2024 ревите появился метод tag.SetLeaderEnd() а в 21 его нет(
                        break;
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
