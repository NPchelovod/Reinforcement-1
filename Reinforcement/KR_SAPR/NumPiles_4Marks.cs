using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using System.Windows.Controls;
using Autodesk.Revit.UI;
namespace Reinforcement
{
    public partial class NumPiles
    {
       List<PileDataGroup> pileDataGroup = new List<PileDataGroup>();
        public Result CalculateMarks()
        {
            //нам надо собрать все сваи в группы по важности чтобы потом сортировать
            SortPileImportent();
            var result = OpenTspSolver.Solve(AllPiles.Cast<CoordData>().ToList(), TimeSpan.FromSeconds(10));
            //отсортированный возвращаем
            var allPiles = result.Cast<PileData>().ToList();
            int mark = 0;
            foreach (var pile in allPiles)
            {
                mark++;
                pile.MarkNew = mark;
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
            //нам надо собрать все сваи в группы
            foreach (var pile in AllPiles)
            {
                List<string> SravnList = pile.GetSravnData();
                bool set = false;
                foreach (PileDataGroup pileDataGroops in pileDataGroup)
                {
                    if (pileDataGroops.SravnList.SequenceEqual(SravnList))
                    {
                        pileDataGroops.PileDatas.Add(pile);
                        pile.PileDataGroop = pileDataGroops;
                        set = true;
                        break;
                    }
                }
                if (!set)
                {
                    var pg = new PileDataGroup(SravnList);
                    pg.PileDatas.Add(pile);
                    pile.PileDataGroop = pg;
                    pileDataGroup.Add(pg);
                }
            }
            if (pileDataGroup.Count == 0) { return; }

            var sortDataLis = CalcSort(pileDataGroup.Cast<SortData>().ToList(), sortCode);

            //отсортированный возвращаем
            pileDataGroup = sortDataLis.Cast<PileDataGroup>().ToList();

            int NumWay = 0;
            foreach (var pileGroup in pileDataGroup)
            {
                NumWay++;
                foreach (var pile in pileGroup.PileDatas)
                {
                    pile.NumWay = NumWay;
                }
            }
        }

        public List<SortData> CalcSort(List<SortData> sortDatas, string sortCod)
        {
            var sorted = sortDatas.OrderBy(x => x.netrogat);
            
            foreach (char codeChar in sortCode)
            {
                switch (codeChar)
                {
                    case '9':
                        sorted = sorted.ThenByDescending(x => x.Count());
                        break;
                    case '8':
                        sorted = sorted.ThenBy(x => x.Comment());
                        break;
                   default:
                        break;
                }
            }
            return sorted.ToList();
        }
    }
    
}
