using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reinforcement;
using System.Drawing;
using System.Windows.Forms;

namespace Updaters
{
    public class AutoHoleFloorPoint : IUpdater
    {
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            var ids = data.GetModifiedElementIds().ToList();
            ids.AddRange(data.GetAddedElementIds().ToList());
            foreach (var id in ids)
            {
                var element = doc.GetElement(id);

                if (element.Name.Contains("Кубик_Стена_Прям") || element.Name.Contains("Отверстие в стене"))
                {
                    var check = element.get_Parameter(new Guid("467142a7-677d-439a-9bfc-4cadb8761797"));
                    if (check == null)
                    {
                        return;
                    }
                    if (!(element is FamilyInstance fi))
                        continue; // это не экземпляр семейства, пропускаем

                    var level = doc.GetElement(element.LevelId) as Level;
                    var levelElevation = level.ProjectElevation;
                    var elementElevationLevel = element.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).AsDouble();
                    var elementHeight = element.get_Parameter(new Guid("60bf9b18-17f9-4b8f-b214-fc13bc7b357f")).AsDouble();
                    var elementLowerElevation = levelElevation + elementElevationLevel;
                    var elementUpperElevation = elementLowerElevation + elementHeight;
                    element.LookupParameter("Отметка пола").Set(levelElevation);
                    element.LookupParameter("Низ проема").Set(elementLowerElevation);
                    element.LookupParameter("Верх проема").Set(elementUpperElevation);
                }
                else
                {
                    return;
                }
                //TaskDialog.Show("Revit updater",$"Имя измененного элемента {element.Name}");
            }
        }

        public string GetAdditionalInformation()
        {
            return string.Empty;
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.Annotations;
        }

        public UpdaterId GetUpdaterId()
        {
            return new UpdaterId(RegisterUpdater.addInId,
                new Guid("D7C9AA9A-7172-466C-AE34-B1CD8457166E"));
        }

        public string GetUpdaterName()
        {
            return "Updater для авто отметки отверстий";
        }
    }
}
