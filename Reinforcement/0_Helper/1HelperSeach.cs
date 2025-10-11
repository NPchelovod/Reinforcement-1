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

        public static (ElementId pileId, HashSet<string> PossibleNames)  GetExistFamily(HashSet<string> PossibleNames, ExternalCommandData commandData )
        {
            //надо например расставить сваи по виду, вот находит по имени совпадению сваю эту наиболее близкую id и возвращает ее
            //PossibleNames = "ЕС_Буронабивная свая"
            // или предлагает ввести имя семейства

            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;


            ElementId pileId = null;
            FilteredElementCollector collection = new FilteredElementCollector(doc);

            string familyName = null;
            bool famExist = false;
            foreach (string PossibleName in PossibleNames)
            {
                //Проверка есть ли в проекте "ЕС_Буронабивная свая"
                famExist = collection
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Any(family => family.Name.Equals(PossibleName));
                if (famExist)
                {
                    familyName = PossibleName;
                    break;
                }
            }

            if (famExist) 
            {
                //ищем id
                // Продолжить с точным совпадением //Типоразмер нужной сваи
                pileId = collection.OfClass(typeof(Family))
                    .Where(x => x.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                    .Cast<Family>()
                    .FirstOrDefault()
                    .GetFamilySymbolIds()
                    .FirstOrDefault();
            }

            if (!famExist)// не нашли в лоб
            {
                // ищем так
                // Нечеткий поиск, если точный не дал результатов
                var sravnData = collection
                    .OfClass(typeof(Family))
                    .Cast<Family>();
                double maxSimilarity = 0;
                string familyNameMax = "";
                ElementId pileIdMax = null;
                foreach (string PossibleName in PossibleNames)
                {
                    //Family foundFamily = sravnData
                    //.Select(family => new { Family = family, Similarity = HelperPrivateStatic.CalculateSimilarity(familyName, family.Name) })
                    //.Where(x => x.Similarity >= 0.7) // Пороговое значение схожести 70%
                    //.OrderByDescending(x => x.Similarity)
                    //.FirstOrDefault()?.Family;
                    foreach (var family in sravnData)
                    {
                        var Similarity = HelperPrivateStatic.CalculateSimilarity(PossibleName, family.Name);
                        if (Similarity > maxSimilarity)
                        {
                            maxSimilarity = Similarity;
                            familyNameMax = family.Name;
                            pileIdMax = family.Id;
                            //!!!!!!!! остановка чтоб не ходить много
                            if (maxSimilarity > StopSovpad)
                            { break; }
                        }
                    }

                }



                // Спросить пользователя о использовании найденного семейства
                if (maxSimilarity < StopSovpad)
                {
                    // Спросить пользователя о использовании найденного семейства
                    DialogResult result = MessageBox.Show(
                    $"Точное семейство '{PossibleNames.FirstOrDefault()}' не найдено. Использовать '{familyNameMax}'?",
                    "Семейство не найдено",
                    MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        famExist = true;
                        familyName = familyNameMax;
                        pileId = pileIdMax;
                        PossibleNames.Add(familyName);
                    }

                    else
                    {
                        famExist = false;
                    }
                }
                else
                {
                    famExist = true;
                    familyName = familyNameMax;
                    pileId = pileIdMax;
                    PossibleNames.Add(familyName);
                }


                if (!famExist)
                {
                    //предлагаем ввести вручную
                    while (true)
                    {
                        var input = HelperPrivateStatic.GetUserInputWithForm();
                        if (!input.Item2) { break; }
                        string PossibleName = input.Item1;

                        maxSimilarity = 0;
                        foreach (var family in sravnData)
                        {
                            var Similarity = HelperPrivateStatic.CalculateSimilarity(PossibleName, family.Name);
                            if (Similarity > maxSimilarity)
                            {
                                maxSimilarity = Similarity;
                                familyNameMax = family.Name;
                                pileIdMax = family.Id;
                                if (maxSimilarity > StopSovpad)
                                { break; }
                            }

                        }

                        if (maxSimilarity < StopSovpad)
                        {
                            DialogResult result = MessageBox.Show(
                            $"Точное семейство '{PossibleName}' не найдено. Использовать '{familyNameMax}'?",
                            "Семейство не найдено",
                            MessageBoxButtons.YesNo);
                            if (result == DialogResult.Yes)
                            {
                                famExist = true;
                                familyName = familyNameMax;
                                pileId = pileIdMax;
                                PossibleNames.Add(familyName);
                                break;
                            }
                            else
                            {
                                famExist = false;
                            }
                        }
                        else
                        {
                            famExist = true;
                            familyName = familyNameMax;
                            pileId = pileIdMax;
                            PossibleNames.Add(familyName);
                            break;
                        }

                    }

                }

            }
            if (!famExist)
            {
                pileId = null;
                return (pileId, PossibleNames);
            }
            else
            {
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
