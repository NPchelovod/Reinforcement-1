using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    internal class Utilit_1_1_Depth_Seach
    {

        private Dictionary<Document, Dictionary<HashSet<string>, Element>> PastElements = new Dictionary<Document, Dictionary<HashSet<string>, Element>>();


        public static (bool create, List<String> FamNames)  GetResult(Document doc, UIDocument uidoc, List <String> FamNames, string Type_seach )
        {

            // служит для поиска и установки элемента
            FilteredElementCollector col = new FilteredElementCollector(doc);


            IList<Element> sravn_iter;
            bool ElementType=false;
            if (Type_seach == "ElementType")
            {
                IList<Element> elementTypes = col.OfClass(typeof(ElementType)).WhereElementIsElementType().ToElements();
                sravn_iter = elementTypes;
                ElementType = true;
            }
            else
            {
                IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
                sravn_iter = symbols;
            }

            Dictionary<Element, string> familySymbolsNames = HelperSeach.familySymbolsNames;
            familySymbolsNames.Clear();

            string name_sravn;

            
            ElementType elementType = null;
            foreach (Element element in sravn_iter)
            {
                elementType = element as ElementType;
                if (ElementType)
                { name_sravn = elementType.Name; }
                else
                { name_sravn = elementType.FamilyName; }

                familySymbolsNames[element] = name_sravn;
            }

            var FamNamesSet = FamNames.ToHashSet();

            var Data = HelperSeach.GetElement(FamNamesSet);
            Element element_gotov = Data.pile;

            if (element_gotov==null)
            {
                return (false, FamNames);
            }

            elementType = element_gotov as ElementType;
            
            uidoc.PostRequestForElementTypePlacement(elementType);


            return (true, Data.PossibleNamesFamilySymbol.ToList());


        }

    }
}
