using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Reinforcement
{
    public partial class NumPiles
    {
       List<PileDataGroup> pileDataGroup = new List<PileDataGroup>();
        public void CalculateMarks()
        {
            //нам надо собрать все сваи в группы
            foreach (var pile in AllPiles)
            {
                List<string> SravnList = pile.GetSravnData();
                bool set = false;
                foreach (PileDataGroup pileDataGroops in pileDataGroup)
                {
                    if(pileDataGroops.SravnList.Equals(SravnList))
                    {
                        pileDataGroops.PileDatas.Add(pile);
                        pile.PileDataGroop = pileDataGroops;
                        set = true;
                        break;
                    }
                }
                if (!set)
                {
                    pileDataGroup.Add(new PileDataGroup(SravnList));
                }
            }
            if(pileDataGroup.Count == 0) {return; }
            var sortDataLis = CalcSort(pileDataGroup.Cast<SortData>().ToList(), sortCode);
            pileDataGroup = sortDataLis.Cast<PileDataGroup>().ToList();
        }
        public List<SortData> CalcSort(List<SortData> sortDatas, string sortCod)
        {
            var sorted = sortDatas.OrderBy(x => x.netrogat);
            foreach (char codeChar in sortCode)
            {
                switch (codeChar)
                {
                    case '9':
                        sorted.ThenByDescending(x => x.Count());
                        break;
                    case '8':
                        sorted.ThenBy(x => x.Comment());
                        break;
                }
            }



            foreach (char codeChar in sortCode)
            {
                switch (codeChar)
                {
                    case '0':
                        {
                            {
                                sortedList = sortedList.ThenBy(g => g.PilesYGO);
                            }

                            break;
                        }
                    case '1': // сортировка сначала по Y потом по X
                        {


                            if (!inversSort)
                            {
                                sortedList = sortedList.ThenBy(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            }
                            else
                            {
                                sortedList = sortedList.ThenByDescending(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            }

                            //sortedList = sortedList.ThenBy(g => a ? g.XleftS2 : g.CenterS2.xS2);
                            // а по x мы можем сортировать по секетору 3
                            sortedList = sortedList.ThenBy(g => a ? g.XleftS3 : g.CenterS3.xS3);
                        }
                        break;

                    case '2': // сортировка сначала по X потом по Y
                        {

                            sortedList = sortedList.ThenBy(g => a ? g.XleftS2 : g.CenterS2.xS2);

                            //sortedList = sortedList.ThenByDescending(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            //а по y мы можем сортировать по сектору 3
                            if (!inversSort)
                            {
                                sortedList = sortedList.ThenBy(g => a ? g.YtopS3 : g.CenterS3.yS3);
                            }
                            else
                            {
                                sortedList = sortedList.ThenByDescending(g => a ? g.YtopS3 : g.CenterS3.yS3);
                            }

                        }
                        break;

                    case '3': // Ytop (по убыванию)


                        sortedList = sortedList.ThenByDescending(g => g.kolVoPileName);
                        break;

                    case '4': // Xleft

                        sortedList = sortedList.ThenBy(g => g.numName);
                        break;
                    case '8':
                        sortedList = sortedList.ThenBy(g => g.comentDouble);
                        break;
                    case '9':
                        sortedList = sortedList.ThenByDescending(g => g.comentDouble);
                        break;
                    default:
                        // Здесь можно добавить логику для случая по умолчанию
                        break;

                }
                return sorted.ToList();
        }
    }
    
}
