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
        public static List<string> namesLookupParameterString = new List<string>()
        {
            "Уровень"
        };
        public static List<string> namesLookupParameterDouble = new List<string>()
        {
            "Ширина", "Длина"
        };
        public static List<string> namesLookupParameterInt = new List<string>()
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

        
        public static void GetOVData(ExternalCommandData commandData, int pogresZ=500)
        {
            //идем по OVElements делаем DataOV и ataOVLevel

            GetAllOVElements(commandData);

            var Data = new HelperElementsCreate(OVElements, namesLookupParameterString, namesLookupParameterDouble, namesLookupParameterInt, pogresZ);

            DataOV = Data.DataOV;
            DataOVLevel = Data.DataOVLevel;
        }


    }
}
