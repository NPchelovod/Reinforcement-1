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

        public string TypePile => Pile.Name;
        public string Commentary = "";
        public int CommentaryNum = -1;
        public string ADSK_Group = "";
        public int ADSK_GroupNum = -1;

        public int MarkPast = 0;
        public string UGOPast = "";
        public int UGOPastNum = 0;
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Z { get; set; } = 0;
        public int NumWay { get; set; } = 0;//номер типоразмера класстера сваи
        public int MarkNew = 0;
        public bool BorderWays { get; set; } = false;
        public HashSet<CoordData> NestedCoordData { get; set; }//вложенные
        public HashSet<CoordData> AllowedPaths { get; set; }
        public double Dist(CoordData b)
        {
            double dx = X - b.X, dy = Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double SectorStep = 150;//
        public int Xs => (int) Math.Round(X / SectorStep); // сектор для кратных координат свай для сортировки, чтобы 899 и 900 были одним числом
        public int Ys => (int) Math.Round(Y / SectorStep);
       
        public PileData(Element pile)
        {
            Pile = pile;
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
                var oldMarkValue = markParam.AsString();
                Int32.TryParse(oldMarkValue, out int MarkPast);
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


        //public PileDataGroup PileDataGroop = null;
        public int Count()
        {
            return 1;
        }
        public int Comment()
        {
            if (CommentaryNum > -1)
            {
                return CommentaryNum;
            }
            return Commentary.Length;
        }
        public int NumUGO()
        {
            return UGOPastNum;
        }
    }

    public class PileDataGroup: CoordData
    {
        public int netrogat { get; set; } = 0;










        public List<PileData> PileDatas = new List<PileData>();
        public List<string> SravnList = new List<string>();

        public PileDataGroup( List<string> sravnList)
        {
            SravnList = sravnList;
        }

        public int Count()
        {
            return PileDatas.Count;
        }
        public int Comment()
        {
            return PileDatas.First().Comment();
        }
        public int NumUGO()
        {
            return PileDatas.First().UGOPastNum;
        }

        private bool calcSectors=false;
        private double xsg = 0;
        private double ysg = 0;
        public void CalcSectorData()
        {
            if(calcSectors || PileDatas.Count==0) { return;}
            string sortCode = NumPiles.sortCode;
            if (sortCode.Contains("6"))// значит сортируем по центру
            {
                xsg = PileDatas.Select(x => x.Xs).Sum() / PileDatas.Count();
                ysg = PileDatas.Select(x => x.Ys).Sum() / PileDatas.Count();
            }
            else
            {
                

                var topLeft = PileDatas
                .OrderBy(x => x.Xs)        // сортируем по X (по возрастанию — от левого к правому)
                .ThenByDescending(x => x.Ys) // затем по Y (по убыванию — от верхнего к нижнему)
                .First();                   // берём первый элемент
                var bottomLeft = PileDatas
                .OrderBy(x => x.Xs)        // сортируем по X (по возрастанию — от левого к правому)
                .ThenBy(x => x.Ys)       // затем по Y (по возрастанию — от нижнего к верхнему)
                .First();                   // берём первый элемент

                var topRight = PileDatas
                .OrderByDescending(x => x.Xs)  // сортируем по X (по убыванию — от правого к левому)
                .ThenByDescending(x => x.Ys)     // затем по Y (по убыванию — от верхнего к нижнему)
                .First();

                var bottomRight = PileDatas
                .OrderByDescending(x => x.Xs)  // сортируем по X (по убыванию — от правого к левому)
                .ThenBy(x => x.Ys)             // затем по Y (по возрастанию — от нижнего к верхнему)
                .First();                     // берём первый элемент
                PileData answer = bottomLeft;
                if(sortCode.Contains("7"))
                {
                    answer = topLeft;
                }
                xsg = answer.Xs;
                ysg = answer.Ys;

            }
            
        }
        public double Xsg
        {
            get { CalcSectorData();  return xsg; }
        }
        public double Ysg
        {
            get { CalcSectorData(); return ysg; }
        }
        public double X { get { return Xsg; } set { xsg = value; } }
        public double Y { get { return Ysg; } set { ysg = value; } }
    }


}
