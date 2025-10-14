using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    
    public class HelperElement
    {

        private ForgeTypeId units;



        public HashSet<HelperElement> ParentElement=new HashSet<HelperElement>();// допустим нижестоящая шахта вент
        public HashSet<HelperElement> ChildElement = new HashSet<HelperElement>();//вышестоящая шахта вент могут быть 2 шахты потом в одну превращаться


        public HelperElement(Element element, List<string> namesLookupParameterString, List<string> namesLookupParameterDouble, List<string> nameslookupParameterInt)
        {
            units = UnitTypeId.Millimeters;// единицы измерений

            this.element = element;

            this.namesLookupParameterString = namesLookupParameterString;
            this.namesLookupParameterDouble = namesLookupParameterDouble;
            this.namesLookupParameterInt = nameslookupParameterInt;

            this.elementId = element.Id;
            //сбор элементов всех данных какие можно собрать с него
            Document doc = RevitAPI.Document;

            //name = elementId.Value;
            
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

            X = (int) Math.Round(UnitUtils.ConvertFromInternalUnits(locationPointXYZ.X, units),0); // a ConvertToInternalUnits переводит наоборот из метров в футы
            Y = (int) Math.Round(UnitUtils.ConvertFromInternalUnits(locationPointXYZ.Y, units),0);
            Z = (int)Math.Round(UnitUtils.ConvertFromInternalUnits(locationPointXYZ.Z, units),0);

            Rotation = locationPoint.Rotation; // угол поворота
            //геометрические и иные параметры

            foreach (var nameParameters in namesLookupParameterString)
            {
                Parameter foundParam = element.LookupParameter(nameParameters);
                if (foundParam != null)
                {
                    string valueString = foundParam.AsValueString();
                    if (!string.IsNullOrEmpty(valueString))
                        lookupParameterString[nameParameters] = valueString;
                }
            }
            foreach (var nameParameters in namesLookupParameterDouble)
            {
                Parameter foundParam = element.LookupParameter(nameParameters);
                if (foundParam != null)
                {
                    string valueString = foundParam.AsValueString();
                    if (!string.IsNullOrEmpty(valueString))
                    {
                        double result;
                        bool isValid = Double.TryParse(valueString, out result);
                        if (isValid)
                        {
                            lookupParameterDouble[nameParameters] = result;
                        }
                    }
                }
            }
            foreach (var nameParameters in namesLookupParameterInt)
            {
                Parameter foundParam = element.LookupParameter(nameParameters);
                if (foundParam != null)
                {
                    string valueString = foundParam.AsValueString();
                    if (!string.IsNullOrEmpty(valueString))
                    {
                        int result;
                        bool isValid = int.TryParse(valueString, out result);

                        if (isValid)
                        {
                            lookupParameterInt[nameParameters] = result;
                        }
                    }
                }
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
        public int X; // позиции центральной точки в мм координатах
        public int Y;
        public int Z;
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
