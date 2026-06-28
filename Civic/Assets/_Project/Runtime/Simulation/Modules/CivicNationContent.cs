using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicNationDefinition
    {
        public CivicNationDefinition(string id, string displayNameKo, string conceptId, int tier, double preparationSeconds, double treasuryCost, bool secret, IReadOnlyList<string> requiredFeatureIds, string descriptionKo)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            ConceptId = conceptId;
            Tier = tier;
            PreparationSeconds = preparationSeconds;
            TreasuryCost = treasuryCost;
            Secret = secret;
            RequiredFeatureIds = requiredFeatureIds;
            DescriptionKo = descriptionKo;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string ConceptId { get; }
        public int Tier { get; }
        public double PreparationSeconds { get; }
        public double TreasuryCost { get; }
        public bool Secret { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
        public string DescriptionKo { get; }
    }

    public sealed class CivicNationConditionDefinition
    {
        public CivicNationConditionDefinition(string nationId, string metricId, string comparator, double value, string alternativeGroup)
        {
            NationId = nationId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
            AlternativeGroup = alternativeGroup;
        }

        public string NationId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
        public string AlternativeGroup { get; }
    }

    public sealed class CivicNationEffectDefinition
    {
        public CivicNationEffectDefinition(string nationId, string charterId, string effectType, string targetId, double amount, string capGroup)
        {
            NationId = nationId;
            CharterId = charterId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CapGroup = capGroup;
        }

        public string NationId { get; }
        public string CharterId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CapGroup { get; }
    }

    public sealed class CivicNationContent
    {
        public CivicNationContent(IReadOnlyList<CivicNationDefinition> nations, IReadOnlyList<CivicNationConditionDefinition> conditions, IReadOnlyList<CivicNationEffectDefinition> effects)
        {
            Nations = nations;
            Conditions = conditions;
            Effects = effects;
        }

        public IReadOnlyList<CivicNationDefinition> Nations { get; }
        public IReadOnlyList<CivicNationConditionDefinition> Conditions { get; }
        public IReadOnlyList<CivicNationEffectDefinition> Effects { get; }
    }

    public static class CivicNationContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicNationContent LoadFromResources()
        {
            var nations = Resources.Load<TextAsset>(Root + "nations");
            var conditions = Resources.Load<TextAsset>(Root + "nation_conditions");
            var effects = Resources.Load<TextAsset>(Root + "nation_effects");
            if (nations == null || conditions == null || effects == null)
            {
                throw new CivicDataException(new[] { "Nation CSV resources are missing." });
            }

            return Parse(nations.text, conditions.text, effects.text);
        }

        public static CivicNationContent Parse(string nationsCsv, string conditionsCsv, string effectsCsv)
        {
            var errors = new List<string>();
            var nations = CivicCsvParser.Parse(nationsCsv, errors, "nations.csv").Select(row => new CivicNationDefinition(
                Value(row, "id"), Value(row, "displayNameKo"), Value(row, "conceptId"),
                Integer(row, "tier", errors, "nations.csv"), Number(row, "preparationSeconds", errors, "nations.csv"),
                Number(row, "treasuryCost", errors, "nations.csv"), Boolean(row, "secret", errors, "nations.csv"),
                SplitIds(Value(row, "requiredFeatureIds")), Value(row, "descriptionKo"))).ToArray();
            var conditions = CivicCsvParser.Parse(conditionsCsv, errors, "nation_conditions.csv").Select(row => new CivicNationConditionDefinition(
                Value(row, "nationId"), Value(row, "metricId"), Value(row, "comparator"),
                Number(row, "value", errors, "nation_conditions.csv"), Value(row, "alternativeGroup"))).ToArray();
            var effects = CivicCsvParser.Parse(effectsCsv, errors, "nation_effects.csv").Select(row => new CivicNationEffectDefinition(
                Value(row, "nationId"), Value(row, "charterId"), Value(row, "effectType"), Value(row, "targetId"),
                Number(row, "amount", errors, "nation_effects.csv"), Value(row, "capGroup"))).ToArray();

            var ids = nations.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in nations.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add($"Duplicate nation ID: {duplicate.Key}");
            foreach (var reference in conditions.Select(item => item.NationId).Concat(effects.Select(item => item.NationId)))
            {
                if (!ids.Contains(reference)) errors.Add($"Unknown nation reference: {reference}");
            }

            foreach (var nation in nations)
            {
                if (nation.PreparationSeconds < 0d || nation.TreasuryCost < 0d) errors.Add($"Nation has negative cost or duration: {nation.Id}");
                if (!conditions.Any(condition => condition.NationId == nation.Id)) errors.Add($"Nation has no conditions: {nation.Id}");
            }

            foreach (var condition in conditions)
            {
                if (condition.Comparator != ">=" && condition.Comparator != "<=" && condition.Comparator != "==") errors.Add($"Unsupported nation comparator: {condition.NationId}/{condition.Comparator}");
            }

            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicNationContent(nations, conditions, effects);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => (int)Number(row, key, errors, source);
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }

        private static bool Boolean(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (bool.TryParse(raw, out var value)) return value;
            errors.Add($"{source} has invalid boolean {key}: {raw}");
            return false;
        }

        private static IReadOnlyList<string> SplitIds(string value) => value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
    }
}
