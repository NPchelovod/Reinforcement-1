using Autodesk.Revit.DB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reinforcement.Aspirant
{
    /// <summary>
    /// Получаем из Вида плиту перекрытия и колонны как опоры, формируем наши классы данных контура перекрытия и опор
   
    /// </summary>
    public static class GetOpors
    {
        public static double MinDistanceBetweenOporsMM = 500;//мм
        public static int MinCountOpors = 3;//минимальное кол-во опор
        public static List<PlaceData> GetInitialPlaces()
        {
            //получение начальных 
            Document doc = RevitAPI.Document;
            View view= RevitAPI.GetActiveView();
            //получаем контуры плит
            var dataFloors=FloorContourData.GetAllFloorsContoursInView(doc, view);
            //получаем все колонны
            var dataColumns=ColumnContourData.GetAllColumns(doc, view);

            var answer = new List<PlaceData>();
            foreach(var f in dataFloors)
            {
                var pd = new PlaceData() { FloorContourData = f };

                foreach (var c in dataColumns)
                {
                    if( GetColumnIntersectsFloor.ColumnIntersectsFloor(c, f, out XYZ intersectionMm))
                    {
                        pd.ColumnContourDatas.Add(c);
                        pd.OporsMm.Add(intersectionMm);
                    }
                }

                XYZListExtensions.DedupeInPlace(pd.OporsMm, MinDistanceBetweenOporsMM);//чистка от дублей колонных опор
                

                if (pd.OporsMm.Count< MinCountOpors)
                {
                    if (pd.ColumnContourDatas.Count != 0)
                    {
                        //надо вывести ошибку
                        ErrorAnalyse.Error(null, "GetInitialPlaces() pd.OporsMm.Count < MinCountOpors колонны есть но их мало!");
                    }
                    //добавляем еще точки опорные
                    SupportSelectionResult supportSelectionResult=null;
                    if (pd.OporsMm.Count > 0)
                    {
                        supportSelectionResult = PlateSupportSelector.FindBalancedSupportsWithFixed(
                     f.OuterContourMm, pd.OporsMm, N: MinCountOpors, centroidWeight: 2.0);

                        //вместо этого я раньше думал XYZListExtensions.FindFarthestFromSet(f.OuterContourMm, pd.OporsMm, out double distance);
                    }
                    else
                    {
                        supportSelectionResult = PlateSupportSelector.FindBalancedSupports(f.OuterContourMm, N: MinCountOpors);
                    }
                    pd.OporsMm = supportSelectionResult.Supports;

                    if (pd.OporsMm.Count < MinCountOpors)
                    {
                        //вывод ошибки
                        ErrorAnalyse.Error(null, "GetInitialPlaces() pd.OporsMm.Count < MinCountOpors, достаточно опор не создалось");
                        continue;
                    }    
                }
                pd.OporsMm=XYZListCircularSort.SortCircular(pd.OporsMm);
                answer.Add(pd);
            }
            answer= answer.OrderByDescending(x=>x.FloorContourData.AreaMm).ToList();
            return answer;
        }

    }
}
