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
            if (element == null || !element.IsValidObject) throw new ArgumentException("Не найден действительный элемент", nameof(element));
            units = UnitTypeId.Millimeters;
            this.element = element; elementId = element.Id; name = element.Name;
            var doc = element.Document;
            familySymbol = (element as FamilyInstance)?.Symbol ?? element as FamilySymbol;
            levelId = element.LevelId; levelElement = doc.GetElement(levelId);
            var activePlan = doc.ActiveView as ViewPlan;
            viewPlan = activePlan?.GenLevel?.Id == levelId ? activePlan : null;
            location = element.Location; locationPoint = location as LocationPoint;
            locationPointXYZ = locationPoint?.Point;
            if (locationPointXYZ != null)
            {
                X = (int)Math.Round(RevitAPI.ToMm(locationPointXYZ.X));
                Y = (int)Math.Round(RevitAPI.ToMm(locationPointXYZ.Y));
                Z = (int)Math.Round(RevitAPI.ToMm(locationPointXYZ.Z));
                Rotation = locationPoint.Rotation;
            }
            foreach (var parameterName in namesLookupParameterString ?? new List<string>())
            {
                string value = ParameterHelper.ReadText(ParameterHelper.Find(element, parameterName));
                if (!string.IsNullOrEmpty(value)) lookupParameterString[parameterName] = value;
            }
            foreach (var parameterName in namesLookupParameterDouble ?? new List<string>())
            {
                var parameter = ParameterHelper.Find(element, parameterName);
                double value;
                // Lengths retain the legacy millimetre contract; other doubles use internal values.
                if (parameter != null && parameter.Definition.GetDataType().Equals(SpecTypeId.Length)
                    ? ParameterHelper.TryReadLength(parameter, units, out value)
                    : ParameterHelper.TryReadDouble(parameter, out value)) lookupParameterDouble[parameterName] = value;
            }
            foreach (var parameterName in nameslookupParameterInt ?? new List<string>())
            {
                int value;
                if (ParameterHelper.TryReadInteger(ParameterHelper.Find(element, parameterName), out value)) lookupParameterInt[parameterName] = value;
            }
        }
        public bool HasPointLocation => locationPointXYZ != null;

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
