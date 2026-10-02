using System;
using Autodesk.Revit.DB;
namespace Reinforcement
{
    public static class ParameterHelper
    {
        // Named parameters can be ambiguous; never select an arbitrary duplicate.
        public static Parameter Find(Element element, string name, bool includeType = true)
        {
            if (element == null || !element.IsValidObject || string.IsNullOrWhiteSpace(name)) return null;
            var parameters = element.GetParameters(name);
            if (parameters.Count > 1) return null;
            if (parameters.Count == 1 && parameters[0].HasValue) return parameters[0];
            var type = includeType ? element.Document.GetElement(element.GetTypeId()) : null;
            if (type != null && type.Id != element.Id) return Find(type, name, false);
            return parameters.Count == 1 ? parameters[0] : null;
        }
        public static Parameter Find(Element element, BuiltInParameter id, bool includeType = true)
        {
            if (element == null || !element.IsValidObject) return null;
            var parameter = element.get_Parameter(id);
            if (parameter != null && parameter.HasValue) return parameter;
            var type = includeType ? element.Document.GetElement(element.GetTypeId()) : null;
            return type != null && type.Id != element.Id ? Find(type, id, false) : parameter;
        }
        public static bool TryReadDouble(Parameter parameter, out double value)
        {
            value = 0;
            if (parameter == null || !parameter.HasValue || parameter.StorageType != StorageType.Double) return false;
            value = parameter.AsDouble();
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        public static bool TryReadLength(Parameter parameter, ForgeTypeId unit, out double value)
        {
            value = 0;
            double internalValue;
            if (!TryReadDouble(parameter, out internalValue) ||
                !parameter.Definition.GetDataType().Equals(SpecTypeId.Length)) return false;
            value = UnitUtils.ConvertFromInternalUnits(internalValue, unit);
            return true;
        }
        public static bool TryReadInteger(Parameter parameter, out int value)
        {
            value = 0;
            if (parameter == null || !parameter.HasValue || parameter.StorageType != StorageType.Integer) return false;
            value = parameter.AsInteger(); return true;
        }
        public static string ReadText(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue) return null;
            return parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString();
        }
        public static bool TrySetText(Parameter parameter, string value, out string reason)
        {
            reason = null;
            if (parameter == null) reason = "Параметр не найден или имя неоднозначно";
            else if (parameter.IsReadOnly) reason = "Параметр доступен только для чтения";
            else if (parameter.StorageType != StorageType.String) reason = "Параметр должен содержать текст";
            if (reason != null) return false;
            if (parameter.Set(value ?? string.Empty)) return true;
            reason = "Revit не записал значение параметра"; return false;
        }
    }
}
