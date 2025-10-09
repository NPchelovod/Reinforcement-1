using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]

    public class SetPilesByDWG : IExternalCommand
    {





        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            FilteredElementCollector collection = new FilteredElementCollector(doc);

            string familyName = "ЕС_Буронабивная свая";

            //Проверка есть ли в проекте "ЕС_Буронабивная свая"
            bool famExist = collection
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Any(family => family.Name.Equals(familyName));
            ElementId pileId = null;
            if (!famExist)
            {
                // Нечеткий поиск, если точный не дал результатов
                Family foundFamily = collection
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Select(family => new { Family = family, Similarity = CalculateSimilarity(familyName, family.Name) })
                    .Where(x => x.Similarity >= 0.7) // Пороговое значение схожести 70%
                    .OrderByDescending(x => x.Similarity)
                    .FirstOrDefault()?.Family;

                if (foundFamily != null)
                {
                    // Спросить пользователя о использовании найденного семейства
                    DialogResult result = MessageBox.Show(
                        $"Точное семейство '{familyName}' не найдено. Использовать '{foundFamily.Name}'?",
                        "Семейство не найдено",
                        MessageBoxButtons.YesNo);

                    if (result == DialogResult.Yes)
                    {
                        // Использовать найденное семейство
                        pileId = foundFamily.GetFamilySymbolIds().FirstOrDefault();
                        // Дальнейшая логика
                    }
                    else
                    {
                        MessageBox.Show($"Не найдено семейство {familyName}!");
                        return Result.Failed;
                    }
                }
                else
                {
                    MessageBox.Show($"Не найдено семейство {familyName}!");
                    return Result.Failed;
                }
            }
            else
            {
                // Продолжить с точным совпадением //Типоразмер нужной сваи
                pileId = collection.OfClass(typeof(Family))
                    .Where(x => x.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                    .Cast<Family>()
                    .FirstOrDefault()
                    .GetFamilySymbolIds()
                    .FirstOrDefault();
            }


            if (pileId == null)
            {
                
                MessageBox.Show($"Не найдено семейство {familyName}!");
                return Result.Failed;
                
            }


            var pile = doc.GetElement(pileId) as FamilySymbol;

            //Самый нижний уровень
            var check = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements();

            var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .FirstOrDefault();

            TransparentNotificationWindow.ShowNotification("Выберите подложку dwg", uidoc, 5);

            Reference sel = uidoc.Selection.PickObject(ObjectType.Element);          
            var dwg = doc.GetElement(sel);
            if (!(dwg is ImportInstance))
            {
                MessageBox.Show("Выбрана не подложка!\n" + "Категория должна быть ImportInstance");
                return Result.Failed;
            }

            Options opt = new Options()
            {
                ComputeReferences = true,
                View = doc.ActiveView
            };

            var geom = dwg.get_Geometry(opt).First() as GeometryInstance;
            var geomList = geom.GetInstanceGeometry()
                .OfType<Point>()
                .Select(x => x.Coord)                
                .ToList();

            try //ловим ошибку
            {
                using (Transaction t = new Transaction(doc, "действие"))
                {
                    t.Start();
                    //Тут пишем основной код для изменения элементов модели
                    if (pile != null && !pile.IsActive)
                    {
                        pile.Activate();
                        doc.Regenerate();
                    }
                    foreach (XYZ point in geomList) 
                    {
                        doc.Create.NewFamilyInstance(point, pile, level, Autodesk.Revit.DB.Structure.StructuralType.Footing);
                    }
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                //Код в случае ошибки
                MessageBox.Show("Чет пошло не так!\n" + ex.Message);
                return Result.Failed;
            }
            return Result.Succeeded;
        }
        // Метод для вычисления схожести строк
        public static double CalculateSimilarity(string s1, string s2)
        {
            string normalized1 = NormalizeString(s1);
            string normalized2 = NormalizeString(s2);

            int lcsLength = LongestCommonSubstring(normalized1, normalized2);
            int maxLength = Math.Max(normalized1.Length, normalized2.Length);

            return maxLength > 0 ? (double)lcsLength / maxLength : 0;
        }

        // Метод нормализации строки
        public static string NormalizeString(string input)
        {
            return string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        }

        // Ваш метод поиска наибольшей общей подстроки
        public static int LongestCommonSubstring(string s1, string s2)
        {
            int maxLength = 0;
            int[,] dp = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                        maxLength = Math.Max(maxLength, dp[i, j]);
                    }
                }
            }
            return maxLength;
        }
    }
}
