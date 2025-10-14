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

        

        public static (Element pile, HashSet<string> PossibleNamesFamilySymbol) GetExistFamily(HashSet<string> PossibleNamesFamilySymbol, ExternalCommandData commandData)
        {

            //поиск конкретного типоразмера элемента по параметру

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            familySymbolsNames.Clear();

            FilteredElementCollector collection = null;
            //if (PossibleNamesFamily.Count > 0)
            //{
            //    collection = new FilteredElementCollector(doc).OfClass(typeof(Family)); 
            //}
            //else
            //{
            //    collection = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
            //}
            collection = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol));
            foreach (var element in collection)
            {
                familySymbolsNames[element] = element.Name; // элемент и параметр сравнения
            }
            return GetElement(PossibleNamesFamilySymbol);
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
