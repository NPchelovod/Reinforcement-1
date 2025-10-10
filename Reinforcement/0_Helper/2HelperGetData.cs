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


        public HelperGetData(ElementId elementId)
        {
            //сбор элементов всех данных какие можно собрать с него
            Document doc = RevitAPI.Document;

            //name = elementId.Value;
            element = doc.GetElement(elementId);
           
            familySymbol = doc.GetElement(elementId) as FamilySymbol;
        }


        public string name = null;
        Element element =null;
        FamilySymbol familySymbol = null;





    }
}
