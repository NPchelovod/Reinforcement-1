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
        private void SetComment()
        {
            //установка номера комментария для сортировки сваи

            //группировка по типу
            //Нумерация comment должна идти по возрастанию(1, 2, 3…), начиная с наиболее многочисленной группы(сначала большие группы, потом меньшие).
            
            bool sortOnCount=true;
            if(sortCodeEnums.Contains(SortCodeEnum.SortTypePile))
            {
                //значит возможно нам не по кол-ву надо считать а по типу
                if(!sortCodeEnums.Contains(SortCodeEnum.SortCountPiles) || sortCodeEnums.IndexOf(SortCodeEnum.SortTypePile)< sortCodeEnums.IndexOf(SortCodeEnum.SortCountPiles))
                {
                    sortOnCount= false;
                }
            }
            
            int comment = -1;
            var pilesGroup = AllPiles
            .GroupBy(x => x.TypePile)
            .OrderByDescending(x => sortOnCount ? x.Count() : 0)
            .ThenBy(x => x.Key)
            .ToList();



            foreach (var pileGroup1 in pilesGroup)
            {
                //тут делаем сортировку уже по Z
                //var pilesGroup2 = pileGroup1.GroupBy(x => new { x.Zs }).OrderByDescending(x => sortOnCount ? x.Count() : 0).ThenBy(x => x.Key).ToList();
                //и идем по отдельной группы тут у нас 
                var pilesGroup2 = pileGroup1
                .GroupBy(x => new { x.Zs })
                .OrderByDescending(x => sortOnCount ? x.Count() : 0)
                .ThenBy(x => x.Key.Zs)  // ← вот здесь исправление!
                .ToList();
                foreach (var pileGroup2 in pilesGroup2)
                {
                    comment++;
                    foreach (var pile in pileGroup2)
                    {
                        pile.CommentaryNum = comment;
                        pile.Commentary = comment.ToString();
                    }
                }
            }

            var paramCom = new List<string> { "Комментарии" };
            using (Transaction trans2 = new Transaction(Document, "Установка комментария"))
            {
                try
                {
                    trans2.Start();
                    foreach (var pileClass in AllPiles)
                    {
                        Element pile = pileClass.Element;
                        if (pile == null) { continue; }

                        SetPileMark(pile, pileClass.Commentary, paramCom);
                       
                    }
                    trans2.Commit();
                    
                }
                catch (Exception ex)
                {
                    trans2.RollBack();
                    TaskDialog.Show("Ошибка транзакции", $"Ошибка при установке марок: {ex.Message}");

                }
            }
        }
    }
}
