using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace Reinforcement
{
    public class HelperElementsCreate
    {
        //заполнение данныех
        public  List<HelperElement> DataOV = new List<HelperElement>();
        public  Dictionary<int, List<HelperElement>> DataOVLevel = new Dictionary<int, List<HelperElement>>();
        private  List<string> namesLookupParameterString = new List<string>()
        {
            "Уровень"
        };
        private  List<string> namesLookupParameterDouble = new List<string>()
        {
            "Ширина", "Длина"
        };
        private  List<string> namesLookupParameterInt = new List<string>()
        {

        };
        public HelperElementsCreate(List<Element> OVElements, List<string> namesLookupParameterString, List<string> namesLookupParameterDouble, List<string> nameslookupParameterInt, int pogresZ = 500)
        {
            if (OVElements == null) throw new ArgumentNullException(nameof(OVElements));
            if (pogresZ < 0) throw new ArgumentOutOfRangeException(nameof(pogresZ));
            foreach (var element in OVElements.Where(e => e != null && e.IsValidObject).OrderBy(e => e.Id.Value))
            {
                var data = new HelperElement(element, namesLookupParameterString, namesLookupParameterDouble, nameslookupParameterInt);
                if (!data.HasPointLocation) { SkippedElementIds.Add(element.Id); continue; }
                DataOV.Add(data);
                int key = ElevationGrouping.FindKey(DataOVLevel.Keys, data.Z, pogresZ);
                List<HelperElement> group;
                if (!DataOVLevel.TryGetValue(key, out group)) DataOVLevel[key] = group = new List<HelperElement>();
                group.Add(data);
            }
        }
        public List<ElementId> SkippedElementIds { get; } = new List<ElementId>();
    }
}
