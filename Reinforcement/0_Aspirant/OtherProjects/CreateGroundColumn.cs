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
        private const double DefaultX_Mm = 0.0;
        private const double DefaultY_Mm = 0.0;
        private const double FtToMm = 304.8;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData?.Application?.ActiveUIDocument?.Document;
            if (doc == null)
            {
                message = "Нет активного документа.";
                return Result.Failed;
            }

            // 1. PBP читаем только для справки в окне — расчёт Position.Z
            //    полностью на сдвиге, который пользователь подтвердил в окне.
            double pbpElevationM = 0.0;
            try
            {
                ProjectPosition pp = doc.ActiveProjectLocation
                    .GetProjectPosition(XYZ.Zero);
                pbpElevationM = pp.Elevation * FtToMm / 1000.0;
            }
            catch { /* среда без PBP — игнорируем */ }

            // 2. Окно
            var win = new GroundLayersWindow(pbpElevationM);
            if (win.ShowDialog() != true) return Result.Cancelled;

            // 3. Типоразмер
            Element beamEl = HelperSeach.SeachElement(
                FamilyName, null, ElementTypeOrSymbol.Symbol, true);
            FamilySymbol symbol = beamEl as FamilySymbol;
            if (symbol == null)
            {
                ErrorAnalyse.Error(null,
                    $"Не найден FamilySymbol '{FamilyName}'. " +
                    $"Проверьте, что семейство загружено в проект " +
                    $"и имя совпадает точно.");
                return Result.Failed;
            }

            // 4. Position.Z верхней точки.
            //    EffectiveZ1Meters = Z1Meters + OffsetMeters — уже готовая
            //    координата в метрах. PBP не вычитаем повторно: сдвиг его
            //    уже учитывает (инициализируется как −PBP.Elevation).
            double zTopMm = win.EffectiveZ1Meters * 1000.0;

            int created = 0, paramErrors = 0;

            using (Transaction t = new Transaction(doc, "Создание грунтовой колонки"))
            {
                t.Start();

                foreach (var layer in win.Layers)
                {
                    double zBotMm = zTopMm - layer.ThicknessM * 1000.0;

                    XYZ startMm = new XYZ(DefaultX_Mm, DefaultY_Mm, zTopMm);
                    XYZ endMm = new XYZ(DefaultX_Mm, DefaultY_Mm, zBotMm);

                    FamilyInstance inst = AdaptiveBeamCreator.CreateAdaptiveBeam(
                        doc, symbol, startMm, endMm);

                    if (inst != null)
                    {
                        if (!TrySetString(inst, NameParam, layer.Name))
                            paramErrors++;
                        if (!TrySetString(inst, ColumnNumParam, win.ColumnNumber))
                            paramErrors++;
                        created++;
                    }
                    zTopMm = zBotMm;
                }

                t.Commit();
            }

            // 5. Отчёт
            string msg =
                $"Создано: {created} из {win.Layers.Count} слоёв.\n" +
                $"Номер колонки: {win.ColumnNumber}\n" +
                $"Z1 (ввод): {win.Z1Meters:0.###} м\n" +
                $"Сдвиг: {win.OffsetMeters:+0.###;-0.###;0} м\n" +
                $"Position.Z верхней точки: {win.EffectiveZ1Meters:0.###} м\n" +
                $"Справка: PBP.Elevation = {pbpElevationM:0.###} м\n" +
                $"Ожидаемая отметка в Revit ≈ {win.Z1Meters:0.###} м";

            if (paramErrors > 0)
                msg += $"\n⚠ Не удалось записать параметры у {paramErrors} значений. " +
                       $"Проверьте, что '{NameParam}' и '{ColumnNumParam}' — " +
                       $"параметры ЭКЗЕМПЛЯРА типа String.";

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