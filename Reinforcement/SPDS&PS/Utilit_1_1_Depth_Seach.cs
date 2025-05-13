using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    internal class Utilit_1_1_Depth_Seach
    {
         public static ElementType GetResult(Document doc, UIDocument uidoc, List <String> FamNames, string Type_seach )
        {

            FilteredElementCollector col = new FilteredElementCollector(doc);


            IList<Element> sravn_iter;

            if (Type_seach == "elementTypes")
            {
                IList<Element> elementTypes = col.OfClass(typeof(ElementType)).WhereElementIsElementType().ToElements();
                sravn_iter = elementTypes;
            }
            else
            {
                IList<Element> symbols = col.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();
                sravn_iter = symbols;
            }


                ElementType elementType = null;
            ElementType symbol = null;


            bool contol_proxod = false;

            foreach (var FamName in FamNames)
            {
                
                foreach (var element in sravn_iter)
                {
                    elementType = element as ElementType;
                    if (elementType.Name == FamName)
                    {
                        contol_proxod = true;
                        uidoc.PostRequestForElementTypePlacement(elementType);

                        break;
                    }
                }
                

            }

            if (contol_proxod == false)
            {
                // ищем максимальную подстроку в строке для этого удаляем все символы чипушные
                string FamName = FamNames[0];


                string FamName2 = Utilit_Helper.unific_sravn_string(FamName);

                int simvol_sovpad = 0;
                string name_sovpad = "";
                string FamName_sravn = "";
                int count = 0;

                Element element_gotov = null;
                
                foreach (var element in sravn_iter)
                {
                    ElementType elemType = element as ElementType;
                    string potenc_name_sovpad = elemType.FamilyName;

                    FamName_sravn = Utilit_Helper.unific_sravn_string(FamName_sravn);

                    // количество пересечений
                    count = Utilit_Helper.LongestCommonSubstring(FamName2, FamName_sravn);
                    if (count > simvol_sovpad)
                    {
                        simvol_sovpad = count;
                        name_sovpad = potenc_name_sovpad;
                        element_gotov = element;
                    }
                }
                
                
                if (simvol_sovpad > 3 && simvol_sovpad > Convert.ToInt32(0.7 * FamName2.Count()))
                {
                    
                    elementType = element_gotov as ElementType;
                    contol_proxod = true;
                    TaskDialog.Show("Не найдено точное совпадение имени семейства", $"Нашёл аналог {FamName} : {name_sovpad}");
                    uidoc.PostRequestForElementTypePlacement(elementType);
                }
                else
                {
                    TaskDialog.Show("Не найдено точное совпадение имени семейства", $"Аналогов нет {FamName}");
                }
            }

            return elementType;


        }

    }
}
