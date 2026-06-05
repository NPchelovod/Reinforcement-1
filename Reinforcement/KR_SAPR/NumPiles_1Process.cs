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
        public static ForgeTypeId units = UnitTypeId.Millimeters;

        HashSet<PileData> AllPiles = new HashSet<PileData>();

        private void  ReadPiles()
        {
            AllPiles.Clear();
            foreach (Element pile in Seacher)
            {
                // получаем координаты
                LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
                
                if (tek_locate == null) continue; // Добавьте проверку
                AllPiles.Add(new PileData(pile));
               
            }
        }
        private Result ProcessPiles(
                
                ExternalCommandData commandData,
                Document doc)
        {
            Result result = new Result();
            //если надо повернуть сваи
            if (RotorPiles)
            {
                result = NumPilesRotate.RotatePiles(doc, Seacher);
            }
            //чтение всех свай
            ReadPiles();

            // Корректируем координаты свай если нужно
            if (adjustPilePositions && minDistanceBetweenPiles > 0)
            {
                // Получаем настройки из окна
                bool applyRounding = coordinateRoundingStep > 0;
            }

            if (recreateAllPiles)
            {
                //"Пересоздание свай";
                RecreatePiles.RecreatePile(doc, AllPiles);
            }

            CalculateMarks();

















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
