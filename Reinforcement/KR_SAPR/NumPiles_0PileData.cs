using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
   
        public enum PileEnum
        {
            TypePile,
            CommentPast,
            UGOPast
        }
    public interface SortData // для сортировки надо
    {
        int netrogat { get; set; }
        int Count();

        int Comment();

    }
    public class PileData : CoordData, SortData
    {
        public int netrogat { get; set; } = 0;
        public Element Pile { get; set; } = null;
        
        // Реализация интерфейса
        //прошлые данные

        public string TypePile => Pile.Name;
        public string Commentary = "";
        public int CommentaryNum = -1;
        public int MarkPast = 0;
        public string UGOPast = "";
        public int UGOPastNum = 0;
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Z { get; set; } = 0;
        public int NumWay { get; set; } = 0;//номер типоразмера класстера сваи
        public int MarkNew = 0;


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
        }

        public ForgeTypeId units => NumPiles.units;
        public List<string> GetSravnData()
        {
            //возвращает сравнение для того чтобы сваи отнести в одну группы
            var rezalt = new List<string>() { TypePile };
            string sortCode = NumPiles.sortCode;

            foreach (char codeChar in sortCode)
            {
                switch (codeChar)
                {
                    case '0':
                        rezalt.Add(UGOPast);
                        rezalt.Add(((int)Math.Round(Z, 0)).ToString());
                        break;
                    case '3':
                        rezalt.Add(TypePile);
                        break;

                    case '8':
                        rezalt.Add(Commentary);
                        break;
                    default:
                        break;
                }
            }
            
            return rezalt;
        }

        public PileDataGroup PileDataGroop = null;
        public int Count()
        {
            return 1;
        }
        public int Comment()
        {
            if(CommentaryNum>-1)
            {
                return CommentaryNum;
            }
            return Commentary.Length;
        }
    }

    public class PileDataGroup: SortData
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
    }


}
