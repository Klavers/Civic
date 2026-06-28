using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
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
        public CivicInstitutionEffectDefinition(string institutionId, string effectType, string targetId, double amount, string costType, string capGroup)
        {
            InstitutionId = institutionId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CostType = costType;
            CapGroup = capGroup;
        }

        public string InstitutionId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CostType { get; }
        public string CapGroup { get; }
    }

    public sealed class CivicPoliticsContent
    {
        public CivicPoliticsContent(IReadOnlyList<CivicInstitutionDefinition> institutions, IReadOnlyList<CivicInstitutionUnlockDefinition> unlocks, IReadOnlyList<CivicInstitutionEffectDefinition> effects)
        {
            Institutions = institutions;
            Unlocks = unlocks;
            Effects = effects;
        }

        public IReadOnlyList<CivicInstitutionDefinition> Institutions { get; }
        public IReadOnlyList<CivicInstitutionUnlockDefinition> Unlocks { get; }
        public IReadOnlyList<CivicInstitutionEffectDefinition> Effects { get; }
    }

    public static class CivicPoliticsContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicPoliticsContent LoadFromResources()
        {
            var institutions = Resources.Load<TextAsset>(Root + "institutions");
            var unlocks = Resources.Load<TextAsset>(Root + "institution_unlocks");
            var effects = Resources.Load<TextAsset>(Root + "institution_effects");
            if (institutions == null || unlocks == null || effects == null)
            {
                throw new CivicDataException(new[] { "Politics CSV resources are missing." });
            }
            return Parse(institutions.text, unlocks.text, effects.text);
        }

        public static CivicPoliticsContent Parse(string institutionsCsv, string unlocksCsv, string effectsCsv)
        {
            var errors = new List<string>();
            var institutions = CivicCsvParser.Parse(institutionsCsv, errors, "institutions.csv").Select(row => new CivicInstitutionDefinition(
                Value(row, "id"), Value(row, "category"), Value(row, "displayNameKo"), Value(row, "descriptionKo"), Integer(row, "order", errors, "institutions.csv"),
                Boolean(row, "isDefault", errors, "institutions.csv"), Number(row, "politicalCost", errors, "institutions.csv"), Number(row, "treasuryCost", errors, "institutions.csv"),
                Number(row, "reformSeconds", errors, "institutions.csv"), Number(row, "fatigueSeconds", errors, "institutions.csv"))).ToArray();
            var unlocks = CivicCsvParser.Parse(unlocksCsv, errors, "institution_unlocks.csv").Select(row => new CivicInstitutionUnlockDefinition(
                Value(row, "institutionId"), Value(row, "metricId"), Value(row, "comparator"), Number(row, "value", errors, "institution_unlocks.csv"), Value(row, "alternativeGroup"))).ToArray();
            var effects = CivicCsvParser.Parse(effectsCsv, errors, "institution_effects.csv").Select(row => new CivicInstitutionEffectDefinition(
                Value(row, "institutionId"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "institution_effects.csv"), Value(row, "costType"), Value(row, "capGroup"))).ToArray();
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
            return new CivicPoliticsContent(institutions, unlocks, effects);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => (int)Number(row, key, errors, source);
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key); if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}"); return 0d;
        }
        private static bool Boolean(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key); if (bool.TryParse(raw, out var value)) return value;
            errors.Add($"{source} has invalid boolean {key}: {raw}"); return false;
        }
    }
}
