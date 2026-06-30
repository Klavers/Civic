using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicPoliticsRules
    {
        private readonly IReadOnlyDictionary<string, double> values;

        public CivicPoliticsRules(IReadOnlyDictionary<string, double> values)
        {
            this.values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public double this[string id] => values.TryGetValue(id, out var value) ? value : throw new CivicDataException(new[] { "Missing politics rule: " + id });

        public static CivicPoliticsRules Defaults => new CivicPoliticsRules(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["neutralLegitimacy"] = 50d,
            ["neutralSupport"] = 0.5d,
            ["basePoliticalCapitalPerSecond"] = 1d,
            ["politicalCapitalLegitimacyBase"] = 0.5d,
            ["politicalCapitalLegitimacyWeight"] = 0.01d,
            ["supportLivingStandardWeight"] = 0.002d,
            ["legitimacyLivingStandardWeight"] = 0.2d,
            ["livingStandardBase"] = 20d,
            ["livingStandardSupplyWeight"] = 50d,
            ["livingStandardGdpPerCapitaWeight"] = 20d,
            ["livingStandardTreasuryWeight"] = 10d,
            ["livingStandardMinimum"] = 0d,
            ["livingStandardMaximum"] = 100d
        });
    }

    public sealed class CivicInstitutionDefinition
    {
        public CivicInstitutionDefinition(string id, string category, string displayNameKo, string descriptionKo, int order, bool isDefault, double politicalCost, double treasuryCost, double reformSeconds, double fatigueSeconds)
        {
            Id = id;
            Category = category;
            DisplayNameKo = displayNameKo;
            DescriptionKo = descriptionKo;
            Order = order;
            IsDefault = isDefault;
            PoliticalCost = politicalCost;
            TreasuryCost = treasuryCost;
            ReformSeconds = reformSeconds;
            FatigueSeconds = fatigueSeconds;
        }

        public string Id { get; }
        public string Category { get; }
        public string DisplayNameKo { get; }
        public string DescriptionKo { get; }
        public int Order { get; }
        public bool IsDefault { get; }
        public double PoliticalCost { get; }
        public double TreasuryCost { get; }
        public double ReformSeconds { get; }
        public double FatigueSeconds { get; }
    }

    public sealed class CivicInstitutionUnlockDefinition
    {
        public CivicInstitutionUnlockDefinition(string institutionId, string metricId, string comparator, double value, string alternativeGroup)
        {
            InstitutionId = institutionId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
            AlternativeGroup = alternativeGroup;
        }

        public string InstitutionId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
        public string AlternativeGroup { get; }
    }

    public sealed class CivicInstitutionEffectDefinition
    {
        public CivicInstitutionEffectDefinition(string institutionId, string effectType, string targetId, double amount, string costType, string capGroup, double duration = 0d, string runtimeEffectType = "", string runtimeTargetId = "")
        {
            InstitutionId = institutionId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CostType = costType;
            CapGroup = capGroup;
            Duration = duration;
            RuntimeEffectType = runtimeEffectType;
            RuntimeTargetId = runtimeTargetId;
        }

        public string InstitutionId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CostType { get; }
        public string CapGroup { get; }
        public double Duration { get; }
        public string RuntimeEffectType { get; }
        public string RuntimeTargetId { get; }
        public CivicResolvedModuleEffect Resolve() => CivicProvisionalEffect.Resolve(EffectType, TargetId, RuntimeEffectType, RuntimeTargetId, Amount, Duration, CapGroup);
    }

    public sealed class CivicPoliticsContent
    {
        public CivicPoliticsContent(IReadOnlyList<CivicInstitutionDefinition> institutions, IReadOnlyList<CivicInstitutionUnlockDefinition> unlocks, IReadOnlyList<CivicInstitutionEffectDefinition> effects, CivicPoliticsRules rules = null)
        {
            Institutions = institutions;
            Unlocks = unlocks;
            Effects = effects;
            Rules = rules ?? CivicPoliticsRules.Defaults;
        }

        public IReadOnlyList<CivicInstitutionDefinition> Institutions { get; }
        public IReadOnlyList<CivicInstitutionUnlockDefinition> Unlocks { get; }
        public IReadOnlyList<CivicInstitutionEffectDefinition> Effects { get; }
        public CivicPoliticsRules Rules { get; }
    }

    public static class CivicPoliticsContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicPoliticsContent LoadFromResources()
        {
            var institutions = Resources.Load<TextAsset>(Root + "institutions");
            var unlocks = Resources.Load<TextAsset>(Root + "institution_unlocks");
            var effects = Resources.Load<TextAsset>(Root + "institution_effects");
            var rules = Resources.Load<TextAsset>(Root + "politics_rules");
            if (institutions == null || unlocks == null || effects == null || rules == null)
            {
                throw new CivicDataException(new[] { "Politics CSV resources are missing." });
            }
            return Parse(institutions.text, unlocks.text, effects.text, rules.text);
        }

        public static CivicPoliticsContent Parse(string institutionsCsv, string unlocksCsv, string effectsCsv, string rulesCsv = null)
        {
            var errors = new List<string>();
            var institutions = CivicCsvParser.Parse(institutionsCsv, errors, "institutions.csv").Select(row => new CivicInstitutionDefinition(
                Value(row, "id"), Value(row, "category"), Value(row, "displayNameKo"), Value(row, "descriptionKo"), Integer(row, "order", errors, "institutions.csv"),
                Boolean(row, "isDefault", errors, "institutions.csv"), Number(row, "politicalCost", errors, "institutions.csv"), Number(row, "treasuryCost", errors, "institutions.csv"),
                Number(row, "reformSeconds", errors, "institutions.csv"), Number(row, "fatigueSeconds", errors, "institutions.csv"))).ToArray();
            var unlocks = CivicCsvParser.Parse(unlocksCsv, errors, "institution_unlocks.csv").Select(row => new CivicInstitutionUnlockDefinition(
                Value(row, "institutionId"), Value(row, "metricId"), Value(row, "comparator"), Number(row, "value", errors, "institution_unlocks.csv"), Value(row, "alternativeGroup"))).ToArray();
            var effects = CivicCsvParser.Parse(effectsCsv, errors, "institution_effects.csv").Select(row => new CivicInstitutionEffectDefinition(
                Value(row, "institutionId"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "institution_effects.csv"), Value(row, "costType"), Value(row, "capGroup"),
                OptionalNumber(row, "duration", errors, "institution_effects.csv"), Value(row, "runtimeEffectType"), Value(row, "runtimeTargetId"))).ToArray();
            var rules = string.IsNullOrWhiteSpace(rulesCsv)
                ? CivicPoliticsRules.Defaults
                : new CivicPoliticsRules(CivicCsvParser.Parse(rulesCsv, errors, "politics_rules.csv")
                    .ToDictionary(row => Value(row, "id"), row => Number(row, "value", errors, "politics_rules.csv"), StringComparer.Ordinal));
            var ids = institutions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in institutions.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add($"Duplicate institution ID: {duplicate.Key}");
            foreach (var group in institutions.GroupBy(item => item.Category, StringComparer.Ordinal))
            {
                if (group.Count(item => item.IsDefault) != 1) errors.Add($"Institution category must have one default: {group.Key}");
            }
            foreach (var reference in unlocks.Select(item => item.InstitutionId).Concat(effects.Select(item => item.InstitutionId))) if (!ids.Contains(reference)) errors.Add($"Unknown institution reference: {reference}");
            foreach (var institution in institutions) if (institution.ReformSeconds <= 0d || institution.FatigueSeconds < 0d) errors.Add($"Invalid reform timing: {institution.Id}");
            foreach (var unlock in unlocks) if (unlock.Comparator != ">=" && unlock.Comparator != "<=" && unlock.Comparator != "==") errors.Add($"Unsupported institution comparator: {unlock.InstitutionId}/{unlock.Comparator}");
            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicPoliticsContent(institutions, unlocks, effects, rules);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => (int)Number(row, key, errors, source);
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key); if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}"); return 0d;
        }
        private static double OptionalNumber(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => string.IsNullOrEmpty(Value(row, key)) ? 0d : Number(row, key, errors, source);
        private static bool Boolean(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key); if (bool.TryParse(raw, out var value)) return value;
            errors.Add($"{source} has invalid boolean {key}: {raw}"); return false;
        }
    }
}
