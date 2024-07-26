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

    public class CreateViewBySelectedWalls : IExternalCommand
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
                //Тут пишем основной код для изменения элементов модели
                IList <Reference> reference = uidoc.Selection.PickObjects(ObjectType.Element);
                var activeView = doc.ActiveView;
                Wall wall = null;

                if (reference.Count == 0)
                {
                    MessageBox.Show("Не выбрано ни одной стены");
                    return Result.Failed;
                }

                foreach (Reference r in reference)
                {
                    wall = doc.GetElement(r) as Wall;
                    // Ensure wall is straight
                    LocationCurve lc = wall.Location as LocationCurve;
                    Line line = lc.Curve as Line;
                    if (null == line)
                    {
                        message = "Не получилось определить местоположение линии стены";

                        return Result.Failed;
                    }
                    // Determine view family type to use
                    ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                                                        .OfClass(typeof(ViewFamilyType))
                                                        .Cast<ViewFamilyType>()
                                                        .FirstOrDefault<ViewFamilyType>(x =>
                                                         ViewFamily.Section == x.ViewFamily);
                    //Determine section box
                    XYZ p = line.GetEndPoint(0),
                        q = line.GetEndPoint(1),
                        v = q - p;
                    BoundingBoxXYZ bb = wall.get_BoundingBox(null);
                    double minZ = bb.Min.Z,
                           maxZ = bb.Max.Z,

                           w = v.GetLength(),
                           h = maxZ - minZ,
                           d = wall.WallType.Width,
                           offset = 0.1 * w;
                    XYZ min = new XYZ(-w, minZ - offset, -offset),
                        max = new XYZ(w, maxZ + offset, offset),
                        midpoint = p + 0.5 * v,
                        walldir = v.Normalize(),
                        up = XYZ.BasisZ,
                        viewDir = walldir.CrossProduct(up);

                    Transform transform = Transform.Identity;
                    transform.Origin = midpoint;
                    transform.BasisX = walldir;
                    transform.BasisY = up;
                    transform.BasisZ = viewDir;
                    BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                    sectionBox.Transform = transform;
                    sectionBox.Min = min;
                    sectionBox.Max = max;
                    using (Transaction t = new Transaction(doc, "Создание вида стен"))
                    {
                        t.Start();
                        ViewSection.CreateSection(doc,viewFamilyType.Id, sectionBox);
                        t.Commit();
                    }
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
