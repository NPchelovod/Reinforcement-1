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

        public static Dictionary<Element, string> familySymbolsNames = new Dictionary<Element, string>();

        //обеспечивает скорость в дальнейшем
        private static Dictionary<Document, Dictionary<HashSet<string>, Element>> PastElements = new Dictionary<Document, Dictionary<HashSet<string>, Element>>();


        public static (Element pile, HashSet<string> PossibleNamesFamilySymbol) GetExistFamily(HashSet<string> PossibleNamesFamilySymbol, ExternalCommandData commandData)
        {

            //поиск конкретного типоразмера элемента по параметру

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;

            Element element = null;
            bool pastExist = false;
            

            if (PastElements.TryGetValue(doc, out var dats))
            {
                // Если словарь для документа существует, пробуем найти элемент по PossibleNamesFamilySymbol

                foreach (var dat in dats.Keys)
                {
                    if (dat.Count != PossibleNamesFamilySymbol.Count)
                    { continue; }
                    // Проверяем, что все элементы совпадают
                    bool allMatch = true;

                    foreach (var fam in PossibleNamesFamilySymbol)
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
                        element = dats[dat];
                        pastExist = true;
                        break;
                    }

                }

            }
            else
            {
                //dats = new Dictionary<HashSet<string>, ElementType>(HashSet<string>.CreateSetComparer());
                PastElements[doc] = new Dictionary<HashSet<string>, Element>();
            }



            if (!pastExist)
            {
                familySymbolsNames.Clear();

                FilteredElementCollector collection = null;
                collection = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
                foreach (var element1 in collection)
                {
                    familySymbolsNames[element1] = element1.Name; // элемент и параметр сравнения
                }

                var seachData = GetElement(PossibleNamesFamilySymbol);
                element = seachData.pile;
                if (element != null)
                {
                    PossibleNamesFamilySymbol = seachData.PossibleNamesFamilySymbol;
                    PastElements[doc][PossibleNamesFamilySymbol] = element;
                }
            }

            return (element, PossibleNamesFamilySymbol);
        }



        public static (Element pile, HashSet<string> PossibleNamesFamilySymbol) GetElement(HashSet<string> PossibleNamesFamilySymbol)
        { 
            int iter = -1;
            Element pileMax = null;
            string pileMaxName = null;

            HashSet<string> PossibleNamesChange = new HashSet<string>(PossibleNamesFamilySymbol);
            bool famExist = false;
            while (iter < 6)
            {
                iter++;

                
                double maxSimilarity = 0;

                foreach (string PossibleName in PossibleNamesChange)
                {
                    if (PossibleName.Count() < 4) { continue; }

                    foreach (var elementData in familySymbolsNames)
                    {
                        var element = elementData.Key;

                        var name = elementData.Value;

                        var Similarity = HelperPrivateStatic.CalculateSimilarity(PossibleName, name);
                        if (Similarity > 0.7 && Similarity > maxSimilarity)
                        {
                            maxSimilarity = Similarity;
                            pileMax = element;
                            pileMaxName = name;
                            if (maxSimilarity > 0.98 && name.Count() > 4)
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
                // Спросить пользователя о использовании найденного семейства
                DialogResult result = MessageBox.Show(
                $"Точный типоразмер семейства '{PossibleNamesChange.FirstOrDefault()}' не найден. Использовать '{pileMaxName}'?",
                "Семейство не найдено",
                MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes && maxSimilarity > 0.2)
                {
                    famExist = true;

                    break;
                }
                int iter2 = -1;
                bool proxod = true;
                while (iter2 < 4)
                {
                    iter2++;
                    var input = HelperPrivateStatic.GetUserInputWithForm();
                    if (!input.Item2) { proxod = false; break; }


                    if (input.Item1.Count() > 5)
                    {
                        PossibleNamesChange.Clear();
                        PossibleNamesChange.Add(input.Item1);
                        break;
                    }
                }
                if (!proxod)
                {
                    break;
                }

            }


            if (!famExist)
            {
                pileMax = null;
                return (pileMax, PossibleNamesFamilySymbol);
            }
            else
            {
                if (!PossibleNamesFamilySymbol.Contains(pileMaxName))
                { PossibleNamesFamilySymbol.Add(pileMaxName); }

                return (pileMax, PossibleNamesFamilySymbol);
            }


        }

    }
}
