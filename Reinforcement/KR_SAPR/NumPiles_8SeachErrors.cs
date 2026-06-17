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

namespace Reinforcement
{
    public partial class NumPiles
    {

        public double minDistPiles => minDistanceBetweenPiles - 2;
        public void SeachErrors()
        {
            var Piles = AllPiles.OrderBy(x=>x.MarkNew).ThenBy(x=>x.MarkPast).ToList();
            var errors = new List<string>(); // Список для хранения сообщений об ошибках
            //ошибки в сваях дистанция соседней


            bool existNewMark = Piles.Where(x => x.MarkNew > 0).Count() > 0;
            bool existPastMark = Piles.Where(x => x.MarkPast > 0).Count() > 0;
            bool existNewUGO = Piles.Where(x => x.UGONewNum > 0).Count() > 0;
            bool existPastUGO = Piles.Where(x => !string.IsNullOrEmpty(x.UGOPast)).Count() > 0;
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
            if (ustanNumPile && existNewMark)
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
            //проверка УГО соотсветсвию всем требованиям
            if (!ustanUGO && existPastUGO)
            {
                ///*var PilesUGO =  Piles.Where(x => !string.IsNullOrEmpty(x.UGOPast)).OrderBy(x=>x.UGOPastNum).ThenBy(x=>x.UGOPast).ThenBy(x => ustanNumPile?x.MarkNew: */x.MarkPast).ToList();
                var groupedPiles = Piles
                .Where(x => !string.IsNullOrEmpty(x.UGOPast))
                .OrderBy(x => x.UGOPastNum)
                .ThenBy(x => x.UGOPast)
                .ThenBy(x => ustanNumPile ? x.MarkNew : x.MarkPast)
                .GroupBy(x => x.UGOPast)
                .ToList();

                
                foreach (var group in groupedPiles)
                {
                    
                    var groupAsList = group.OrderBy(x=>x.MarkNew).ToList();

                    //в отдельной группе проверяем нумерацию свай
                    if(ustanNumPile && existNewMark)
                    {
                        for (int i = 0; i < groupAsList.Count - 1; i++)
                        {
                            var Pile1 = groupAsList[i];
                            var Pile2 = groupAsList[i + 1];
                            if (Pile1.MarkNew != Pile2.MarkNew - 1)
                            {
                                string errorMessage = "";
                                var PileAver = AllPiles.Where(x => x.MarkNew == Pile1.MarkNew + 1).FirstOrDefault();
                                if (PileAver != null)
                                {
                                    if(PileAver.TypePile!= Pile1.TypePile)
                                    {
                                        errorMessage += "!(тип свай) ";
                                    }
                                }
                                errorMessage += $"Нерационально УГО_{Pile1.UGOPastNum} разрыв номера {Pile2.MarkNew} от сваи {Pile1.MarkNew} отличаться должны на 1, ID:({Pile2.IdValue}),({Pile1.IdValue})";

                                if (PileAver != null)
                                {
                                    errorMessage += $" Промежуточная свая {PileAver.MarkNew} с УГО_{PileAver.UGOPastNum} и ID:{PileAver.IdValue}";
                                }
                                errors.Add(errorMessage);
                            }
                        }
                    }
                    else if(existPastMark)
                    {
                        groupAsList = group.OrderBy(x => x.MarkPast).ToList();
                        for (int i = 0; i < groupAsList.Count - 1; i++)
                        {
                            var Pile1 = groupAsList[i];
                            var Pile2 = groupAsList[i + 1];
                            if (Pile1.MarkPast != Pile2.MarkPast - 1 && (Pile1.MarkPastIsString !=Pile2.MarkPastIsString))
                            {

                                var PileAver = AllPiles.Where(x => x.MarkPast == Pile1.MarkPast + 1).OrderBy(x => x.MarkPastIsString).FirstOrDefault();
                                string errorMessage = "";
                                if (PileAver != null)
                                {
                                    if (PileAver.TypePile != Pile1.TypePile)
                                    {
                                        errorMessage += "!(тип свай) ";
                                    }
                                }
                                 errorMessage += $"Нерационально УГО_{Pile1.UGOPastNum} разрыв номера {Pile2.MarkPast} от сваи {Pile1.MarkPast} отличаться должны на 1, ID:({Pile2.IdValue}),({Pile1.IdValue})";

                                //попытка найти промежуточную сваю
                                
                                if (PileAver != null)
                                {
                                    errorMessage += $" Промежуточная свая {PileAver.MarkPast} с УГО_{PileAver.UGOPastNum} и ID:{PileAver.IdValue}";
                                }
                                errors.Add(errorMessage);
                            }
                        }
                    }

                    //поиск несовпадающих
                    var grouped2 = groupAsList.GroupBy(x => new { x.Zs, x.TypePile });
                    
                    if (grouped2.Count()>1)
                    {
                        var maxGroup = grouped2.OrderByDescending(g => g.Count()).First();
                        var pileEtalon = maxGroup.First();
                        foreach (var group2 in grouped2)
                        {
                            // Сравниваем ключи групп, а не объекты
                            if (!group2.Key.Equals(maxGroup.Key))
                            {
                                foreach(var item in group2)
                                {
                                    string errorMessage = $"Несовпадение УГО_{pileEtalon.UGOPastNum} сваи марки new/past ({item.MarkNew})/ ({item.MarkPast}),Z = ({(int)item.Z}), type = ({item.TypePile}) с эталоном ({pileEtalon.MarkNew})/ ({pileEtalon.MarkPast}),Z = ({(int)pileEtalon.Z}), type = ({pileEtalon.TypePile}), ID:({pileEtalon.IdValue}),({item.IdValue})";
                                    errors.Add(errorMessage);
                                }
                            }
                        }
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

       
        //поиск не перпендикулярной сваи
        public List<string> SeachNotPerpendicularPiles()
        {
            var errors = new List<string>(); // Список для хранения сообщений об ошибках
            //сначала находим достоверные горизонтали 
            //и достоверные вертикали
            HashSet<PileData> XEquel = new HashSet<PileData>();
            HashSet<PileData> YEquel = new HashSet<PileData>();
            double errorSize = 1;//1 мм для горизонтов и вертикала
            double errorOtSosed = 250;//до этого отклонения от сосендних вертикал и горизонтальных соседей - мы считаем за ошибку иначе наверно это так надо
            foreach (var pile in AllPiles)
            {
                foreach (var sosed in pile.SosedPileData)
                {
                    if(Math.Abs(sosed.X-pile.X)< errorSize)
                    {
                        XEquel.Add(sosed);
                        XEquel.Add(pile);
                    }
                    else if(Math.Abs(sosed.Y-pile.Y)< errorSize)
                    {
                        YEquel.Add(sosed);
                        YEquel.Add(pile);
                    }
                }
            }
            //нашли все соседи 
            foreach (var pile in AllPiles)
            {
                if (pile.SosedPileData.Count == 0) { continue; }
                //соседи есть, сама не параллельная им по одной из линий
                bool osX = XEquel.Contains(pile);
                bool osY = YEquel.Contains(pile);

                if(osX && osY) { continue; }

                bool readErrorX = false;
                bool readErrorY = false;
                if(!osX)
                {
                    var sosX = pile.SosedPileData.Where(x => XEquel.Contains(x));
                    
                    foreach(var x in sosX)
                    {
                        if(errorOtSosed> Math.Abs(x.X - pile.X))
                        {
                            readErrorX=true;
                        }
                    }
                    
                }

                if (!osY)
                {
                    var sosY = pile.SosedPileData.Where(x => YEquel.Contains(x));
                    foreach (var y in sosY)
                    {
                        if (errorOtSosed > Math.Abs(y.Y - pile.Y))
                        {
                            readErrorY = true;
                        }
                    }
                }
                if (readErrorX || readErrorY)
                {
                    string errorMessage = $"Несоосна соседям свая марки new/past ({pile.MarkNew })/({pile.MarkPast}), ID:({pile.IdValue})";
                    errors.Add(errorMessage);
                }



            }
            return errors;
        }
    }
}