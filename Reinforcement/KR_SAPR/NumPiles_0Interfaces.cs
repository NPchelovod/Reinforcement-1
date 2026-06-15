using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    //коды сортировки
    public enum SortCodeEnum
    {
        SortUGO,
        SortNumComment,
        SortADSKGroup,
        SortCountPiles,
        SortTypePile,
        SortYthenX,
        SortXthenY,
        SortUpToDown,
        SortOnCenterCust,
       
    }
    public enum PileEnum
    {

        //для сортировки
        CountPiles,
        NumUGO,
        NumComment,

    }
    public interface Coord
    {
        double X { get; set; }
        double Y { get; set; }
    }
    public interface CoordSector
    {
        int Xs { get; }
        int Ys { get; }
    }
    public interface CoordData : Coord, CoordSector
    {
        int NumWay { get; set; } // число которое показывает порядок, 1 - первые элементы значит идут до вторых и тд
        bool BorderWays { get; set; }
        HashSet<CoordData> NestedCoordData { get; set; } // вложенные
        HashSet<CoordData> AllowedPaths { get; set; } //разрешенные пути
        double Dist(CoordData b);

        //именная сортировка типы свай и тд
        List<string> GetSravnDataString();
        List<int> GetSravnDataInt();



    }
    //public interface SortData : CoordData  // для сортировки надо
    //{
    //    int netrogat { get; set; }

        

    //    //List<string> 
    //    //int SortVal(PileEnum pileEnum);//некая сортировочная величина например колличество и тд

    //}
}