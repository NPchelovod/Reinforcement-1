using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Reinforcement.Worksets
{
    /// <summary>
    /// Одно правило: какие элементы → в какой рабочий набор.
    /// </summary>
    public class WorksetRule
    {
        public string WorksetName { get; set; }

        /// <summary>Список BuiltInCategory (не используется для ImportInstance / CAD-связей).</summary>
        public List<BuiltInCategory> Categories { get; set; } = new List<BuiltInCategory>();

        /// <summary>Подстрока в имени ТИПА элемента. null/"" — без фильтра.</summary>
        public string TypeNameContains { get; set; }

        /// <summary>Подстрока, при наличии которой в имени ТИПА правило НЕ применяется.</summary>
        public string TypeNameExcludes { get; set; }

        /// <summary>Подстрока в имени файла связи (RevitLinkType.Name или CADLinkType.Name).</summary>
        public string LinkNameContains { get; set; }

        /// <summary>Только импортированные DWG (ImportInstance с IsLinked == false).</summary>
        public bool MatchImportInstanceOnly { get; set; }

        /// <summary>Только связанные DWG (ImportInstance с IsLinked == true).</summary>
        public bool MatchCadLinkOnly { get; set; }

        public bool IsEnabled { get; set; } = true;

        public string Description
        {
            get
            {
                var parts = new List<string>();
                if (MatchImportInstanceOnly) parts.Add("импорт DWG");
                else if (MatchCadLinkOnly) parts.Add("CAD-связь");
                else if (Categories != null && Categories.Count > 0)
                    parts.Add(string.Join(", ",
                        Categories.ConvertAll(c => c.ToString().Replace("OST_", ""))));

                if (!string.IsNullOrWhiteSpace(TypeNameContains))
                    parts.Add($"тип содержит «{TypeNameContains}»");
                if (!string.IsNullOrWhiteSpace(TypeNameExcludes))
                    parts.Add($"исключая «{TypeNameExcludes}»");
                if (!string.IsNullOrWhiteSpace(LinkNameContains))
                    parts.Add($"связь содержит «{LinkNameContains}»");

                return string.Join("; ", parts);
            }
        }
    }
}