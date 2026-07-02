using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicTechnologyAliasDefinition
    {
        public CivicTechnologyAliasDefinition(string semanticTechnologyId, string mappedTechnologyId, string reasonKo)
        {
            SemanticTechnologyId = semanticTechnologyId;
            MappedTechnologyId = mappedTechnologyId;
            ReasonKo = reasonKo;
        }

        public string SemanticTechnologyId { get; }
        public string MappedTechnologyId { get; }
        public string ReasonKo { get; }
    }

    public sealed class CivicModuleBalanceContent
    {
        public CivicModuleBalanceContent(
            IReadOnlyList<CivicModifierCapDefinition> modifierCaps,
            IReadOnlyList<CivicTechnologyAliasDefinition> technologyAliases)
        {
            ModifierCaps = modifierCaps;
            TechnologyAliases = technologyAliases;
        }

        public IReadOnlyList<CivicModifierCapDefinition> ModifierCaps { get; }
        public IReadOnlyList<CivicTechnologyAliasDefinition> TechnologyAliases { get; }

        public void ValidateAgainst(CivicGameData data)
        {
            var errors = TechnologyAliases
                .Where(alias => !data.TechnologiesById.ContainsKey(alias.MappedTechnologyId))
                .Select(alias => $"Unknown mapped technology alias target: {alias.SemanticTechnologyId} -> {alias.MappedTechnologyId}")
                .ToArray();
            if (errors.Length > 0) throw new CivicDataException(errors);
        }
    }

    public static class CivicModuleBalanceContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicModuleBalanceContent LoadFromResources()
        {
            var caps = Resources.Load<TextAsset>(Root + "modifier_caps");
            var aliases = Resources.Load<TextAsset>(Root + "technology_aliases");
            if (caps == null || aliases == null) throw new CivicDataException(new[] { "Module balance CSV resources are missing." });
            return Parse(caps.text, aliases.text);
        }

        public static CivicModuleBalanceContent Parse(string capsCsv, string aliasesCsv)
        {
            var errors = new List<string>();
            var caps = CivicCsvParser.Parse(capsCsv, errors, "modifier_caps.csv")
                .Select(row => new CivicModifierCapDefinition(
                    Value(row, "id"),
                    Number(row, "minimum", errors, "modifier_caps.csv"),
                    Number(row, "maximum", errors, "modifier_caps.csv")))
                .ToArray();
            var aliases = CivicCsvParser.Parse(aliasesCsv, errors, "technology_aliases.csv")
                .Select(row => new CivicTechnologyAliasDefinition(
                    Value(row, "semanticTechnologyId"),
                    Value(row, "mappedTechnologyId"),
                    Value(row, "reasonKo")))
                .ToArray();

            foreach (var duplicate in caps.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add("Duplicate modifier cap: " + duplicate.Key);
            foreach (var duplicate in aliases.GroupBy(item => item.SemanticTechnologyId, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add("Duplicate technology alias: " + duplicate.Key);
            foreach (var alias in aliases) if (string.IsNullOrWhiteSpace(alias.SemanticTechnologyId) || string.IsNullOrWhiteSpace(alias.MappedTechnologyId)) errors.Add("Technology alias IDs cannot be empty.");
            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicModuleBalanceContent(caps, aliases);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }
    }
}
