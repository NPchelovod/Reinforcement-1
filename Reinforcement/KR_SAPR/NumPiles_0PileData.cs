using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
   
    
    public class PileData : CoordData
    {
        public int netrogat { get; set; } = 0;
        public Element Pile { get; set; } = null;

        // Реализация интерфейса
        //прошлые данные
        public CoordData Father { get; set; } = null;//отец
        public string TypePile => Pile.Name;
        public string Commentary = "";
        public int CommentaryNum = -1;
        public string ADSK_Group = "";
        public int ADSK_GroupNum = -1;

        public int MarkPast = 0;
        public string MarkPastString = "";
        public bool MarkPastIsString = true;

        public string UGOPast = "";
        public int UGOPastNum = 0;


        public string UGONew = "";
        public int UGONewNum = 0;


        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Z { get; set; } = 0;
        public int NumWay { get; set; } = 0;//номер типоразмера класстера сваи
        public int MarkNew = 0;
        public bool BorderWays { get; set; } = false;
        public List<CoordData> NestedCoordData { get; set; } = new List<CoordData>();//вложенные
        public HashSet<CoordData> AllowedPaths { get; set; }
        public double Dist(CoordData b)
        {
            double dx = X - b.X, dy = Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double SectorStep = 150;//
        public int Xs => (int) Math.Round(X / SectorStep); // сектор для кратных координат свай для сортировки, чтобы 899 и 900 были одним числом
        public int Ys => (int) Math.Round(Y / SectorStep);
        public int Zs => (int) (Math.Round(Z / 3.0) * 3.0);
        public long IdValue = 0;
        public PileData(Element pile)
        {
            Pile = pile;

            ElementId elementId = Pile.Id;
            IdValue = elementId.Value;

            LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
            XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

            X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
            Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
            Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

            var comParam = pile.LookupParameter("Комментарии");
            if (comParam != null && comParam.HasValue)
            {
                Commentary = comParam.AsString();
                if (!string.IsNullOrEmpty(Commentary))
                {
                    if (!int.TryParse(comParam.AsString(), out CommentaryNum))
                    {
                        CommentaryNum = comParam.AsString().Length;
                    }
                }

            }
            var markParam = pile.LookupParameter("Марка");
            if (markParam != null && markParam.HasValue)
            {
                MarkPastString = markParam.AsString();
                if (!string.IsNullOrEmpty(MarkPastString))
                {
                    if (int.TryParse(MarkPastString, out MarkPast))
                    {
                        MarkPastIsString = false;
                    }
                    else
                    {
                        // Извлекаем первую последовательность цифр
                        var match = System.Text.RegularExpressions.Regex.Match(MarkPastString, @"\d+");
                        if (match.Success && Int32.TryParse(match.Value, out MarkPast))
                        {
                            // MarkPast готов: "10к" → 10, "а1" → 1, "5" → 5
                        }
                    }
                }
            }

            Parameter UGOParam = pile.LookupParameter(NumPiles.nameYGO);
            if (UGOParam != null && UGOParam.HasValue)
            {
                //с уго сложно 
                UGOPast = UGOParam.AsValueString();
                if (!string.IsNullOrEmpty(UGOPast))
                {
                    Match match = Regex.Match(UGOPast, @"\d+");
                    if (match.Success)
                    {

                        UGOPastNum = int.Parse(match.Value);
                    }
                }
            }
            Parameter adskGroop = pile.LookupParameter("ADSK_Группирование");
            if (adskGroop != null && adskGroop.HasValue)
            {
                //с уго сложно 
                ADSK_Group = adskGroop.AsValueString();
                if (!string.IsNullOrEmpty(ADSK_Group))
                {
                    Match match = Regex.Match(ADSK_Group, @"\d+");
                    if (match.Success)
                    {

                        UGOPastNum = int.Parse(match.Value);
                    }
                }
            }
        }

        public ForgeTypeId units => NumPiles.units;
        public List<string> GetSravnDataString()
        {
            //возвращает сравнение для того чтобы сваи отнести в одну группы
            var rezalt = new List<string>();
            //string sortCode = NumPiles.sortCode;

            foreach (var sortCode in NumPiles.sortCodeEnums)
            {
                switch (sortCode)
                {
                    case SortCodeEnum.SortUGO:
                        rezalt.Add(UGOPast);
                        rezalt.Add(((int)Math.Round(Z, 0)).ToString());
                        break;
                    case SortCodeEnum.SortNumComment:
                        rezalt.Add(Commentary);
                        break;
                    case SortCodeEnum.SortADSKGroup:
                        rezalt.Add(ADSK_Group);
                        break;
                    case SortCodeEnum.SortTypePile:
                        rezalt.Add(TypePile);
                        break;
                    default:
                        break;
                }
            }

            return rezalt;
        }
        public List<int> GetSravnDataInt()
        {
            return new List<int>();
        }
        public void SortNestedCoordData()
        {

        }
        public void CutPileOnGroop() { }
        //public PileDataGroup PileDataGroop = null;

        public HashSet<PileData> SosedPileData { get; set; } = new HashSet<PileData>();

    }

    public class PileDataGroup: CoordData
    {
        public int netrogat { get; set; } = 0;
        public int NumWay { get { return NestedCoordData.Count > 0 ? NestedCoordData.First().NumWay : 0; } set { foreach (var n in NestedCoordData) { n.NumWay = value; } } }


        public bool BorderWays { get; set; } =false;

        public CoordData Father { get; set; } = null;//отец

        public List<CoordData> NestedCoordData { get; set; } = new List<CoordData>(); // вложенные
        public HashSet<CoordData> AllowedPaths { get; set; } //разрешенные пути


        public List<string> SravnList = new List<string>();

        public PileDataGroup( List<string> sravnList)
        {
            SravnList = sravnList;
        }

        public List<int> GetSravnDataInt()
        {
            //в том порядуе сранения который нужен 
            var answer = new List<int>();
            if (NestedCoordData.Count > 0)
            {
                var pile = NestedCoordData.First();
                if (pile is PileData pileData)
                {
                    foreach (var s in NumPiles.sortCodeEnums)
                    {
                        switch (s)
                        {
                            case SortCodeEnum.SortCountPiles:
                                answer.Add(NestedCoordData.Count);
                                break;
                            case SortCodeEnum.SortNumComment:
                                answer.Add(pileData.CommentaryNum);
                                break;
                            case SortCodeEnum.SortUGO:
                                answer.Add(pileData.UGOPastNum);
                                break;
                        }
                    }
                }
                else
                {
                    return pile.GetSravnDataInt();
                }
            }
            return answer;
        }
        public List<string> GetSravnDataString()
        {
            return SravnList;
        }
        

        private bool calcSectors=false;
        private double xsg = 0;
        private double ysg = 0;
        private double zsg = 0;
        private void CalcSectorData()
        {
            if(calcSectors || NestedCoordData.Count==0) { return;}

            CoordData coordData = null;
            if (NumPiles.sortCodeEnums.Contains(SortCodeEnum.SortOnCenterCust))// значит сортируем по центру
            {
                xsg = NestedCoordData.Select(x => x.Xs).Sum() / NestedCoordData.Count();
                ysg = NestedCoordData.Select(x => x.Ys).Sum() / NestedCoordData.Count();
            }
            else if (NumPiles.sortCodeEnums.Contains(SortCodeEnum.SortUpToDown))
            {
                coordData = NestedCoordData
                .OrderBy(x => x.Xs)        // сортируем по X (по возрастанию — от левого к правому)
                .ThenByDescending(x => x.Ys) // затем по Y (по убыванию — от верхнего к нижнему)
                .First();                   // берём первый элемент

            }
            else
            {
                coordData = NestedCoordData
                .OrderBy(x => x.Xs)        // сортируем по X (по возрастанию — от левого к правому)
                .ThenBy(x => x.Ys)       // затем по Y (по возрастанию — от нижнего к верхнему)
                .First();                   // берём первый элемент

            }
            if (coordData != null)
            {
                xsg = coordData.Xs;
                ysg = coordData.Ys;

            }
            zsg = NestedCoordData.First().Zs;


        }

        public int Xs { get { CalcSectorData(); return (int)xsg; } }
        public int Ys { get { CalcSectorData(); return (int)ysg; } }
        public int Zs { get { CalcSectorData(); return (int)zsg; } }
        public double X { get { return Xs; } set { xsg = value; calcSectors = false; } }
        public double Y { get { return Ys; } set { ysg = value; calcSectors = false; } }
        public double Dist(CoordData b)
        {
            double dx = X - b.X, dy = Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public void SortNestedCoordData()
        {
            //сортировка внутренних коорд дата
            if (NestedCoordData.Count == 0) { return; }
            //сортировка вложенных обьектов в самого себя
            
            NestedCoordData = SortNestedCoordData(NumPiles.sortCodeEnums, NestedCoordData);

        }

        public static List<CoordData> SortNestedCoordData( List<SortCodeEnum> sortCodeEnums, List<CoordData> coordDatas)
        {
            //сортировка чего угодно
            coordDatas = new List<CoordData> ( coordDatas );

            //сортировка внутренних коорд дата
            if (sortCodeEnums.Count == 0) { return coordDatas; }
            //сортировка вложенных обьектов в самого себя

            //сортировка групп свай
            var sorted = coordDatas.OrderBy(x => x.netrogat);

            foreach (var sortCode in sortCodeEnums)
            {
                switch (sortCode)
                {

                    case SortCodeEnum.SortUGO:
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.UGOPastNum ?? -1;
                        });
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.UGOPast ?? "";
                        });
                        sorted = sorted.ThenBy(x => x.Zs);
                        break;
                    case SortCodeEnum.SortNumComment:
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.CommentaryNum ?? -1;
                        });
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.Commentary ?? "";
                        });
                        break;

                    case SortCodeEnum.SortADSKGroup:
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.ADSK_GroupNum ?? -1;
                        });
                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData?.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.ADSK_Group ?? "";
                        });

                        break;
                    case SortCodeEnum.SortCountPiles:

                        sorted = sorted.ThenByDescending(x => x.NestedCoordData.Count);
                        break;
                    case SortCodeEnum.SortTypePile:

                        sorted = sorted.ThenBy(x =>
                        {
                            var pile = x.NestedCoordData.FirstOrDefault() as PileData
                             ?? (x as PileData);
                            return pile?.TypePile ?? "";
                        });

                        break;

                    case SortCodeEnum.SortYthenX:

                        if (sortCodeEnums.Contains(SortCodeEnum.SortUpToDown))
                        {
                            sorted = sorted.ThenByDescending(x => x.Ys);
                        }
                        else
                        {
                            sorted = sorted.ThenBy(x => x.Ys);
                        }
                        sorted = sorted.ThenBy(x => x.Xs);
                        break;
                    case SortCodeEnum.SortXthenY:
                        sorted = sorted.ThenBy(x => x.Xs);
                        if (sortCodeEnums.Contains(SortCodeEnum.SortUpToDown))
                        {
                            sorted = sorted.ThenByDescending(x => x.Ys);
                        }
                        else
                        {
                            sorted = sorted.ThenBy(x => x.Ys);
                        }
                        break;
                }
            }
            coordDatas = sorted.ToList();



            return coordDatas;
        }




        public void CutPileOnGroop()
        {
            //дробление свай на вложенные группы 
            double distGroup = NumPiles.sectorStep;
            int maxGroup = NumPiles.predelGroup;
            var listIter = new List<CoordData>(NestedCoordData);

            if (maxGroup <= 1) { return; }

            foreach(var nc in NestedCoordData)
            {
                nc.Father=null;//сбиваем отца
            }
            NestedCoordData.Clear();//жертва ЕГЭ

            // var dictSravn = new Dictionary<CoordData, PileDataGroup>();
            for (int i = 0; i < listIter.Count; i++)
            {
                var coord1 = listIter[i];
                for (int j = i+1; j < listIter.Count; j++)
                {
                    var coord2 = listIter[j];
                    if(coord1.Dist(coord2)> distGroup)
                    {
                        continue;
                    }
                    CoordData father=null;
                    CoordData f1 = coord1.Father;
                    CoordData f2 = coord2.Father;

                    if(f1 == null && f2 == null)
                    {
                        father = new PileDataGroup(SravnList);
                        coord1.Father = father;
                        coord2.Father = father;

                        father.NestedCoordData.Add(coord1);
                        father.NestedCoordData.Add(coord2);
                        
                    }
                    else if(f1 ==f2)
                    {
                        continue;
                    }
                    else if (f2 == null)
                    {
                        father = f1;
                        if (father.NestedCoordData.Count < maxGroup)
                        {
                            coord2.Father = father;
                            father.NestedCoordData.Add(coord2);
                        }
                    }
                    else if (f1 == null)
                    {
                        father = f2;
                        if (father.NestedCoordData.Count < maxGroup)
                        {
                            coord1.Father = father;
                            father.NestedCoordData.Add(coord1);
                        }
                    }
                    else
                    {

                        if (f1.NestedCoordData.Count > f2.NestedCoordData.Count)
                        {
                            father = f1;
                            if (father.NestedCoordData.Count < maxGroup)
                            {
                                foreach (var coord in f2.NestedCoordData)
                                {
                                    coord.Father = father;
                                    father.NestedCoordData.Add(coord);

                                }
                                f2.NestedCoordData.Clear();
                            }
                        }
                        else
                        {
                            father = f2;
                            if (father.NestedCoordData.Count < maxGroup)
                            {
                                foreach (var coord in f1.NestedCoordData)
                                {
                                    coord.Father = father;
                                    father.NestedCoordData.Add(coord);

                                }
                                f1.NestedCoordData.Clear();
                            }
                        }
                    }


                }
            }
            foreach(var vlog in listIter)
            {
                if(vlog.Father == null)
                {
                    NestedCoordData.Add(vlog);
                }
                else
                {
                    NestedCoordData.Add(vlog.Father);
                }
            }
            NestedCoordData = NestedCoordData.ToHashSet().ToList();


            //сортируем вложенные
            foreach (var nc in NestedCoordData)
            {
                nc.SortNestedCoordData();//сбиваем отца
            }
            //итогово сортируем
            SortNestedCoordData();
            
        }


    }


}
