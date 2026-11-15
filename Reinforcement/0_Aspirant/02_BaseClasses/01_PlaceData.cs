using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    public class PlaceData
    {
        //основной класс в котором заложены начальные данные
        public PlaceData() { }

        /// <summary>Начальный Контур будущего перекрытия</summary>
        public FloorContourData FloorContourData { get; set; }

        /// <summary>Опорные колонны</summary>
        public List<ColumnContourData> ColumnContourDatas = new List<ColumnContourData>();
        /// <summary>Опорные точки, бывает колонн нет тогда опорные точки= контуру плиты</summary>
        public List<XYZ> OporsMm = new List<XYZ>();

        public List<AspBeam> AspBeams = new List<AspBeam>();//все балки
    }
}
