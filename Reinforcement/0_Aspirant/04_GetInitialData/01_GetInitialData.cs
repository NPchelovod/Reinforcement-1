using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Reinforcement.Aspirant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Reinforcement.Aspirant
{
    [Transaction(TransactionMode.Manual)]
    public class GetInitialData : IExternalCommand
    {
        /// <summary>
        /// Формируем по плитам перекрытия и колоннам наше начальное приближение балочное

        /// </summary>
        public Result Execute(
                ExternalCommandData commandData,
                ref string message,
                ElementSet elements)
        {
            InitialPlaceCreater(RevitAPI.Document);
            return Result.Succeeded;
        }
        public static Element BeamElement;
        public void InitialPlaceCreater(Document doc)
        {
            //получили все перекрытия и опорные точки
            var floors = GetOpors.GetInitialPlaces();
            if (floors.Count == 0)
            {
                //ошибка
                ErrorAnalyse.Error(null, "InitialPlaceCreater: нет плит");
                return;
            }

            // 1. Собираем пары опор → AspBeam для каждой плиты
            foreach (var floor in floors)
            {
                var opors = floor.OporsMm;
                for (int i = 0; i < opors.Count; i++)
                {
                    var floorOp1 = opors[i];
                    for (int j = i+1; j < opors.Count; j++)
                    {
                        var floorOp2 = opors[j];
                        AspBeam aspBeam = new AspBeam(floorOp1, floorOp2);
                        floor.AspBeams.Add(aspBeam);
                    }
                }
            }
            // 2. Находим типоразмер
            Element beamEl = HelperSeach.SeachElement(
         AspBeam.FamilyName, null, ElementTypeOrSymbol.Symbol, true);
            FamilySymbol symbol = beamEl as FamilySymbol;
            if (symbol == null)
            {
                ErrorAnalyse.Error(null, $"Не найден FamilySymbol '{AspBeam.FamilyName}'");
                return;
            }
            BeamElement = symbol;
            // 3. Создаём балки в ОДНОЙ транзакции — быстрее и атомарно
            int created = 0;
            using (Transaction t = new Transaction(doc, "Создание адаптивных балок"))
            {
                t.Start();
                //пытаемся построить
                foreach (var floor in floors)
                {
                    foreach (var beam in floor.AspBeams)
                    {
                        if (beam.ElementId != null) continue; // уже создан

                        // ⚠️ Правильное извлечение точек через PairPoints
                        var pair = beam[ETypeCalcXYZ.Initial];
                        if (pair == null) continue;

                        XYZ startMm = pair[ETypeXYZ.Start];
                        XYZ endMm = pair[ETypeXYZ.End];
                        if (startMm == null || endMm == null) continue;

                        // Создаём
                        FamilyInstance inst = AdaptiveBeamCreator.CreateAdaptiveBeam(
                            doc, symbol, startMm, endMm);

                        if (inst != null)
                        {
                            beam.ElementId = inst.Id;
                            created++;
                        }
                    }
                }

                t.Commit();
            }
                
            
        }
    }

       


}
