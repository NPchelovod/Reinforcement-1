using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    [Transaction(TransactionMode.Manual)]
    public class DeleteDublicateFamily : IExternalCommand
    {
        // 1 мм в футах (внутренние единицы Revit)
        private const double MmToFeet = 10.0 / 304.8;

        // Ограничение на вывод в TaskDialog, чтобы не упереться в лимиты UI
        private const int MaxGroupsToPrint = 100;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            UIDocument uiDoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;

            //тут все элементы с вида
            List <Element> elems = ArmLengthEquels.SelectOrAllElements(true);//чтобы группы не меняла
                                                                  // Получаем активный вид и документ
           
            View activeView = doc.ActiveView;
            // Словарь для группировки: "ИмяСемейства" -> список экземпляров
            var familyGroups = new Dictionary<string, List<FamilyInstance>>();

            foreach (Element elem in elems)
            {
                if(elem==null) continue;
                if (elem is FamilyInstance fi && fi.Symbol != null)
                {
                    string familyName = fi.Symbol.FamilyName;
                    if (!familyGroups.ContainsKey(familyName))
                        familyGroups[familyName] = new List<FamilyInstance>();
                    familyGroups[familyName].Add(fi);
                }

            }
            //Шаг 2: Поиск «оригинального» семейства (без числового суффикса)
            // Получаем все семейства в документе (для быстрой проверки существования)
            var allFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            // Регулярное выражение для поиска цифр в конце имени
            var suffixRegex = new System.Text.RegularExpressions.Regex(@"(\d+)$");
            var replacementMap = new Dictionary<string, Family>();

            foreach (var kvp in familyGroups)
            {
                string currentFamilyName = kvp.Key; // Например, "СемействоА1"
                var match = suffixRegex.Match(currentFamilyName);

                if (match.Success)
                {
                    // Отрезаем цифры в конце: "СемействоА1" -> "СемействоА"
                    string baseName = currentFamilyName.Substring(0, currentFamilyName.Length - match.Length);

                    // Проверяем, существует ли такое "чистое" семейство в проекте
                    if (allFamilies.TryGetValue(baseName, out Family originalFamily))
                    {
                        replacementMap[currentFamilyName] = originalFamily;
                    }
                }
            }
            //Шаг 3: Замена семейства и перенос параметров
            using (Transaction trans = new Transaction(doc, "Замена семейств и перенос параметров"))
            {
                trans.Start();

                foreach (var pair in replacementMap)
                {
                    string duplicateName = pair.Key;     // "СемействоА1"
                    Family originalFamily = pair.Value;  // Семейство "СемействоА"

                    // Получаем все типоразмеры "оригинального" семейства
                    var originalSymbols = originalFamily.GetFamilySymbolIds()
                        .Select(id => doc.GetElement(id) as FamilySymbol)
                        .Where(s => s != null)
                        .ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

                    foreach (FamilyInstance instance in familyGroups[duplicateName])
                    {
                        try
                        {
                            string currentTypeName = instance.Symbol.Name; // Например, "А500С 2" или "Р1"
                            FamilySymbol targetSymbol = null;

                            // --- 1. Пытаемся найти ТОЧНОЕ совпадение имени типоразмера ---
                            if (!originalSymbols.TryGetValue(currentTypeName, out targetSymbol))
                            {
                                // --- 2. Точного совпадения нет. Пробуем отрезать числовой суффикс ---
                                var typeMatch = NumericSuffixRegex.Match(currentTypeName);
                                if (typeMatch.Success)
                                {
                                    string baseTypeName = currentTypeName
                                        .Substring(0, currentTypeName.Length - typeMatch.Length)
                                        .TrimEnd(); // убираем пробел перед числом, если был

                                    // Ищем базовое имя ("А500С" вместо "А500С 2")
                                    if (baseTypeName.Length > 0)
                                    {
                                        originalSymbols.TryGetValue(baseTypeName, out targetSymbol);
                                    }
                                }
                            }

                            // Если типоразмер не найден ни точно, ни по базе — пропускаем элемент
                            if (targetSymbol == null)
                                continue;

                            // Если целевой символ совпадает с текущим — менять нечего
                            if (targetSymbol.Id == instance.Symbol.Id)
                                continue;

                            // --- Копируем параметры ДО замены ---
                            var paramValues = new Dictionary<string, object>();
                            foreach (Parameter p in instance.Parameters)
                            {
                                if (p.HasValue && p.StorageType != StorageType.None)
                                    paramValues[p.Definition.Name] = GetParameterValue(p);
                            }

                            // --- Выполняем замену ---
                            if (!targetSymbol.IsActive)
                                targetSymbol.Activate();

                            //instance.Symbol = targetSymbol; // Сама замена
                            ElementId failedId = instance.ChangeTypeId(targetSymbol.Id);
                            if (failedId != ElementId.InvalidElementId)
                            {
                                // замена НЕ удалась
                                continue;
                            }
                            //Причина №4: параметры после замены «откатывают» тип
                            // --- Применяем сохранённые значения к новому типу ---
                            foreach (var kvp in paramValues)
                            {
                                Parameter newParam = instance.LookupParameter(kvp.Key);
                                if (newParam == null || newParam.IsReadOnly) continue;
                                if (newParam.StorageType == StorageType.ElementId) continue; // не трогаем ссылки
                                try { SetParameterValue(newParam, kvp.Value); }
                                catch (Exception ex)
                                {
                                    continue;

                                }
                            }
                        }

                        catch (Exception ex)
                        {
                            App_Apdater_1.AppErrors.LogError(ex);
                            // Логируем или пропускаем — чтобы один проблемный элемент не убил всю команду
                            // System.Diagnostics.Debug.WriteLine($"Не удалось заменить {instance.Id}: {ex.Message}");
                            continue;
                        }

                    }
                }

                trans.Commit();
            }
            return Result.Succeeded;
        }
        // Регулярка для числового суффикса в КОНЦЕ имени (с опциональным пробелом перед ним).
        // Примеры: "А500С 2" -> суффикс " 2", база "А500С"
        //          "Р1"      -> суффикс "1",   база "Р"
        //          "Бетон10" -> суффикс "10",  база "Бетон"
        private static readonly System.Text.RegularExpressions.Regex NumericSuffixRegex =
            new System.Text.RegularExpressions.Regex(@"\s*(\d+)$");
        // Чтение значения параметра любого типа
        private object GetParameterValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.Integer: return p.AsInteger();
                case StorageType.Double: return p.AsDouble();
                case StorageType.String: return p.AsString();
                case StorageType.ElementId: return p.AsElementId();
                default: return null;
            }
        }
        // Запись значения в параметр
        private void SetParameterValue(Parameter p, object value)
        {
            switch (p.StorageType)
            {
                case StorageType.Integer: p.Set((int)value); break;
                case StorageType.Double: p.Set((double)value); break;
                case StorageType.String: p.Set((string)value); break;
                case StorageType.ElementId: p.Set((ElementId)value); break;
            }
        }
    }
}