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
    [Transaction(TransactionMode.Manual)]

    public class NumPiles : IExternalCommand
    {

        //имена типоразмеров семейства

        private HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };

        private double sectorStep = 1400; // шаг поиска свай
        private double sectorStepZ = 100; // шаг разбивки УГО по высоте
        private int predelGroup = 12; // предел наполнения иначе принудительно для каждого элемента
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            var Seacher = HelperSeachAllElements.SeachAllElements(Piles, commandData, true);

            if (Seacher.Count == 0 || sectorStep < 1)
            { return Result.Failed; }

            ForgeTypeId units = UnitTypeId.Millimeters;
            //var DictPiles = new Dictionary<(int Xs, int Ys), D<Element>>();

            var PropertiesPiles = new Dictionary<Element, (int Xs, int Ys, int Zs, double x, double y, double z, string Name, int numPile, PilesGroup pilesGroup)>();
            var DictSector = new Dictionary<(int Xs, int Ys, string name), HashSet<Element>>(); // сектор и имя сваи

            var allNamesPile = new HashSet<string>();

            foreach (Element pile in Seacher)
            {
                // получаем координаты
                LocationPoint tek_locate = pile.Location as LocationPoint; // текущая локация вентканала
                XYZ tek_locate_point = tek_locate.Point; // текущая координата расположения

                double coord_X = UnitUtils.ConvertFromInternalUnits(tek_locate_point.X, units); // a ConvertToInternalUnits переводит наоборот из метров в футы
                double coord_Y = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Y, units);
                double coord_Z = UnitUtils.ConvertFromInternalUnits(tek_locate_point.Z, units);

                //определение сектора
                int Xs = (int)Math.Round(coord_X / sectorStep);
                int Ys = (int)Math.Round(coord_Y / sectorStep);
                int Zs = (int)Math.Round(coord_Z / sectorStepZ);
                string name = pile.Name;
                PilesGroup pilesGroup = null;
                allNamesPile.Add(name);
                PropertiesPiles[pile] = (Xs, Ys, Zs, coord_X, coord_Y, coord_Z, name, -1, pilesGroup);
                var sector = (Xs, Ys, name);
                if (DictSector.ContainsKey(sector))
                {
                    DictSector[sector].Add(pile);
                }
                else
                {
                    DictSector[sector] = new HashSet<Element> { pile };
                }
            }

            //сортируем все имена в порядке возрастания

            var ListNamesPiles=PileNameSorter.SortPileNamesByLength(allNamesPile);


            var ListPilesGroup = new List<PilesGroup>();
            //создаем группы свай
            foreach (var pileSector in DictSector.Keys)
            {
                var elementPile = DictSector[pileSector].FirstOrDefault();
                if (PropertiesPiles.TryGetValue(elementPile, out var Propertypiles))
                {
                    if (Propertypiles.pilesGroup == null)
                    {
                        ListPilesGroup.Add(new PilesGroup(pileSector, ListNamesPiles.IndexOf(pileSector.name), PropertiesPiles, DictSector));
                    }
                }
            }

            //обрубаем группы свай если в их элементов оч много
            if(predelGroup>0)
            {
                foreach (var pileSector in DictSector.Keys)
                {
                    var elementPile = DictSector[pileSector].FirstOrDefault();
                    if (PropertiesPiles.TryGetValue(elementPile, out var Propertypiles))
                    {
                        if (Propertypiles.pilesGroup == null || Propertypiles.pilesGroup.intPiles> predelGroup)
                        {
                            ListPilesGroup.Add(new PilesGroup(pileSector, ListNamesPiles.IndexOf(pileSector.name), PropertiesPiles, DictSector,true));
                        }
                    }
                }
            }

            //теперь сортируем сначала по оси x идя по оси y
            ListPilesGroup = ListPilesGroup
            .OrderBy(group => group.numName)          // по возрастанию numName
            .ThenByDescending(group => group.Center.Ys) // по убыванию Y (сверху вниз)
            .ThenBy(group => group.Center.Xs)         // по возрастанию X (слева направо)
            .ToList();



            int numPile = 0;
            foreach(var classPile in ListPilesGroup)
            {
                foreach(var pileSector in classPile.dataPilesSort)
                {
                    if(DictSector.TryGetValue(pileSector,out var piles))
                    {
                        //сортировка внутри сектора
                        // Сортируем сваи внутри сектора
                        var sortedPilesInSector = piles
                            .Select(p => new {
                                Pile = p,
                                Props = PropertiesPiles[p]
                            })
                            .OrderByDescending(p => p.Props.y)  // По Y убывание (сверху вниз)
                            .ThenBy(p => p.Props.x)            // По X возрастание (слева направо)
                            .ToList();
                        foreach (var pileData in sortedPilesInSector)
                        {

                            numPile++;
                            PropertiesPiles[pileData.Pile] = (
                                pileData.Props.Xs,
                                pileData.Props.Ys,
                                pileData.Props.Zs,
                                pileData.Props.x,
                                pileData.Props.y,
                                pileData.Props.z,
                                pileData.Props.Name,
                                numPile,
                                pileData.Props.pilesGroup);
                        }

                    }
                }
            }
            // Начинаем транзакцию для установки марок
            using (Transaction trans = new Transaction(doc, "Установка марок свай"))
            {
                try
                {
                    trans.Start();

                    int successCount = 0;
                    int failCount = 0;

                    foreach (var kvp in PropertiesPiles)
                    {
                        Element pile = kvp.Key;
                        int markValue = kvp.Value.numPile;

                        bool result = SetPileMark(pile, markValue);

                        if (result)
                            successCount++;
                        else
                            failCount++;
                    }

                    trans.Commit();

                    // Показываем результат
                    string resultMessage = $"Установлено марок: {successCount}\n" +
                                          $"Не удалось установить: {failCount}\n" +
                                          $"Всего свай: {PropertiesPiles.Count}";

                    TaskDialog.Show("Результат", resultMessage);

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    message = $"Ошибка при установке марок: {ex.Message}";
                    TaskDialog.Show("Ошибка", message);
                    return Result.Failed;
                }
            }




            


        }
        // Метод для установки марки сваи
        private bool SetPileMark(Element pile, int markValue)
        {
            try
            {
                // Пробуем разные варианты параметра "Марка"
                Parameter markParam = null;

                // 1. Стандартный параметр "Марка"
                markParam = pile.LookupParameter("Марка");

                // 2. Параметр "Марка элемента"
                if (markParam == null)
                    markParam = pile.LookupParameter("Марка элемента");

                // 3. Параметр "Mark" (английский)
                if (markParam == null)
                    markParam = pile.LookupParameter("Mark");

                // 4. Встроенный параметр ALL_MODEL_MARK
                if (markParam == null)
                    markParam = pile.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);

                // 5. Параметр "ADSK_Марка" (Autodesk)
                if (markParam == null)
                    markParam = pile.LookupParameter("ADSK_Марка");

                // 6. Ищем любой параметр, содержащий "марк" в названии (без учета регистра)
                if (markParam == null)
                {
                    foreach (Parameter param in pile.Parameters)
                    {
                        if (param.Definition.Name.IndexOf("марк", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            markParam = param;
                            break;
                        }
                    }
                }

                if (markParam != null)
                {
                    // Проверяем, можно ли записывать в параметр
                    if (markParam.IsReadOnly)
                    {
                        TaskDialog.Show("Предупреждение",
                            $"Параметр '{markParam.Definition.Name}' только для чтения у элемента {pile.Id}");
                        return false;
                    }

                    // Устанавливаем значение в зависимости от типа хранения
                    if (markParam.StorageType == StorageType.Integer)
                    {
                        markParam.Set(markValue);
                    }
                    else if (markParam.StorageType == StorageType.String)
                    {
                        markParam.Set(markValue.ToString());
                    }
                    else if (markParam.StorageType == StorageType.Double)
                    {
                        markParam.Set((double)markValue);
                    }
                    else
                    {
                        // Пробуем как строку
                        try
                        {
                            markParam.Set(markValue.ToString());
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    return false;
                    // Пробуем создать общий параметр, если нет подходящего
                    //return CreateAndSetMarkParameter(pile, markValue);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка установки марки",
                    $"Ошибка для элемента {pile.Id}: {ex.Message}");
                return false;
            }
        }



    }


    public class PilesGroup
    {
        public HashSet<Element> Piles = new HashSet<Element>();

        public HashSet<(int Xs, int Ys, string name)> dataPiles = new HashSet<(int Xs, int Ys, string name)>();

        public List<(int Xs, int Ys, string name)> dataPilesSort = new List<(int Xs, int Ys, string name)> (); // отсортированный по координатам

        public int  numName =0;
        public string namePile = "";

        public int intPiles = 1;// но это кол-во секторов а не кол-во свай!!!!!
        public (int Xs, int Ys) Center;






        public PilesGroup((int Xs, int Ys, string name) CurrentPiles, int numName,
            Dictionary<Element, (int Xs, int Ys, int Zs, double x, double y, double z, string Name, int numPile, PilesGroup pilesGroup)> PropertiesPiles,
             Dictionary<(int Xs, int Ys, string name), HashSet<Element>> DictSector, bool prinudOne = false)
        {
            // когда обьявили элемент pile ищем его родственников всех

            //если prinudOne = true то только один элемент в класс

            namePile = CurrentPiles.name;

            //номер имени
            this.numName = numName;
            //теперь ищем родственные сваи аналог графа
            var listInt = new List<int> { -1, 1 };

            var HashSeachPile = new HashSet<(int Xs, int Ys, string name)> { CurrentPiles }; // найденные новые элементы
            dataPiles.Add(CurrentPiles);

            Center = (CurrentPiles.Xs, CurrentPiles.Ys);
            intPiles = DictSector[CurrentPiles].Count;

            if (!prinudOne)
            { //находим все типы
                while (HashSeachPile.Count > 0)
                {
                    //и начинаем циркуляцию
                    var HashSeachPile2 = new HashSet<(int Xs, int Ys, string name)>();// будет переприсвоено в конце
                    foreach (var CurrentPiles2 in HashSeachPile)
                    {
                        dataPiles.Add(CurrentPiles2);
                        //ищем в кругую
                        int Xs = CurrentPiles2.Xs;
                        int Ys = CurrentPiles2.Ys;
                        foreach (int i in listInt)
                        {
                            foreach (int j in listInt)
                            {
                                (int Xs, int Ys, string) sector2 = (Xs + i, Ys + i, namePile);
                                if (dataPiles.Contains(sector2))
                                { continue; }
                                if (DictSector.TryGetValue(sector2, out var piles))
                                {
                                    HashSeachPile2.Add(sector2);
                                    dataPiles.Add(sector2);

                                    int addIntPiles = piles.Count;

                                    Center = ((Center.Xs * intPiles + sector2.Xs * addIntPiles) / (intPiles + addIntPiles), (Center.Ys * intPiles + sector2.Ys * addIntPiles) / (intPiles + addIntPiles));

                                    intPiles += addIntPiles;//

                                }

                            }
                        }
                    }

                    HashSeachPile = HashSeachPile2;

                }
            }
            //а теперь наполняем
            foreach (var sector in dataPiles)
            {
                if (DictSector.TryGetValue(sector, out var piles))
                {
                    foreach (var pile in piles)
                    {
                        Piles.Add(pile);
                        if (PropertiesPiles.TryGetValue(pile, out var dataPile))
                        {
                            PropertiesPiles[pile] = (dataPile.Xs, dataPile.Ys, dataPile.Zs, dataPile.x, dataPile.y, dataPile.z, dataPile.Name, -1, this);
                        }

                    }
                }
            }
            dataPilesSort = dataPiles
            .OrderByDescending(group => group.Ys) // по убыванию Y (сверху вниз)
            .ThenBy(group => group.Xs)         // по возрастанию X (слева направо)
            .ToList();

        }


    }
    public class PileNameSorter
    {
        public static List<string> SortPileNamesByLength(HashSet<string> pileNames)
        {
            return pileNames
                .OrderBy(name => ExtractFirstNumber(name))
                .ThenBy(name => name) // если числа равны, сортируем по полному имени
                .ToList();
        }

        private static double ExtractFirstNumber(string input)
        {
            if (string.IsNullOrEmpty(input))
                return double.MaxValue; // без числа - в конец

            // Ищем первое число (целое или дробное) в строке
            Match match = Regex.Match(input, @"\d+(\.\d+)?");

            if (match.Success && double.TryParse(match.Value, out double number))
            {
                return number;
            }

            return double.MaxValue; // если число не найдено - в конец
        }
    }
    
    }