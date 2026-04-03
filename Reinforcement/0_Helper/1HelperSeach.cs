using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{



    public class HelperSeach
    {

        //public static Dictionary<Document, Dictionary<Element, string>> familySymbolsNames = new Dictionary<Document, Dictionary<Element, string>>();

        //обеспечивает скорость в дальнейшем
        //обеспечивает скорость в дальнейшем
        private static Dictionary<Document, Dictionary<string, Element>> PastElements = new Dictionary<Document, Dictionary<string, Element>>();

        public static bool ResetNamesParam = true; // если тру то мы переопределяем параметр


        public static Element GetExistFamily(HashSet<string> PossibleNamesFamilySymbol, ElementTypeOrSymbol Type_seach)
        {

            //поиск конкретного типоразмера элемента по имени семейства

            Document doc = RevitAPI.Document;

            Element element = null;
           
            

            if (PastElements.TryGetValue(doc, out var dats))
            {
                // Если словарь для документа существует, пробуем найти элемент по PossibleNamesFamilySymbol


                foreach(string name in PossibleNamesFamilySymbol)
                {
                    if (dats.TryGetValue(name, out element) && element!=null)
                    {
                        break;
                    }
                }

            }
            else
            {
                //dats = new Dictionary<HashSet<string>, ElementType>(HashSet<string>.CreateSetComparer());
                dats = new Dictionary<string, Element>();
                PastElements[doc] = dats;
            }

            if (element == null)
            {
                FilteredElementCollector col = new FilteredElementCollector(doc);
                IList<Element> sravn_iter;
                bool boolElementType = false;
                if (Type_seach == ElementTypeOrSymbol.ElementType)
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

                var dictAnswer = new Dictionary<string, Element>();
                foreach (var elem in sravn_iter)
                {
                    ElementType elemType = elem as ElementType;
                    string eName = Type_seach == ElementTypeOrSymbol.ElementType ? elemType.Name : elemType.FamilyName;
                    dictAnswer[eName] = elem;
                }

                var answer = SeachNameElement(PossibleNamesFamilySymbol, dictAnswer.Keys.ToList());
                if (string.IsNullOrEmpty(answer.ePile)) { return null; }
                dictAnswer.TryGetValue(answer.ePile, out element);

                if (ResetNamesParam)
                {
                    dats[answer.ePile] = element;
                    PossibleNamesFamilySymbol.Add(answer.ePile);
                }
                
            }
            return element;

        }

        public static (string nPile, string ePile) SeachNameElement(HashSet<string> PossibleNamesFamilySymbol, List<string> elements)
        {
            //поиск совпадеиний имен просто

            List<string> names = PossibleNamesFamilySymbol.ToList();
            

            bool famExist = false;

            int iter = -1;
            string nPile = null;

            string ePile = null;

            while (iter < 3)
            {
                iter++;
                double maxSimilarity = 0;
                foreach (var nName in names)
                {
                    if (nName.Length < 3) { continue; }
                    foreach (var eName in elements)
                    {

                        if (eName.Length < 3) { continue; }

                        var Similarity = HelperPrivateStatic.CalculateSimilarity(eName, nName);
                        if (Similarity > 0.7 && Similarity > maxSimilarity)
                        {
                            maxSimilarity = Similarity;
                            ePile = eName;
                            nPile = nName;

                            //хотя можно добавить и по ??? чтобы так не делало а то вдруг что не то
                            if (maxSimilarity > 0.98 && nPile.Count() > 4)
                            {
                                famExist = true;
                                break;
                            }

                        }
                    }
                    if (famExist)
                    {
                        break;
                    }
                }
                if (famExist)
                {
                    break;
                }

                if (nPile == null)
                {
                    nPile = PossibleNamesFamilySymbol.FirstOrDefault();
                }
                // Спросить пользователя о использовании найденного семейства
                DialogResult result = MessageBox.Show(
                $"Точный типоразмер семейства '{nPile}' не найден. Использовать '{ePile}'?",
                "Семейство не найдено",
                MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes && maxSimilarity > 0.2)
                {
                    famExist = true;

                    break;
                }
                int iter2 = -1;
                bool proxod = true;
                while (iter2 < 3)
                {
                    iter2++;
                    var input = HelperPrivateStatic.GetUserInputWithForm();
                    if (!input.Item2) { proxod = false; break; }


                    if (input.Item1.Count() > 2)
                    {
                        names.Insert(0, input.Item1);
                        break;
                    }
                }
                if (!proxod)
                {
                    break;
                }

            }


            if (famExist)
            {
                return (nPile, ePile);
            }
            return ("", null);

        }

        //public static (Element pile, string PossibleNamesFamilySymbol) GetElement(HashSet<string> PossibleNamesFamilySymbol)
        //{ 
        //    Document doc = RevitAPI.Document;
        //    int iter = -1;
        //    Element pileMax = null;
        //    string pileMaxName = null;

        //    bool famExist = false;

        //    if(!familySymbolsNames.TryGetValue(doc, out var dats))
        //    {

        //    }

        //    while (iter < 6)
        //    {
        //        iter++;



        //        double maxSimilarity = 0;


        //        foreach (string PossibleName in PossibleNamesFamilySymbol)
        //        {
        //            if (PossibleName.Count() < 3) { continue; }

        //            foreach (var elementData in familySymbolsNames)
        //            {
        //                var element = elementData.Key;

        //                var name = elementData.Value;

        //                if (name.Count() < 3) { continue; }

        //                var Similarity = HelperPrivateStatic.CalculateSimilarity(PossibleName, name);
        //                if (Similarity > 0.7 && Similarity > maxSimilarity)
        //                {
        //                    maxSimilarity = Similarity;
        //                    pileMax = element;
        //                    pileMaxName = name;
        //                    if (maxSimilarity > 0.98 && name.Count() > 4)
        //                    {
        //                        famExist = true;
        //                        break;
        //                    }

        //                }
        //            }
        //            if (famExist)
        //            {
        //                break;
        //            }
        //        }

        //        if (famExist)
        //        {
        //            break;
        //        }
        //        // Спросить пользователя о использовании найденного семейства
        //        DialogResult result = MessageBox.Show(
        //        $"Точный типоразмер семейства '{PossibleNamesChange.FirstOrDefault()}' не найден. Использовать '{pileMaxName}'?",
        //        "Семейство не найдено",
        //        MessageBoxButtons.YesNo);
        //        if (result == DialogResult.Yes && maxSimilarity > 0.2)
        //        {
        //            famExist = true;

        //            break;
        //        }
        //        int iter2 = -1;
        //        bool proxod = true;
        //        while (iter2 < 4)
        //        {
        //            iter2++;
        //            var input = HelperPrivateStatic.GetUserInputWithForm();
        //            if (!input.Item2) { proxod = false; break; }


        //            if (input.Item1.Count() > 2)
        //            {
        //                PossibleNamesChange.Clear();
        //                PossibleNamesChange.Add(input.Item1);
        //                break;
        //            }
        //        }
        //        if (!proxod)
        //        {
        //            break;
        //        }

        //    }


        //    if (!famExist)
        //    {
        //        pileMax = null;
        //        return (pileMax, PossibleNamesFamilySymbol);
        //    }
        //    else
        //    {
        //        if (!PossibleNamesFamilySymbol.Contains(pileMaxName))
        //        { PossibleNamesFamilySymbol.Add(pileMaxName); }

        //        return (pileMax, PossibleNamesFamilySymbol);
        //    }


        //}

    }
}
