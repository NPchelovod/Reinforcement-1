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
        //обеспечивает скорость в дальнейшем
        private static Dictionary<Document, Dictionary<HashSet<string>, ElementType>> PastElements = new Dictionary<Document, Dictionary<HashSet<string>, ElementType>>();


        public static bool ResetNamesParam=false; // если тру то мы переопределяем параметр

        public static (bool create, HashSet<String> FamNames)  GetResult(Document doc, UIDocument uidoc, HashSet<String> FamNames, string Type_seach )
        {
            var pastFamNames = new HashSet<String>(FamNames);
            if(ResetNamesParam)
            {
                ResetNamesParam = false;
                FamNames.Clear();
            }


            // служит для поиска и установки элемента
            FilteredElementCollector col = new FilteredElementCollector(doc);

            ElementType elementType = null;
            
            bool pastExist = false;
            // Получаем или создаем словарь dats для документа с компаратором
            
            if (PastElements.TryGetValue(doc, out var dats))
            {
                // Если словарь для документа существует, пробуем найти элемент по FamNames
                if (FamNames.Count > 0)
                {
                    foreach (var dat in dats.Keys)
                    {
                        if (dat.Count != FamNames.Count)
                        { continue; }
                        // Проверяем, что все элементы совпадают
                        bool allMatch = true;

                        foreach (var fam in FamNames)
                        {
                            if (!dat.Contains(fam))
                            {
                                allMatch = false;
                                continue;
                            }
                            pastExist = false; break;
                        }
                        if (allMatch)
                        {
                            elementType = dats[dat];
                            pastExist = true;
                            break;
                        }

                    }
                }
                
            }
            else 
            {
                //dats = new Dictionary<HashSet<string>, ElementType>(HashSet<string>.CreateSetComparer());
                PastElements[doc] = new Dictionary<HashSet<string>, ElementType>();
            }

            if (!pastExist)
            {
                IList<Element> sravn_iter;
                bool boolElementType = false;
                if (Type_seach == "ElementType")
                {
                    IList<Element> elementTypes = col.OfClass(typeof(ElementType)).WhereElementIsElementType().ToElements();
                    sravn_iter = elementTypes;
                    boolElementType = true;
                }
                else
                {
                    IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
                    sravn_iter = symbols;
                }

                Dictionary<Element, string> familySymbolsNames = HelperSeach.familySymbolsNames;
                familySymbolsNames.Clear();

                string name_sravn;


                //заполнение словаря сравнения это ссылочный параметр!!!!!!!!!
                foreach (Element element in sravn_iter)
                {
                    elementType = element as ElementType;
                    if (boolElementType)
                    { name_sravn = elementType.Name; }
                    else
                    { name_sravn = elementType.FamilyName; }

                    familySymbolsNames[element] = name_sravn;
                }



                var Data = HelperSeach.GetElement(FamNames);
                Element element_gotov = Data.pile;

                if (element_gotov == null)
                {
                    return (false, pastFamNames);// чтобы возврат к первоначальным настройкам
                }

                elementType = element_gotov as ElementType;

                // Добавляем в словарь dats, который уже имеет компаратор
                FamNames = Data.PossibleNamesFamilySymbol;
                PastElements[doc][FamNames] = elementType;
                

            }

            uidoc.PostRequestForElementTypePlacement(elementType);

            
            return (true, FamNames);

        }

    }
}
