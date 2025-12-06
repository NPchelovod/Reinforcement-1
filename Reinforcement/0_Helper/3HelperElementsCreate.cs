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
        public HelperElementsCreate(List<Element> OVElements, List<string> namesLookupParameterString, List<string> namesLookupParameterDouble, List<string> nameslookupParameterInt,int pogresZ = 500)
        {
            //идем по OVElements делаем DataOV и ataOVLevel

            DataOV.Clear();
            DataOVLevel.Clear();


            HelperElement newData;
            bool proxod = false;
            foreach (var element in OVElements)
            {
                newData = new HelperElement(element, namesLookupParameterString, namesLookupParameterDouble, namesLookupParameterInt);
                DataOV.Add(newData);

                int Z = newData.Z;

                if (!DataOVLevel.ContainsKey(Z))
                {
                    DataOVLevel[Z] = new List<HelperElement> { newData };
                }
                else
                {
                    proxod = false;
                    if (pogresZ > 0)
                    {
                        //сначала проверка погрешности
                        foreach (var Z_exist in DataOVLevel.Keys.ToList())
                        {
                            if (Math.Abs(Z_exist - Z) < pogresZ)
                            {
                                DataOVLevel[Z_exist].Add(newData);
                                proxod = true;
                                break;
                            }
                        }
                    }
                    if (!proxod)

                    {
                        DataOVLevel[Z].Add(newData);
                    }
                }

            }

        }
    }
}
