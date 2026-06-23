using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using System.Security.Cryptography;
namespace Reinforcement
{
    public partial class NumPiles
    {
        List<PileDataGroup> pileDataGroup = new List<PileDataGroup>();

       

        public Result CalculateMarks()
        {
            //нам надо собрать все сваи в группы по важности чтобы потом сортировать
            SortPileImportent();//сначала по важности сортируем

            //List<PileData> allPiles = new List<PileData>();

            
            if (GroupPiles)
            {
                //группировка
                foreach (var pg in pileDataGroup)
                {
                    pg.CutPileOnGroop(SectorStep);
                    
                }
            }
            

            // и теперь эти группы должны в один список 
            

            var listCD = new List<CoordData>();//это если группировка есть - список групп свай иначе список свай
            if (BoolNumPileIandex)
            {
                foreach (var pg in pileDataGroup)
                {
                    foreach (var ns in pg.NestedCoordData)
                    {
                        listCD.Add(ns);
                    }
                }

                listCD = OpenTspSolver.Solve(new List<CoordData>(listCD), TimeSpan.FromSeconds((double)AllPiles.Count / 1000.0 * 15));
                //отсортированный возвращаем
                foreach (var ns in listCD)
                {
                    ns.SortNestedCoordData();
                }

            }
            else
            {
                listCD = new List<CoordData>(pileDataGroup);
                foreach (var pg in pileDataGroup)
                {
                    pg.SortNestedCoordData(); //сортируем внутри группу или сваи
                    foreach (var ns in pg.NestedCoordData)
                    {
                        ns.SortNestedCoordData();
                    }
                }
            }


            int mark = MarkStart-1;
            List< PileData> allPiles = GetPileData(listCD);

            foreach (var pile in allPiles)
            {
                if (pile.MarkNew==0)//тут и на 0 так как дубляжи реально возможны...
                {
                    mark++;

                    pile.MarkNewString = MarkPrefix + mark + MarkPostfix;
                    pile.MarkNew = mark;
                    
                }
            }

            //устанавливаем марку нашу
            int ustanMarok = 0;
            using (Transaction trans2 = new Transaction(Document, "Установка Марки"))
            {
                try
                {
                    trans2.Start();
                    foreach (var pileClass in allPiles)
                    {
                        Element pile = pileClass.Pile;
                        if (pile == null || pileClass.MarkNewString == "") {continue;}
                        
                        if( SetPileMark(pile, pileClass.MarkNewString, nameMarks))
                        {
                            ustanMarok++;
                        }

                    }
                    trans2.Commit();
                    string resultMessage = $"Всего свай: {AllPiles.Count}\n";
                    resultMessage += $"Установлено марок: {ustanMarok}\n";
                    TaskDialog.Show("Результат", resultMessage);
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
        public List<PileData> GetPileData(List<CoordData> coordList, List<PileData> answer=null)
        {
            //рекурсивное нахождение сваи
            if(answer== null) answer = new List<PileData>();

            bool selfP = false; // для надежности только одну кучу смотрим
            bool notselfP = false;
            foreach (var coord in coordList)
            {
                if(!notselfP && coord is PileData pile)
                {
                    answer.Add(pile);
                    selfP= true;
                }
                else if(!selfP)
                {
                    notselfP= true;
                    answer.AddRange(GetPileData(coord.NestedCoordData));
                }
            }
            return answer;
        }

        public void SortPileImportent()
        {
            pileDataGroup.Clear();
            //нам надо собрать все сваи в группы по важности и по  и по типу сваи были и по z разные
            foreach (var pile in AllPiles)
            {
                List<string> SravnList = pile.GetSravnDataString();
                bool set = false;
                foreach (PileDataGroup pileDataGroops in pileDataGroup)
                {
                    if (pileDataGroops.SravnList.SequenceEqual(SravnList))
                    {
                        pileDataGroops.NestedCoordData.Add(pile);
                        pile.Father = pileDataGroops;
                        set = true;
                        break;
                    }
                }
                if (!set)
                {
                    var pg = new PileDataGroup(SravnList);
                    pg.NestedCoordData.Add(pile);
                    pile.Father = pg;
                    pileDataGroup.Add(pg);
                }
            }
            if (pileDataGroup.Count == 0) { return; }

            //сортировка группы по её свойствам
            pileDataGroup = PileDataGroup.SortNestedCoordData(NumPiles.sortCodeEnums, pileDataGroup.Cast<CoordData>().ToList()).Cast<PileDataGroup>().ToList(); 

            //pileDataGroup = CalcSort(pileDataGroup.ToList(), NumPiles.sortCodeEnums);

            //отсортированный возвращаем
            
            int NumWay = 0;
            foreach (var pileGroup in pileDataGroup)
            {
                NumWay++;
                pileGroup.NumWay = NumWay;
                foreach (var pile in pileGroup.NestedCoordData)
                {
                    pile.NumWay = NumWay;
                }
            }
        }

        
        //public List<CoordData> CalcSortPileData(List<CoordData> sortDatas, string sortCod)
        //{
        //    //если не по алгоритму яндекс карта
        //    var sorted = sortDatas.OrderBy(x => x.NumWay);
        //    foreach (char codeChar in sortCode)
        //    {
        //        switch (codeChar)
        //        {
        //            case '1':
        //                if (!sortCode.Contains("7"))
        //                {
        //                    sorted = sorted.ThenBy(x => x.Ys).ThenBy(x => x.Xs);
        //                }
        //                else
        //                {
        //                    sorted = sorted.OrderBy(x => x.Ys).ThenBy(x => x.Xs);
        //                }
        //                    break;
        //            case '2':
        //                sorted = sorted.ThenBy(x => x.X);
        //                if (!sortCode.Contains("7"))
        //                {
        //                    sorted = sorted.ThenBy(x => x.Ys);
        //                }
        //                else
        //                {
        //                    sorted = sorted.OrderBy(x => x.Ys);
        //                }
        //                break;

        //        }
        //    }
        //    return sorted.ToList();
        //}
    }
    
}
