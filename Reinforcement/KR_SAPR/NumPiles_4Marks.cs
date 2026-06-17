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

            var listCD = new List<CoordData>();//это если группировка есть - список групп свай иначе список свай
            if (GroupPiles)
            {
                //группировка
                foreach (var pg in pileDataGroup)
                {
                    pg.CutPileOnGroop();
                }
            }

            // и теперь эти группы должны в один список 
            foreach (var pg in pileDataGroup)
            {
                foreach (var ns in pg.NestedCoordData)
                {
                    ns.NumWay = pg.NumWay;//на всякий случай
                    listCD.Add(ns);
                }
            }

            if (BoolNumPileIandex)
            {

                listCD = OpenTspSolver.Solve(listCD, TimeSpan.FromSeconds((double)AllPiles.Count/ 1000.0*15));
                //отсортированный возвращаем
                var allpSpisok = new List<CoordData>();
                foreach (var pg in listCD) //это если группировка есть - список групп свай иначе список свай
                {
                    
                    if (pg is PileData pileData)
                    {
                        allpSpisok.Add(pg);
                    }
                    else
                    {
                        //вложенная группировка
                        var listInsider = OpenTspSolver.Solve(pg.NestedCoordData, TimeSpan.FromSeconds(3));
                        foreach (var ns2 in listInsider)
                        {
                            if (ns2 is PileData pileData2)
                            {
                                allpSpisok.Add(ns2);
                            }
                        }
                    }
                    
                }
                listCD = allpSpisok;
            }
            else
            {
                listCD.Clear();
                foreach (var pg in pileDataGroup)
                {
                    pg.SortNestedCoordData(); //сортируем внутри группу или сваи
                    foreach (var ns in pg.NestedCoordData)
                    {
                        if(ns is PileData pileData)
                        {
                            listCD.Add(ns);
                        }
                        else
                        {
                            ns.SortNestedCoordData();
                            foreach (var ns2 in ns.NestedCoordData)
                            {
                                if (ns2 is PileData pileData2)
                                {
                                    listCD.Add(ns);
                                }
                            }
                        }
                    }
                }      
            }

            int mark = 0;
            List< PileData> allPiles = new List< PileData>();
            foreach (var coord in listCD)
            {
                if (coord is PileData pile)
                {
                    mark++;
                    pile.MarkNew = mark;
                    allPiles.Add(pile);
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
                        if (pile == null) {continue;}
                        
                        if( SetPileMark(pile, pileClass.MarkNew.ToString(), nameMarks))
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
