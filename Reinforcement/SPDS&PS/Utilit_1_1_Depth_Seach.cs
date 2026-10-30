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
        
        public static Dictionary<Document, Dictionary<string, Element>> MemoryTypes = new Dictionary<Document, Dictionary<string, Element>>();

        public static bool GetResult( HashSet<String> FamNames, ElementTypeOrSymbol Type_seach, HashSet<string> TypeNames = null  )
        {
            /// <summary>
            /// Найти типоразмер (или тип) по именам и поставить его в режим размещения.
            /// Мемоизация результата — целиком в HelperSeach (PastElements + _overriddenNames).
            /// </summary>
            public static bool GetResult(
                HashSet<string> famNames,
                ElementTypeOrSymbol mode,
                HashSet<string> typeNames = null)
            {
                if (famNames == null || famNames.Count == 0)
                    return false;

            string key = TypeNames!=null&& TypeNames.Any()? ToKey(TypeNames): ToKey(FamNames);
            Element element = null;
            
            Document doc = RevitAPI.Document;
            bool existHistory=false;
            if(MemoryTypes.TryGetValue(doc, out var els))
            {
                if(els.TryGetValue(key, out element)&&element!=null)
                {
                    existHistory=true;
                }
            }
            else
            {
                els = new Dictionary<string, Element>();
                MemoryTypes[doc] = els;
            }

            //расстановка по модели элемента
            if (!existHistory)
            {
                element = TypeNames == null ? HelperSeach.GetExistFamily(FamNames, Type_seach) : HelperSeach.GetExistFamily(FamNames, TypeNames, Type_seach);
                if (element == null) { return false; }
            }

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
            if (!existHistory)
            {
                //
                els[key] = element;
            }
            return true;

        }
        static string ToKey(HashSet<string> set)
         => string.Join("|", set.OrderBy(x => x ?? ""));

        public static void ClearCache(Document document = null)
        {
            if (document == null)
            {
                MemoryTypes.Clear();

            }
            else
            {
                MemoryTypes.Remove(document);
            }
        }


