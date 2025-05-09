using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    internal class Utilit_1_1_Depth_Seach_Name
    {
         public static ElementType GetResult(string FamName, IList<Element> symbols)
        {
            
            ElementType symbol = null;

            bool contol_proxod = false;
            foreach (var element in symbols)
            {
                ElementType elemType = element as ElementType;
                if (elemType.FamilyName == FamName)
                {

                    symbol = element as FamilySymbol;
                    contol_proxod = true;
                    break;
                }
            }


            if (contol_proxod == false)
            {
                // ищем максимальную подстроку в строке для этого удаляем все символы чипушные

                var simvol_del = new List<string>() { ".", ",", "_", "-", "/",";",":", " " };
                string FamName2 = FamName.ToLower();
                foreach (var simvol in simvol_del)
                {
                    // приводим строку к виду
                    FamName2=FamName2.Replace(simvol, "");
                }

                int simvol_sovpad = 0;
                string name_sovpad = "";
                string FamName_sravn = "";
                foreach (var element in symbols)
                {
                    ElementType elemType = element as ElementType;
                    string potenc_name_sovpad = elemType.FamilyName;
                    FamName_sravn = elemType.FamilyName.ToLower();

                    foreach (var simvol in simvol_del)
                    {
                        // приводим строку к виду
                        FamName_sravn = FamName_sravn.Replace(simvol, "");
                    }
                    // количество пересечений
                    int count = FamName2.Zip(FamName_sravn, (c1, c2) => c1 == c2).Count(match => match);
                    if (count > simvol_sovpad)
                    {
                        simvol_sovpad = count;
                        name_sovpad = potenc_name_sovpad;
                    }
                }

                if (simvol_sovpad > 3 && simvol_sovpad > Convert.ToInt32(0.7 * FamName2.Count()))
                {
                    foreach (var element in symbols)
                    {
                        ElementType elemType = element as ElementType;
                        if (elemType.FamilyName == name_sovpad)
                        {

                            symbol = element as FamilySymbol;
                            contol_proxod = true;
                            TaskDialog.Show("Не найдено точное совпадение имени семейства", $"Нашёл аналог {FamName}: {name_sovpad}");
                            
                            break;
                        }
                    }
                }

            }

            return symbol;


        }

    }
}
