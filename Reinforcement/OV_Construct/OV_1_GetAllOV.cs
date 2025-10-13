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
        public static List<HelperGetData> DataOV = new List<HelperGetData>();

        public static void GetOVData()
        {
            DataOV.Clear();
            foreach (var element in OVElements)
            {
                DataOV.Add(new HelperGetData(element, namesLookupParameterString, namesLookupParameterDouble, namesLookupParameterInt));
            }

        }


    }
}
