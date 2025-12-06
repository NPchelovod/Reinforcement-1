using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
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
using System.Windows;
using System.Windows.Forms;

namespace Reinforcement
{
    
    public class PileData
    {
        public Element Pile { get; set; }
        public int Xs { get; set; }
        public int Ys { get; set; }
        public int Zs { get; set; }

        public int Xs2 { get; set; }
        public int Ys2 { get; set; }
        public int Zs2 { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Name { get; set; }
        public int NumPile { get; set; }
        public PilesGroup PilesGroup { get; set; }
        public int PilesYGO { get; set; }


        public PileData(Element pile,int xs, int ys, int zs, int xs2, int ys2, int zs2, double x, double y, double z, string name, int numPile, PilesGroup pilesGroup, int pilesYGO)
        {
            Pile = pile;
            Xs = xs;
            Ys = ys;
            Zs = zs;

            Xs2 = xs2;
            Ys2 = ys2;
            Zs2 = zs2;


              X = x;
            Y = y;
            Z = z;
            Name = name;
            NumPile = numPile;
            PilesGroup = pilesGroup;
            PilesYGO = pilesYGO;
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class NumPiles : IExternalCommand
    {

        //имена типоразмеров семейства

        private static HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };
        private static List<string> nameMarks = new List<string> { "Марка" };
        private static string nameYGO = "ADSK_Типоразмер элемента узла";//ADSK_Типоразмер элемента узла<Элементы узлов>" };
        private static string YGOPrefix = "ADSK_ЭУ_УсловноеОбозначениеСваи : УГО_";

        private static  double sectorStep = 1400; // шаг поиска свай
        private static double sectorStepPile = 250;// округление координаты одной сваи
        private static double sectorStepZ = 100; // шаг разбивки УГО по высоте
        private static int predelGroup = 12; // предел наполнения иначе принудительно для каждого элемента
        private static bool ustanNumPile = true;
        private static bool ustanUGO = false;
        private static bool returnCoord = false;


        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                RevitAPI.Initialize(commandData);
                UIDocument uidoc = RevitAPI.UiDocument;
                Document doc = RevitAPI.Document;
                // 1. Находим сваи
                var Seacher = HelperSeachAllElements.SeachAllElements(Piles, commandData, true);

                if (Seacher.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Сваи не найдены");
                    return Result.Failed;
                }

                // 2. Показываем окно настроек
                var settingsWindow = new PileSettingsWindow(
                Seacher.Count,
                sectorStep,
                sectorStepPile, // Добавлен новый параметр
                sectorStepZ,
                predelGroup,
                ustanNumPile,
                ustanUGO,
                returnCoord // Добавлен новый параметр
                );

                // Устанавливаем владельца окна
                var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                var windowWrapper = new System.Windows.Interop.WindowInteropHelper(settingsWindow);
                windowWrapper.Owner = revitWindow;

                bool? dialogResult = settingsWindow.ShowDialog();

                if (dialogResult != true || !settingsWindow.ContinueExecution)
                {
                    return Result.Cancelled;
                }

                // 3. Получаем новые настройки из окна
                sectorStep = settingsWindow.SectorStep;
                sectorStepZ = settingsWindow.SectorStepZ;
                predelGroup = settingsWindow.PredelGroup;
                ustanNumPile = settingsWindow.UstanNumPile;
                ustanUGO = settingsWindow.UstanUGO;
                sectorStepPile = settingsWindow.SectorStepPile;
                returnCoord = settingsWindow.ReturnCoord;
                // 4. Продолжаем выполнение с новыми параметрами
                return ProcessPiles(Seacher, commandData, doc);
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}\n{ex.StackTrace}";
                TaskDialog.Show("Критическая ошибка", message);
                return Result.Failed;
            }
        }
        private Result ProcessPiles(
                HashSet<Element> Seacher,
                ExternalCommandData commandData,
                Document doc)
        {

            ForgeTypeId units = UnitTypeId.Millimeters;
            //var DictPiles = new Dictionary<(int Xs, int Ys), D<Element>>();

            var PropertiesPiles = new HashSet< PileData>();

            var DictSector = new Dictionary<(int Xs, int Ys, string name), HashSet<PileData>>(); // сектор и имя сваи

            var allNamesPile = new HashSet<string>();
            var HashDataTypeYGO = new HashSet<(string name,int intName, int Z)>(); //потенциальное УГО


            if(sectorStepPile<1)
            {
                sectorStepPile = 1;
            }
            if (sectorStep<1)
            {
                sectorStep = 1;
            }

            // Собираем информацию о сваях
            foreach (Element pile in Seacher)
            {
                // получаем координаты
                LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
                if (tek_locate == null) continue; // Добавьте проверку
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

                double coord_X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                //шаг поиска свай соседей
                //определение сектора дипсик то так то сяк пишет
                //int Xs = (int)Math.Floor(coord_X / sectorStep);
                //int Ys = (int)Math.Floor(coord_Y / sectorStep);
                //int Zs = (int)Math.Floor(coord_Z / sectorStepZ);
                int Xs = (int)Math.Round(coord_X / sectorStep);
                int Ys = (int)Math.Round(coord_Y / sectorStep);
                int Zs = (int)Math.Round(coord_Z / sectorStepZ);

                //шаг округления координат свай в одном кусте
                int Xs2 = (int)Math.Round(coord_X / sectorStepPile);
                int Ys2 = (int)Math.Round(coord_Y / sectorStepPile);
                int Zs2 = (int)Math.Round(coord_Z / sectorStepPile);

                string name = pile.Name;
                PilesGroup pilesGroup = null;
                allNamesPile.Add(name);

                var PileClass = new PileData(pile, Xs, Ys, Zs, Xs2, Ys2, Zs2, coord_X, coord_Y, coord_Z, name, -1, pilesGroup, -1);

                PropertiesPiles.Add(PileClass);
                var sector = (Xs, Ys, name);
                if (DictSector.ContainsKey(sector))
                {
                    DictSector[sector].Add(PileClass);
                }
                else
                {
                    DictSector[sector] = new HashSet<PileData> { PileClass };
                }
                HashDataTypeYGO.Add((name,-1, Zs));
            }

            //сортируем все имена в порядке возрастания

            var ListNamesPiles = PileNameSorter.SortPileNamesByLength(allNamesPile);
            
            //получение УГО потенциального
            var listDataTypeYGO = HashDataTypeYGO.ToList();
            for (int i = 0; i < listDataTypeYGO.Count; i++)
            {
                var tekYGO = listDataTypeYGO[i];
                listDataTypeYGO[i] = (tekYGO.name, ListNamesPiles.IndexOf(tekYGO.name), tekYGO.Z);
            }

            listDataTypeYGO = listDataTypeYGO.OrderBy(p => p.intName).ThenBy(p => p.Z)
                            .ToList();

            //заполняем свойство уго
            // Создаем словарь для быстрого поиска индекса YGO по имени и Z координате
            var ygoIndexDict = new Dictionary<(string name, int Z), int>();
            for (int i = 0; i < listDataTypeYGO.Count; i++)
            {
                var item = listDataTypeYGO[i];
                ygoIndexDict[(item.name, item.Z)] = i+1;
            }
            

            // Теперь обновляем PropertiesPiles
            
            foreach (var pileClass in PropertiesPiles)
            {
                int nameIndex = ListNamesPiles.IndexOf(pileClass.Name);

                // Ищем YGO индекс через словарь
                int ugo = -1;
                // Сначала ищем по полному имени
                if (!ygoIndexDict.TryGetValue((pileClass.Name, pileClass.Zs), out ugo))
                {
                   //тут была альтернатива
                }
                pileClass.PilesYGO = ugo;
            }


            var ListPilesGroup = new List<PilesGroup>();

            //создаем группы свай
            
            // Используйте:
            var sectorKeys = DictSector.Keys.ToList();
            foreach (var pileSector in sectorKeys)
            {
                var elementPile = DictSector[pileSector].FirstOrDefault();
                if (elementPile != null && elementPile.PilesGroup == null)
                {
                    
                     ListPilesGroup.Add(new PilesGroup(pileSector, ListNamesPiles.IndexOf(pileSector.name), DictSector, sectorStepPile));
                    
                }
            }

            //обрубаем группы свай если в их элементов оч много
            // Обрубаем группы если слишком много элементов
            if (predelGroup > 0)
            {
                foreach (var pileSector in DictSector.Keys.ToList())
                {
                    var elementPile = DictSector[pileSector].FirstOrDefault();
                    
                    if (elementPile != null)
                    {
                        if (elementPile.PilesGroup==null || elementPile.PilesGroup.intPiles > predelGroup)
                        {
                            if (elementPile.PilesGroup != null)
                            { 
                                ListPilesGroup.Remove(elementPile.PilesGroup); 
                            }
                            ListPilesGroup.Add(new PilesGroup(pileSector, ListNamesPiles.IndexOf(pileSector.name), DictSector, sectorStepPile,  true));
                        }
                    }
                }
            }
            //сортировка групп свай
            //теперь сортируем сначала по оси x идя по оси y
            if (ustanNumPile)
            {
                if (!returnCoord)
                {
                    ListPilesGroup = ListPilesGroup
                    .OrderBy(group => group.numName)          // по возрастанию numName
                    .ThenByDescending(group => group.Center.y) // по убыванию Y (сверху вниз)
                    .ThenBy(group => group.Center.x)         // по возрастанию X (слева направо)
                    .ToList();
                }
                else
                {
                    // по x надо слева направо
                    ListPilesGroup = ListPilesGroup
                    .OrderBy(group => group.numName)          // по возрастанию numName
                    .ThenBy(group => group.Center.x) // по убыванию Y (сверху вниз)
                    .ThenByDescending(group => group.Center.y)         // по возрастанию X (слева направо)
                    .ToList();
                }
            }

             // Нумерация свай
            int numPile = 0;
            foreach (var classPile in ListPilesGroup)
            {
                //сваи одной группы
                var allPilesGroup = classPile.Piles.ToList();
                if (ustanNumPile)//накладно ведь каждый раз
                {
                    //сваи сортируем по секторам позволяющим в один ряд их укладывать
                    if (!returnCoord)
                    {
                        allPilesGroup = allPilesGroup
                        .OrderByDescending(pile => pile.Ys2) // по убыванию Y (сверху вниз)
                        .ThenBy(pile => pile.Xs2)         // по возрастанию X (слева направо)
                        .ToList();
                    }
                    else
                    {
                        allPilesGroup = allPilesGroup
                        .OrderBy(pile => pile.Xs2) // по убыванию Y (сверху вниз)
                        .ThenByDescending(pile => pile.Ys2)         // по возрастанию X (слева направо)
                        .ToList();
                    }
                }

                foreach (var pile in allPilesGroup)
                {
                    numPile++;
                    pile.NumPile = numPile;
                }

            }
            

            // Начинаем транзакцию для установки марок

           

            using (Transaction trans = new Transaction(doc, "Установка марок свай и УГО"))
            {
                try
                {
                    trans.Start();

                    int successCount = 0;
                    int failCount = 0;

                    int successCount2 = 0;
                    int failCount2 = 0;

                    var failedElements = new List<ElementId>();
                    var failedElements2 = new List<ElementId>();
                    bool resultNum = false;
                    bool resultUGO = false;
                    foreach (var kvp in PropertiesPiles)
                    {
                        Element pile = kvp.Pile;
                        int markValue = kvp.NumPile;
                        
                        
                        if (ustanNumPile)
                        {
                            resultNum = SetPileMark(pile, markValue.ToString(), nameMarks);
                        }
                        if (ustanUGO && kvp.PilesYGO>0)
                        {
                            string YGOValue = YGOPrefix + kvp.PilesYGO;
                            resultUGO = SetYGO(pile, kvp.PilesYGO);
                           //resultUGO = SetPileMark(pile, YGOValue, nameYGO); 
                        }

                        // Проверяем оба результата в зависимости от настроек
                        if (ustanNumPile && !resultNum)
                        {
                            failCount++;
                            failedElements.Add(pile.Id);
                        }
                        else if (ustanNumPile && resultNum)
                        {
                            successCount++;
                        }

                        if (ustanUGO && !resultUGO)
                        {
                            failCount2++;
                            failedElements2.Add(pile.Id);
                        }
                        else if (ustanUGO && resultUGO)
                        {
                            successCount2++;
                        }


                    }

                    trans.Commit();

                    // Показываем результат
                    // Показ результата
                    string resultMessage = $"Всего свай: {PropertiesPiles.Count}\n";
                    if (ustanNumPile)
                    {
                        resultMessage += $"Установлено марок: {successCount}\nНе удалось: {failCount}\n";
                    }
                    if (ustanUGO)
                    {
                        resultMessage += $"Установлено УГО: {successCount2}\nНе удалось: {failCount2}\n";
                    }

                    resultMessage  +=$"Всего свай: {PropertiesPiles.Count}";

                    //if (failedElements.Count > 0)
                    //{
                    //    resultMessage += $"\n\nСписок ID неудачных элементов (первые 10):\n";
                    //    resultMessage += string.Join("\n", failedElements.Take(10).Select(id => id.IntegerValue));

                    //    if (failedElements.Count > 10)
                    //    {
                    //        resultMessage += $"\n... и еще {failedElements.Count - 10} элементов";
                    //    }
                    //}

                    TaskDialog.Show("Результат", resultMessage);

                    // Дополнительно: создаем отчет о нумерации
                    //CreateNumberingReport(PropertiesPiles, ListPilesGroup);

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Ошибка транзакции", $"Ошибка при установке марок: {ex.Message}");
                    return Result.Failed;
                }
            }
        }


