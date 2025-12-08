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

        public string Commit { get; set; } = "";


        public PilesGroup PilesGroup { get; set; }


        private int pilesYgo = -1;
        public int PilesYGO 
        {
            get
            {
                if(pilesYgo<1)
                {
                    pilesYgo = YgoIndexDict[(Name, Zs)].nomer;
                }
                return pilesYgo;
            }
                
        }

        private Dictionary<(string name, int Z), (int nomer, int numPile)> YgoIndexDict;

        public PileData(Element pile,int xs, int ys, int zs, int xs2, int ys2, int zs2, double x, double y, double z, string name, int numPile, PilesGroup pilesGroup,  Dictionary<(string name, int Zs), (int nomer, int numPile)> ygoIndexDict)
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
            YgoIndexDict = ygoIndexDict;
            //YgoIndexDict[(Name, Zs)] = -1;
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
        private static List<string> namePrimech = new List<string> { "ADSK_Примечание" };


        private static string nameYGO = "ADSK_Типоразмер элемента узла";//ADSK_Типоразмер элемента узла<Элементы узлов>" };
        private static string YGOPrefix = "ADSK_ЭУ_УсловноеОбозначениеСваи : УГО_";

        private static  double sectorStep = 1150; // шаг поиска свай
        private static double sectorStepPile = 1000;// округление координаты одной сваи
        private static double sectorStepZ = 100; // шаг разбивки УГО по высоте
        private static int predelGroup = 20; // предел наполнения иначе принудительно для каждого элемента
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
           // var HashDataTypeYGO = new HashSet<(string name,int intName, int Zs)>(); //потенциальное УГО


            if(sectorStepPile<1)
            {
                sectorStepPile = 10;
            }
            if (sectorStep<1)
            {
                sectorStep = 10;
            }
            if (sectorStepZ < 1)
            {
                sectorStepZ = 50;
            }
            if (predelGroup < 0)
            {
                predelGroup = 1;
            }


            //QuickUGOAudit(doc);

            if (ustanUGO)
            {
                // Инициализируем кэш типов УГО один раз для этого документа
                InitializeUgoCache(doc);
            }

            if(! ustanUGO&& !ustanNumPile)
            {
                return Result.Succeeded;
            }

            // Собираем информацию о сваях
            var ygoIndexDict = new Dictionary<(string name, int Zs), (int nomer, int numPile)>();

            var namePileAndNum = new Dictionary<string, int >();
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

                var PileClass = new PileData(pile, Xs, Ys, Zs, Xs2, Ys2, Zs2, coord_X, coord_Y, coord_Z, name, -1, pilesGroup, ygoIndexDict);

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

                //записываем количество уникальных УГО
                if (ygoIndexDict.TryGetValue((name, Zs), out var past))
                {
                    ygoIndexDict[(name, Zs)] = (past.nomer, past.numPile + 1);
                }
                else
                {
                    ygoIndexDict[(name, Zs)] = (-1, 1);
                }

                if(namePileAndNum.TryGetValue(name, out var pastInt))
                {
                    namePileAndNum[name] = pastInt + 1;
                }
                else
                {
                    namePileAndNum[name] = 1;
                }

                    //HashDataTypeYGO.Add((name, -1, Zs));

            }
            
            //сортируем все имена в порядке возрастания
            var ListNamesPiles = PileNameSorter.SortPileNamesByLength(allNamesPile);
            //создаем список для словаря
            var listForYgoSort = new List<(string name,int numName, int Zs, int numPile)>();
            foreach(var ygoData in ygoIndexDict)
            {
                listForYgoSort.Add((ygoData.Key.name, ListNamesPiles.IndexOf(ygoData.Key.name), ygoData.Key.Zs, ygoData.Value.numPile  ));
            }
            //по номеру имени сваи по кол-ву свай одного имени и затем по высотной отметке
            var listDataTypeYGO = listForYgoSort.OrderBy(p => p.numName).ThenByDescending(p => p.numPile).ThenBy(p => p.Zs).ToList();


            //получение УГО потенциального
           // var listDataTypeYGO = HashDataTypeYGO.ToList();
           //теперь заполняем словарь свойства
            for (int i = 0; i < listDataTypeYGO.Count; i++)
            {
                var tekYGO = listDataTypeYGO[i];
                if(ygoIndexDict.TryGetValue((tekYGO.name, tekYGO.Zs), out var past))
                {
                    ygoIndexDict[(tekYGO.name, tekYGO.Zs)] = (i + 1, past.numPile);
                }
                
            }



            var ListPilesGroup = new List<PilesGroup>();

            //создаем группы свай

            // Используйте:
            if (predelGroup != 1 && ustanNumPile)
            {
                var sectorKeys = DictSector.Keys.ToList();
                foreach (var pileSector in sectorKeys)
                {
                    var elementPile = DictSector[pileSector].FirstOrDefault();
                    if (elementPile != null && elementPile.PilesGroup == null)
                    {
                        if (DictSector.TryGetValue(pileSector, out var piles) && piles.Count > 0)
                        {
                            ListPilesGroup.Add(new PilesGroup(piles.FirstOrDefault(), namePileAndNum[pileSector.name], ListNamesPiles.IndexOf(pileSector.name), DictSector));
                        }

                    }
                }
            }

            //обрубаем группы свай если в их элементов оч много
            // Обрубаем группы если слишком много элементов
            if (predelGroup > 0 ||!ustanNumPile)
            {
                foreach (var pile in PropertiesPiles)
                {
                    if(pile.PilesGroup == null || pile.PilesGroup.intPiles > predelGroup)
                    {
                        if (pile.PilesGroup != null)
                        {
                            ListPilesGroup.Remove(pile.PilesGroup);
                        }
                        ListPilesGroup.Add(new PilesGroup(pile, namePileAndNum[pile.Name], ListNamesPiles.IndexOf(pile.Name), DictSector, true));
                    }
                }
            }
            //сортировка групп свай
            //теперь сортируем сначала по оси x идя по оси y
            if (ustanNumPile)
            {
                if (!returnCoord)
                {
                    //ListPilesGroup = ListPilesGroup
                    //.OrderBy(group => group.numName)          // по возрастанию numName
                    //.ThenByDescending(group => group.Center.y) // по убыванию Y (сверху вниз)
                    //.ThenBy(group => group.Center.x)         // по возрастанию X (слева направо)
                    //.ToList();
                    ListPilesGroup = ListPilesGroup
                   .OrderBy(group => group.kolVoPileName)          // по возрастанию numName group.numName
                   .OrderBy(group => group.numName)
                   .ThenByDescending(group => group.Ytop) // по убыванию Y (сверху вниз)
                   .ThenBy(group => group.Xleft)         // по возрастанию X (слева направо)
                   
                   .ToList();
                }
                else
                {
                    // по x надо слева направо
                    //ListPilesGroup = ListPilesGroup
                    //.OrderBy(group => group.numName)          // по возрастанию numName
                    //.ThenBy(group => group.Center.x) // по убыванию Y (сверху вниз)
                    //.ThenByDescending(group => group.Center.y)         // по возрастанию X (слева направо)
                    //.ToList();
                    ListPilesGroup = ListPilesGroup
                   .OrderBy(group => group.kolVoPileName)          // по возрастанию numName
                   .OrderBy(group => group.numName)
                   .ThenBy(group => group.Xleft) // по убыванию Y (сверху вниз)
                   .ThenByDescending(group => group.Ytop)         // по возрастанию X (слева направо)
                   .ToList();
                }
            }

            // Нумерация свай
            int numPile = 0;
            int kust = 0;
            foreach (var classPile in ListPilesGroup)
            {
                kust++;
                //сваи одной группы
                var allPilesGroup = classPile.Piles.ToList();
                if (ustanNumPile && allPilesGroup.Count>0)//накладно ведь каждый раз
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
                    string primeh = "УГО_" + pile.PilesYGO + ", КУСТ_" + kust+", Сектор Xs2="+ pile.Xs2+", Ys2="+ pile.Ys2;
                    pile.Commit = primeh;
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
                        

                        string primeh = kvp.Commit;
                        if (primeh != "")
                        {
                            SetPileMark(pile, primeh, namePrimech);
                        }

                        if (ustanNumPile)
                        {
                            resultNum = SetPileMark(pile, markValue.ToString(), nameMarks);


                        }
                        if (ustanUGO && kvp.PilesYGO>0)
                        {
                            string YGOValue = YGOPrefix + kvp.PilesYGO;
                            //resultUGO = SetUGOValue(pile, kvp.PilesYGO);
                            //FamilyInstance pileInstance = pile as FamilyInstance;
                            //if (pileInstance != null)
                            //{
                            //    resultUGO = SetUGOValue(doc, pile as FamilyInstance, kvp.PilesYGO);
                            //}
                            resultUGO = SetUGOValue(doc, pile, kvp.PilesYGO);
                            //SetUGOValue(Document doc, FamilyInstance pileInstance, int ygoIndex)
                            //resultUGO = SetYGO(pile, kvp.PilesYGO);
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


        // 1. ОБЪЯВЛЯЕМ СТАТИЧЕСКИЙ СЛОВАРЬ ДЛЯ КЭШИРОВАНИЯ
        // Ключ: Имя типа (например, "УГО_1"), Значение: ElementId этого типа
        private static Dictionary<string, ElementId> _ugoTypeCache = null;

        // 2. МЕТОД ДЛЯ ИНИЦИАЛИЗАЦИИ (ЗАПОЛНЕНИЯ) СЛОВАРЯ
        private static void InitializeUgoCache(Document doc, string prefix="УГО_")
        {
            if (_ugoTypeCache != null) return; // Уже инициализирован

            _ugoTypeCache = new Dictionary<string, ElementId>();

            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol));

            //foreach (FamilySymbol symbol in collector)
            //{
            //    string symbolName = symbol.Name;
            //    // Сохраняем ВСЕ типы, которые могут быть УГО (или начинаются на "УГО_")
            //    // Это позволит быстро находить их позже.
            //    _ugoTypeCache[symbolName] = symbol.Id;

            //    // Опционально: можно добавить логирование для отладки
            //    // TaskDialog.Show("Кэш", $"Добавлено в кэш: {symbolName} -> {symbol.Id.IntegerValue}");
            //}

            // Если вы точно знаете, что нужны только типы, начинающиеся с "УГО_",
            // можно фильтровать сразу здесь, уменьшив размер словаря:
            // var ugoSymbols = collector.Cast<FamilySymbol>().Where(s => s.Name.StartsWith("УГО_"));
            // foreach (var symbol in ugoSymbols) { _ugoTypeCache[symbol.Name] = symbol.Id; }
            var ugoSymbols = collector.Cast<FamilySymbol>().Where(s => s.Name.StartsWith(prefix));
            foreach (var symbol in ugoSymbols) { _ugoTypeCache[symbol.Name] = symbol.Id; }


        }

        // 3. ОПТИМИЗИРОВАННЫЙ МЕТОД SetUGOValue
        private bool SetUGOValue(Document doc, Element pileElement, int ygoIndex)
        {
            // Убедимся, что кэш инициализирован (делаем это один раз за запуск)
            if (_ugoTypeCache == null)
            {
                InitializeUgoCache(doc);
            }

            // 1. Формируем имя типа
            string targetUgoName = "УГО_" + ygoIndex;

            // 2. Пытаемся получить ID типа ИЗ КЭША (мгновенно!)
            if (!_ugoTypeCache.TryGetValue(targetUgoName, out ElementId targetTypeId))
            {
                // Если не нашли в кэше, значит, такого типа действительно нет в проекте
                //TaskDialog.Show("Ошибка",
                //    $"Тип '{targetUgoName}' не найден в проекте.\n" +
                //    $"Возможно, в проекте нет типов УГО, или их имена отличаются.\n" +
                //    $"Доступные имена в кэше: {string.Join(", ", _ugoTypeCache.Keys.OrderBy(k => k))}");
                return false;
            }

            // 3. Нашли ID! Теперь находим и устанавливаем параметр на свае.
            Parameter ugoParam = pileElement.LookupParameter("ADSK_Типоразмер элемента узла");

            if (ugoParam == null || ugoParam.IsReadOnly)
            {
                // Можно не показывать диалог для каждой ошибки, а просто вернуть false
                // и вести статистику в основном методе
                return false;
            }

            // 4. Устанавливаем значение
            try
            {
                return ugoParam.Set(targetTypeId);
            }
            catch
            {
                return false;
            }
        }

        private bool ppSetUGOValue(Document doc, Element pileElement, int ygoIndex)
        {
            // 1. Формируем имя типа, который ищем
            string targetUgoName = "УГО_" + ygoIndex; // Например, "УГО_2"

            // 2. Ищем в проекте ВСЕ типы семейств (FamilySymbol)
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol));

            FamilySymbol targetSymbol = null;

            // 3. Перебираем и ищем по ТОЧНОМУ совпадению имени типа (Symbol.Name)
            foreach (FamilySymbol symbol in collector)
            {
                // СРАВНИВАЕМ ИМЯ ТИПА. Это самое важное!
                if (symbol.Name == targetUgoName)
                {
                    targetSymbol = symbol;
                    break; // Нашли, выходим из цикла
                }
            }

            // 4. Если тип не найден, выводим ошибку и список похожих имен для отладки
            if (targetSymbol == null)
            {
                // Собираем все имена типов для отладки
                List<string> allSymbolNames = new List<string>();
                foreach (FamilySymbol s in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)))
                {
                    allSymbolNames.Add(s.Name);
                }
                string allNames = string.Join("\n", allSymbolNames.OrderBy(n => n));

                TaskDialog.Show("Ошибка поиска типа УГО",
                    $"Тип семейства с именем '{targetUgoName}' не найден в проекте.\n\n" +
                    $"Доступные имена типов в проекте:\n{allNames}");
                return false;
            }

            // 5. Нашли тип! Теперь находим и устанавливаем параметр на свае.
            // Параметр называется именно так, как вы указали:
            Parameter ugoParam = pileElement.LookupParameter("ADSK_Типоразмер элемента узла");

            if (ugoParam == null)
            {
                TaskDialog.Show("Ошибка", "На свае не найден параметр 'ADSK_Типоразмер элемента узла'");
                return false;
            }

            if (ugoParam.IsReadOnly)
            {
                TaskDialog.Show("Ошибка", "Параметр 'ADSK_Типоразмер элемента узла' доступен только для чтения");
                return false;
            }

            // 6. УСТАНАВЛИВАЕМ ЗНАЧЕНИЕ. Это ключевая строка!
            // Мы передаем в параметр ID найденного типа семейства (FamilySymbol)
            try
            {
                // Метод Set() для параметра типа ElementId ожидает именно ElementId
                bool success = ugoParam.Set(targetSymbol.Id);
                return success;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка установки значения",
                    $"Не удалось установить значение параметра.\nОшибка: {ex.Message}");
                return false;
            }
        }



        public void QuickUGOAudit(Document doc)
        {
            //для проверки был создан
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== БЫСТРЫЙ АУДИТ СВАЙ И УГО ===");

            // 1. Какие типы УГО есть в проекте?
            sb.AppendLine("\n1. ВСЕ типы УГО в проекте (FamilySymbol):");
            var allUGOSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => s.Name.Contains("УГО")); // Ищем все, что содержит "УГО"

            foreach (var symbol in allUGOSymbols)
            {
                sb.AppendLine($"   - Имя: '{symbol.Name}', ID: {symbol.Id.IntegerValue}, Семейство: '{symbol.Family?.Name}'");
            }

            // 2. Проверяем параметр на первой попавшейся свае
            sb.AppendLine("\n2. Проверка параметра на случайной свае:");
            var testPile = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WhereElementIsNotElementType()
                .FirstElement();

            if (testPile != null)
            {
                sb.AppendLine($"   Свая: ID{testPile.Id.IntegerValue}, Имя: {testPile.Name}");
                Parameter testParam = testPile.LookupParameter("ADSK_Типоразмер элемента узла");

                if (testParam != null)
                {
                    sb.AppendLine($"   Параметр найден. StorageType: {testParam.StorageType}");
                    if (testParam.HasValue)
                    {
                        if (testParam.StorageType == StorageType.ElementId)
                        {
                            ElementId id = testParam.AsElementId();
                            sb.AppendLine($"   Текущее значение ID: {id?.IntegerValue}");
                            if (id != null)
                            {
                                Element elem = doc.GetElement(id);
                                sb.AppendLine($"   Соответствующий элемент: {elem?.Name} (Тип: {elem?.GetType()?.Name})");
                            }
                        }
                        sb.AppendLine($"   AsValueString: '{testParam.AsValueString()}'");
                    }
                }
                else
                {
                    sb.AppendLine("   Параметр НЕ НАЙДЕН!");
                }
            }

            TaskDialog.Show("Аудит УГО", sb.ToString());
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

        // Исправленный метод для установки параметра УГО
        // Исправленный метод для установки параметра УГО
        private bool pSetUGOValue(Document doc, Element pileElement, int ygoIndex)
        {
            // 1. Находим целевой элемент УГО в проекте
            string targetUgoName = "УГО_" + ygoIndex;

            // Ищем экземпляры семейства с нужным именем типа
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType();

            FamilyInstance targetUgoInstance = null;

            foreach (FamilyInstance instance in collector)
            {
                // Проверяем имя типа семейства (не имя экземпляра!)
                if (instance.Symbol != null && instance.Symbol.Name == targetUgoName)
                {
                    // Дополнительная проверка на нужное семейство
                    if (instance.Symbol.Family != null &&
                        instance.Symbol.Family.Name.Contains("УсловноеОбозначениеСваи"))
                    {
                        targetUgoInstance = instance;
                        break;
                    }
                }
            }

            // 2. Если не нашли экземпляр, ищем тип (FamilySymbol)
            if (targetUgoInstance == null)
            {
                // Ищем FamilySymbol по имени
                FilteredElementCollector symbolCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol));

                FamilySymbol targetSymbol = null;
                foreach (FamilySymbol symbol in symbolCollector)
                {
                    if (symbol.Name == targetUgoName)
                    {
                        // Проверяем, что это нужное семейство
                        if (symbol.Family != null &&
                            symbol.Family.Name.Contains("УсловноеОбозначениеСваи"))
                        {
                            targetSymbol = symbol;
                            break;
                        }
                    }
                }

                if (targetSymbol != null)
                {
                    // Используем тип (FamilySymbol)
                    return SetUGOParameter(pileElement, targetSymbol.Id);
                }
                else
                {
                    TaskDialog.Show("Ошибка поиска", $"Не найден элемент или тип: {targetUgoName}");
                    return false;
                }
            }

            // 3. Если нашли экземпляр, используем его ID
            return SetUGOParameter(pileElement, targetUgoInstance.Id);
        }
        // Вспомогательный метод для установки параметра
        private bool SetUGOParameter(Element pileElement, ElementId ugoElementId)
        {
            // Пробуем разные возможные имена параметра
            string[] possibleParamNames = new string[]
            {
        "ADSK_Типоразмер элемента узла",
        "ADSK_Типоразмер элемента узла<Элементы узлов>",
        "Условное обозначение",
        "УГО"
            };

            foreach (string paramName in possibleParamNames)
            {
                Parameter ugoParam = pileElement.LookupParameter(paramName);
                if (ugoParam != null && !ugoParam.IsReadOnly)
                {
                    try
                    {
                        // Устанавливаем ссылку на элемент УГО
                        bool success = ugoParam.Set(ugoElementId);
                        return success;
                    }
                    catch
                    {
                        // Пробуем следующий вариант имени параметра
                        continue;
                    }
                }
            }

            // Если ни один параметр не найден или не удалось установить
            return false;
        }
        private bool SetUGOValue(Document doc, FamilyInstance pileInstance, int ygoIndex)
        {
            // 1. Формируем имя типа, который ищем (например, "УГО_2")
            string targetTypeName = "УГО_" + ygoIndex;

            // 2. Ищем в проекте все типы семейств (FamilySymbol)
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType();

            FamilySymbol targetSymbol = null;
            foreach (FamilySymbol symbol in collector)
            {
                // Ищем точное совпадение по имени
                if (symbol.Name == targetTypeName)
                {
                    targetSymbol = symbol;
                    break;
                }
            }

            // 3. Если тип не найден, возвращаем false
            if (targetSymbol == null)
            {
                // Для отладки можно добавить сообщение
                // TaskDialog.Show("Ошибка", $"Не найден тип семейства: {targetTypeName}");
                return false;
            }

            // 4. Назначаем свае новый тип
            try
            {
                // Основной способ: прямое назначение Symbol
                // Убедитесь, что код выполняется внутри транзакции!
                pileInstance.Symbol = targetSymbol;
                return true;
            }
            catch
            {
                // Альтернативный способ: через параметр типа
                try
                {
                    Parameter typeParam = pileInstance.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM);
                    if (typeParam != null && !typeParam.IsReadOnly)
                    {
                        return typeParam.Set(targetSymbol.Id);
                    }
                }
                catch
                {
                    return false;
                }
                return false;
            }
        }


        private bool SetUGOValue(Element pile, int ygoIndex)
        {
            ygoIndex++;// можеьт нулевой того...
            try
            {
                Parameter ygoParam = pile.LookupParameter("ADSK_Типоразмер элемента узла");
                if (ygoParam == null || ygoParam.IsReadOnly) return false;

                // Для списочных параметров УСТАНАВЛИВАЕМ ЧИСЛОВОЕ ЗНАЧЕНИЕ!
                // ygoIndex - это индекс в списке (обычно начинается с 0 или 1)

                // Важно: в вашем случае YGO начинается с 1
                // Поэтому нужно установить значение от 1 до N

                if (ygoIndex >= 1) // Убедитесь, что индекс не отрицательный
                {
                    try
                    {
                        // Пробуем установить как целое число
                        return ygoParam.Set(ygoIndex);
                    }
                    catch (Exception ex1)
                    {
                        // Если не получается, пробуем установить как строку
                        try
                        {
                            return ygoParam.Set(ygoIndex.ToString());
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
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


        //для красоты не по центру масс нумеровать а по крайним точкам
        public int Xleft = 0;
        public int Ydown = 0;
        public int Xright = 0;
        public int Ytop = 0;

        private static int _numCreate = 0;
        public int numCreate = 0;

        public int kolVoPileName = 1;

        public PilesGroup(
            PileData CurrentPile, int kolVoPileName,
            int numName,
             Dictionary<(int Xs, int Ys, string name), HashSet<PileData>> DictSector, bool prinudOne = false)
        {

            this.kolVoPileName = kolVoPileName;
            _numCreate++;
            numCreate = _numCreate;

            // когда обьявили элемент pile ищем его родственников всех

            //если prinudOne = true то только один элемент в класс

            namePile = CurrentPile.Name;
            (int Xs, int Ys, string name) SectorParent = (CurrentPile.Xs, CurrentPile.Ys, CurrentPile.Name);



            //номер имени
            this.numName = numName;
            //теперь ищем родственные сваи аналог графа
            var listInt = new List<int> { -1,0, 1 };

            var HashSeachPile = new HashSet<(int Xs, int Ys, string name)> { SectorParent }; // найденные новые элементы

            //dataPiles.Add(CurrentPiles);

            //Center = ((double)CurrentPiles.Xs, (double)CurrentPiles.Ys);

            Piles.Add(CurrentPile);
            if (!prinudOne)
            { //находим все типы
                dataPiles.Add(SectorParent);
                Piles.UnionWith(DictSector[SectorParent]);
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
                                { 
                                    continue; 
                                }

                                if (DictSector.TryGetValue(sector2, out var piles))
                                {
                                    HashSeachPile2.Add(sector2);
                                    dataPiles.Add(sector2);
                                    Piles.UnionWith(piles);
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
            int x = 0;
            int y = 0;
            int iter = 0;
            foreach (var pile in Piles)
            {
                
                pile.PilesGroup = this;
                
                x += pile.Xs2;
                y += pile.Ys2;

                if (iter == 0)
                {
                     Xleft = pile.Xs2;
                     Ydown = pile.Ys2;
                     Xright = pile.Xs2;
                     Ytop = pile.Ys2;
                }
                else
                {
                    Xleft = Math.Min(Xleft, pile.Xs2);
                    Ydown  = Math.Min(Ydown, pile.Ys2);
                    Xright = Math.Max(pile.Xs2, Xright);
                    Ytop = Math.Max(pile.Ys2, Ytop);
                }

                iter++;
            }

            intPiles = Piles.Count;
            if (intPiles > 0)
            {
                //надо по сектору иначе нумератор свай плохой!!!!!
                //но сектор берем свайный для точности
                Center = ((int)Math.Round(((double)x / (double)intPiles)), (int)Math.Round(((double)y / (double)intPiles) ));

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