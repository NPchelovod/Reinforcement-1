using System.Linq;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.DB.Visual;
using System.Security.Policy;

namespace Reinforcement
{
    public partial class NumPiles
    {

        public double minDistPiles => MinDistanceBetweenPiles - 2;
        bool existNewMark ;
        bool existPastMark ;
        bool existNewUGO ;
        bool existPastUGO ;
        public void SeachErrors()
        {
            var Piles = AllPiles.OrderBy(x=>x.MarkNew).ThenBy(x=>x.MarkPast).ToList();

            //ДЛЯ ОТЛАДКИ уго ВСЕХ ТИПОВ
            var ugos = Piles.Select(x => x.UGOPast).ToHashSet();

            var errors = new List<string>(); // Список для хранения сообщений об ошибках
            //ошибки в сваях дистанция соседней

            

             existNewMark = UstanNumPile && Piles.Any(x => x.MarkNew > 0);
            existPastMark = Piles.Any(x => x.MarkPast > 0);
             existNewUGO = Piles.Any(x => x.UGONewNum > 0) ;
             existPastUGO = Piles.Any(x => !string.IsNullOrEmpty(x.UGOPast));
            //запись данных какие сваи какие
            errors.AddRange(SeachNumAndTypePiles());
            for (int i = 0; i < Piles.Count; i++)
            {
                var Pile1 = Piles[i];
                for (int j = i + 1; j < Piles.Count; j++)
                {
                    var Pile2 = Piles[j];
                    double dist = Pile1.Dist(Pile2);
                    if (minDistPiles > dist)
                    {
                        //запись ошибки
                        string errorMessage = $"Дистанция {(int)dist} мм, свая марки new/past ({Pile1.MarkNew })/({Pile1.MarkPast}) - свая ({Pile2.MarkNew})/({Pile2.MarkPast})" +
                                  $", должно быть ≥ {minDistPiles}, ID:({Pile1.IdValue}),({Pile2.IdValue})";
                        errors.Add(errorMessage);
                    }
                }
            }
            //дублирование или пропуск номера нумерации
            if ( existNewMark)
            {
                Piles = AllPiles.OrderBy(x => x.MarkNew).ToList();
                for (int i = 0; i < Piles.Count-1; i++)
                {
                    var Pile1 = Piles[i];
                    var Pile2 = Piles[i+1];
                    if(Pile1.MarkNew != Pile2.MarkNew-1)
                    {
                        string errorMessage = $"Нумерация new свая марки {Pile2.MarkNew} от сваи {Pile1.MarkNew} отличаться должны на 1, ID:({Pile2.IdValue}),({Pile1.IdValue})";
                        errors.Add(errorMessage);
                    }
                }
            }
            else if(existPastMark)
            {
                Piles = AllPiles.OrderBy(x => x.MarkPast).ToList();
                for (int i = 0; i < Piles.Count - 1; i++)
                {
                    var Pile1 = Piles[i];
                    var Pile2 = Piles[i + 1];
                    if (Pile1.MarkPast != Pile2.MarkPast - 1 && (Pile1.MarkPastIsString != Pile2.MarkPastIsString))
                    {
                        string errorMessage = $"Нумерация past свая марки {Pile2.MarkPast} от сваи {Pile1.MarkPast} отличаться должны на 1, ID:({Pile2.IdValue}),({Pile1.IdValue})";
                        errors.Add(errorMessage);
                    }
                }
            }




            //поиск свай которые не перпендикулярны следующей и последующей сваи
            errors.AddRange(SeachNotPerpendicularPiles());

            // Записываем ошибки в файл на рабочем столе
            if (errors.Count > 0)
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, "Ошибки_свай.txt");

