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
        private void UstanUGOProcess()
        {
            //надо установить угошку при этом надо отсортировать как-то...

            var sortedGroups = AllPiles
            .GroupBy(x => x.netrogat).ToList();

            if (sortCodeEnums.Contains(SortCodeEnum.SortNumComment))
            {
                 sortedGroups = AllPiles
                .GroupBy(x => x.CommentaryNum)
                .OrderBy(g => g.Key) // Сортируем группы по возрастанию CommentaryNum (ключ группы)
                .ToList();
            }
            else if(sortCodeEnums.Contains(SortCodeEnum.SortADSKGroup))
            {
                sortedGroups = AllPiles
               .GroupBy(x => x.ADSK_GroupNum)
               .OrderBy(g => g.Key) // Сортируем группы по возрастанию CommentaryNum (ключ группы)
               .ToList();
            }



            int ugo = 0;
            foreach (var group1 in sortedGroups) 
            {
                //а группы на всякий случай группируем по типу и количеству
                var pilesGroup2 = group1.GroupBy(x => new { x.Zs, x.TypePile }).OrderBy(x => x.Key.TypePile).ToList();
                foreach (var pileg in pilesGroup2)
                {
                    ugo++;
                    foreach (var pile in pileg)
                    {
                        pile.UGONewNum = ugo;
                    }
                }
            }
            using (Transaction trans1 = new Transaction(Document, "Установка УГО"))
            {
                try
                {
                    trans1.Start();
                    foreach (var pile in AllPiles)
                    {
                        bool ustan = SetUGOValue(Document, pile.Element, pile.UGONewNum);
                    }

                    trans1.Commit();
                }
                catch (Exception ex)
                {

                    trans1.RollBack();
                    TaskDialog.Show("Ошибка транзакции", $"Ошибка при установке УГО: {ex.Message}");
                    return;
                }
            }
        }
    }
}