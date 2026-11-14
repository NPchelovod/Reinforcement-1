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



    public class HelperSeach2
    {


        public static List<(FamilySymbol FamilySymbol, double maxFamilySymbol, ElementId elementId)> DataSovpad = new List<(FamilySymbol FamilySymbol, double maxFamilySymbol, ElementId elementId)> ();
        public static (Element pile,  HashSet<string> PossibleNamesFamilySymbol) GetExistFamily(HashSet<string> PossibleNamesFamilySymbol, ExternalCommandData commandData)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            if (PossibleNamesFamilySymbol == null) throw new ArgumentNullException(nameof(PossibleNamesFamilySymbol));

            List<Element> collection;
            using (var collector = new FilteredElementCollector(doc))
                collection = collector.OfClass(typeof(FamilySymbol)).ToElements().OrderBy(e => e.Id.Value).ToList();

            int iter = -1;
            Element pileMax = null;
            string pileMaxName = null;

            HashSet<string> PossibleNamesChange = new HashSet<string>(PossibleNamesFamilySymbol);
            bool famExist = false;
            while (iter<6)
            {
                iter++;

                DataSovpad.Clear();

               

                double maxSimilarity = 0;
                pileMax = null;
                pileMaxName = null;


                foreach (Element element in collection)
                {
                    var name = element.Name;
                    foreach (string PossibleName in PossibleNamesChange)
                    {
                        if (string.IsNullOrWhiteSpace(PossibleName)) { continue; }

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
