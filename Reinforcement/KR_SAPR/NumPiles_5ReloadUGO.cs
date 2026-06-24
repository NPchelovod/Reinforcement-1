using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text.RegularExpressions;

namespace Reinforcement
{
    public partial class NumPiles
    {
        public HashSet<string> AllUgoName { get; set; } = new HashSet<string>();
        public void ReloadUGOM()
        {
            var listPilesChangeUGO = AllPiles.Where(x => !string.IsNullOrEmpty(x.UGOPast)).OrderBy(x => x.UGOPast).ToList();
            if (listPilesChangeUGO.Count == 0) { return; }
            //все варианты угошек
            AllUgoName = listPilesChangeUGO.Select(x => x.UGOPast).ToHashSet();
            if (AllUgoName.Count <= 1) {  return; } // недостаточно

            foreach (var pile in listPilesChangeUGO)
            {
                foreach (var ugo in AllUgoName)
                {
                    if (ugo != pile.UGOPast)
                    {
                        pile.UGONew = ugo;
                        break;
                    }
                }
            }
            SetUGOCircle(listPilesChangeUGO);

            //возврат
            foreach (var pile in listPilesChangeUGO)
            {
                pile.UGONew = pile.UGOPast;
            }
            SetUGOCircle(listPilesChangeUGO);


        }


        private void SetUGOCircle(List<PileData> listPilesChangeUGO)
        {
            using (Transaction trans1 = new Transaction(Document, "Установка УГО"))
            {
                try
                {
                    trans1.Start();
                    foreach(var pile in listPilesChangeUGO)
                    {
                        bool ustan = SetUGOValue(Document, pile.Element, 0, pile.UGONew);
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


        // 3. ОПТИМИЗИРОВАННЫЙ МЕТОД SetUGOValue
        private bool SetUGOValue(Document doc, Element pileElement, int ygoIndex, string targetUgoName="")
        {
            // Убедимся, что кэш инициализирован (делаем это один раз за запуск)
            string prefix = "УГО_";
            if (_ugoTypeCache == null)
            {
                
                if (targetUgoName.Length > 0 && !targetUgoName.Contains(prefix))
                {
                    prefix = targetUgoName;
                    Match match = Regex.Match(targetUgoName, @"\d+");
                    if (match.Success)
                    {
                        prefix = prefix.Replace(int.Parse(match.Value).ToString(), "");

                    }
                    if (string.IsNullOrEmpty(prefix))
                    {
                        prefix = "УГО_";
                    }
                }
                if (string.IsNullOrEmpty(prefix))
                {
                    InitializeUgoCache(doc);
                }
                else
                {
                    InitializeUgoCache(doc, prefix);
                }
            }

            // 1. Формируем имя типа
            if (string.IsNullOrEmpty(targetUgoName))
            {
                targetUgoName = prefix + ygoIndex;
            }

            // 2. Пытаемся получить ID типа ИЗ КЭША (мгновенно!)
            if (!_ugoTypeCache.TryGetValue(targetUgoName, out ElementId targetTypeId))
            {
                // Если не нашли в кэше, значит, такого типа действительно нет в проекте
                //TaskDialog.Show("Ошибка",
                //    $"Тип '{targetUgoName}' не найден в проекте.\n" +
                //    $"Возможно, в проекте нет типов УГО, или их имена отличаются.\n" +
                //    $"Доступные имена в кэше: {string.Join(", ", _ugoTypeCache.Keys.OrderBy(k => k))}");
                return false;
            }

            // 3. Нашли ID! Теперь находим и устанавливаем параметр на свае.
            Parameter ugoParam = pileElement.LookupParameter("ADSK_Типоразмер элемента узла");

            if (ugoParam == null || ugoParam.IsReadOnly)
            {
                // Можно не показывать диалог для каждой ошибки, а просто вернуть false
                // и вести статистику в основном методе
                return false;
            }

            // 4. Устанавливаем значение
            try
            {
                return ugoParam.Set(targetTypeId);
            }
            catch
            {
                return false;
            }
        }
        // 1. ОБЪЯВЛЯЕМ СТАТИЧЕСКИЙ СЛОВАРЬ ДЛЯ КЭШИРОВАНИЯ
        // Ключ: Имя типа (например, "УГО_1"), Значение: ElementId этого типа
        private static Dictionary<string, ElementId> _ugoTypeCache = null;

        // 2. МЕТОД ДЛЯ ИНИЦИАЛИЗАЦИИ (ЗАПОЛНЕНИЯ) СЛОВАРЯ
        private static void InitializeUgoCache(Document doc, string prefix = "УГО_")
        {
            //if (_ugoTypeCache != null) return; // Уже инициализирован

            _ugoTypeCache = new Dictionary<string, ElementId>();

            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol));

            //foreach (FamilySymbol symbol in collector)
            //{
            //    string symbolName = symbol.Name;
            //    // Сохраняем ВСЕ типы, которые могут быть УГО (или начинаются на "УГО_")
            //    // Это позволит быстро находить их позже.
            //    _ugoTypeCache[symbolName] = symbol.Id;

            //    // Опционально: можно добавить логирование для отладки
            //    // TaskDialog.Show("Кэш", $"Добавлено в кэш: {symbolName} -> {symbol.Id.IntegerValue}");
            //}

            // Если вы точно знаете, что нужны только типы, начинающиеся с "УГО_",
            // можно фильтровать сразу здесь, уменьшив размер словаря:
            // var ugoSymbols = collector.Cast<FamilySymbol>().Where(s => s.Name.StartsWith("УГО_"));
            // foreach (var symbol in ugoSymbols) { _ugoTypeCache[symbol.Name] = symbol.Id; }
            var ugoSymbols = collector.Cast<FamilySymbol>().Where(s => s.Name.StartsWith(prefix));
            foreach (var symbol in ugoSymbols) { _ugoTypeCache[symbol.Name] = symbol.Id; }


        }
    }
}