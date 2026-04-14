using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
namespace Reinforcement
{
    public static class VstavkaAnyElement
    {
        //возможно использовать для кубиков и тд скоростной простановки

        public  readonly struct DataElementCoord
        {
            public XYZ XYZ { get; }//для куюиков и тп элементов

            //для балок
            public XYZ XYZ0 { get; }
            public XYZ XYZ1 { get; }
            public double Rotation {  get; } // в радианах
        }
            


        public static bool SetElement( Dictionary<(DataElementCoord coordData, Element element), Dictionary<string, string>> Properties)
        {
            //Properties - ключ свойства и значение
            if (Properties.Count == 0) { return true; }
            Autodesk.Revit.DB.Document doc = RevitAPI.Document;


            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation).ToList();
            if(levels.Count == 0) { return true; }

            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "Создание элементов"))
                {
                    foreach (var data in Properties)
                    {
                        (XYZ xYZ, XYZ startPoint, XYZ endPoint) = (data.Key.coordData.XYZ, data.Key.coordData.XYZ0, data.Key.coordData.XYZ1);
                        

                        Level level = GetNearlyCoordUpperLevel(levels, xYZ);
                        if (level == null) { continue; }
                         
                        FamilySymbol pile = data.Key.element as FamilySymbol;
                        if(pile == null) { continue; }
                        Element element = null;

                        if (startPoint == null || endPoint == null)
                        {
                            element = doc.Create.NewFamilyInstance(xYZ, pile, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                        }
                        else
                        {
                            Line beamCurve = Line.CreateBound(startPoint, endPoint);
                            element = doc.Create.NewFamilyInstance(beamCurve, pile, level, StructuralType.Beam);
                        }

                        //пытаемся заполнить свойства
                        foreach (var propData in data.Value)
                        {
                            Parameter markParam = pile.LookupParameter(propData.Key);
                            if (markParam == null || markParam.IsReadOnly) { continue; }
                                
                            try
                            {
                                markParam.Set(propData.Value);
                            }
                            catch
                            {
                                continue;
                            }
                        }


                    }
                }
            }
            catch { return false; }


            return true;
        }

        public static Level GetNearlyCoordUpperLevel(List<Level> levels, XYZ xYZ)
        {
            //ближайшие уровень который ниже данной координаты
            if (levels.Count == 0)
            {
                Autodesk.Revit.DB.Document doc = RevitAPI.Document;
                levels = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation).ToList();
                if (levels.Count == 0)
                    { return null; }
            }
            if (levels[0].Elevation> xYZ.Z)
            {
                return levels[0]; // все уровни выше жанного элемента
            }

            foreach (var level in levels)
            {
                if(xYZ.Z>level.Elevation)
                {
                    return level;
                }
            }
            return levels.FirstOrDefault();
        }
    }
}
