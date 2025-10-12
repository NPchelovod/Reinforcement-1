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
        private const double StopSovpad = 0.95;// с такой долей слова считаются одинаковыми

        private static Dictionary<string, ElementId> familyNames = new Dictionary<string, ElementId>();
        private static Document docPast = null;
        private static int pastFamily = 0;

        private static int popitka = 0;
        private static int popitkamax = 4;

        //словарь ускорений
        private static Dictionary<HashSet<string>, ElementId> PastExistData = new Dictionary<HashSet<string>, ElementId>();


        public static (ElementId pileId, HashSet<string> PossibleNames)  GetExistFamily(HashSet<string> PossibleNames, ExternalCommandData commandData )
        {
            //надо например расставить сваи по виду, вот находит по имени совпадению сваю эту наиболее близкую id и возвращает ее
            //PossibleNames = "ЕС_Буронабивная свая"
            // или предлагает ввести имя семейства

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;


            ElementId pileId = null;
            var collection = new FilteredElementCollector(doc).OfClass(typeof(Family));

            string familyName = null;
            bool famExist = false;

            //чтобы по многу раз не находить
            if (familyNames.Count == 0 || collection.Count()!= pastFamily || docPast != doc || popitka > popitkamax)
            {
                familyNames.Clear();
                PastExistData.Clear();
                foreach (var element in collection)
                {
                    familyNames[element.Name] = element.Id;
                }
                popitka = 0;
                pastFamily = collection.Count();
                docPast = doc;
            }
            else
            {
                popitka++;
                if (PastExistData.TryGetValue(PossibleNames,out pileId))
                {
                    famExist=true;// из словаря найти прошлое
                }
            }

            

            HashSet<string> PossibleNamesChange = new HashSet<string>(PossibleNames);
            int circle = -1;
            while (!famExist && circle < 7)
            {
                circle++;
                double maxSimilarity = 0;
                string familyNameMax = "";
                ElementId pileIdMax = null;

                foreach (string PossibleName in PossibleNamesChange)
                {
                    //Проверка есть ли в проекте "ЕС_Буронабивная свая"
                    foreach (var name in familyNames.Keys)
                    {

                        var Similarity = HelperPrivateStatic.CalculateSimilarity(PossibleName, name);
                        if (Similarity>0.7 && Similarity > maxSimilarity)
                        {
                            maxSimilarity = Similarity;
                            familyNameMax = name;
                            pileIdMax = familyNames[name];
                            //!!!!!!!! остановка чтоб не ходить много
                            if (maxSimilarity > 0.99 && name.Count() > 6)
                            {
                                famExist = true;
                                familyName = familyNameMax;
                                pileId = pileIdMax;
                                break; 
                            }
                        }
                    }
                }

                if (famExist)
                {
                    break;
                }

                // Спросить пользователя о использовании найденного семейства
                DialogResult result = MessageBox.Show(
                $"Точное семейство '{PossibleNames.FirstOrDefault()}' не найдено. Использовать '{familyNameMax}'?",
                "Семейство не найдено",
                MessageBoxButtons.YesNo);

                if (result == DialogResult.Yes && maxSimilarity>0.2)
                {
                    famExist = true;
                    familyName = familyNameMax;
                    pileId = pileIdMax;
                    PossibleNames.Add(familyName);
                    break;
                }
                int circle2 = -1;

                bool proxod=true;
                while (circle2<5)
                {
                    circle2++;
                    var input = HelperPrivateStatic.GetUserInputWithForm();
                    if (!input.Item2) { proxod = false; break; }
                    PossibleNamesChange.Clear();

                    if (input.Item1.Count() > 5)
                    {
                        PossibleNamesChange.Add(input.Item1);
                        break;
                    }
                }
                if(!proxod)
                {
                    break;
                }
            }

            if (!famExist)
            {
                pileId = null;
                return (pileId, PossibleNames);
            }
            else
            {
                if (familyName!=null && !PossibleNames.Contains(familyName))
                { 
                    PossibleNames.Add(familyName);
                
                }
                PastExistData[PossibleNames] = pileId;
                return (pileId, PossibleNames);
            }

        }


        public List<HelperGetData> GetAllFamily()
        {
            //а тут мы получаем все данные элемента в заданном формате

            var listHelperGetData = new List<HelperGetData>();

            return listHelperGetData;
        }


    }


}
