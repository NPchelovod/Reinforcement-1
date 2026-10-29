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

        internal static class Utilit_1_1_Depth_Seach
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

                Element element;
                try
                {
                    // Пустой typeNames трактуем как «не задан» — ищем только по семейству
                    element = (typeNames != null && typeNames.Count > 0)
                        ? HelperSeach.GetExistFamily(famNames, typeNames, mode)
                        : HelperSeach.GetExistFamily(famNames, mode);
                }
                catch (Exception ex)
                {
                    RevitAPI.AppErrors.LogError(ex);
                    return false;
                }

                // ── было: if (element is not ElementType elementType) return false;
                var elementType = element as ElementType;
                if (elementType == null)
                    return false;

            // HelperSeach.Cached() уже отсеивает мёртвые объекты,
            // но между возвратом и вызовом API элемент теоретически
            // мог быть удалён (например, reload семейства в другой транзакции)
            if (!elementType.IsValidObject)
                    return false;

                var uidoc = RevitAPI.UiDocument;
                if (uidoc == null)
                    return false;

                try
                {
                    uidoc.PostRequestForElementTypePlacement(elementType);
                    return true;
                }
                catch (Exception ex)
                {
                    RevitAPI.AppErrors.LogError(ex);
                    return false;
                }
            }

            /// <summary>
            /// Сброс кэша. Проксируется в HelperSeach, чтобы у всех вызывающих
            /// (включая старые команды) было единое поведение.
            /// </summary>
            public static void ClearCache(Document document = null)
                => HelperSeach.ClearCache(document);
        }
    }


