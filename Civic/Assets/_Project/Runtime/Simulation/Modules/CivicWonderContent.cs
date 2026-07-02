using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicWonderDefinition
    {
        public CivicWonderDefinition(string id, string displayNameKo, string conceptId, string eraId, string technologyId, double upfrontTreasury, string upkeepType, double upkeepAmount, IReadOnlyList<string> requiredFeatureIds)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            ConceptId = conceptId;
            EraId = eraId;
            TechnologyId = technologyId;
            UpfrontTreasury = upfrontTreasury;
            UpkeepType = upkeepType;
            UpkeepAmount = upkeepAmount;
            RequiredFeatureIds = requiredFeatureIds;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string ConceptId { get; }
        public string EraId { get; }
        public string TechnologyId { get; }
        public double UpfrontTreasury { get; }
        public string UpkeepType { get; }
        public double UpkeepAmount { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
    }

    public sealed class CivicWonderCostDefinition
    {
        public CivicWonderCostDefinition(string wonderId, string resourceId, double amount, double deliveryRate)
        {
            WonderId = wonderId;
            ResourceId = resourceId;
            Amount = amount;
            DeliveryRate = deliveryRate;
        }

        public string WonderId { get; }
        public string ResourceId { get; }
        public double Amount { get; }
        public double DeliveryRate { get; }
    }

    public sealed class CivicWonderConditionDefinition
    {
        public CivicWonderConditionDefinition(string wonderId, string metricId, string comparator, double value)
        {
            WonderId = wonderId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
        }

        public string WonderId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
    }

    public sealed class CivicWonderEffectDefinition
    {
        public CivicWonderEffectDefinition(string wonderId, string effectType, string targetId, double amount, double duration, string capGroup, string runtimeEffectType = "", string runtimeTargetId = "")
        {
            WonderId = wonderId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            Duration = duration;
            CapGroup = capGroup;
            RuntimeEffectType = runtimeEffectType;
            RuntimeTargetId = runtimeTargetId;
        }

        public string WonderId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public double Duration { get; }
        public string CapGroup { get; }
        public string RuntimeEffectType { get; }
        public string RuntimeTargetId { get; }
        public CivicResolvedModuleEffect Resolve() => CivicProvisionalEffect.Resolve(EffectType, TargetId, RuntimeEffectType, RuntimeTargetId, Amount, Duration, CapGroup);
    }

    public sealed class CivicWonderContent
    {
        public CivicWonderContent(IReadOnlyList<CivicWonderDefinition> wonders, IReadOnlyList<CivicWonderCostDefinition> costs, IReadOnlyList<CivicWonderConditionDefinition> conditions, IReadOnlyList<CivicWonderEffectDefinition> effects)
        {
            Wonders = wonders;
            Costs = costs;
            Conditions = conditions;
            Effects = effects;
        }

        public IReadOnlyList<CivicWonderDefinition> Wonders { get; }
        public IReadOnlyList<CivicWonderCostDefinition> Costs { get; }
        public IReadOnlyList<CivicWonderConditionDefinition> Conditions { get; }
        public IReadOnlyList<CivicWonderEffectDefinition> Effects { get; }
    }

    public static class CivicWonderContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicWonderContent LoadFromResources()
        {
            var wonders = Resources.Load<TextAsset>(Root + "wonders");
            var costs = Resources.Load<TextAsset>(Root + "wonder_costs");
            var conditions = Resources.Load<TextAsset>(Root + "wonder_conditions");
            var effects = Resources.Load<TextAsset>(Root + "wonder_effects");
            if (wonders == null || costs == null || conditions == null || effects == null)
            {
                throw new CivicDataException(new[] { "Wonder CSV resources are missing." });
            }

            return Parse(wonders.text, costs.text, conditions.text, effects.text);
        }

        public static CivicWonderContent Parse(string wondersCsv, string costsCsv, string conditionsCsv, string effectsCsv)
        {
            var errors = new List<string>();
            var wonders = CivicCsvParser.Parse(wondersCsv, errors, "wonders.csv").Select(row => new CivicWonderDefinition(
                Value(row, "id"), Value(row, "displayNameKo"), Value(row, "conceptId"), Value(row, "eraId"), Value(row, "technologyId"),
                Number(row, "upfrontTreasury", errors, "wonders.csv"), Value(row, "upkeepType"), Number(row, "upkeepAmount", errors, "wonders.csv"),
                SplitIds(Value(row, "requiredFeatureIds")))).ToArray();
            var costs = CivicCsvParser.Parse(costsCsv, errors, "wonder_costs.csv").Select(row => new CivicWonderCostDefinition(
                Value(row, "wonderId"), Value(row, "resourceId"), Number(row, "amount", errors, "wonder_costs.csv"), Number(row, "deliveryRate", errors, "wonder_costs.csv"))).ToArray();
            var conditions = CivicCsvParser.Parse(conditionsCsv, errors, "wonder_conditions.csv").Select(row => new CivicWonderConditionDefinition(
                Value(row, "wonderId"), Value(row, "metricId"), Value(row, "comparator"), Number(row, "value", errors, "wonder_conditions.csv"))).ToArray();
            var effects = CivicCsvParser.Parse(effectsCsv, errors, "wonder_effects.csv").Select(row => new CivicWonderEffectDefinition(
                Value(row, "wonderId"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "wonder_effects.csv"),
                Number(row, "duration", errors, "wonder_effects.csv"), Value(row, "capGroup"), Value(row, "runtimeEffectType"), Value(row, "runtimeTargetId"))).ToArray();

            var ids = wonders.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in wonders.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add($"Duplicate wonder ID: {duplicate.Key}");
            foreach (var reference in costs.Select(item => item.WonderId).Concat(conditions.Select(item => item.WonderId)).Concat(effects.Select(item => item.WonderId)))
            {
                if (!ids.Contains(reference)) errors.Add($"Unknown wonder reference: {reference}");
            }

            foreach (var wonder in wonders)
            {
                if (!costs.Any(cost => cost.WonderId == wonder.Id)) errors.Add($"Wonder has no costs: {wonder.Id}");
            }

            foreach (var cost in costs)
            {
                if (cost.Amount <= 0d || cost.DeliveryRate <= 0d) errors.Add($"Wonder cost must be positive: {cost.WonderId}/{cost.ResourceId}");
            }

            foreach (var condition in conditions)
            {
                if (condition.Comparator != ">=" && condition.Comparator != "<=" && condition.Comparator != "==") errors.Add($"Unsupported wonder comparator: {condition.WonderId}/{condition.Comparator}");
            }

            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicWonderContent(wonders, costs, conditions, effects);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }

        private static IReadOnlyList<string> SplitIds(string value) => value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
    }
}
