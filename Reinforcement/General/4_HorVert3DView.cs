using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Reinforcement
{


    [Transaction(TransactionMode.Manual)]
    public class SectionBoxFitHorizontal : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Границы 3D вида",
                    "Откройте ортогональный 3D вид.");
                return Result.Cancelled;
            }

            Result r = SectionBoxFitHelper.Fit(
                doc, view3D,
                fitX: true, fitY: true, fitZ: false,
                message: ref message);

            if (r == Result.Succeeded)
            {
                TaskDialog.Show("Границы 3D вида",
                    "Границы растянуты по горизонтали (X и Y) до габаритов модели.\n" +
                    "Отметки по Z остались как были.");
            }
            return r;
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class SectionBoxFitVertical : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            if (uidoc == null) return Result.Cancelled;

            Document doc = uidoc.Document;
            View3D view3D = doc.ActiveView as View3D;
            if (view3D == null || view3D.IsPerspective)
            {
                TaskDialog.Show("Границы 3D вида",
                    "Откройте ортогональный 3D вид.");
                return Result.Cancelled;
            }

            Result r = SectionBoxFitHelper.Fit(
                doc, view3D,
                fitX: false, fitY: false, fitZ: true,
                message: ref message);

            if (r == Result.Succeeded)
            {
                TaskDialog.Show("Границы 3D вида",
                    "Границы растянуты по вертикали (Z) до габаритов модели.\n" +
                    "Обрезка по X и Y осталась как была.");
            }
            return r;
        }
    }
    internal static class SectionBoxFitHelper
    {
        // Небольшой отступ, чтобы граница не резала элементы вплотную.
        private const double Pad = 100.0 / 304.8;

        /// <summary>
        /// Подгоняет границы 3D вида по габаритам модели.
        /// fitX/fitY/fitZ — какие оси растягивать; остальные сохраняются как у текущего бокса.
        /// </summary>
        public static Result Fit(
            Document doc, View3D view3D,
            bool fitX, bool fitY, bool fitZ,
            ref string message)
        {
            BoundingBoxXYZ modelBox = ComputeModelBoundingBox(doc);
            if (modelBox == null)
            {
                message = "Не удалось определить габариты модели.";
                TaskDialog.Show("Границы 3D вида", message);
                return Result.Failed;
            }

            BoundingBoxXYZ current = view3D.GetSectionBox();
            XYZ curMin, curMax;
            if (current == null)
            {
                // Границ нет — считаем, что текущий бокс = габариты модели.
                curMin = modelBox.Min;
                curMax = modelBox.Max;
            }
            else
            {
                GetWorldExtents(current, out curMin, out curMax);
            }

            double minX = fitX ? modelBox.Min.X - Pad : curMin.X;
            double minY = fitY ? modelBox.Min.Y - Pad : curMin.Y;
            double minZ = fitZ ? modelBox.Min.Z - Pad : curMin.Z;
            double maxX = fitX ? modelBox.Max.X + Pad : curMax.X;
            double maxY = fitY ? modelBox.Max.Y + Pad : curMax.Y;
            double maxZ = fitZ ? modelBox.Max.Z + Pad : curMax.Z;

            var newBox = new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };

            using (Transaction t = new Transaction(doc, "Подгонка границ 3D вида"))
            {
                t.Start();
                view3D.SetSectionBox(newBox);
                view3D.IsSectionBoxActive = true;
                t.Commit();
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// Мировые габариты (AABB) секционного бокса с учётом его Transform.
        /// </summary>
        private static void GetWorldExtents(BoundingBoxXYZ box, out XYZ min, out XYZ max)
        {
            Transform t = box.Transform ?? Transform.Identity;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            for (int i = 0; i < 8; i++)
            {
                XYZ local = new XYZ(
                    (i & 1) == 0 ? box.Min.X : box.Max.X,
                    (i & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (i & 4) == 0 ? box.Min.Z : box.Max.Z);
                XYZ p = t.OfPoint(local);

                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            min = new XYZ(minX, minY, minZ);
            max = new XYZ(maxX, maxY, maxZ);
        }
        /// <summary>
        /// Категории, элементы которых учитываются при расчёте габаритов модели.
        /// Всё, чего нет в списке, игнорируется — так «мусорные» категории
        /// (мебель, оборудование, импорт, аннотации на листах и пр.) не растягивают бокс.
        /// </summary>
        public static readonly HashSet<BuiltInCategory> AllowedCategories =
            new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_Walls,                // Стены
            BuiltInCategory.OST_Floors,               // Перекрытия
            BuiltInCategory.OST_Ceilings,             // Потолки
            BuiltInCategory.OST_StructuralColumns,    // Несущие колонны
            BuiltInCategory.OST_StructuralFraming,    // Несущий каркас (балки)
            BuiltInCategory.OST_StructuralFoundation, // Фундаменты, сваи
            BuiltInCategory.OST_GenericModel,         // Обобщённые модели
            //BuiltInCategory.OST_GenericAnnotation,    // Типовые аннотации
            BuiltInCategory.OST_Doors,                // Двери
            BuiltInCategory.OST_Windows,              // Окна
            BuiltInCategory.OST_Stairs,               // Лестницы
            //BuiltInCategory.OST_Lines,                // Линии
            //BuiltInCategory.OST_Grids,                // Оси
            //BuiltInCategory.OST_Views,                // Виды (если нужно учитывать)
            BuiltInCategory.OST_DuctCurves,           // Воздуховоды
            BuiltInCategory.OST_DuctFitting,          // Фитинги воздуховодов
            BuiltInCategory.OST_DuctAccessory,        // Арматура воздуховодов
            BuiltInCategory.OST_PipeCurves,           // Трубы
            BuiltInCategory.OST_PipeFitting,          // Фитинги труб
            BuiltInCategory.OST_PipeAccessory,        // Арматура труб
        };
        /// <summary>
        /// Мировые габариты всей модели. Пропускаем категории, которые
        /// обычно «раздувают» bbox: оси, уровни, референсные плоскости, камеры и т.п.
        /// </summary>
        private static BoundingBoxXYZ ComputeModelBoundingBox(Document doc)
        {
            //    var skipCats = new HashSet<int>
            //{
            //    (int)BuiltInCategory.OST_Grids,
            //    (int)BuiltInCategory.OST_Levels,
            //    //(int)BuiltInCategory.OST_ReferencePlanes,
            //    (int)BuiltInCategory.OST_CLines,
            //    (int)BuiltInCategory.OST_Cameras,
            //    (int)BuiltInCategory.OST_SectionBox,
            //    (int)BuiltInCategory.OST_Views,
            //    (int)BuiltInCategory.OST_Sheets,
            //    (int)BuiltInCategory.OST_IOSModelGroups,
            //    (int)BuiltInCategory.OST_RoomSeparationLines,
            //    (int)BuiltInCategory.OST_AreaSchemeLines,
            //};

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool any = false;

            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
            foreach (Element el in collector)
            {
                Category cat = el.Category;
                if (cat == null) continue;

                // Белый список: всё, чего в нём нет — пропускаем.
                if (!AllowedCategories.Contains((BuiltInCategory)cat.Id.IntegerValue))
                    continue;

                BoundingBoxXYZ bb;
                try { bb = el.get_BoundingBox(null); }
                catch { continue; }
                if (bb == null) continue;

                Transform t = bb.Transform ?? Transform.Identity;
                for (int i = 0; i < 8; i++)
                {
                    XYZ local = new XYZ(
                        (i & 1) == 0 ? bb.Min.X : bb.Max.X,
                        (i & 2) == 0 ? bb.Min.Y : bb.Max.Y,
                        (i & 4) == 0 ? bb.Min.Z : bb.Max.Z);
                    XYZ p = t.OfPoint(local);

                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z > maxZ) maxZ = p.Z;
                    any = true;
                }
            }

            if (!any) return null;

            return new BoundingBoxXYZ
            {
                Transform = Transform.Identity,
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }
    }
}
