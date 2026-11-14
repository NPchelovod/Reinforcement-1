using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{

    public enum ElementTypeOrSymbol
    {
        Symbol,
        ElementType
    }
    internal class Utilit_1_1_Depth_Seach
    {


        public static bool GetResult(
            HashSet<string> famNames,
            ElementTypeOrSymbol mode,
            HashSet<string> typeNames = null, bool userShow=true)
        {
            if (famNames == null || famNames.Count == 0)
                return false;

            
            
            //расстановка по модели элемента
            Element element = HelperSeach.SeachElement(famNames, typeNames, mode, userShow);//надо протестить будет
            //element = typeNames == null ? HelperSeach.GetExistFamily(famNames, mode) : HelperSeach.GetExistFamily(famNames, typeNames, mode);
            if (element == null) { return false; }
            

            ElementType elementType = element as ElementType;

            // HelperSeach.Cached() уже отсеивает мёртвые объекты,
            // но между возвратом и вызовом API элемент теоретически
            // мог быть удалён (например, reload семейства в другой транзакции)
            if (!elementType.IsValidObject)
                return false;

            var uidoc = RevitAPI.UiDocument;
            if (uidoc == null)
                return false;

            //надо проверить поставился ли элемент
            uidoc.PostRequestForElementTypePlacement(elementType);
            
            return true;

        }
      

       
    }
}


