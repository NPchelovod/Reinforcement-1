using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    public static class GetDataAllOV
    {
        public static string parameter = "Тип системы";
        public static string valueParameter = "Вентканал";
        private static List<string> namesLookupParameterString = new List<string>()
        {
            "Уровень"
        };
        private static List<string> namesLookupParameterDouble = new List<string>()
        {
            "Ширина", "Длина"
        };
        private static List<string> namesLookupParameterInt = new List<string>()
        {

        };

        public static List<Element> OVElements = new List<Element>();
        public static void GetAllOVElements (ExternalCommandData commandData)
        {
            var data = HelperSeachLookup.GetElements(parameter, valueParameter, commandData);

            parameter = data.parameter;
            valueParameter = data.value;
            OVElements =  data.Item1;

        }
        

        //заполнение данныех
        public static List<HelperElement> DataOV = new List<HelperElement>();
        public static Dictionary<int, List<HelperElement>> DataOVLevel= new Dictionary<int, List<HelperElement>>();
        public static void GetOVData(ExternalCommandData commandData)
        {
            GetAllOVElements(commandData);
            DataOV.Clear();
            DataOVLevel.Clear();


            HelperElement newData;
            foreach (var element in OVElements)
            {
                newData = new HelperElement(element, namesLookupParameterString, namesLookupParameterDouble, namesLookupParameterInt);
                DataOV.Add(newData);
                if (!DataOVLevel.ContainsKey(newData.Z))
                {
                    DataOVLevel[newData.Z] = new List<HelperElement> { newData };
                }
                else
                {
                    DataOVLevel[newData.Z].Add(newData);
                }

            }

        }


    }
}
