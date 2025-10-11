using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public class HelperGetData
    {

        private ForgeTypeId units;
        public HelperGetData(ElementId elementId)
        {
            units = UnitTypeId.Millimeters;// единицы измерений

            this.elementId = elementId;
            //сбор элементов всех данных какие можно собрать с него
            Document doc = RevitAPI.Document;

            //name = elementId.Value;
            element = doc.GetElement(elementId);
            name = element.Name;

            familySymbol = doc.GetElement(elementId) as FamilySymbol;


            //где находится данный элемент уровень и тд
            levelId = element.LevelId;
            levelElement = doc.GetElement(elementId);
            viewPlan = levelElement as ViewPlan;

            //надо определить высотную отметку где находится также

            //определяем позицию
            location = element.Location;
            locationPoint = location as LocationPoint; // текущая локация вентканала
            locationPointXYZ = locationPoint.Point; // текущая координата расположения

            X = UnitUtils.ConvertFromInternalUnits(locationPointXYZ.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
            Y = UnitUtils.ConvertFromInternalUnits(locationPointXYZ.Y, units);
            Z = UnitUtils.ConvertFromInternalUnits(locationPointXYZ.Z, units);

            Rotation = locationPoint.Rotation; // угол поворота
            //геометрические и иные параметры

            foreach (var nameParameters in namesLookupParameterString)
            {
                lookupParameterString[nameParameters] = element.LookupParameter(nameParameters).AsValueString();
            }
            foreach (var nameParameters in namesLookupParameterDouble)
            {
                lookupParameterDouble[nameParameters] = Convert.ToDouble(element.LookupParameter(nameParameters).AsValueString());
            }
            foreach (var nameParameters in namesLookupParameterInt)
            {
                lookupParameterInt[nameParameters] = Convert.ToInt32(element.LookupParameter(nameParameters).AsValueString());
            }

        }

        public ElementId elementId;
        public string name;
        public Element element;
        public FamilySymbol familySymbol;


        public ElementId levelId;
        public Element levelElement;
        public ViewPlan viewPlan;

        public Location location;
        public LocationPoint locationPoint;
        public XYZ locationPointXYZ;
        public double X; // позиции центральной точки в мм координатах
        public double Y;
        public double Z;
        public double Rotation;// угол поворота


        //может их надо сделать пожаваемыми на поиск в этот класс?
        private List<string> namesLookupParameterString = new List<string>()
        {
            "Уровень"
        };
        public Dictionary<string, string> lookupParameterString = new Dictionary<string, string>();
        private List<string> namesLookupParameterDouble = new List<string>()
        {
            "Ширина", "Длина","Уровень"
        };
        public Dictionary<string, double> lookupParameterDouble = new Dictionary<string, double>();
        private List<string> namesLookupParameterInt = new List<string>()
        {
            
        };
        public Dictionary<string, int> lookupParameterInt = new Dictionary<string, int>();



    }
}
