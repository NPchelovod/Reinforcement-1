using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    public class ColumnContourData
    {
        public ElementId ColumnId { get; set; }
        public string ColumnName { get; set; }

        /// <summary>Нижняя точка оси колонны в мм.</summary>
        public XYZ StartMm { get; set; }
        /// <summary>Верхняя точка оси колонны в мм.</summary>
        public XYZ EndMm { get; set; }
        /// <summary>Единичное направление оси (в тех же координатах).</summary>
        public XYZ Direction { get; set; }

        public double LengthMm => StartMm.DistanceTo(EndMm);



        private const double FtToMm = 304.8;

        public static List<ColumnContourData> GetAllColumns(
        Document doc,
        View view = null,
        bool structuralOnly = true)
        {
            var result = new List<ColumnContourData>();
            if (doc == null) return result;

            BuiltInCategory category = structuralOnly
                ? BuiltInCategory.OST_StructuralColumns
                : BuiltInCategory.OST_Columns;

            FilteredElementCollector collector = view != null
                ? new FilteredElementCollector(doc, view.Id)
                : new FilteredElementCollector(doc);

            var columns = collector
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();

            foreach (var fi in columns)
            {
                try
                {
                    if (!TryGetColumnAxis(fi, out XYZ startFt, out XYZ endFt))
                        continue;

                    var data = new ColumnContourData
                    {
                        ColumnId = fi.Id,
                        ColumnName = fi.Name ?? "?",
                        StartMm = ToMm(startFt),
                        EndMm = ToMm(endFt),
                    };
                    // Направление считаем в футах, чтобы избежать деления на ноль при малых длинах
                    XYZ dir = (endFt - startFt);
                    data.Direction = dir.GetLength() > 1e-9 ? dir.Normalize() : XYZ.BasisZ;
                    result.Add(data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Column {fi.Id}: {ex.Message}");
                }
            }
            return result;
        }

        private static bool TryGetColumnAxis(FamilyInstance column, out XYZ start, out XYZ end)
        {
            start = end = XYZ.Zero;

            // Основной путь: ось колонны = LocationCurve (Line)
            var locCurve = column.Location as LocationCurve;
            if (locCurve?.Curve is Line line)
            {
                start = line.GetEndPoint(0);
                end = line.GetEndPoint(1);
                // Упорядочим по Z: начало — снизу, конец — сверху
                if (start.Z > end.Z)
                {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }
                return true;
            }

            // Резерв: BoundingBox (для колонн, у которых нет LocationCurve)
            var bb = column.get_BoundingBox(null);
            if (bb == null) return false;

            double cx = (bb.Min.X + bb.Max.X) / 2.0;
            double cy = (bb.Min.Y + bb.Max.Y) / 2.0;
            start = new XYZ(cx, cy, bb.Min.Z);
            end = new XYZ(cx, cy, bb.Max.Z);
            return true;
        }

        private static XYZ ToMm(XYZ p) => new XYZ(p.X * FtToMm, p.Y * FtToMm, p.Z * FtToMm);

    }
}
