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

        

        public static bool GetResult( HashSet<String> FamNames, ElementTypeOrSymbol Type_seach, HashSet<string> TypeNames = null  )
        {
            //расстановка по модели элемента

            Element element = TypeNames == null ? HelperSeach.GetExistFamily(FamNames, Type_seach) : HelperSeach.GetExistFamily(FamNames, TypeNames, Type_seach);

            if ( element == null ) {return false;}

            ElementType elementType = element as ElementType;
            if ( elementType == null ) {return false;}

            UIDocument uidoc = RevitAPI.UiDocument;

            uidoc.PostRequestForElementTypePlacement(elementType);

            return true;

        }


    }
}
