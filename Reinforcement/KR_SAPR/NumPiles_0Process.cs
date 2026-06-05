using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    public partial class NumPiles
    {
        private Result ProcessPiles(
                HashSet<Element> Seacher,
                ExternalCommandData commandData,
                Document doc)
        {
            Result result = new Result();
            //если надо повернуть сваи
            if (RotorPiles)
            {
                result = NumPilesRotate.RotatePiles(doc, Seacher);
                
            }

            ForgeTypeId units = UnitTypeId.Millimeters;
            //var DictPiles = new Dictionary<(int Xs, int Ys), D<Element>>();

            var DictSector = new Dictionary<(int Xs, int Ys, string name), HashSet<PileData>>(); // сектор и имя сваи

            if (sectorStepPile < 1)
            {
                sectorStepPile = 10;
            }
            if (sectorStep < 1)
            {
                sectorStep = 10;
            }
            if (sectorStepZ < 1)
            {
                sectorStepZ = 50;
            }
            if (predelGroup < 0)
            {
                predelGroup = 1;
            }

            //QuickUGOAudit(doc);
            bool sortPilePoUgo = sortCode.Contains("0");
            if (ustanUGO || sortPilePoUgo)
            {
                // Инициализируем кэш типов УГО один раз для этого документа
                InitializeUgoCache(doc);
            }
            bool sortPoComment = sortCode.Contains("8");
            //if(! ustanUGO&& !ustanNumPile)
            //{
            //    return Result.Succeeded;
            //}

            
            var AllPiles = new HashSet<PileData>();

            foreach (Element pile in Seacher)
            {
                // получаем координаты
                LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
                if (tek_locate == null) continue; // Добавьте проверку
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

                double coord_X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                int Xs = (int)Math.Round(coord_X / sectorStep);
                int Ys = (int)Math.Round(coord_Y / sectorStep);
                int Zs = (int)Math.Round(coord_Z / sectorStepZ);

                //шаг округления координат свай в одном кусте
                int Xs2 = (int)Math.Round(coord_X / sectorStepPile);
                int Ys2 = (int)Math.Round(coord_Y / sectorStepPile);
                int Zs2 = (int)Math.Round(coord_Z / sectorStepPile);

                string name = pile.Name;

                var PileClass = new PileData(pile, Xs, Ys, Zs, Xs2, Ys2, Zs2, coord_X, coord_Y, coord_Z, name, -1, null);

                AllPiles.Add(PileClass);

                var sector = (Xs, Ys, name);
                if (DictSector.ContainsKey(sector))
                {
                    DictSector[sector].Add(PileClass);
                }
                else
                {
                    DictSector[sector] = new HashSet<PileData> { PileClass };
                }

                int comment = 0;
                if (sortPoComment)
                {
                    var comParam = pile.LookupParameter("Комментарии");
                    if (comParam != null && comParam.HasValue)
                    {
                        string cp = comParam.AsString();
                        if (!string.IsNullOrEmpty(cp))
                        {
                            if (!int.TryParse(comParam.AsString(), out comment))
                            {
                                comment = comParam.AsString().Length;
                            }
                            PileClass.comentDouble = comment;
                        }

                    }
                }
                if (doNotRenumberNumberedPiles)
                {
                    var markParam = pile.LookupParameter(Marka);
                    if (markParam != null && markParam.HasValue)
                    {
                        var oldMarkValue = markParam.AsString();
                        if (Int32.TryParse(oldMarkValue, out int numValue))
                        {
                            PileClass.pastNum = numValue;
                        }
                        // можно дубли еще посмотреть
                    }
                }
                //надо получить уго так то
                if (doNotChangeUGOIfExist || sortPilePoUgo)
                {
                    Parameter UGOParam = pile.LookupParameter(nameYGO);
                    if (UGOParam != null && UGOParam.HasValue)
                    {
                        //с уго сложно 
                        string ugoValue = UGOParam.AsValueString();
                        if (!string.IsNullOrEmpty(ugoValue))
                        {
                            PileClass.PilesNamePastYgo= ugoValue;

                            Match match = Regex.Match(ugoValue, @"\d+");
                            int number = -1;
                            if (match.Success)
                            {
                                number = int.Parse(match.Value);

                                // иначе заново ставим
                                if (!recreateAllPiles)
                                {
                                    PileClass.pastUGONum = true;
                                }
                            }
                        }

                    }

                }

            // Корректируем координаты свай если нужно
            if (adjustPilePositions && minDistanceBetweenPiles > 0)
            {
                // Получаем настройки из окна
                bool applyRounding = coordinateRoundingStep > 0;
                // Вызываем метод корректировки
                var HashIPileCorrect = new HashSet<IPileCorrect>(AllPiles).ToHashSet();                // новый HashSet<PileDataCorrect>
                HashIPileCorrect = SetPilesByDWG.MethodDepthPile(
                    HashIPileCorrect,
                    minDistanceBetweenPiles,
                    coordinateRoundingStep,
                    applyRounding
                );

                // Обновляем физические позиции свай в Revit
                if (!recreateAllPiles)
                {
                    //иначе они итак обновятся
                    UpdatePilePositionsInRevit(HashIPileCorrect.Where(p => p is PileData).Cast<PileData>().ToList());
                }
            }

            if (recreateAllPiles)
            {
                using (Transaction recreateTrans = new Transaction(doc, "Пересоздание свай"))
                {
                    try
                    {
                        recreateTrans.Start();
                        foreach (var pile in PropertiesPiles)
                        {
                            Element ePile = pile.Pile;
                            if (ePile == null) { continue; }
                            var locationPoint = ePile.Location as LocationPoint;
                            if (locationPoint == null) continue;
                            var familyInstance = pile.Pile as FamilyInstance;
                            if (familyInstance == null) continue;
                            // Получаем уровень из старой сваи
                            var levelId = ePile.LevelId;
                            var level = doc.GetElement(levelId) as Level;
                            if (level == null) continue;
                            // Получаем тип сваи
                            var symbol = familyInstance.Symbol;
                            if (symbol == null || !symbol.IsActive)
                            {
                                if (symbol != null) symbol.Activate();
                                else continue;
                            }
                            // Вычисляем координаты
                            double newX = UnitUtils.ConvertToInternalUnits(pile.itogX, units);
                            double newY = UnitUtils.ConvertToInternalUnits(pile.itogY, units);

                            var Point = new XYZ(newX, newY, locationPoint.Point.Z);




                            var newPoint = new XYZ(newX, newY, locationPoint.Point.Z);
                            // Создаем новую сваю
                            // Создаем новую сваю
                            FamilyInstance newPile = doc.Create.NewFamilyInstance(
                                newPoint, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.Footing);

                            pile.Pile = newPile;
                            doc.Delete(ePile.Id);


                        }
                        recreateTrans.Commit();
                    }
                    catch (Exception ex)
                    {
                        recreateTrans.RollBack();
                        TaskDialog.Show("Ошибка", $"Ошибка обработки свай: {ex.Message}");
                        return Result.Failed;
                    }
                }

            }






            //сортируем все имена в порядке возрастания
            var ListNamesPiles = PileNameSorter.SortPileNamesByLength(allNamesPile);
            //создаем список для словаря
            var listForYgoSort = new List<(string name, int numName, int Zs, int numPile)>();
            foreach (var ygoData in ygoIndexDict)
            {
                listForYgoSort.Add((ygoData.Key.name, ListNamesPiles.IndexOf(ygoData.Key.name), ygoData.Key.Zs, ygoData.Value.numPile));
            }
            //по номеру имени сваи по кол-ву свай одного имени и затем по высотной отметке
            //var listDataTypeYGO = listForYgoSort.OrderBy(p => p.numName).ThenByDescending(p => p.numPile).ThenBy(p => p.Zs).ToList();

            var listDataTypeYGO = sortedUGO(listForYgoSort, sortCodeUGO, ustanUGO);
            //получение УГО потенциального
            // var listDataTypeYGO = HashDataTypeYGO.ToList();
            //теперь заполняем словарь свойства




            numUGO = 1;
            for (int i = 0; i < listDataTypeYGO.Count; i++)
            {
                while (hashZapretUGO.Contains(numUGO))
                {
                    numUGO++;
                }
                var tekYGO = listDataTypeYGO[i];
                if (ygoIndexDict.TryGetValue((tekYGO.name, tekYGO.Zs), out var past))
                {
                    if (past.nomer < 1)
                    {
                        ygoIndexDict[(tekYGO.name, tekYGO.Zs)] = (numUGO, past.numPile);
                        hashZapretUGO.Add(numUGO);
                    }
                }

            }
            //foreach (var kvp in PropertiesPiles)
            //{
            //    if (ygoIndexDict.TryGetValue((kvp.Name, kvp.Zs), out var ugoData))
            //    {
            //        kvp.PilesYGO = ugoData.nomer;
            //    }
            //}




            //создаем группы свай

            var ListPilesGroup = IntersectSectors(PropertiesPiles, sectorStep, namePileAndNum, ListNamesPiles, predelGroup, sortPilePoUgo).ToList();


            //сортировка групп свай
            //теперь сортируем сначала по оси x идя по оси y

            ListPilesGroup = sortedCodNumPile(ustanNumPile, sortCode, ListPilesGroup);
            bool yxSort = true;
            if (sortCode != null && sortCode.Contains("2") && !sortCode.Contains("1"))
            {
                yxSort = false;
            }
            //контроль свай
            control3D(PropertiesPiles, SizePile3D);
            // Нумерация свай
            bool inversSort = false;
            if (sortCode != null && sortCode.Contains("7"))
            {
                //нумерация свай сверху вниз
                inversSort = true;
            }







            int kust = 0;
            numPile++;
            HashSet<int> zapretNumPile = new HashSet<int>();
            HashSet<PileData> pastPileData = new HashSet<PileData>();
            foreach (var classPile in ListPilesGroup)
            {
                kust++;
                //сваи одной группы
                var allPilesGroup = classPile.Piles.ToList();
                if (ustanNumPile && allPilesGroup.Count > 0)//накладно ведь каждый раз
                {
                    //сваи сортируем по секторам позволяющим в один ряд их укладывать
                    if (yxSort)
                    {
                        if (!inversSort)
                        {
                            allPilesGroup = allPilesGroup
                            .OrderBy(pile => pile.Ys2) // по убыванию Y (сверху вниз)
                            .ThenBy(pile => pile.Xs2)
                            .ThenBy(pile => pile.Ys3)
                            .ThenBy(pile => pile.Xs3)
                            .ToList();// по возрастанию X (слева направо)
                        }
                        else
                        {
                            allPilesGroup = allPilesGroup
                            .OrderByDescending(pile => pile.Ys2) // по убыванию Y (сверху вниз)
                            .ThenBy(pile => pile.Xs2)
                            .ThenByDescending(pile => pile.Ys3)
                            .ThenBy(pile => pile.Xs3)
                            .ToList();// по возрастанию X (слева направо)
                        }
                    }
                    else
                    {
                        if (!inversSort)
                        {
                            allPilesGroup = allPilesGroup
                            .OrderBy(pile => pile.Xs2) // по убыванию Y (сверху вниз)
                            .ThenBy(pile => pile.Ys2)         // по возрастанию X (слева направо)
                            .ThenBy(pile => pile.Xs3)
                            .ThenBy(pile => pile.Ys3)
                            .ToList();
                        }
                        else
                        {
                            allPilesGroup = allPilesGroup
                            .OrderBy(pile => pile.Xs2) // по убыванию Y (сверху вниз)
                            .ThenByDescending(pile => pile.Ys2)         // по возрастанию X (слева направо)
                            .ThenBy(pile => pile.Xs3)
                            .ThenByDescending(pile => pile.Ys3)
                            .ToList();
                        }
                    }
                }


                foreach (var pile in allPilesGroup)
                {
                    if (pastPileData.Contains(pile)) { continue; }
                    pastPileData.Add(pile);

                    int x = (int)Math.Round(pile.X);
                    int y = (int)Math.Round(pile.Y);

                    if (!doNotRenumberNumberedPiles || pile.pastNum <= 0)
                    {
                        while (zapretNumPile.Contains(numPile))
                        {
                            numPile++;
                        }
                        pile.NumPile = numPile;
                        //numPile++;
                    }
                    else if (pile.pastNum > 0)
                    {
                        while (zapretNumPile.Contains(pile.pastNum))
                        {
                            pile.pastNum++;
                        }

                        pile.NumPile = pile.pastNum;

                        //numPile--;
                    }
                    zapretNumPile.Add(pile.NumPile);

                    string primeh = pile.NumPile + ", УГО_" + pile.PilesYGO + ", КУСТ_" + kust + ", X=" + x + ", Y=" + y;
                    if ((x + y) % 50 > 0)
                    {
                        primeh += " неКратКоорд.";
                        if (x % 5 > 0 || y % 5 > 0)
                        {
                            primeh += "!";
                        }
                    }
                    if (pile.intersect3D > 0)
                    {
                        primeh += " Пересеч. " + pile.intersect3D + " мм";
                    }
                    pile.Commit = primeh;



                }

            }


            // Начинаем транзакцию для установки марок



            using (Transaction trans2 = new Transaction(doc, "Пересоздание свай"))
            {
                try
                {
                    trans2.Start();

                    int successCount = 0;
                    int failCount = 0;

                    int successCount2 = 0;
                    int failCount2 = 0;

                    var failedElements = new List<ElementId>();
                    var failedElements2 = new List<ElementId>();
                    bool resultNum = false;
                    bool resultUGO = false;

                    foreach (var kvp in PropertiesPiles)
                    {
                        Element pile = kvp.Pile;
                        int markValue = kvp.NumPile;


                        string primeh = kvp.Commit;
                        if (primeh != "" && WriterPrimech)
                        {
                            //установка примечание
                            SetPileMark(pile, primeh, namePrimech);
                        }

                        if (ustanNumPile)
                        {
                            if (!doNotRenumberNumberedPiles || kvp.pastNum < 0 || recreateAllPiles)// чтобы заново номер присвоить
                            {


                                resultNum = SetPileMark(pile, markValue.ToString(), nameMarks);
                            }

                        }
                        if (ustanUGO && kvp.PilesYGO > 0)
                        {
                            if (!doNotChangeUGOIfExist || !kvp.pastUGONum || recreateAllPiles)
                            {
                                string YGOValue = YGOPrefix + kvp.PilesYGO;
                                resultUGO = SetUGOValue(doc, pile, kvp.PilesYGO);
                            }
                            //string YGOValue = YGOPrefix + kvp.PilesYGO;
                            //resultUGO = SetUGOValue(pile, kvp.PilesYGO);
                            //FamilyInstance pileInstance = pile as FamilyInstance;
                            //if (pileInstance != null)
                            //{
                            //    resultUGO = SetUGOValue(doc, pile as FamilyInstance, kvp.PilesYGO);
                            //}
                            //resultUGO = SetUGOValue(doc, pile, kvp.PilesYGO);
                            //SetUGOValue(Document doc, FamilyInstance pileInstance, int ygoIndex)
                            //resultUGO = SetYGO(pile, kvp.PilesYGO);
                            //resultUGO = SetPileMark(pile, YGOValue, nameYGO); 
                        }

                        // Проверяем оба результата в зависимости от настроек
                        if (ustanNumPile && !resultNum)
                        {
                            failCount++;
                            failedElements.Add(pile.Id);
                        }
                        else if (ustanNumPile && resultNum)
                        {
                            successCount++;
                        }

                        if (ustanUGO && !resultUGO)
                        {
                            failCount2++;
                            failedElements2.Add(pile.Id);
                        }
                        else if (ustanUGO && resultUGO)
                        {
                            successCount2++;
                        }


                    }

                    trans2.Commit();

                    // Показываем результат
                    // Показ результата
                    if (ustanNumPile || ustanUGO)
                    {
                        string resultMessage = $"Всего свай: {PropertiesPiles.Count}\n";
                        if (ustanNumPile)
                        {
                            resultMessage += $"Установлено марок: {successCount}\nНе удалось: {failCount}\n";
                        }
                        if (ustanUGO)
                        {
                            resultMessage += $"Установлено УГО: {successCount2}\nНе удалось: {failCount2}\n";
                        }

                        //resultMessage  +=$"Всего свай: {PropertiesPiles.Count}";

                        //if (failedElements.Count > 0)
                        //{
                        //    resultMessage += $"\n\nСписок ID неудачных элементов (первые 10):\n";
                        //    resultMessage += string.Join("\n", failedElements.Take(10).Select(id => id.IntegerValue));

                        //    if (failedElements.Count > 10)
                        //    {
                        //        resultMessage += $"\n... и еще {failedElements.Count - 10} элементов";
                        //    }
                        //}
                        TaskDialog.Show("Результат", resultMessage);
                    }


                    // Дополнительно: создаем отчет о нумерации
                    //CreateNumberingReport(PropertiesPiles, ListPilesGroup);

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    trans2.RollBack();
                    TaskDialog.Show("Ошибка транзакции", $"Ошибка при установке марок: {ex.Message}");
                    return Result.Failed;
                }
            }
        }
    }
}
