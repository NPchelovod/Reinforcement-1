using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Reinforcement
{
    public partial class PeriodicHandler
    {
        //"Сброс согласования при перемещении закладных";
        //"При перемещении элемента семейства, содержащего 'Закладная', сбрасывает параметр 'Согласовано'.";
        private static void CubicWriter(DateTime dateTime, Document doc, Element element, ETypeChangeEl eTypeChangeEl)
        {
            var cat = element.Category?.BuiltInCategory;
            if (cat != builtInCategoryCubic) { return; }

            if (eTypeChangeEl != ETypeChangeEl.GeomModifiedElements && eTypeChangeEl != ETypeChangeEl.AddedElements) { return; }

            // Проверяем, является ли элемент экземпляром семейства
            if (!(element is FamilyInstance fi)) { return; }
            // Получаем имя семейства
            ElementType type = doc.GetElement(fi.GetTypeId()) as ElementType;
            if (type == null) { return; }
            string familyName = type.FamilyName;
            if (string.IsNullOrEmpty(familyName) || !familyName.Contains("Закладная"))
            { return; }
            // Ищем параметр "Согласовано" (или "Согласновано" с опечаткой)

            foreach (string soglasSeach in Soglass)
            {
                Parameter soglas = fi.LookupParameter(soglasSeach);

                if (soglas != null && soglas.StorageType == StorageType.Integer)
                {
                    // Для Yes/No: 0 = false, 1 = true
                    if (soglas.AsInteger() != 0)
                        soglas.Set(0);
                }
            }

        }
        static readonly BuiltInCategory builtInCategoryCubic = BuiltInCategory.OST_GenericModel;

        static readonly BuiltInCategory builtInCategoryCubicAnnotation = BuiltInCategory.OST_GenericAnnotation;

        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls; // приоритет не принципиален
        static readonly List<string> Soglass = new List<string>() { "Согласовано АР", "Согласовано ИОС", "Согласовано КР" };
        private static void CubicWriterLevel(DateTime dateTime, Document doc, Element element, ETypeChangeEl eTypeChangeEl)
        {
            var cat = element.Category?.BuiltInCategory;
            if (cat != builtInCategoryCubicAnnotation) { return; }

            if (!element.Name.Contains("Кубик_Стена_Прям") && !element.Name.Contains("Отверстие в стене"))
            { return; }

            var check = element.get_Parameter(new Guid("467142a7-677d-439a-9bfc-4cadb8761797"));
            if (check == null)
            {
                return;
            }
            if (!(element is FamilyInstance fi))
                return; // это не экземпляр семейства, пропускаем

            var level = doc.GetElement(element.LevelId) as Level;
            var levelElevation = level.ProjectElevation;
            var elementElevationLevel = element.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).AsDouble(); // относительная высота от отметки уровня (для стен и др HEIGHT_PARAM)
            var elementHeight = element.get_Parameter(new Guid("60bf9b18-17f9-4b8f-b214-fc13bc7b357f")).AsDouble(); // ищем по гуиду
            var elementLowerElevation = levelElevation + elementElevationLevel;
            var elementUpperElevation = elementLowerElevation + elementHeight;
            element.LookupParameter("Отметка пола").Set(levelElevation);
            element.LookupParameter("Низ проема").Set(elementLowerElevation);
            element.LookupParameter("Верх проема").Set(elementUpperElevation);

        }
    }
}