using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using static Autodesk.Revit.DB.SpecTypeId;


namespace Reinforcement
{
    

    [Transaction(TransactionMode.Manual)]
    public partial class NumPiles : IExternalCommand
    {
        private static int SizePile3D=900;// меньше которого нельзя
        //имена типоразмеров семейства

        private static HashSet<string> Piles = new HashSet<string>()
        {
            //"ЕС_Буронабивная свая",  "ЕС_Буронабивная Свая"
            "ADSK_Свая_", "ЕС_Буронабивная, ЕС_Свая", "Свая", "свая"
        };

        private string Marka = "Марка";
        private static List<string> nameMarks = new List<string> { "Марка" };
        private static List<string> namePrimech = new List<string> { "ADSK_Примечание" };


        public static string nameYGO = "ADSK_Типоразмер элемента узла";//ADSK_Типоразмер элемента узла<Элементы узлов>" };
        private static string YGOPrefix = "ADSK_ЭУ_УсловноеОбозначениеСваи : УГО_";


        // В классе NumPiles добавьте новые статические переменные в начало класса:
        public static bool adjustPilePositions = false;
        public static bool recreateAllPiles = false;
        public static double minDistanceBetweenPiles = 900;
        public static double coordinateRoundingStep = 25;

        public static  double sectorStep = 1010; // шаг поиска соседей свай
        public static double sectorStepPile = 510;// округление координаты одной сваи
        public static double sectorStepZ = 100; // шаг разбивки УГО по высоте
        public static int predelGroup = 51; // предел наполнения иначе принудительно для каждого элемента
        private static bool ustanNumPile = true;
        public bool WriterPrimech = false;
        private static bool ustanUGO = false;
        private static bool doNotRenumberNumberedPiles = false;
        private static bool doNotChangeUGOIfExist = false;



        public static string sortCode = "801346"; // тип 2
        private static string sortCodeUGO = "123"; // тип 2
        public bool RotorPiles { get; set; } = false;

        PileSettingsWindow SettingsWindow = null;

        public HashSet<Element> Seacher = new HashSet<Element>();
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
                Seacher = HelperSeachAllElements.SeachAllElements(Piles, commandData, true);

                if (Seacher.Count == 0)
                {
                    TaskDialog.Show("Ошибка", "Сваи не найдены");
                    return Result.Failed;
                }

                // 2. Показываем окно настроек
                SettingsWindow = new PileSettingsWindow(
                Seacher.Count,
                sectorStep,
                sectorStepPile, // Добавлен новый параметр
                sectorStepZ,
                predelGroup,
                ustanNumPile,
                ustanUGO,
                doNotRenumberNumberedPiles,
                doNotChangeUGOIfExist,
                sortCode, // Добавлен новый параметр
                sortCodeUGO,
                adjustPilePositions, // Новый параметр
                minDistanceBetweenPiles, // Новый параметр
                coordinateRoundingStep, // Новый параметр

                recreateAllPiles
                );

                // Устанавливаем владельца окна
                var revitWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                var windowWrapper = new System.Windows.Interop.WindowInteropHelper(SettingsWindow);
                windowWrapper.Owner = revitWindow;

                bool? dialogResult = SettingsWindow.ShowDialog();

                if (dialogResult != true || !SettingsWindow.ContinueExecution)
                {
                    return Result.Cancelled;
                }

                // 3. Получаем новые настройки из окна
                sectorStep = SettingsWindow.SectorStep;
                sectorStepZ = SettingsWindow.SectorStepZ;
                predelGroup = SettingsWindow.PredelGroup;
                ustanNumPile = SettingsWindow.UstanNumPile;
                ustanUGO = SettingsWindow.UstanUGO;
                doNotRenumberNumberedPiles = SettingsWindow.DoNotRenumberNumberedPiles;
                doNotChangeUGOIfExist = SettingsWindow.DoNotChangeUGOIfExists;
                sectorStepPile = SettingsWindow.SectorStepPile;
                sortCode = SettingsWindow.SortCode;
                sortCodeUGO = SettingsWindow.SortCodeUGO;
                WriterPrimech = SettingsWindow.WriterPrimech;
                adjustPilePositions = SettingsWindow.AdjustPilePositions;
                minDistanceBetweenPiles = SettingsWindow.MinDistanceBetweenPiles;
                coordinateRoundingStep = SettingsWindow.CoordinateRoundingStep;

