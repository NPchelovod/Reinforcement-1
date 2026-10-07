using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class App_Dimensions_MultipleOf200 : IExternalCommand
    {
        private const long Step = 200; // шаг кратности в мм
        private const long Min = 200; // строго больше этого

        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "Откройте проект.";
                return Result.Failed;
            }

            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            // Пробуем взять выделение
            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

            List<Dimension> dimensions = new List<Dimension>();

            if (selectedIds != null && selectedIds.Count > 0)
            {
                // Пользователь что-то выделил — работаем только с выделенными размерами
                dimensions = selectedIds
                    .Select(id => doc.GetElement(id))
                    .OfType<Dimension>()
                    .Where(d => d.DimensionShape == DimensionShape.Linear)
                    .ToList();

               
            }
            if(dimensions.Count==0)
            {
                // Ничего не выделено — берём все размеры активного вида
                dimensions = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Dimension))
                    .Cast<Dimension>()
                    .Where(d => d.DimensionShape == DimensionShape.Linear)
                    .ToList();

               
            }

            if (dimensions.Count == 0)
            {
                TaskDialog.Show("ЕС BIM", "На виде нет линейных размеров.");
                return Result.Cancelled;
            }

            int changed = 0;

            using (Transaction t = new Transaction(doc, "Префикс кратных 200"))
            {
                t.Start();

                foreach (Dimension dim in dimensions)
                {
                    if (dim.NumberOfSegments == 0)
                    {
                        // Обычный односегментный размер
                        if (dim.Value.HasValue &&
                         TryBuildPrefix(dim.Value.Value, out string prefix))
                        {
                            dim.Prefix = prefix;
                            changed++;
                        }
                        else
                        {
                            dim.Prefix = string.Empty;
                        }
                    }
                    else
                    {
                        // Цепочка размеров — обрабатываем каждый сегмент
                        foreach (DimensionSegment seg in dim.Segments)
                        {
                            if (seg.Value.HasValue &&
                                TryBuildPrefix(seg.Value.Value, out string prefix))
                            {
                                seg.Prefix = prefix;
                                changed++;
                            }
                            else
                            {
                                seg.Prefix = string.Empty;
                            }
                        }
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("ЕС BIM", $"Обработано сегментов: {changed}");
            return Result.Succeeded;
        }

        /// <summary>
        /// Если значение кратно 200 и строго больше 200,
        /// возвращает префикс вида "200xN=".
        /// </summary>
        private static bool TryBuildPrefix(double valueInternal, out string prefix)
        {
            prefix = string.Empty;

            // Revit хранит длины во внутренних единицах (футах).
            double mm = UnitUtils.ConvertFromInternalUnits(valueInternal,
                                                           UnitTypeId.Millimeters);

            long rounded = (long)Math.Round(mm);

            if (rounded <= Min) return false;
            if (rounded % Step != 0) return false;

            long n = rounded / Step;
            prefix = $"200x{n}=";
            return true;
        }
    }
}