            // Метод для создания отчета о нумерации
        private void CreateNumberingReport(
            Dictionary<Element, (int Xs, int Ys, int Zs, double x, double y, double z, string Name, int numPile, PilesGroup pilesGroup)> properties,
            List<PilesGroup> groups)
        {
            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine("=== ОТЧЕТ О НУМЕРАЦИИ СВАЙ ===");
                report.AppendLine($"Параметры:");
                report.AppendLine($"• Шаг сектора: {sectorStep} мм");
                report.AppendLine($"• Шаг по высоте: {sectorStepZ} мм");
                report.AppendLine($"• Лимит группы: {predelGroup}");
                report.AppendLine();
                report.AppendLine($"Всего свай: {properties.Count}");
                report.AppendLine($"Всего групп: {groups.Count}");
                report.AppendLine();

                // Статистика по группам
                report.AppendLine("=== СТАТИСТИКА ПО ГРУППАМ ===");
                foreach (var group in groups.OrderBy(g => g.numName).ThenBy(g => g.namePile))
                {
                    report.AppendLine($"Группа: {group.namePile}");
                    report.AppendLine($"• Центр: X={group.Center.x}, Y={group.Center.y}");
                    report.AppendLine($"• Секторов: {group.intPiles}");
                    report.AppendLine($"• Свай: {group.Piles.Count}");
                    report.AppendLine();
                }

                // Сохраняем отчет в файл
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string reportPath = System.IO.Path.Combine(desktopPath, $"Отчет_нумерации_свай_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                System.IO.File.WriteAllText(reportPath, report.ToString(), System.Text.Encoding.UTF8);

                TaskDialog.Show("Отчет создан", $"Отчет сохранен на рабочем столе:\n{System.IO.Path.GetFileName(reportPath)}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка отчета", $"Не удалось создать отчет: {ex.Message}");
            }
        }





        
        // Метод для установки марки сваи
        private bool SetPileMark(Element pile, string markValue, List<string> parameterNames)
        {
            Parameter markParam = null;

            foreach (var paramName in parameterNames)
            {
                markParam = pile.LookupParameter(paramName);
                if (markParam != null) break;
            }

            if (markParam == null) return false;
            if (markParam.IsReadOnly) return false;

            try
            {
                return markParam.Set(markValue);
            }
            catch
            {
                return false;
            }
        }
        public bool SetYGO(Element pile, int ygoIndex)
        {
            Parameter ygoParam = pile.LookupParameter(nameYGO);
            if (ygoParam == null) return false;
            if (ygoParam.IsReadOnly) return false;

            // Пробуем установить целым числом (индексом, начиная с 1)
            try
            {
                if (ygoParam.Set(ygoIndex))
                    return true;
            }
            catch { }

            // Пробуем установить целым числом (индекс-1, начиная с 0)
            try
            {
                if (ygoParam.Set(ygoIndex - 1))
                    return true;
            }
            catch { }

            // Пробуем установить строкой
            string possibleName = YGOPrefix + ygoIndex;
            

            try
            {
                if (ygoParam.Set(possibleName))
                    return true;
            }
            catch { }
            

            return false;
        }



    }


    public class PilesGroup
    {
        public HashSet<PileData> Piles = new HashSet<PileData>();

        public HashSet<(int Xs, int Ys, string name)> dataPiles = new HashSet<(int Xs, int Ys, string name)>();

        

        //public List<(int Xs, int Ys, string name)> PilesSort = new List<(int Xs, int Ys, string name)> (); // отсортированный по координатам

        public int  numName =0;
        public string namePile = "";

        public int intPiles = 1;// но это кол-во секторов а не кол-во свай!!!!!
        public (int x, int y) Center;






        public PilesGroup(
            (int Xs, int Ys, string name) CurrentPiles,
            int numName,
             Dictionary<(int Xs, int Ys, string name), HashSet<PileData>> DictSector, double sectorStepPile, bool prinudOne = false)
        {

           


            // когда обьявили элемент pile ищем его родственников всех

            //если prinudOne = true то только один элемент в класс

            namePile = CurrentPiles.name;

            //номер имени
            this.numName = numName;
            //теперь ищем родственные сваи аналог графа
            var listInt = new List<int> { -1,0, 1 };

            var HashSeachPile = new HashSet<(int Xs, int Ys, string name)> { CurrentPiles }; // найденные новые элементы
            dataPiles.Add(CurrentPiles);

            //Center = ((double)CurrentPiles.Xs, (double)CurrentPiles.Ys);

            
            if (!prinudOne)
            { //находим все типы
                while (HashSeachPile.Count > 0)
                {
                    //и начинаем циркуляцию
                    var HashSeachPile2 = new HashSet<(int Xs, int Ys, string name)>();// будет переприсвоено в конце
                    foreach (var CurrentPiles2 in HashSeachPile)
                    {
                        int Xs = CurrentPiles2.Xs;
                        int Ys = CurrentPiles2.Ys;

                        // Проверяем всех соседей
                        foreach (int i in listInt)
                        {
                            foreach (int j in listInt)
                            {
                                // Пропускаем саму текущую точку
                                if (i == 0 && j == 0) continue;

                                (int Xs, int Ys, string) sector2 = (Xs + i, Ys + j, namePile);

                                // Если уже в группе - пропускаем
                                if (dataPiles.Contains(sector2))
                                    continue;

                                if (DictSector.TryGetValue(sector2, out var piles))
                                {
                                    HashSeachPile2.Add(sector2);
                                    dataPiles.Add(sector2);

                                    //int addIntPiles = piles.Count;
                                    //// Пересчитываем центр масс группы с учетом нового сектора
                                    ////Center = (
                                    ////    (Center.Xs * (double)intPiles + (double)(sector2.Xs * addIntPiles)) / ((double)(intPiles + addIntPiles)),
                                    ////    (Center.Ys * (double)intPiles + (double)(sector2.Ys * addIntPiles)) / ((double)(intPiles + addIntPiles))
                                    ////);

                                    //intPiles += addIntPiles;
                                }
                            }
                        }
                    
                }

                  HashSeachPile = HashSeachPile2;

                }
            }
            //а теперь наполняем
            double x = 0;
            double y = 0;
            foreach (var sector in dataPiles)
            {
                if (DictSector.TryGetValue(sector, out var piles))
                {
                    foreach (var pile in piles)
                    {
                        Piles.Add(pile);
                        pile.PilesGroup = this;
                        x += pile.X;
                        y += pile.Y;

                    }
                }
            }
            intPiles = Piles.Count;
            if (intPiles > 0)
            {
                //надо по сектору иначе нумератор свай плохой!!!!!
                //но сектор берем свайный для точности
                Center = ((int)Math.Round((x / (double)intPiles)/ sectorStepPile), (int)Math.Round((y / (double)intPiles) / sectorStepPile));

                // Используйте реальные координаты, а не секторы:
               // Center = ((int)Math.Round(x / (double)intPiles), (int)Math.Round(y / (double)intPiles));
            }
            else
            {
                Center = ( 0,  0);
            }



            //dataPilesSort = dataPiles
            //.OrderByDescending(group =>!returnCoord? group.Ys: group.Xs) // по убыванию Y (сверху вниз)
            //.ThenBy(group => !returnCoord ? group.Xs: group.Ys)         // по возрастанию X (слева направо)
            //.ToList();

        }


    }
    public class PileNameSorter
    {
        public static List<string> SortPileNamesByLength(HashSet<string> pileNames)
        {
            return pileNames
                .Select(name => new PileNameInfo(name))
                .OrderBy(info => info, new PileNameInfoComparer())
                .Select(info => info.OriginalName)
                .ToList();
        }
    }

    public class PileNameInfo
    {
        public string OriginalName { get; }
        public List<double> Numbers { get; }
        public string TextPart { get; }

        public PileNameInfo(string name)
        {
            OriginalName = name;
            Numbers = ExtractAllNumbers(name);
            TextPart = ExtractTextPart(name);
        }

        private List<double> ExtractAllNumbers(string input)
        {
            var numbers = new List<double>();

            if (string.IsNullOrEmpty(input))
                return numbers;

            // Ищем все числа (целые и дробные)
            // Разделители: точка, запятая, дефис, пробел, скобки и т.д.
            string pattern = @"[\d]+(?:[.,]\d+)?";

            var matches = Regex.Matches(input, pattern);

            foreach (Match match in matches)
            {
                // Заменяем запятую на точку для корректного парсинга
                string numberStr = match.Value.Replace(',', '.');

                if (double.TryParse(numberStr, out double number))
                {
                    numbers.Add(number);
                }
            }

            return numbers;
        }

        private string ExtractTextPart(string input)
        {
            // Удаляем все числа и разделители чисел, оставляем только текст
            string pattern = @"[\d]+(?:[.,]\d+)?";
            string result = Regex.Replace(input, pattern, "");

            // Удаляем лишние разделители
            result = Regex.Replace(result, @"[-_\s.,]+", " ").Trim();

            return result.ToLower();
        }
    }

    public class PileNameInfoComparer : IComparer<PileNameInfo>
    {
        public int Compare(PileNameInfo x, PileNameInfo y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // Сравниваем по всем числам последовательно
            int maxLength = Math.Max(x.Numbers.Count, y.Numbers.Count);

            for (int i = 0; i < maxLength; i++)
            {
                double numX = i < x.Numbers.Count ? x.Numbers[i] : 0;
                double numY = i < y.Numbers.Count ? y.Numbers[i] : 0;

                int comparison = numX.CompareTo(numY);
                if (comparison != 0)
                    return comparison;
            }

            // Если все числа равны, сравниваем по текстовой части
            return string.Compare(x.TextPart, y.TextPart, StringComparison.OrdinalIgnoreCase);
        }
    }

}