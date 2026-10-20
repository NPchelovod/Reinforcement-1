using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Reinforcement
{
    /// <summary>
    /// Универсальный отчёт по экземплярам:
    ///  * Активна спецификация → спрашиваем № строки → находим её элементы →
    ///    строим отчёт «Лист → Вид → Семейство [Тип] + Группы».
    ///  * Иначе → ищем по имени семейства/типоразмера (диалог + авто из выделения).
    /// Найденные экземпляры выделяются в модели.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SelectParentElement : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitAPI.Initialize(commandData);
            Document doc = RevitAPI.Document;
            UIDocument uidoc = RevitAPI.UiDocument;

            // ---------------------------------------------------------------
            // Ветка A: активный вид — спецификация.
            //тут ищем не все семейства а только виды специфицируемые
            // ---------------------------------------------------------------
            if (doc.ActiveView is ViewSchedule schedule)
            {

                //может в спецификации уже выделена строка и она подсвечивает элементы, 
                //надо только их перечислить и оформить возврат

                // 1. Пробуем взять элементы из текущего выделения в модели.
                //    При щелчке по строке спецификации Revit подсвечивает именно экземпляры,
                //    и они попадают в Selection.GetElementIds().
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

                if (selectedIds != null && selectedIds.Count > 0)
                {
                    // Опционально: отфильтровать служебные элементы спецификации,
                    // если они вдруг попали в выделение (обычно их там нет).
                    List<ElementId> preselected = new List<ElementId>();
                    foreach (ElementId id in selectedIds)
                    {
                        Element el = doc.GetElement(id);
                        if (el == null) continue;
                        if (el is ViewSchedule) continue; // сама спецификация — не экземпляр
                        preselected.Add(id);
                    }

                    if (preselected.Count > 0)
                    {
                        return ShowElementsViewReport(
                            doc: doc,
                            uidoc: uidoc,
                            elementIds: preselected,
                            headerLine: $"Источник: выделенная строка спецификации " +
                                        $"(элементов: {preselected.Count})");
                    }
                }

                // 2. Если выделения нет — продолжаем по-старому: спрашиваем номер строки.
                //часто не срабатывает, но бывает работает

                int? rowNumber = ScheduleRowInput.Show();
                if (!rowNumber.HasValue)
                    return Result.Cancelled;

                List<ElementId> scheduleElementIds =
                    ResolveScheduleRowToElements(
                        doc: doc,
                        schedule: schedule,
                        userRowNumber: rowNumber.Value);

                if (scheduleElementIds == null || scheduleElementIds.Count == 0)
                {
                    TaskDialog.Show(
                        title: "Поиск по строке спецификации",
                        mainInstruction:
                            "Не удалось определить элементы выбранной строки.\n\n" +
                            "Возможные причины:\n" +
                            " • введён номер не из тела спецификации;\n" +
                            " • строка — заголовок группы или итог;\n" +
                            " • спецификация пуста или фильтрует все элементы.");
                    return Result.Cancelled;
                }

                return ShowElementsViewReport(
                    doc: doc,
                    uidoc: uidoc,
                    elementIds: scheduleElementIds,
                    headerLine: $"Источник: строка спецификации №{rowNumber.Value} " +
                                $"(элементов: {scheduleElementIds.Count})");
            }

            // ---------------------------------------------------------------
            // Ветка B: поиск по имени семейства / типоразмера.
            //а тут все
            // ---------------------------------------------------------------
            FamilySearchRequest familyInput = TryReadFamilyFromSelection(
                uidoc: uidoc,
                doc: doc,
                selectionInfo: out string selectionInfo);

            if (familyInput == null)
            {
                familyInput = FamilySearchInput2.Show();
                if (familyInput == null)
                    return Result.Cancelled;
            }

            string familyQuery = SafeTrim(familyInput.FamilyName);
            string typeQuery = SafeTrim(familyInput.TypeName);
            bool hasTypeFilter = !string.IsNullOrEmpty(typeQuery);

            if (string.IsNullOrEmpty(familyQuery))
            {
                TaskDialog.Show("Поиск семейства", "Не задано имя семейства.");
                return Result.Cancelled;
            }

            HashSet<ElementId> targetSymbolIds = CollectTargetSymbolIds(
                doc: doc,
                familyQuery: familyQuery,
                typeQuery: typeQuery,
                hasTypeFilter: hasTypeFilter);

            if (targetSymbolIds.Count == 0)
            {
                TaskDialog.Show(
                    title: "Поиск семейства",
                    mainInstruction: hasTypeFilter
                        ? $"Не найдено типоразмер «{typeQuery}» у семейства «{familyQuery}»."
                        : $"Семейство «{familyQuery}» в проекте не найдено.");
                return Result.Succeeded;
            }

            List<ElementId> matchedElementIds = CollectInstancesOfSymbols(
                doc: doc, targetSymbolIds: targetSymbolIds);

            if (matchedElementIds.Count == 0)
            {
                TaskDialog.Show("Поиск семейства", "Экземпляров не найдено.");
                return Result.Succeeded;
            }

            string headerLine = string.IsNullOrEmpty(selectionInfo)
                ? $"Семейство: «{familyQuery}»"
                  + (hasTypeFilter ? $"  |  типоразмер: «{typeQuery}»" : "")
                : selectionInfo;

            return ShowElementsViewReport(
                doc: doc,
                uidoc: uidoc,
                elementIds: matchedElementIds,
                headerLine: headerLine);
        }

        // ===================================================================
        //  Ветка A: из строки спецификации получаем ElementId'ы
        // ===================================================================
        private static List<ElementId> ResolveScheduleRowToElements(
            Document doc,
            ViewSchedule schedule,
            int userRowNumber)
        {
            ScheduleDefinition definition = schedule.Definition;

            // 1. Запоминаем, какие поля были скрыты — их нужно будет вернуть.
            List<ScheduleFieldId> hiddenFieldIds = new List<ScheduleFieldId>();
            foreach (ScheduleFieldId fieldId in definition.GetFieldOrder())
            {
                ScheduleField f = definition.GetField(fieldId);
                if (f != null && f.IsHidden)
                    hiddenFieldIds.Add(fieldId);
            }

            // 2. Временно показываем их, чтобы GetCellText видел все столбцы.
            if (hiddenFieldIds.Count > 0)
            {
                using (Transaction t = new Transaction(doc, "Показать скрытые поля"))
                {
                    t.Start();
                    foreach (ScheduleFieldId fieldId in hiddenFieldIds)
                    {
                        ScheduleField f = definition.GetField(fieldId);
                        if (f != null) f.IsHidden = false;
                    }
                    t.Commit();
                }
            }

            try
            {
                TableSectionData body = schedule.GetTableData()
                    .GetSectionData(SectionType.Body);

                int firstRow = body.FirstRowNumber;
                int lastRow = body.LastRowNumber;
                int targetRow = firstRow + userRowNumber - 1;
                if (targetRow < firstRow || targetRow > lastRow)
                    return null;

                // 3. Собираем список полей и значения целевой строки.
                List<ScheduleField> fields = new List<ScheduleField>();
                foreach (ScheduleFieldId fieldId in definition.GetFieldOrder())
                {
                    ScheduleField f = definition.GetField(fieldId);
                    if (f != null) fields.Add(f);
                }

                int numColumns = body.NumberOfColumns;
                string[] expected = new string[numColumns];
                for (int col = 0; col < numColumns && col < fields.Count; col++)
                {
                    string cell = body.GetCellText(targetRow, col);
                    expected[col] = string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();
                }

                // 4. Кандидаты — элементы, попадающие в спецификацию.
                List<Element> candidates = new FilteredElementCollector(doc, schedule.Id)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (candidates.Count == 0)
                {
                    candidates = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .ToList();
                }
                if (candidates.Count == 0)
                    return null;

                // 5. Сужаем список кандидатов последовательно по столбцам.
                List<Element> filtered = FilterByRow(doc, candidates, fields, expected);

                return filtered.Select(e => e.Id).ToList();
            }
            finally
            {
                // 6. Возвращаем скрытые поля в исходное состояние.
                if (hiddenFieldIds.Count > 0)
                {
                    using (Transaction t = new Transaction(doc, "Вернуть скрытые поля"))
                    {
                        t.Start();
                        foreach (ScheduleFieldId fieldId in hiddenFieldIds)
                        {
                            ScheduleField f = definition.GetField(fieldId);
                            if (f != null) f.IsHidden = true;
                        }
                        t.Commit();
                    }
                }
            }
        }
        /// <summary>
        /// Последовательно сужает список кандидатов: для каждого столбца оставляет
        /// только те элементы, у которых значение поля совпадает со значением строки.
        /// Если по какому-то полю не совпал никто — поле игнорируется (скорее всего,
        /// параметр нечитаем или формат другой), список сохраняется как есть.
        /// </summary>
        private static List<Element> FilterByRow(
            Document doc,
            List<Element> candidates,
            List<ScheduleField> fields,
            string[] expected)
        {
            List<Element> current = candidates;

            for (int col = 0; col < expected.Length && col < fields.Count; col++)
            {
                if (current.Count == 0) break;

                string exp = expected[col];
                if (string.IsNullOrEmpty(exp)) continue;   // пустая ячейка
                if (IsVariationsCell(exp)) continue;       // "<варианты>" и т.п.

                ScheduleField field = fields[col];
                if (field == null) continue;

                // Вычисляемые поля — не сравнимы по элементу.
                if (field.FieldType == ScheduleFieldType.Count
                    || field.FieldType == ScheduleFieldType.Formula
                    || field.FieldType == ScheduleFieldType.Percentage)
                    continue;
                
                ElementId paramId = field.ParameterId;
                if (paramId == null || paramId == ElementId.InvalidElementId)
                    continue;

                List<Element> next = new List<Element>(current.Count);
                
                foreach (Element el in current)
                {
                    if (ElementMatchesField(doc, el, paramId, exp))
                        next.Add(el);
                }

                // Если по этому столбцу не совпал никто — значит параметр у элементов
                // не прочитался (нет у них, ReadOnly, другой формат). Не сужаем.
                if (next.Count == 0)
                    continue;

                current = next;
            }
            return current;
        }

        /// <summary>
        /// Проверяет совпадение одного элемента с одним полем строки спецификации.
        /// </summary>
        private static bool ElementMatchesField(
    Document doc,
    Element element,
    ElementId paramId,
    string expected)
        {
            if (element == null) return false;

            Parameter p = FindParameter(doc, element, paramId);
            if (p == null) return false;

            string actual = ParameterToScheduleString(p);
            if (actual == null) return false;

            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryCompareAsNumbers(actual, expected))
                return true;

            return false;
        }

        /// <summary>
        /// Ищет параметр у элемента (или у его типа) по ElementId. Если не находит —
        /// пробует через BuiltInParameter: для встроенных параметров Id, возвращаемый
        /// ScheduleField.ParameterId, иногда не совпадает с Parameter.Id.
        /// </summary>
        private static Parameter FindParameter(
            Document doc, Element element, ElementId paramId)
        {
            if (element == null || paramId == null
                || paramId == ElementId.InvalidElementId)
                return null;

            foreach (Parameter p in element.Parameters)
            {
                if (p != null && p.Id == paramId)
                    return p;
            }

            Element type = doc.GetElement(element.GetTypeId());
            if (type != null)
            {
                foreach (Parameter p in type.Parameters)
                {
                    if (p != null && p.Id == paramId)
                        return p;
                }
            }

            return FindBuiltInParameter(element, type, paramId);
        }
        private static bool ElementMatchesScheduleRow(
            Document doc,
            Element element,
            List<ScheduleField> fields,
            string[] expected)
        {
            if (element == null) return false;

            Dictionary<ElementId, Parameter> instIndex = BuildParamIndex(element);
            Element type = doc.GetElement(element.GetTypeId());
            Dictionary<ElementId, Parameter> typeIndex = BuildParamIndex(type);

            for (int col = 0; col < expected.Length && col < fields.Count; col++)
            {
                string exp = expected[col];
                if (string.IsNullOrEmpty(exp)) continue;    // пустая ячейка
                if (IsVariationsCell(exp)) continue;        // "<варианты>"

                ScheduleField field = fields[col];
                if (field == null) continue;

                // Вычисляемые поля — не сравнимы по элементу.
                if (field.FieldType == ScheduleFieldType.Count
                    || field.FieldType == ScheduleFieldType.Formula
                    || field.FieldType == ScheduleFieldType.Percentage)
                    continue;

                ElementId paramId = field.ParameterId;
                if (paramId == null || paramId == ElementId.InvalidElementId)
                    continue;

                // Сначала ищем по индексу.
                Parameter p = null;
                if (!instIndex.TryGetValue(paramId, out p) || p == null)
                {
                    if (!typeIndex.TryGetValue(paramId, out p) || p == null)
                    {
                        // Fallback: BuiltInParameter по отрицательному IntegerValue.
                        p = FindBuiltInParameter(element, type, paramId);
                    }
                }

                // Параметр отсутствует у элемента, а в строке значение есть → не подходит.
                if (p == null) return false;

                string actual = ParameterToScheduleString(p);
                if (actual == null) continue;   // не смогли получить — не фильтруем

                if (string.Equals(actual, exp, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Числовое сравнение с учётом единиц измерения.
                if (TryCompareAsNumbers(actual, exp))
                    continue;

                return false;
            }
            return true;
        }

        /// <summary>
        /// Для встроенных параметров field.ParameterId может не совпадать с Parameter.Id.
        /// Тогда пробуем достучаться до параметра через BuiltInParameter.
        /// Enum.IsDefined требует точного совпадения типа со underlying-type перечисления,
        /// поэтому подбираем его динамически (Int32 или Int64 в зависимости от версии API).
        /// </summary>
        private static Parameter FindBuiltInParameter(
            Element element, Element type, ElementId paramId)
        {
            int bipInt = paramId.IntegerValue;
            if (bipInt >= 0) return null;

            Type enumType = typeof(BuiltInParameter);
            Type underlying = Enum.GetUnderlyingType(enumType);

            object boxed = (underlying == typeof(long))
                ? (object)(long)bipInt
                : (object)bipInt;

            if (!Enum.IsDefined(enumType, boxed))
                return null;

            BuiltInParameter bip = (BuiltInParameter)boxed;

            Parameter p = element.get_Parameter(bip);
            if (p == null && type != null)
                p = type.get_Parameter(bip);
            return p;
        }

        /// <summary>
        /// Возвращает текстовое значение параметра так, как его показывает Revit
        /// в спецификации: сначала AsValueString (единицы, формат), затем AsString,
        /// затем — прямое значение по StorageType.
        /// </summary>
        private static string ParameterToScheduleString(Parameter p)
        {
            if (p == null) return null;

            string s = p.AsValueString();
            if (!string.IsNullOrEmpty(s)) return s.Trim();

            s = p.AsString();
            if (!string.IsNullOrEmpty(s)) return s.Trim();

            switch (p.StorageType)
            {
                case StorageType.Integer:
                    return p.AsInteger().ToString(CultureInfo.InvariantCulture);

                case StorageType.Double:
                    return p.AsDouble().ToString(CultureInfo.InvariantCulture);

                case StorageType.String:
                    return p.AsString()?.Trim();

                case StorageType.ElementId:
                    {
                        ElementId id = p.AsElementId();
                        return (id == null || id == ElementId.InvalidElementId)
                            ? null
                            : id.IntegerValue.ToString(CultureInfo.InvariantCulture);
                    }
            }
            return null;
        }

        private static Dictionary<ElementId, Parameter> BuildParamIndex(Element element)
        {
            Dictionary<ElementId, Parameter> index =
                new Dictionary<ElementId, Parameter>();

            if (element == null) return index;

            foreach (Parameter p in element.Parameters)
            {
                if (p == null) continue;
                ElementId id = p.Id;
                if (id == null || id == ElementId.InvalidElementId) continue;
                if (!index.ContainsKey(id))
                    index[id] = p;
            }
            return index;
        }

        private static bool IsVariationsCell(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.Trim();
            if (t.Equals("<варианты>", StringComparison.OrdinalIgnoreCase)) return true;
            if (t.Equals("<varies>", StringComparison.OrdinalIgnoreCase)) return true;
            if (t.Equals("<различные>", StringComparison.OrdinalIgnoreCase)) return true;
            // На всякий случай — любые системные значения в угловых скобках.
            if (t.StartsWith("<") && t.EndsWith(">")) return true;
            return false;
        }

        // -------------------------------------------------------------------
        //  Сравнение чисел с учётом единиц измерения
        // -------------------------------------------------------------------
        private static readonly Dictionary<string, (string BaseUnit, double Factor)>
            UnitMap = new Dictionary<string, (string, double)>(
                StringComparer.OrdinalIgnoreCase)
        {
            // Длина (база — метр)
            { "нм",  ("m", 1e-9) }, { "nm",  ("m", 1e-9) },
            { "мкм", ("m", 1e-6) }, { "um",  ("m", 1e-6) }, { "µm", ("m", 1e-6) },
            { "мм",  ("m", 1e-3) }, { "mm",  ("m", 1e-3) },
            { "см",  ("m", 1e-2) }, { "cm",  ("m", 1e-2) },
            { "дм",  ("m", 1e-1) }, { "dm",  ("m", 1e-1) },
            { "м",   ("m", 1.0)  }, { "m",   ("m", 1.0)  },
            { "км",  ("m", 1e3)  }, { "km",  ("m", 1e3)  },

            // Площадь (база — м²)
            { "мм2", ("m2", 1e-6) }, { "mm2", ("m2", 1e-6) },
            { "см2", ("m2", 1e-4) }, { "cm2", ("m2", 1e-4) },
            { "дм2", ("m2", 1e-2) }, { "dm2", ("m2", 1e-2) },
            { "м2",  ("m2", 1.0)  }, { "m2",  ("m2", 1.0)  },
            { "га",  ("m2", 1e4)  }, { "ha",  ("m2", 1e4)  },

            // Объём (база — м³)
            { "мм3", ("m3", 1e-9) }, { "mm3", ("m3", 1e-9) },
            { "см3", ("m3", 1e-6) }, { "cm3", ("m3", 1e-6) },
            { "мл",  ("m3", 1e-6) }, { "ml",  ("m3", 1e-6) },
            { "л",   ("m3", 1e-3) }, { "l",   ("m3", 1e-3) },
            { "дм3", ("m3", 1e-3) }, { "dm3", ("m3", 1e-3) },
            { "м3",  ("m3", 1.0)  }, { "m3",  ("m3", 1.0)  },

            // Масса (база — кг)
            { "г",  ("kg", 1e-3) }, { "g",  ("kg", 1e-3) },
            { "кг", ("kg", 1.0)  }, { "kg", ("kg", 1.0)  },
            { "т",  ("kg", 1e3)  }, { "t",  ("kg", 1e3)  },
        };

        /// <summary>
        /// Разбирает строку вида "200 мм", "0.2м", "1,5 м²" в числовое значение,
        /// приведённое к базовой единице измерения (м, м², м³, кг).
        /// Единица возвращается отдельно, чтобы можно было убедиться, что
        /// сравниваемые величины одной размерности.
        /// </summary>
        private static bool TryParseValueWithUnit(
            string text, out double value, out string baseUnit)
        {
            value = double.NaN;
            baseUnit = null;
            if (string.IsNullOrEmpty(text)) return false;

            text = text.Trim().Replace('\u00A0', ' ');

            int i = 0;
            if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
            int digitStart = i;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            if (i < text.Length && (text[i] == '.' || text[i] == ','))
            {
                i++;
                while (i < text.Length && char.IsDigit(text[i])) i++;
            }
            if (i == digitStart) return false;

            string numPart = text.Substring(0, i).Replace(',', '.');
            if (!double.TryParse(numPart, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out value))
                return false;

            string unit = text.Substring(i).Trim();
            if (unit.Length == 0)
            {
                baseUnit = "";
                return true;
            }

            // Убираем пробелы внутри записи единицы: "м м", "кг / м3" не трогаем,
            // а вот "мм ²" имеет смысл нормализовать.
            string unitNorm = unit
                .Replace("\u00B2", "2")   // ²
                .Replace("\u00B3", "3")   // ³
                .Replace(" ", "");
            string unitNormDot = unitNorm.Replace('.', ',');

            if (UnitMap.TryGetValue(unit, out var info) ||
                UnitMap.TryGetValue(unitNorm, out info) ||
                UnitMap.TryGetValue(unitNormDot, out info))
            {
                value *= info.Factor;
                baseUnit = info.BaseUnit;
                return true;
            }

            // Неизвестная единица — сравниваем её как есть, только со «своей же».
            baseUnit = unit;
            return true;
        }

        private static bool TryCompareAsNumbers(string a, string b)
        {
            if (!TryParseValueWithUnit(a, out double va, out string ua)) return false;
            if (!TryParseValueWithUnit(b, out double vb, out string ub)) return false;

            // Размерности должны совпадать (м ↔ м, м² ↔ м², а не м ↔ м²).
            if (!string.Equals(ua ?? "", ub ?? "",
                               StringComparison.OrdinalIgnoreCase))
                return false;

            double diff = Math.Abs(va - vb);
            double scale = Math.Max(Math.Abs(va), Math.Abs(vb));
            return diff <= Math.Max(1e-9, scale * 1e-6);
        }

        // ===================================================================
        //  Ветка B: семейство / тип
        // ===================================================================
        private static HashSet<ElementId> CollectTargetSymbolIds(
            Document doc,
            string familyQuery,
            string typeQuery,
            bool hasTypeFilter)
        {
            HashSet<ElementId> targetSymbolIds = new HashSet<ElementId>();

            IEnumerable<FamilySymbol> symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            foreach (FamilySymbol symbol in symbols)
            {
                if (!string.Equals(
                        SafeTrim(symbol.FamilyName), familyQuery,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (hasTypeFilter &&
                    !string.Equals(
                        SafeTrim(symbol.Name), typeQuery,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                targetSymbolIds.Add(symbol.Id);
            }
            return targetSymbolIds;
        }

        private static List<ElementId> CollectInstancesOfSymbols(
            Document doc,
            HashSet<ElementId> targetSymbolIds)
        {
            List<ElementId> ids = new List<ElementId>();

            IEnumerable<Element> allInstances = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (Element element in allInstances)
            {
                ElementId typeId = element.GetTypeId();
                if (typeId == ElementId.InvalidElementId)
                    continue;
                if (targetSymbolIds.Contains(typeId))
                    ids.Add(element.Id);
            }
            return ids;
        }

        // ===================================================================
        //  Общий отчёт «Лист → Вид → Семейство [Тип] + Группы»
        // ===================================================================
        private static Result ShowElementsViewReport(
            Document doc,
            UIDocument uidoc,
            List<ElementId> elementIds,
            string headerLine)
        {
            if (elementIds == null || elementIds.Count == 0)
            {
                TaskDialog.Show("Поиск", "Элементы не найдены.");
                return Result.Succeeded;
            }

            Dictionary<ElementId, (string Name, string Number)> viewToSheet =
                BuildViewToSheetMap(doc);

            Dictionary<string, SheetBucket> sheetsWithModel =
                new Dictionary<string, SheetBucket>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ViewBucket> viewsWithoutSheet =
                new Dictionary<string, ViewBucket>(StringComparer.OrdinalIgnoreCase);

            List<ElementId> selectedIds = new List<ElementId>();
            int skippedNoView = 0;

            foreach (ElementId id in elementIds)
            {
                Element element = doc.GetElement(id);
                if (element == null) continue;

                GetFamilyAndType(element, doc,
                    out string familyName, out string typeName);

                string viewName = null;
                string sheetName = null;
                string sheetNumber = null;

                ResolveViewAndSheet(element, doc, viewToSheet,
                    out viewName, out sheetName, out sheetNumber);

                if (string.IsNullOrEmpty(viewName))
                {
                    skippedNoView++;
                    viewName = "<вид не определён>";
                }

                string groupName = null;
                if (element.GroupId != ElementId.InvalidElementId)
                    groupName = GetGroupName(doc, element);

                if (!string.IsNullOrEmpty(sheetName))
                {
                    string sheetKey = sheetNumber + "|" + sheetName;
                    if (!sheetsWithModel.TryGetValue(sheetKey, out SheetBucket bucket))
                    {
                        bucket = new SheetBucket
                        {
                            Name = sheetName,
                            Number = sheetNumber
                        };
                        sheetsWithModel[sheetKey] = bucket;
                    }
                    if (!bucket.Views.TryGetValue(viewName, out ViewBucket viewBucket))
                        bucket.Views[viewName] = viewBucket = new ViewBucket();

                    viewBucket.IncrementFamilyType(familyName, typeName);
                    if (!string.IsNullOrEmpty(groupName))
                        viewBucket.IncrementGroup(groupName);

                    bucket.TotalInstances++;
                }
                else
                {
                    if (!viewsWithoutSheet.TryGetValue(viewName, out ViewBucket viewBucket))
                        viewsWithoutSheet[viewName] = viewBucket = new ViewBucket();

                    viewBucket.IncrementFamilyType(familyName, typeName);
                    if (!string.IsNullOrEmpty(groupName))
                        viewBucket.IncrementGroup(groupName);
                }

                selectedIds.Add(element.Id);
            }

            string report = ComposeReport(
                headerLine: headerLine,
                totalInstances: selectedIds.Count,
                sheets: sheetsWithModel,
                viewsWithoutSheet: viewsWithoutSheet,
                skippedNoView: skippedNoView);

            if (selectedIds.Count > 0)
                uidoc.Selection.SetElementIds(selectedIds);

            TaskDialog dialog = new TaskDialog("Поиск семейства на видах")
            {
                MainInstruction = $"Найдено экземпляров: {selectedIds.Count}",
                MainContent = report
            };
            dialog.Show();

            return Result.Succeeded;
        }

        private static Dictionary<ElementId, (string Name, string Number)>
            BuildViewToSheetMap(Document doc)
        {
            Dictionary<ElementId, (string Name, string Number)> map =
                new Dictionary<ElementId, (string Name, string Number)>();

            IEnumerable<Viewport> viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (Viewport viewport in viewports)
            {
                ElementId sheetId = viewport.SheetId;
                ElementId viewId = viewport.ViewId;

                if (sheetId == ElementId.InvalidElementId) continue;
                if (viewId == ElementId.InvalidElementId) continue;
                if (!(doc.GetElement(sheetId) is ViewSheet sheet)) continue;
                if (map.ContainsKey(viewId)) continue;

                map[viewId] = (sheet.Name, sheet.SheetNumber);
            }
            return map;
        }

        private static void ResolveViewAndSheet(
            Element element,
            Document doc,
            Dictionary<ElementId, (string Name, string Number)> viewToSheet,
            out string viewName,
            out string sheetName,
            out string sheetNumber)
        {
            viewName = sheetName = sheetNumber = null;

            ElementId viewId = element.OwnerViewId;
            if (viewId == ElementId.InvalidElementId
                && element.GroupId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(element.GroupId) is Group group)
                    viewId = group.OwnerViewId;
            }

            if (viewId == ElementId.InvalidElementId)
                return;

            Element viewElement = doc.GetElement(viewId);
            if (viewElement is ViewSheet directSheet)
            {
                sheetName = directSheet.Name;
                sheetNumber = directSheet.SheetNumber;
                viewName = "<на листе>";
            }
            else if (viewElement is View view)
            {
                viewName = view.Name;
                if (viewToSheet.TryGetValue(
                        viewId, out (string Name, string Number) sheetInfo))
                {
                    sheetName = sheetInfo.Name;
                    sheetNumber = sheetInfo.Number;
                }
            }
        }

        private static void GetFamilyAndType(
            Element element,
            Document doc,
            out string familyName,
            out string typeName)
        {
            if (doc.GetElement(element.GetTypeId()) is FamilySymbol symbol)
            {
                familyName = SafeTrim(symbol.FamilyName);
                typeName = SafeTrim(symbol.Name);
                return;
            }

            familyName = "<не семейство>";
            Element typeElement = doc.GetElement(element.GetTypeId());
            typeName = typeElement == null ? "<без типа>" : SafeTrim(typeElement.Name);
        }

        private static string ComposeReport(
            string headerLine,
            int totalInstances,
            Dictionary<string, SheetBucket> sheets,
            Dictionary<string, ViewBucket> viewsWithoutSheet,
            int skippedNoView)
        {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(headerLine))
                sb.AppendLine(headerLine);

            sb.AppendLine($"Найдено экземпляров: {totalInstances}");
            sb.AppendLine($"Листов: {sheets.Count}");
            sb.AppendLine($"Видов без листа: {viewsWithoutSheet.Count}");
            sb.AppendLine();

            foreach (SheetBucket bucket in sheets.Values
                .OrderBy(s => s.Number, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(
                    $"=== Лист «{bucket.Name}» №{bucket.Number} " +
                    $"(экз.: {bucket.TotalInstances}) ===");

                foreach (KeyValuePair<string, ViewBucket> viewPair in bucket.Views
                    .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    AppendViewLine(sb, viewPair.Key, viewPair.Value);
                }

                sb.AppendLine();
            }

            if (viewsWithoutSheet.Count > 0)
            {
                sb.AppendLine("=== Виды без листа ===");
                foreach (KeyValuePair<string, ViewBucket> pair in viewsWithoutSheet
                    .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    AppendViewLine(sb, pair.Key, pair.Value);
                }
            }

            if (skippedNoView > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Экземпляров без определённого вида: {skippedNoView}");
            }

            return sb.ToString();
        }

        private static void AppendViewLine(
            StringBuilder sb, string viewName, ViewBucket bucket)
        {
            List<string> familyParts = new List<string>();

            foreach (KeyValuePair<string, Dictionary<string, int>> familyPair
                in bucket.FamilyTypes
                    .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                string family = familyPair.Key;
                Dictionary<string, int> types = familyPair.Value;
                int familyTotal = types.Values.Sum();

                if (types.Count == 1)
                {
                    KeyValuePair<string, int> only = types.First();
                    familyParts.Add($"{family} [{only.Key}] ({only.Value})");
                }
                else
                {
                    string typesText = string.Join(", ",
                        types.OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
                             .Select(t => $"{t.Key} ({t.Value})"));
                    familyParts.Add($"{family} ({familyTotal}): {typesText}");
                }
            }

            StringBuilder line = new StringBuilder();
            line.Append($"  Вид «{viewName}» — {string.Join("; ", familyParts)}");

            if (bucket.Groups.Count > 0)
            {
                IEnumerable<string> groups = bucket.Groups
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Value > 1 ? $"{g.Key} ({g.Value})" : g.Key);
                line.Append($" | Группы: {string.Join(", ", groups)}");
            }

            sb.AppendLine(line.ToString());
        }

        // ===================================================================
        //  Прочие утилиты
        // ===================================================================
        private static FamilySearchRequest TryReadFamilyFromSelection(
            UIDocument uidoc,
            Document doc,
            out string selectionInfo)
        {
            selectionInfo = null;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
                return null;

            foreach (ElementId id in selectedIds)
            {
                Element element = doc.GetElement(id);
                if (element == null) continue;

                FamilySymbol symbol = element as FamilySymbol;
                if (symbol == null)
                {
                    ElementId typeId = element.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                        symbol = doc.GetElement(typeId) as FamilySymbol;
                }
                if (symbol == null || symbol.Family == null) continue;

                string familyName = SafeTrim(symbol.FamilyName);
                if (string.IsNullOrEmpty(familyName)) continue;

                string typeName = SafeTrim(symbol.Name);
                selectionInfo = $"Из выделения: «{familyName}» [{typeName}]";

                return new FamilySearchRequest
                {
                    FamilyName = familyName,
                    TypeName = typeName
                };
            }
            return null;
        }

        private sealed class ViewBucket
        {
            public Dictionary<string, Dictionary<string, int>> FamilyTypes =
                new Dictionary<string, Dictionary<string, int>>(
                    StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, int> Groups =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public void IncrementFamilyType(string family, string type)
            {
                if (!FamilyTypes.TryGetValue(
                        family, out Dictionary<string, int> types))
                {
                    types = new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
                    FamilyTypes[family] = types;
                }
                types.TryGetValue(type, out int count);
                types[type] = count + 1;
            }

            public void IncrementGroup(string groupName)
            {
                Groups.TryGetValue(groupName, out int count);
                Groups[groupName] = count + 1;
            }
        }

        private sealed class SheetBucket
        {
            public string Name;
            public string Number;
            public Dictionary<string, ViewBucket> Views =
                new Dictionary<string, ViewBucket>(StringComparer.OrdinalIgnoreCase);
            public int TotalInstances;
        }

        private static string GetGroupName(Document doc, Element element)
        {
            try
            {
                ElementId groupId = element.GroupId;
                if (groupId == null || groupId == ElementId.InvalidElementId)
                    return null;

                if (doc.GetElement(groupId) is Group group)
                {
                    if (!string.IsNullOrEmpty(group.Name))
                        return group.Name;
                    if (group.GroupType != null
                        && !string.IsNullOrEmpty(group.GroupType.Name))
                        return group.GroupType.Name;
                }
                return $"<группа Id {groupId.IntegerValue}>";
            }
            catch { return null; }
        }

        private static readonly char[] TrimChars = new[]
        {
            ' ', '\t', '\r', '\n', '\f', '\v',
            '\u00A0', '\uFEFF',
            '\u200B', '\u200C', '\u200D', '\u2060'
        };

        private static string SafeTrim(string s)
            => string.IsNullOrEmpty(s) ? (s ?? "") : s.Trim(TrimChars);
    }

    // ===================================================================
    //  Диалоги
    // ===================================================================
    internal sealed class FamilySearchRequest
    {
        public string FamilyName;
        public string TypeName;
    }

    internal static class ScheduleRowInput
    {
        public static int? Show()
        {
            int? result = null;

            Window window = new Window
            {
                Title = "Номер строки спецификации",
                Width = 380,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            System.Windows.Controls.Grid grid =
                new System.Windows.Controls.Grid { Margin = new Thickness(12) };
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++)
                grid.RowDefinitions.Add(
                    new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock
            {
                Text = "Номер строки:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };
            System.Windows.Controls.TextBox input =
                new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Controls.Grid.SetRow(label, 0);
            System.Windows.Controls.Grid.SetColumn(label, 0);
            System.Windows.Controls.Grid.SetRow(input, 0);
            System.Windows.Controls.Grid.SetColumn(input, 1);
            grid.Children.Add(label);
            grid.Children.Add(input);

            TextBlock hint = new TextBlock
            {
                Text = "Нумерация строк начинается с 1 (как видно в теле спецификации).",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 4)
            };
            System.Windows.Controls.Grid.SetRow(hint, 1);
            System.Windows.Controls.Grid.SetColumn(hint, 0);
            System.Windows.Controls.Grid.SetColumnSpan(hint, 2);
            grid.Children.Add(hint);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Button ok = new Button
            {
                Content = "Найти",
                Width = 100,
                Margin = new Thickness(4),
                IsDefault = true
            };
            Button cancel = new Button
            {
                Content = "Отмена",
                Width = 80,
                Margin = new Thickness(4),
                IsCancel = true
            };

            ok.Click += (s, e) =>
            {
                if (int.TryParse(input.Text.Trim(), out int value) && value > 0)
                {
                    result = value;
                    window.DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Введите положительное целое число.");
                }
            };
            cancel.Click += (s, e) =>
            {
                window.DialogResult = false;
            };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            System.Windows.Controls.Grid.SetRow(buttons, 2);
            System.Windows.Controls.Grid.SetColumn(buttons, 0);
            System.Windows.Controls.Grid.SetColumnSpan(buttons, 2);
            grid.Children.Add(buttons);

            window.Content = grid;
            window.ShowDialog();
            return result;
        }
    }

    internal static class FamilySearchInput2
    {
        public static FamilySearchRequest Show()
        {
            FamilySearchRequest result = null;

            Window window = new Window
            {
                Title = "Поиск семейства на видах",
                Width = 460,
                Height = 210,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            System.Windows.Controls.Grid grid =
                new System.Windows.Controls.Grid { Margin = new Thickness(12) };
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 4; i++)
                grid.RowDefinitions.Add(
                    new RowDefinition { Height = GridLength.Auto });

            TextBlock familyLabel = new TextBlock
            {
                Text = "Семейство:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };
            System.Windows.Controls.TextBox familyBox =
                new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Controls.Grid.SetRow(familyLabel, 0);
            System.Windows.Controls.Grid.SetColumn(familyLabel, 0);
            System.Windows.Controls.Grid.SetRow(familyBox, 0);
            System.Windows.Controls.Grid.SetColumn(familyBox, 1);
            grid.Children.Add(familyLabel);
            grid.Children.Add(familyBox);

            TextBlock typeLabel = new TextBlock
            {
                Text = "Типоразмер:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4)
            };
            System.Windows.Controls.TextBox typeBox =
                new System.Windows.Controls.TextBox { Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Controls.Grid.SetRow(typeLabel, 1);
            System.Windows.Controls.Grid.SetColumn(typeLabel, 0);
            System.Windows.Controls.Grid.SetRow(typeBox, 1);
            System.Windows.Controls.Grid.SetColumn(typeBox, 1);
            grid.Children.Add(typeLabel);
            grid.Children.Add(typeBox);

            TextBlock hint = new TextBlock
            {
                Text = "Типоразмер можно оставить пустым — будут показаны все типы семейства.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 4)
            };
            System.Windows.Controls.Grid.SetRow(hint, 2);
            System.Windows.Controls.Grid.SetColumn(hint, 0);
            System.Windows.Controls.Grid.SetColumnSpan(hint, 2);
            grid.Children.Add(hint);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Button ok = new Button
            {
                Content = "Найти",
                Width = 100,
                Margin = new Thickness(4),
                IsDefault = true
            };
            Button cancel = new Button
            {
                Content = "Отмена",
                Width = 80,
                Margin = new Thickness(4),
                IsCancel = true
            };

            ok.Click += (s, e) =>
            {
                result = new FamilySearchRequest
                {
                    FamilyName = familyBox.Text,
                    TypeName = typeBox.Text
                };
                window.DialogResult = true;
            };
            cancel.Click += (s, e) =>
            {
                window.DialogResult = false;
            };

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            System.Windows.Controls.Grid.SetRow(buttons, 3);
            System.Windows.Controls.Grid.SetColumn(buttons, 0);
            System.Windows.Controls.Grid.SetColumnSpan(buttons, 2);
            grid.Children.Add(buttons);

            window.Content = grid;
            window.ShowDialog();
            return result;
        }
    }
}