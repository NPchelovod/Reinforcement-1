using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement.Aspirant
{
    [Transaction(TransactionMode.Manual)]
    public class CreateGroundColumn : IExternalCommand
    {
        private const string FamilyName = "ЕСБалкаАдапV1";
        private const string NameParam = "ЕС_Имя";
        private const string ColumnNumParam = "ЕС_Номер колонки";
        private const double FtToMm = 304.8;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData?.Application?.ActiveUIDocument?.Document;
            if (doc == null) { message = "Нет активного документа."; return Result.Failed; }

            double pbpElevationM = 0.0;
            try
            {
                ProjectPosition pp = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                pbpElevationM = pp.Elevation * FtToMm / 1000.0;
            }
            catch { }

            var win = new GroundLayersWindow(pbpElevationM);
            if (win.ShowDialog() != true) return Result.Cancelled;

            Element beamEl = HelperSeach.SeachElement(
                FamilyName, null, ElementTypeOrSymbol.Symbol, true);
            FamilySymbol symbol = beamEl as FamilySymbol;
            if (symbol == null)
            {
                ErrorAnalyse.Error(null, $"Не найден FamilySymbol '{FamilyName}'");
                return Result.Failed;
            }

            double xMm = win.XMeters * 1000.0;
            double yMm = win.YMeters * 1000.0;
            double zTopMm = win.EffectiveZ1Meters * 1000.0;

            int created = 0, paramErrors = 0;

            using (Transaction t = new Transaction(doc, "Создание грунтовой колонки"))
            {
                t.Start();

                foreach (var layer in win.Layers)
                {
                    double zBotMm = zTopMm - layer.ThicknessM * 1000.0;

                    XYZ startMm = new XYZ(xMm, yMm, zTopMm);
                    XYZ endMm = new XYZ(xMm, yMm, zBotMm);

                    FamilyInstance inst = AdaptiveBeamCreator.CreateAdaptiveBeam(
                        doc, symbol, startMm, endMm);

                    if (inst != null)
                    {
                        if (!TrySetString(inst, NameParam, layer.Name)) paramErrors++;
                        if (!TrySetString(inst, ColumnNumParam, win.ColumnNumber)) paramErrors++;
                        created++;
                    }
                    zTopMm = zBotMm;
                }

                t.Commit();
            }

            string msg =
                $"Создано: {created} из {win.Layers.Count} слоёв.\n" +
                $"Номер колонки: {win.ColumnNumber}\n" +
                $"X, Y: ({win.XMeters:0.###}, {win.YMeters:0.###}) м\n" +
                $"Z1 (ввод): {win.Z1Meters:0.###} м\n" +
                $"Сдвиг: {win.OffsetMeters:+0.###;-0.###;0} м\n" +
                $"Position.Z верхней точки: {win.EffectiveZ1Meters:0.###} м";
            if (paramErrors > 0)
                msg += $"\n⚠ Не удалось записать параметры у {paramErrors} значений.";

            TaskDialog.Show("Грунтовая колонка", msg);
            return Result.Succeeded;
        }

        private static bool TrySetString(Element el, string name, string value)
        {
            Parameter p = el.LookupParameter(name);
            if (p == null || p.IsReadOnly) return false;
            if (p.StorageType != StorageType.String) return false;
            p.Set(value ?? "");
            return true;
        }
    }
}