                recreateAllPiles = SettingsWindow.RecreateAllPiles;
                RotorPiles = SettingsWindow.RotorPiles;
                // 4. Продолжаем выполнение с новыми параметрами
                // 3. ВСЕ операции в одной транзакционной группе
                using (TransactionGroup transGroup = new TransactionGroup(doc, "Полная обработка свай"))
                {
                    transGroup.Start();

                    try
                    {
                        CorrectData();//корректировка входгных данных
                        Result result = ProcessPiles( commandData, doc);

                        if (result == Result.Succeeded)
                        {
                            transGroup.Assimilate(); // Фиксируем все изменения
                            return Result.Succeeded;
                        }
                        else
                        {
                            transGroup.RollBack(); // Откатываем все изменения
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        transGroup.RollBack();
                        message = $"Ошибка: {ex.Message}\n{ex.StackTrace}";
                        TaskDialog.Show("Критическая ошибка", message);
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}\n{ex.StackTrace}";
                TaskDialog.Show("Критическая ошибка", message);
                return Result.Failed;
            }
        }

        public void CorrectData()
        {
            if (sectorStepPile < 1)
            {
                sectorStepPile = 10;
            }
            if (sectorStep < 1)
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
        }
       


        private void control3D(HashSet<PileData> PropertiesPiles, double size3D=900)
        {
            //взаименое пересечение свай
            var PropertiesPilesList = PropertiesPiles.ToList();
            for (int i = 0; i < PropertiesPilesList.Count; i++)
            {
                var pile1 = PropertiesPilesList[i];
                for (int j = i+1; j< PropertiesPilesList.Count; j++)
                {
                    var pile2 = PropertiesPilesList[j];
                    var raznX = Math.Abs(pile2.X - pile1.X);
                    if(raznX > size3D) {continue;}
                    var raznY = Math.Abs(pile2.Y - pile1.Y);
                    if (raznY > size3D) { continue; }

                    var dist = (int)Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY) - size3D);
                    if (dist>0) { continue; }
                    pile1.intersect3D = Math.Max(pile1.intersect3D, Math.Abs(dist));
                    pile2.intersect3D = Math.Max(pile2.intersect3D, Math.Abs(dist));
                }
            }
        }

        private HashSet<PilesGroup> IntersectSectors(HashSet<PileData> PropertiesPiles, double distSector, Dictionary<string, int> namePileAndNum, List<string> ListNamesPiles, int predelGroup, bool sortPilePoUgo)
        {
            var PropertiesPilesList = PropertiesPiles.ToList();
            
            var HashPilesGroup = new HashSet<PilesGroup> ();
            for (int i = 0; i < PropertiesPilesList.Count; i++)
            {
                var pile1 = PropertiesPilesList[i];
                var pilesGroup = pile1.PilesGroup;

                if (predelGroup != 1)
                {
                    for (int j = i + 1; j < PropertiesPilesList.Count; j++)
                    {
                        var pile2 = PropertiesPilesList[j];

                        if (pile1.Name != pile2.Name) { continue; } // не одинаковое имя - разные кусты кустики кустья

                        if ( pile1.PilesYGO!= pile2.PilesYGO) { continue; } //(sortPilePoUgo && Разные УГО в разные части
                        if(pile1.comentDouble!=pile2.comentDouble) { continue; }


                        var raznX = Math.Abs(pile2.X - pile1.X);
                        if (raznX > distSector) { continue; }
                        var raznY = Math.Abs(pile2.Y - pile1.Y);
                        if (raznY > distSector) { continue; }

                        var dist = (int)Math.Round(Math.Sqrt(raznX * raznX + raznY * raznY) - distSector);
                        if (dist > 0) { continue; }

                        //обнаружено пересечение
                        var pilesGroup2 = pile2.PilesGroup;
                        PilesGroup pileReplace = null; // группа замены в группу pilesGroup
                        if (pilesGroup2 == null && pilesGroup == null)
                        {
                            pilesGroup = new PilesGroup();
                            //pilesGroup.comentDouble=pile1.comentDouble;
                        }
                        else if (pilesGroup2 != null && pilesGroup == null)
                        {
                            pilesGroup = pilesGroup2;

                        }
                        else if (pilesGroup2 == null && pilesGroup != null)
                        {
                            //итак норм

                        }
                        else if (pilesGroup2 != null && pilesGroup != null)
                        {
                            pileReplace = pilesGroup2;// ну так принимаем группу замены

                        }

                        if (pileReplace != null)
                        {
                            HashPilesGroup.Remove(pileReplace);
                            foreach (var pile in pileReplace.Piles)
                            {
                                pilesGroup.Piles.Add(pile);
                                pile.PilesGroup = pilesGroup;
                            }
                        }
                        pilesGroup.Piles.Add(pile1);
                        pilesGroup.Piles.Add(pile2);
                        pile1.PilesGroup = pilesGroup;
                        pile2.PilesGroup = pilesGroup;
                    }
                }

                //ни одного пересечения и группы не создано
                if(pilesGroup==null)
                {
                    pilesGroup = new PilesGroup();
                    //pilesGroup.comentDouble = pile1.comentDouble;

                    pile1.PilesGroup = pilesGroup;
                    pilesGroup.Piles.Add(pile1);
                }
                HashPilesGroup.Add(pilesGroup);
            }


            var removeHashPilesGroup = new HashSet<PilesGroup>();
            var addPilesGroup = new HashSet<PilesGroup>();
            //находим центра масс
            foreach (var pilesGroup in HashPilesGroup)
            {
                var pile = pilesGroup.Piles.FirstOrDefault();
                if(pile == null) 
                {
                    removeHashPilesGroup.Add(pilesGroup);// пустую группу не её...
                    continue; 
                }
                if(predelGroup == 0 || predelGroup >= pilesGroup.Piles.Count)
                {
                    pilesGroup.Initializator(namePileAndNum[pile.Name], ListNamesPiles.IndexOf(pile.Name));
                }
                else
                {
                    //расформировываем эту группу
                    removeHashPilesGroup.Add(pilesGroup);
                    //и создаем из ее элементов отдельные
                    foreach(var piles in pilesGroup.Piles)
                    {
                        piles.PilesGroup = new PilesGroup();
                        piles.comentDouble = pile.comentDouble;
                        piles.PilesGroup.Piles.Add(piles);
                        piles.PilesGroup.Initializator(namePileAndNum[piles.Name], ListNamesPiles.IndexOf(piles.Name));
                        addPilesGroup.Add(piles.PilesGroup);
                    }

                }

                
            }

            foreach (var del in removeHashPilesGroup)
            {
                HashPilesGroup.Remove(del);
            }
            foreach (var add in addPilesGroup)
            {
                HashPilesGroup.Add(add);
            }

            return HashPilesGroup;

        }





        private List<(string name, int numName, int Zs, int numPile)> sortedUGO(List<(string name, int numName, int Zs, int numPile)> listForYgoSort, string sortCodeUGO, bool ustanUGO)
        {
            if (!ustanUGO || string.IsNullOrEmpty(sortCodeUGO))
            {
                return listForYgoSort;
            }
            IOrderedEnumerable<(string name, int numName, int Zs, int numPile)> sortedList = null;
            bool isFirst = true;


           


            foreach (char codeChar in sortCodeUGO)
            {

                switch (codeChar)
                {
                    case '1':
                        if (isFirst)
                        {
                            sortedList = listForYgoSort.OrderBy(g => g.numName);
                            isFirst = false;
                        }
                        else
                        {
                            sortedList = sortedList.ThenBy(g => g.numName);
                        }
                        break;

                    case '2':
                        if (isFirst)
                        {
                            sortedList = listForYgoSort.OrderByDescending(g => g.numPile);
                            isFirst = false;
                        }
                        else
                        {
                            sortedList = sortedList.ThenByDescending(g => g.numPile);
                        }
                        break;
                    case '3':
                        if (isFirst)
                        {
                            sortedList = listForYgoSort.OrderByDescending(g => g.Zs);
                            isFirst = false;
                        }
                        else
                        {
                            sortedList = sortedList.ThenByDescending(g => g.Zs );
                        }
                        break;
                    case '4':
                        if (isFirst)
                        {
                            sortedList = listForYgoSort.OrderBy(g => g.Zs);
                            isFirst = false;
                        }
                        else
                        {
                            sortedList = sortedList.ThenBy(g => g.Zs);
                        }
                        break;


                }
            }
            return sortedList?.ToList() ?? listForYgoSort.ToList();
        }
       
        private List<PilesGroup> sortedCodNumPile( bool ustanNumPile, string sortCode, List<PilesGroup> ListPilesGroup)
        {
            if (!ustanNumPile || string.IsNullOrEmpty(sortCode))
            {
                return ListPilesGroup;
            }



            bool isFirst = true;

            bool firstUGO = false;

            bool firstY = false;
            bool firstX = false;
            bool XY = false; // разрешение на сортировку двойную
            bool pastSort = false;
            bool a = true;

            if (sortCode.Contains("6"))
            { a = false; }

            bool inversSort = false;
            if (sortCode.Contains("7"))
            {
                //нумерация свай сверху вниз
                inversSort = true;
            }



            IOrderedEnumerable < PilesGroup > sortedList = ListPilesGroup.OrderBy(x => x.netrogat);
            foreach (char codeChar in sortCode)
            {
                switch (codeChar)
                {
                    case '0':
                    {
                            {
                               sortedList = sortedList.ThenBy(g => g.PilesYGO);
                            }

                            break;
                    }
                    case '1': // сортировка сначала по Y потом по X
                        {

                           
                            if (!inversSort)
                            {
                                sortedList = sortedList.ThenBy(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            }
                            else
                            {
                                sortedList = sortedList.ThenByDescending(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            }
                            
                            //sortedList = sortedList.ThenBy(g => a ? g.XleftS2 : g.CenterS2.xS2);
                            // а по x мы можем сортировать по секетору 3
                            sortedList = sortedList.ThenBy(g => a ? g.XleftS3 : g.CenterS3.xS3);

                        }
                        break;

                    case '2': // сортировка сначала по X потом по Y
                        {

                             sortedList = sortedList.ThenBy(g => a ? g.XleftS2 : g.CenterS2.xS2);
                            
                            //sortedList = sortedList.ThenByDescending(g => a ? g.YtopS2 : g.CenterS2.yS2);
                            //а по y мы можем сортировать по сектору 3
                            if (!inversSort)
                            {
                                sortedList = sortedList.ThenBy(g => a ? g.YtopS3 : g.CenterS3.yS3);
                            }
                            else
                            {
                                sortedList = sortedList.ThenByDescending(g => a ? g.YtopS3 : g.CenterS3.yS3);
                            }

                        }
                        break;

                    case '3': // Ytop (по убыванию)
                        
                        
                        sortedList = sortedList.ThenByDescending(g => g.kolVoPileName);
                        
                        break;

                    case '4': // Xleft
                        
                        sortedList = sortedList.ThenBy(g => g.numName);
                        break;
                    case '8':
                        sortedList = sortedList.ThenBy(g => g.comentDouble);
                        break;
                    case '9':
                        sortedList = sortedList.ThenByDescending(g => g.comentDouble);
                        break;
                    default:
                        // Здесь можно добавить логику для случая по умолчанию
                        break;

                }
            }



            //сортировка глуюбже для глубокой расстановке

            if (firstY)
            {
                pastSort = true;
                if (!inversSort)
                {
                    sortedList = sortedList.ThenBy(g => a ? g.YtopS3 : g.CenterS3.yS3);
                }
                else
                {
                    sortedList = sortedList.ThenByDescending(g => a ? g.YtopS3 : g.CenterS3.yS3);
                }
                //sortedList = sortedList.ThenBy(g => a ? g.XleftS3 : g.CenterS3.xS3);
            }
            else if (firstX)
            {
                pastSort = true;
                sortedList = sortedList.ThenBy(g => a ? g.XleftS3 : g.CenterS3.xS3);
                //sortedList = sortedList.ThenBy(g => a ? g.YtopS3 : g.CenterS3.yS3);
            }

            return sortedList?.ToList() ?? ListPilesGroup.ToList();


        }





        // Добавьте новый метод в класс NumPiles для корректировки координат свай:
        //private void AdjustPileCoordinates(HashSet<PileData> pileDataList)
        //{
        //    if (!adjustPilePositions || minDistanceBetweenPiles <= 0)
        //        return;

        //    var pileDataArray = pileDataList.ToList();
        //    var movedPiles = new List<PileData>();
        //    bool hasChanges;
        //    int maxIterations = 1000;
        //    int iteration = 0;

        //    do
        //    {
        //        hasChanges = false;
        //        iteration++;

        //        for (int i = 0; i < pileDataArray.Count; i++)
        //        {
        //            var pile1 = pileDataArray[i];
        //            if (pile1.reCoord) continue; // Если свая уже двигалась, пропускаем

        //            for (int j = i + 1; j < pileDataArray.Count; j++)
        //            {
        //                var pile2 = pileDataArray[j];
        //                if (pile2.reCoord) continue;

        //                double dx = pile1.X - pile2.X;
        //                double dy = pile1.Y - pile2.Y;
        //                double distance = Math.Sqrt(dx * dx + dy * dy);

        //                if (distance < minDistanceBetweenPiles && distance > 0)
        //                {
        //                    // Сваи слишком близко, нужно сдвинуть
        //                    double overlap = minDistanceBetweenPiles - distance;
        //                    double moveDistance = overlap / 2.0;

        //                    // Вычисляем вектор смещения
        //                    double angle = Math.Atan2(dy, dx);

        //                    // Сдвигаем сваи в противоположных направлениях
        //                    pile1.itogX += moveDistance * Math.Cos(angle + Math.PI);
        //                    pile1.itogY += moveDistance * Math.Sin(angle + Math.PI);
        //                    pile2.itogX += moveDistance * Math.Cos(angle);
        //                    pile2.itogY += moveDistance * Math.Sin(angle);

        //                    // Округляем координаты если нужно
        //                    if (coordinateRoundingStep > 0)
        //                    {
        //                        pile1.itogX = Math.Round(pile1.X / coordinateRoundingStep) * coordinateRoundingStep;
        //                        pile1.itogY = Math.Round(pile1.Y / coordinateRoundingStep) * coordinateRoundingStep;
        //                        pile2.itogX = Math.Round(pile2.X / coordinateRoundingStep) * coordinateRoundingStep;
        //                        pile2.itogY = Math.Round(pile2.Y / coordinateRoundingStep) * coordinateRoundingStep;
        //                    }

        //                    pile1.reCoord = true;
        //                    pile2.reCoord = true;
        //                    movedPiles.Add(pile1);
        //                    movedPiles.Add(pile2);
        //                    hasChanges = true;
        //                }
        //            }
        //        }

        //        // Сбрасываем флаги для следующей итерации
        //        foreach (var pile in movedPiles)
        //        {
        //            pile.reCoord = false;
        //        }
        //        movedPiles.Clear();

        //    } while (hasChanges && iteration < maxIterations);

        //    // Обновляем физическое расположение свай в Revit
        //    UpdatePilePositions(pileDataArray);
        //}

        //// Метод для обновления позиций свай в Revit
        //private void UpdatePilePositions(List<PileData> pileDataList)
        //{
        //    using (Transaction trans = new Transaction(RevitAPI.Document, "Корректировка позиций свай"))
        //    {
        //        try
        //        {
        //            trans.Start();

        //            ForgeTypeId units = UnitTypeId.Millimeters;
        //            int movedCount = 0;

        //            foreach (var pileData in pileDataList)
        //            {
        //                if (pileData.Pile == null) continue;

        //                // Получаем текущую позицию
        //                var locationPoint = pileData.Pile.Location as LocationPoint;
        //                if (locationPoint == null) continue;

        //                // Вычисляем новые координаты
        //                double newX = UnitUtils.ConvertToInternalUnits(pileData.X, units);
        //                double newY = UnitUtils.ConvertToInternalUnits(pileData.Y, units);
        //                double currentZ = locationPoint.Point.Z;

        //                // Создаем новую точку
        //                var newPoint = new XYZ(newX, newY, currentZ);

        //                // Перемещаем свая
        //                locationPoint.Point = newPoint;
        //                movedCount++;
        //            }

        //            trans.Commit();

        //            if (movedCount > 0)
        //            {
        //                TaskDialog.Show("Корректировка завершена",
        //                    $"Скорректировано позиций свай: {movedCount}");
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            trans.RollBack();
        //            TaskDialog.Show("Ошибка корректировки",
        //                $"Не удалось скорректировать позиции свай: {ex.Message}");
        //        }
        //    }
        //}






        //// Метод для создания отчета о нумерации
        //private void CreateNumberingReport(
        //    Dictionary<Element, (int Xs, int Ys, int Zs, double x, double y, double z, string Name, int numPile, PilesGroup pilesGroup)> properties,
        //    List<PilesGroup> groups)
        //{
        //    try
        //    {
        //        StringBuilder report = new StringBuilder();
        //        report.AppendLine("=== ОТЧЕТ О НУМЕРАЦИИ СВАЙ ===");
        //        report.AppendLine($"Параметры:");
        //        report.AppendLine($"• Шаг сектора: {sectorStep} мм");
        //        report.AppendLine($"• Шаг по высоте: {sectorStepZ} мм");
        //        report.AppendLine($"• Лимит группы: {predelGroup}");
        //        report.AppendLine();
        //        report.AppendLine($"Всего свай: {properties.Count}");
        //        report.AppendLine($"Всего групп: {groups.Count}");
        //        report.AppendLine();

        //        // Статистика по группам
        //        report.AppendLine("=== СТАТИСТИКА ПО ГРУППАМ ===");
        //        foreach (var group in groups.OrderBy(g => g.numName).ThenBy(g => g.namePile))
        //        {
        //            report.AppendLine($"Группа: {group.namePile}");
        //            report.AppendLine($"• Центр: X={group.CenterS2.xS2}, Y={group.CenterS2.xS2}");
        //            report.AppendLine($"• Секторов: {group.intPiles}");
        //            report.AppendLine($"• Свай: {group.Piles.Count}");
        //            report.AppendLine();
        //        }

        //        // Сохраняем отчет в файл
        //        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //        string reportPath = System.IO.Path.Combine(desktopPath, $"Отчет_нумерации_свай_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        //        System.IO.File.WriteAllText(reportPath, report.ToString(), System.Text.Encoding.UTF8);

        //        TaskDialog.Show("Отчет создан", $"Отчет сохранен на рабочем столе:\n{System.IO.Path.GetFileName(reportPath)}");
        //    }
        //    catch (Exception ex)
        //    {
        //        TaskDialog.Show("Ошибка отчета", $"Не удалось создать отчет: {ex.Message}");
        //    }
        //}





        
        // Метод для установки марки сваи
        private bool SetPileMark(Element pile, string markValue, List<string> parameterNames)
        {
            if (parameterNames.Count == 0) { return false; }
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
            //if (_ugoTypeCache != null) return; // Уже инициализирован

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


        //private bool SetUGOValue(Element pile, int ygoIndex)
        //{
        //    ygoIndex++;// можеьт нулевой того...
        //    try
        //    {
        //        Parameter ygoParam = pile.LookupParameter("ADSK_Типоразмер элемента узла");
        //        if (ygoParam == null || ygoParam.IsReadOnly) return false;

        //        // Для списочных параметров УСТАНАВЛИВАЕМ ЧИСЛОВОЕ ЗНАЧЕНИЕ!
        //        // ygoIndex - это индекс в списке (обычно начинается с 0 или 1)

        //        // Важно: в вашем случае YGO начинается с 1
        //        // Поэтому нужно установить значение от 1 до N

        //        if (ygoIndex >= 1) // Убедитесь, что индекс не отрицательный
        //        {
        //            try
        //            {
        //                // Пробуем установить как целое число
        //                return ygoParam.Set(ygoIndex);
        //            }
        //            catch (Exception ex1)
        //            {
        //                // Если не получается, пробуем установить как строку
        //                try
        //                {
        //                    return ygoParam.Set(ygoIndex.ToString());
        //                }
        //                catch
        //                {
        //                    return false;
        //                }
        //            }
        //        }

        //        return false;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        private void UpdatePilePositionsInRevit(List<PileData> pileDataList)
        {
            Document doc = RevitAPI.Document;
            // Проверяем, есть ли что корректировать
            int movedCount = 0;
            int skippedCount = 0;
            double minRanzToCorrect = 2; // 2 mm
            ForgeTypeId units = UnitTypeId.Millimeters;
            double mmToInternal = UnitUtils.ConvertToInternalUnits(1, units);
            var pilesToMove = new List<PileData>();

            // 1. Сначала собираем только сваи, которые действительно нужно переместить
            foreach (var pileData in pileDataList)
            {
                if (pileData.Pile == null) continue;

                double totalChange = Math.Abs(pileData.initialX - pileData.itogX) +
                                   Math.Abs(pileData.initialY - pileData.itogY);

                if (totalChange < minRanzToCorrect)
                {
                    skippedCount++;
                    continue;
                }

                pilesToMove.Add(pileData);
            }

            if (pilesToMove.Count == 0)
            {
                TaskDialog.Show("Корректировка",
                    $"Все сваи уже находятся в корректных позициях (изменение < {minRanzToCorrect} мм).");
                return;
            }

            // 2. Используем более эффективный подход - ElementTransformUtils для массового перемещения
            using (Transaction trans = new Transaction(doc, "Корректировка позиций свай"))
            {
                try
                {
                    trans.Start();
                    foreach (var pileData in pilesToMove)
                    {
                        var locationPoint = pileData.Pile.Location as LocationPoint;
                        if (locationPoint == null) continue;

                        // Вычисляем вектор перемещения
                        double dx = pileData.itogX - pileData.initialX;
                        double dy = pileData.itogY - pileData.initialY;

                        
                        // Если перемещение очень маленькое, пропускаем
                        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
                        {
                            skippedCount++;
                            continue;
                        }
                        double newX = pileData.itogX * mmToInternal;
                        double newY = pileData.itogY * mmToInternal;


                        locationPoint.Point = new XYZ(newX, newY, locationPoint.Point.Z);
                        //vectors.Add(new XYZ(dx, dy, 0));
                        movedCount++;
                        //ElementTransformUtils.MoveElement(doc, pileData.Pile.Id, new XYZ(dx* mmToInternal, dy* */mmToInternal, 0));
                    }

                    // Массовое перемещение - это должно быть быстрее
                    //if (elementIds.Count > 0)
                    //{
                    //    ElementTransformUtils.MoveElements(doc, elementIds, vectors);
                    //}


                    trans.Commit();
                    TaskDialog.Show("Корректировка завершена",
                        $"Перемещено свай: {movedCount}\n" +
                        $"Пропущено (изменение < {minRanzToCorrect} мм): {skippedCount}");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Ошибка корректировки",
                        $"Не удалось скорректировать позиции свай: {ex.Message}");
                }
            }
        }
    }


    public class PilesGroup
    {
        public int comentDouble => Piles.Count>0? Piles.First().comentDouble:-1;
        private static int _numPilesGroup = 0;

        public int numPiles = 0;
        public int netrogat = 1;//нужна для сортировки

        public HashSet<PileData> Piles = new HashSet<PileData>();

        public HashSet<(int Xs, int Ys, string name)> dataPiles = new HashSet<(int Xs, int Ys, string name)>();

        

        //public List<(int Xs, int Ys, string name)> PilesSort = new List<(int Xs, int Ys, string name)> (); // отсортированный по координатам

        public int  numName =0;




        public string namePile = "";

        public int intPiles = 1;// но это кол-во секторов а не кол-во свай!!!!!
        public (int xS2, int yS2) CenterS2 =(0,0);
        public (int xS3, int yS3) CenterS3 = (0, 0);

        //для красоты не по центру масс нумеровать а по крайним точкам
        public int XleftS2 = 0;
        public int YdownS2 = 0;
        public int XrightS2 = 0;
        public int YtopS2 = 0;



        public int XleftS3 = 0;
        public int YdownS3 = 0;
        public int XrightS3 = 0;
        public int YtopS3 = 0;


        private static int _numCreate = 0;
        public int numCreate = 0;

        public int kolVoPileName = 1;
        public int PilesYGO => Piles.FirstOrDefault()?.PilesYGO ?? 0;

        public PilesGroup()
        {
            _numPilesGroup++;
            numPiles = _numPilesGroup;
        }

        public void Initializator( int kolVoPileName,int numName)
        {

            this.kolVoPileName = kolVoPileName;
            _numCreate++;
            numCreate = _numCreate;

            if(Piles.Count==0)
            {
                return;
            }

            var anyPile = Piles.FirstOrDefault();


            // когда обьявили элемент pile ищем его родственников всех

            //если prinudOne = true то только один элемент в класс

            namePile = anyPile.Name;
            this.numName = numName;



            //а теперь наполняем
            int xs2 = 0;
            int ys2 = 0;

            int xs3 = 0;
            int ys3 = 0;

            int iter = 0;
            foreach (var pile in Piles)
            {
                
                pile.PilesGroup = this;
                
                xs2 += pile.Xs2;
                ys2 += pile.Ys2;

                xs3 += pile.Xs3;
                ys3 += pile.Ys3;
                if (iter == 0)
                {
                    XleftS2 = pile.Xs2;
                    YdownS2 = pile.Ys2;
                    XrightS2 = pile.Xs2;
                    YtopS2 = pile.Ys2;

                    XleftS3 = pile.Xs3;
                    YdownS3 = pile.Ys3;
                    XrightS3 = pile.Xs3;
                    YtopS3 = pile.Ys3;


                }
                else
                {
                    XleftS2 = Math.Min(XleftS2, pile.Xs2);
                    YdownS2 = Math.Min(YdownS2, pile.Ys2);
                    XrightS2 = Math.Max(pile.Xs2, XrightS2);
                    YtopS2 = Math.Max(pile.Ys2, YtopS2);


                    XleftS3 = Math.Min(XleftS3, pile.Xs3);
                    YdownS3 = Math.Min(YdownS3, pile.Ys3);
                    XrightS3 = Math.Max(pile.Xs3, XrightS3);
                    YtopS3 = Math.Max(pile.Ys3, YtopS3);
                }

                iter++;
            }

            intPiles = Piles.Count;
            if (intPiles > 0)
            {
                //надо по сектору иначе нумератор свай плохой!!!!!
                //но сектор берем свайный для точности
                CenterS2 = ((int)Math.Round(((double)xs2 / (double)intPiles)), (int)Math.Round(((double)ys2 / (double)intPiles) ));

                // Используйте реальные координаты, а не секторы:
                // Center = ((int)Math.Round(x / (double)intPiles), (int)Math.Round(y / (double)intPiles));

                CenterS3 = ((int)Math.Round(((double)xs3 / (double)intPiles)), (int)Math.Round(((double)ys3 / (double)intPiles)));
            }
            

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