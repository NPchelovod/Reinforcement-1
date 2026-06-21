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

        public static double SectorStep = NumPiles.coordinateRoundingStep>0? NumPiles.coordinateRoundingStep : 150;//
        public static double SectorStepZ => NumPiles.sectorStepZ;

        public int Xs => (int) (Math.Round(X / SectorStep)* SectorStep); // сектор для кратных координат свай для сортировки, чтобы 899 и 900 были одним числом
        public int Ys => (int) (Math.Round(Y / SectorStep) * SectorStep);
        public int Zs => (int) (Math.Round(Z / SectorStepZ)* SectorStepZ);
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
                    if (match.Success && Int32.TryParse(match.Value, out UGOPastNum))
                    {

                        
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

                        ADSK_GroupNum = int.Parse(match.Value);
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
                        //rezalt.Add(Zs.ToString());
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
                    case SortCodeEnum.SortZ:
                        rezalt.Add(Zs.ToString());
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
        public void CutPileOnGroop(double distance) { }
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
            if(calcSectors) { return;}
            calcSectors = true;

            if(NestedCoordData.Count == 0)
            {
                xsg = X;
                ysg = Y;
                zsg = Zs;
                return;
            }


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
            var list = new List<CoordData>(coordDatas);
            if (sortCodeEnums.Count <=1) return list;
            //сортировка вложенных обьектов в самого себя

            //сортировка групп свай
            // Вспомогательная функция
            PileData GetPile(CoordData x) =>
                x.NestedCoordData?.FirstOrDefault() as PileData ?? x as PileData;

            IOrderedEnumerable<CoordData> sorted = list.OrderBy(x => x.netrogat); // или убрать, если не нужно

            foreach (var sortCode in sortCodeEnums)
            {
                switch (sortCode)
                {
                    case SortCodeEnum.SortUGO:
                        sorted = sorted.ThenBy(x => GetPile(x)?.UGOPastNum ?? -1);
                        sorted = sorted.ThenBy(x => GetPile(x)?.UGOPast ?? "");
                        //sorted = sorted.ThenBy(x => x.Zs);
                        break;
                    case SortCodeEnum.SortNumComment:
                        sorted = sorted.ThenBy(x => GetPile(x)?.CommentaryNum ?? -1);
                        sorted = sorted.ThenBy(x => GetPile(x)?.Commentary ?? "");
                        break;
                    case SortCodeEnum.SortADSKGroup:
                        sorted = sorted.ThenBy(x => GetPile(x)?.ADSK_GroupNum ?? -1);
                        sorted = sorted.ThenBy(x => GetPile(x)?.ADSK_Group ?? "");
                        break;
                    case SortCodeEnum.SortCountPiles:
                        sorted = sorted.ThenByDescending(x => x.NestedCoordData?.Count ?? 0);
                        break;
                    case SortCodeEnum.SortTypePile:
                        sorted = sorted.ThenBy(x => GetPile(x)?.TypePile ?? "");
                        break;
                    case SortCodeEnum.SortYthenX:
                        sorted = sortCodeEnums.Contains(SortCodeEnum.SortUpToDown)
                            ? sorted.ThenByDescending(x => x.Ys)
                            : sorted.ThenBy(x => x.Ys);
                        sorted = sorted.ThenBy(x => x.Xs);
                        break;
                    case SortCodeEnum.SortXthenY:
                        sorted = sorted.ThenBy(x => x.Xs);
                        sorted = sortCodeEnums.Contains(SortCodeEnum.SortUpToDown)
                            ? sorted.ThenByDescending(x => x.Ys)
                            : sorted.ThenBy(x => x.Ys);
                        break;
                    case SortCodeEnum.SortZ:
                        sorted = sorted.ThenBy(x => x.Zs);
                        
                        break;
                }
            }

            return sorted.ToList();
        }




        public void CutPileOnGroop(double distance)
        {
            //        Вход: точки coords, distance D, maxSize K
            //1.Построить список всех пар(i, j), у которых расстояние ≤ D.
            //2.Отсортировать пары по возрастанию расстояния.
            //3.Каждая точка – отдельная группа. Размеры групп size[i] = 1.
            //   Система непересекающихся множеств(DSU) с учётом размера группы.
            //4.Для каждой пары(a, b) из отсортированного списка:
            //            ga = find(a), gb = find(b)
            //       если ga != gb и size[ga] +size[gb] ≤ K:
            //            union(ga, gb)
            //           обновить общий размер
            //5.Результат: компоненты DSU – итоговые группы.
            //   Одиночные точки(не соединённые ни с кем) остаются отдельно.
            //дробление свай на вложенные группы 


            double distGroup = distance;
            int maxGroup = NumPiles.predelGroup;
            if (maxGroup <= 1) { return; }

            var points = NestedCoordData.ToList();
            //NestedCoordData.Clear();

            int n = points.Count;
            var dsu = new DisjointSetUnion(n);
            // Собираем все пары в пределах distance
            var edges = new List<(int i, int j, double dist)>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double d = points[i].Dist(points[j]);
                    if (d <= distance)
                        edges.Add((i, j, d));
                }
            // Сортируем по расстоянию (ближайшие вперёд)
            edges.Sort((a, b) => a.dist.CompareTo(b.dist));

            foreach (var (i, j, dist) in edges)
            {
                int rootI = dsu.Find(i);
                int rootJ = dsu.Find(j);
                if (rootI != rootJ && dsu.Size[rootI] + dsu.Size[rootJ] <= maxGroup)
                    dsu.Union(rootI, rootJ);
            }

            // Группируем результат
            var groupDict = new Dictionary<int, List<CoordData>>();
            for (int i = 0; i < n; i++)
            {
                int root = dsu.Find(i);
                if (!groupDict.ContainsKey(root))
                    groupDict[root] = new List<CoordData>();
                groupDict[root].Add(points[i]);
            }

            // Формируем PileDataGroup (как у вас)
            var result = new List<PileDataGroup>();
            foreach (var kv in groupDict)
            {
                var group = new PileDataGroup(SravnList); // предполагается, что такой конструктор есть

                group.NumWay = NumWay;
                group.Father = this;

                foreach (var kv2 in kv.Value)
                {
                    kv2.Father = group;
                    group.NestedCoordData.Add(kv2);
                }

                result.Add(group);
            }

            NestedCoordData = result.Cast<CoordData>().ToList();


            //double distGroup = distance;
            //int maxGroup = NumPiles.predelGroup;
            //if (maxGroup <= 1) { return; }
            //var listIter = new List<CoordData>(NestedCoordData);


            ////координата каждой сваи и её соседи...
            //Dictionary<CoordData, HashSet<CoordData>> DictFathers = new Dictionary<CoordData, HashSet<CoordData>>();

            //foreach (var nc in NestedCoordData)
            //{
            //    nc.Father=null;//сбиваем отца
            //    nc.NestedCoordData.Clear();
            //    DictFathers[nc] = new HashSet<CoordData> {nc};
            //}
            //NestedCoordData.Clear();//

            //// var dictSravn = new Dictionary<CoordData, PileDataGroup>();


            //for (int i = 0; i < listIter.Count; i++)
            //{
            //    var coord = listIter[i];

            //    double distMin = distGroup;

            //    CoordData betterSosed = null;
            //    var nested = DictFathers[coord];
            //    if(nested.Count>=maxGroup) {continue;}

            //    for (int j = i+1; j < listIter.Count; j++)
            //    {
            //        var sosed = listIter[j];
            //        double dist = coord.Dist(sosed);

            //        if (dist > distMin)//1 добавляем для чёткости
            //        {
            //            continue;
            //        }

            //        var sosedNested = DictFathers[sosed];
            //        if (sosedNested.Count+ nested.Count > maxGroup || sosedNested== nested) { continue;}


            //        betterSosed = sosed;
            //        distMin = dist;
            //    }
            //    if (betterSosed == null) { continue; }
            //    {
            //        var sosedNested = DictFathers[betterSosed];
            //        //иначе добавляем соседуса
            //        foreach (var s in nested)
            //        {
            //            sosedNested.Add(s);
            //        }
            //        nested.Clear();
            //    }

            //}
            //List<string> list = new List<string>(); 

            //foreach(var coordDict in DictFathers)
            //{
            //    var pd = new PileDataGroup(SravnList);
            //    foreach(var coord in coordDict.Value)
            //    {
            //        pd.NestedCoordData.Add(coord);
            //    }
            //    NestedCoordData.Add(pd);
            //}




            //сортируем вложенные
            //foreach (var nc in NestedCoordData)
            //{
            //    nc.SortNestedCoordData();//сбиваем отца
            //}
            ////итогово сортируем
            //SortNestedCoordData();

        }


    }

    public class DisjointSetUnion
    {
        int[] parent;
        public int[] Size;
        public DisjointSetUnion(int n)
        {
            parent = new int[n];
            Size = new int[n];
            for (int i = 0; i < n; i++) { parent[i] = i; Size[i] = 1; }
        }
        public int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            // присоединяем меньшее к большему (опционально)
            if (Size[a] < Size[b]) (a, b) = (b, a);
            parent[b] = a;
            Size[a] += Size[b];
        }
    }
}
