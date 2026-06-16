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

namespace Reinforcement
{
    public partial class NumPiles
    {

        public double minDistPiles = 900;
        public void SeachErrors()
        {
            var Piles = AllPiles.OrderBy(x=>x.MarkNew).ThenBy(x=>x.MarkPast).ToList();
            var errors = new List<string>(); // Список для хранения сообщений об ошибках
            //ошибки в сваях дистанция соседней
            for (int i = 0; i < Piles.Count; i++)
            {
                var Pile1 = Piles[i];
                for (int j = i + 1; j < Piles.Count; j++)
                {
                    var Pile2 = Piles[j];
                    double dist = Pile1.Dist(Pile2)+2;
                    if (minDistPiles > dist)
                    {

                        //запись ошибки
                        string errorMessage = $"Дистанция свая {(Pile1.MarkNew>0? Pile1.MarkNew:Pile1.MarkPast) } ({Pile1.MarkPast}) - свая {(Pile2.MarkNew > 0 ? Pile2.MarkNew : Pile2.MarkPast)} ({Pile2.MarkPast}) = {(int) dist} м. " +
                                  $"Должно быть ≥ {minDistPiles:F2} м.";
                        errors.Add(errorMessage);
                    }
                }
            }



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
    }
}