                try
                {
                    File.WriteAllLines(filePath, errors, Encoding.UTF8);
                    //MessageBox.Show($"Найденно {errors.Count} ошибок. Файл сохранён:\n{filePath}",
                    //   "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Открываем файл автоматически
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true // Позволяет открыть файл в программе по умолчанию
                    });
                }
                catch (Exception ex)
                {
                    //MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}",
                    //             "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // MessageBox.Show("Ошибок не обнаружено.", "Информация",
                //              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        public List<string> SeachNumAndTypePiles()
        {


            var PilesGroup = AllPiles
            .GroupBy(x => new { x.UGOPastNum, x.TypePile })
            .OrderBy(g => g.Min(x => x.MarkNew))
            .ThenBy(g => g.Min(x => x.MarkPast)) // Или Min/Max/Average - зависит от логики
            .ToList();

            var groupAnswer = PilesGroup.Select(g => 
            {
                var minNew = g.Min(x => x.MarkNew);
                var maxNew = g.Max(x => x.MarkNew);
                var minPast = g.Min(x => x.MarkPast);
                var maxPast = g.Max(x => x.MarkPast);

                // Формируем строку для группы
                var groupInfo = $"«{minNew}...{maxNew} new/past {minPast}...{maxPast}, тип: {g.Key.TypePile}, УГО: {g.Key.UGOPastNum}";

                string promegPiles ="";
                string zerrors = "";// ошибки в Z координатах
                
                //поиск промежуточной сваи
                var piles = UstanNumPile? g.ToList().OrderBy(x=>x.MarkNew).ToList() : g.ToList().OrderBy(x=>x.MarkPast).ToList();
                var pileLast = piles[0];
               
                foreach(var pile in piles)
                {
                    if(pile== pileLast) { continue; }
                    if(existNewMark && pile.MarkNew!= pileLast.MarkNew+1)
                    {
                        promegPiles = $"Разрыв нумерации {pileLast.MarkNew} с УГО_{g.Key.UGOPastNum} и типом {g.Key.TypePile}, сваей {pile.MarkNew} с ID:{pile.IdValue}";
                        groupInfo +=", срывы "+ pile.MarkNew + " Id:" + pile.IdValue + ", ";
                        break;
                    }
                    else if(!existNewMark && existPastMark && pile.MarkPast != pileLast.MarkPast + 1)
                    {
                        promegPiles = $"Разрыв нумерации {pileLast.MarkPast} с УГО_{g.Key.UGOPastNum} и типом {g.Key.TypePile}, сваей {pile.MarkPast} с ID:{pile.IdValue}";
                        groupInfo += ", срывы " + pile.MarkPast+" Id:"+pile.IdValue+", ";
                        break;
                    }
                    pileLast = pile;
                }

                var groupZ = g.GroupBy(x => x.Zs).OrderBy(x => x.Count()).ToList();
                if (groupZ.Count > 1)
                {
                    int Zetalon = (int)groupZ.Last().First().Z;
                    var pile = groupZ.First().First();
                    zerrors = $"Для УГО_{pile.UGOPastNum} и Типа {pile.TypePile} неверная отметка Z={pile.Z} сваи относительно других с Z={Zetalon}, свая ({pile.MarkNew})/({pile.MarkPast}), с ID:{pile.IdValue}";
                }

                return new
                {
                    PromegPiles = promegPiles,
                    Zerrors = zerrors,
                    g.Key.UGOPastNum,
                    g.Key.TypePile,
                    MinMarkNew = minNew,
                    MaxMarkNew = maxNew,
                    MinMarkPast = minPast,
                    MaxMarkPast = maxPast,
                    InfoString = groupInfo  // <-- строка с информацией
                };
            });

            var answer = groupAnswer.Select(x=>x.InfoString).ToList();
            answer.AddRange(groupAnswer.Where(x=>!string.IsNullOrEmpty(x.PromegPiles)).Select(x => x.PromegPiles).ToList());
            answer.AddRange(groupAnswer.Where(x => !string.IsNullOrEmpty(x.Zerrors)).Select(x => x.Zerrors).ToList());
            return answer;

        }

        //поиск не перпендикулярной сваи
        public List<string> SeachNotPerpendicularPiles()
        {
            var errors = new List<string>(); // Список для хранения сообщений об ошибках
            //сначала находим достоверные горизонтали 

            var errorsDist3dUp = new List<string>();// например сваи не 900 мм а 910 друг от друга и на одной оси
            double errorUp = 49;// до 49 мм
            double distanceMin = MinDistanceBetweenPiles + 1;
            double distanceMax = MinDistanceBetweenPiles + errorUp;

            //и достоверные вертикали
            HashSet<int> XAxes = new HashSet<int>();
            HashSet<int> YAxes = new HashSet<int>();
            HashSet<CoordCorrectData> XEquel = new HashSet<CoordCorrectData>();
            HashSet<CoordCorrectData> YEquel = new HashSet<CoordCorrectData>();
            double errorSize = 1;//1 мм для горизонтов и вертикала
            double errorOtSosed = 250;//до этого отклонения от сосендних вертикал и горизонтальных соседей - мы считаем за ошибку иначе наверно это так надо
            foreach (var pile in AllPiles)
            {
                bool existX = false;
                bool existY = false;
                double distanceAver = 0;
                CoordCorrectData pileDataSosed =null;
                foreach (var sosed in pile.Neighbours)
                {
                    bool pair=false;
                    if(Math.Abs(sosed.X-pile.X)< errorSize)
                    {
                        XEquel.Add(sosed);
                        XEquel.Add(pile);
                        XAxes.Add((int)Math.Round((sosed.X + pile.X) / 2));
                        existX = true;
                        pair = true;
                    }
                    else if(Math.Abs(sosed.Y-pile.Y)< errorSize)
                    {
                        YEquel.Add(sosed);
                        YEquel.Add(pile);
                        YAxes.Add((int)Math.Round((sosed.Y + pile.Y) / 2));
                        existY = true;
                        pair = true;
                    }
                    if(pair)
                    {
                        //пара пробуем дистанцию
                        double d = pile.Dist(sosed);
                        if(d> distanceMin&&d< distanceMax)
                        {
                            distanceAver = d;
                            pileDataSosed = sosed;
                        }
                    }

                    if(existY && existX) { break; }
                }
                if(distanceAver>0 && pileDataSosed is PileData pileData)
                {
                    errorsDist3dUp.Add($"Подозрительная дистанция {(int) distanceAver} между соосными сваями Num/Past {pile.MarkNew}/{pile.MarkPast}, {pileData.MarkNew}/{pileData.MarkPast}, Id{pile.IdValue}:{pileData.IdValue}");
                }
                
            }
            //нашли все соседи 
            foreach (var pile in AllPiles)
            {
                if (pile.Neighbours.Count == 0) { continue; }
                //соседи есть, сама не параллельная им по одной из линий
                bool osX = XEquel.Contains(pile);
                bool osY = YEquel.Contains(pile);

                if(osX && osY) { continue; }

                bool readErrorX = false;
                bool readErrorY = false;
                if(!osX && !XAxes.Contains((int)pile.X))
                {
                    var sosX = pile.Neighbours.Where(x => XEquel.Contains(x));
                    
                    foreach(var x in sosX)
                    {
                        if(errorOtSosed> Math.Abs(x.X - pile.X))
                        {
                            readErrorX=true;
                            break;
                        }
                    }
                    
                }

                if (!osY && !YAxes.Contains((int)pile.Y))
                {
                    var sosY = pile.Neighbours.Where(x => YEquel.Contains(x));
                    foreach (var y in sosY)
                    {
                        if (errorOtSosed > Math.Abs(y.Y - pile.Y))
                        {
                            readErrorY = true;
                            break;
                        }
                    }
                }
                if (readErrorX || readErrorY)
                {
                    string errorMessage = $"Несоосна соседям свая марки new/past ({pile.MarkNew })/({pile.MarkPast}), ID:({pile.IdValue})";
                    errors.Add(errorMessage);
                }



            }
            errors.AddRange(errorsDist3dUp);
            return errors;
        }
    }
}