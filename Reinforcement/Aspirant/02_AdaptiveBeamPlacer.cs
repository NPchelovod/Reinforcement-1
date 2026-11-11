using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace AspNamespace
{
    public class AdaptiveBeamPlacer
    {
        private const string FamilyName = "ЕСБалкаАдапV1";
        private const double MmToFeet = 1.0 / 304.8; // Revit работает в футах

        /// <summary>
        /// Размещает экземпляр адаптивного стержня между двумя узлами.
        /// Координаты задаются в миллиметрах в системе координат проекта.
        /// </summary>
        /// <param name="doc">Документ Revit</param>
        /// <param name="startMm">Начало стержня в мм</param>
        /// <param name="endMm">Конец стержня в мм</param>
        /// <param name="familyPath">Путь к .rfa — нужен, если семейство ещё не загружено. Можно null, если уже в проекте.</param>
        public FamilyInstance PlaceAdaptiveBeam(Document doc, XYZ startMm, XYZ endMm, string familyPath = null)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            // 1. Найти или загрузить типоразмер
            FamilySymbol symbol = GetOrLoadAdaptiveSymbol(doc, familyPath);
            if (symbol == null)
                throw new InvalidOperationException(
                    $"Не удалось найти или загрузить семейство '{FamilyName}'. Укажите корректный путь к .rfa.");

            // 2. Транзакция: активация + размещение + назначение координат
            using (Transaction t = new Transaction(doc, "Размещение адаптивного стержня"))
            {
                t.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                // 3. Создать экземпляр адаптивного компонента
                FamilyInstance instance = AdaptiveComponentInstanceUtils
                    .CreateAdaptiveComponentInstance(doc, symbol);

                // 4. Получить ссылки на размещаемые (placement) точки
                IList<ElementId> pointIds = AdaptiveComponentInstanceUtils
                    .GetInstancePlacementPointElementRefIds(instance);

                if (pointIds.Count < 2)
                    throw new InvalidOperationException(
                        $"Семейство содержит {pointIds.Count} адаптивных точек, ожидалось минимум 2.");

                // 5. Сконвертировать мм → футы и назначить позиции
                XYZ[] coordsFt = { MmToFeet3(startMm), MmToFeet3(endMm) };

                for (int i = 0; i < coordsFt.Length && i < pointIds.Count; i++)
                {
                    if (doc.GetElement(pointIds[i]) is ReferencePoint rp)
                    {
                        rp.Position = coordsFt[i];
                    }
                }

                doc.Regenerate();
                t.Commit();
                return instance;
            }
        }

        // -------- Вспомогательные методы --------

        private static XYZ MmToFeet3(XYZ mm) =>
            new XYZ(mm.X * MmToFeet, mm.Y * MmToFeet, mm.Z * MmToFeet);

        /// <summary>
        /// Ищет уже загруженный типоразмер по имени семейства или символа.
        /// Если не найден и указан путь — загружает семейство из файла.
        /// </summary>
        private FamilySymbol GetOrLoadAdaptiveSymbol(Document doc, string familyPath)
        {
            // a) уже в проекте?
            FamilySymbol found = FindSymbolByName(doc, FamilyName);
            if (found != null) return found;

            // b) файл не указан — дальше идти некуда
            if (string.IsNullOrWhiteSpace(familyPath) || !File.Exists(familyPath))
                return null;

            // c) загружаем
            Family loaded = null;
            using (Transaction t = new Transaction(doc, $"Загрузка семейства {FamilyName}"))
            {
                t.Start();
                bool ok = doc.LoadFamily(familyPath, out loaded);
                if (ok) t.Commit();
                else t.RollBack();
            }
            if (loaded == null) return null;

            // d) сначала пробуем найти по имени, иначе берём первый типоразмер
            return FindSymbolByName(doc, FamilyName)
                ?? loaded.GetFamilySymbolIds()
                         .Select(id => doc.GetElement(id) as FamilySymbol)
                         .FirstOrDefault(s => s != null);
        }

        /// <summary>
        /// Ищет FamilySymbol сначала по имени символа, затем по имени семейства.
        /// </summary>
        private FamilySymbol FindSymbolByName(Document doc, string name)
        {
            var allSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Family != null &&
                             fs.Family.FamilyPlacementType == FamilyPlacementType.Adaptive)
                .ToList();

            return allSymbols.FirstOrDefault(fs =>
                       fs.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? allSymbols.FirstOrDefault(fs =>
                       fs.Family.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    //var placer = new AdaptiveBeamPlacer();

    //    XYZ start = new XYZ(0, 0, 0);        // мм
    //    XYZ end = new XYZ(12000, 5000, 8500); // мм (пространственный наклонный стержень)

    //    FamilyInstance beam = placer.PlaceAdaptiveBeam(
    //        doc,
    //        start,
    //        end,
    //        familyPath: @"C:\Revit\Families\ЕСБалкаАдапV1.rfa"); // можно null, если уже загружено
    //}
}