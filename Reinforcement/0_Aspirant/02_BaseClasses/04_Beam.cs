using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    /// <summary>Тип конца пары точек.</summary>
    public enum ETypeXYZ
    {
        /// <summary>Начальная точка (в мм).</summary>
        Start = 0,
        /// <summary>Конечная точка (в мм).</summary>
        End = 1
    }

    /// <summary>Состояние координат: исходные или после расчёта.</summary>
    public enum ETypeCalcXYZ
    {
        /// <summary>Исходные координаты (в мм).</summary>
        Initial = 0,
        /// <summary>Координаты после расчёта (в мм).</summary>
        Calculation = 1
    }
    /// <summary>
    /// Пара точек (начало/конец) с быстрым доступом по ETypeXYZ.
    /// Внутри — массив из двух XYZ; доступ без хеширования.
    /// </summary>
    public class PairPoints
    {
        private const int PointCount = 2;
        private readonly XYZ[] _points = new XYZ[PointCount];

        /// <summary>Начальная точка в мм.</summary>
        public XYZ StartMm { get => _points[0]; set => _points[0] = value; }

        /// <summary>Конечная точка в мм.</summary>
        public XYZ EndMm { get => _points[1]; set => _points[1] = value; }

        /// <summary>Доступ по типу конца (Start/End).</summary>
        public XYZ this[ETypeXYZ type]
        {
            get => _points[(int)type];
            set => _points[(int)type] = value;
        }

        public PairPoints() { }

        /// <summary>Создаёт пару с заданными точками.</summary>
        public PairPoints(XYZ startMm, XYZ endMm)
        {
            _points[0] = startMm;
            _points[1] = endMm;
        }

        /// <summary>true, если обе точки заданы.</summary>
        public bool IsComplete => _points[0] != null && _points[1] != null;
    }

    /// <summary>
    /// Адаптивная балка: хранит пары точек для исходного и расчётного состояния.
    /// Внутри — массив PairPoints[2], индексируемый по (int)ETypeCalcXYZ.
    /// </summary>
    public class AspBeam
    {
        public const string FamilyName = "ЕСБалкаАдапV1";
        private const int CalcStateCount = 2;

        private readonly PairPoints[] _pairs = new PairPoints[CalcStateCount];

        /// <summary>ElementId созданной балки; null, если балка ещё не создана.</summary>
        public ElementId ElementId { get; set; }=null;

        /// <summary>Создаёт балку с исходными координатами.</summary>
        public AspBeam(XYZ startMm, XYZ endMm)
        {
            _pairs[(int)ETypeCalcXYZ.Initial] = new PairPoints(startMm, endMm);
        }

        /// <summary>
        /// Доступ к паре точек по состоянию расчёта.
        /// Возвращает null, если для этого состояния пара ещё не задана.
        /// </summary>
        public PairPoints this[ETypeCalcXYZ type = ETypeCalcXYZ.Initial]
        {
            get => _pairs[(int)type];
            set => _pairs[(int)type] = value;
        }

        /// <summary>Исходные координаты (никогда не null после конструктора).</summary>
        public PairPoints Initial => _pairs[(int)ETypeCalcXYZ.Initial];

        /// <summary>Расчётные координаты; null, пока не посчитаны.</summary>
        public PairPoints Calculation => _pairs[(int)ETypeCalcXYZ.Calculation];

        /// <summary>true, если расчётные координаты уже заданы.</summary>
        public bool HasCalculation => _pairs[(int)ETypeCalcXYZ.Calculation] != null;

        /// <summary>
        /// Пытается получить пару без бросания исключения.
        /// Удобно, когда состояние может отсутствовать.
        /// </summary>
        public bool TryGet(ETypeCalcXYZ type, out PairPoints pair)
        {
            pair = _pairs[(int)type];
            return pair != null;
        }
    }


    public static class AdaptiveBeamCreator
    {
        /// <summary>
        /// Создаёт экземпляр адаптивного семейства по двум точкам (мм) и задаёт
        /// позиции размещаемых точек. Должен вызываться ВНУТРИ открытой транзакции.
        /// </summary>
        /// <param name="doc">Документ.</param>
        /// <param name="symbol">Типоразмер адаптивного семейства (уже загруженного).</param>
        /// <param name="startMm">Начало в мм.</param>
        /// <param name="endMm">Конец в мм.</param>
        /// <returns>Созданный FamilyInstance либо null при ошибке.</returns>
        public static FamilyInstance CreateAdaptiveBeam(
            Document doc,
            FamilySymbol symbol,
            XYZ startMm,
            XYZ endMm)
        {
            if (doc == null || symbol == null || startMm == null || endMm == null)
                return null;

            // Активируем типоразмер (иначе первый экземпляр создастся без геометрии)
            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            // 1. Создаём экземпляр
            FamilyInstance instance = AdaptiveComponentInstanceUtils
                .CreateAdaptiveComponentInstance(doc, symbol);
            if (instance == null) return null;

            // 2. Получаем ссылки на размещаемые точки
            IList<ElementId> pointIds = AdaptiveComponentInstanceUtils
                .GetInstancePlacementPointElementRefIds(instance);
            if (pointIds == null || pointIds.Count < 2) return instance;

            // 3. мм → футы (Revit работает в футах)
            XYZ startFt = startMm.ToFoot();
            XYZ endFt = endMm.ToFoot();

            // 4. Назначаем позиции
            ReferencePoint rpStart = doc.GetElement(pointIds[0]) as ReferencePoint;
            ReferencePoint rpEnd = doc.GetElement(pointIds[1]) as ReferencePoint;

            if (rpStart != null) rpStart.Position = startFt;
            if (rpEnd != null) rpEnd.Position = endFt;

            return instance;
        }
    }
}
