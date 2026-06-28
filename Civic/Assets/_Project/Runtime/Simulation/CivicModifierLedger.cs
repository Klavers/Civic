using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Simulation
{
    public static class CivicModifierEffectTypes
    {
        public const string ResourceOutputMultiplier = "resourceOutputMultiplier";
        public const string ResourceOutputAdd = "resourceOutputAdd";
        public const string ResourceInputMultiplier = "resourceInputMultiplier";
        public const string BuildingOutputMultiplier = "buildingOutputMultiplier";
        public const string BuildingOutputAdd = "buildingOutputAdd";
        public const string BuildingInputMultiplier = "buildingInputMultiplier";
        public const string BuildingInputAdd = "buildingInputAdd";
        public const string ConstructionCostMultiplier = "constructionCostMultiplier";
        public const string ConstructionCostAdd = "constructionCostAdd";
        public const string PopulationUseAdd = "populationUseAdd";
        public const string PopulationBaseAdd = "populationBaseAdd";
        public const string PopulationScienceMultiplier = "populationScienceMultiplier";
        public const string TechnologyCostMultiplier = "technologyCostMultiplier";
        public const string TreasuryIncomeMultiplier = "treasuryIncomeMultiplier";
        public const string TaxRateAdd = "taxRateAdd";
        public const string PopulationConsumptionMultiplier = "populationConsumptionMultiplier";
        public const string ResourcePriceFloorAdd = "resourcePriceFloorAdd";
        public const string ResourceGdpMultiplier = "resourceGdpMultiplier";
        public const string FoodConversionMultiplier = "foodConversionMultiplier";
        public const string ConstructionTreasuryCostMultiplier = "constructionTreasuryCostMultiplier";
    }

    public sealed class CivicModifierEntry
    {
        public CivicModifierEntry(string sourceType, string sourceId, string effectType, string targetId, double amount, string capGroup = "")
        {
            SourceType = Require(sourceType, nameof(sourceType));
            SourceId = Require(sourceId, nameof(sourceId));
            EffectType = Require(effectType, nameof(effectType));
            TargetId = string.IsNullOrWhiteSpace(targetId) ? "*" : targetId;
            Amount = amount;
            CapGroup = capGroup ?? string.Empty;
        }

        public string SourceType { get; }
        public string SourceId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CapGroup { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }

            return value;
        }
    }

    public sealed class CivicModifierLedger
    {
        private readonly List<CivicModifierEntry> entries = new List<CivicModifierEntry>();

        public IReadOnlyList<CivicModifierEntry> Entries => entries;

        public void Add(CivicModifierEntry entry)
        {
            entries.Add(entry ?? throw new ArgumentNullException(nameof(entry)));
        }

        public void ReplaceSource(string sourceType, string sourceId, IEnumerable<CivicModifierEntry> replacements)
        {
            RemoveSource(sourceType, sourceId);
            foreach (var replacement in replacements ?? Array.Empty<CivicModifierEntry>())
            {
                if (replacement.SourceType != sourceType || replacement.SourceId != sourceId)
                {
                    throw new ArgumentException("Replacement modifier source does not match the replaced source.", nameof(replacements));
                }

                entries.Add(replacement);
            }
        }

        public void RemoveSource(string sourceType, string sourceId)
        {
            entries.RemoveAll(entry => entry.SourceType == sourceType && entry.SourceId == sourceId);
        }

        public double Additive(string effectType, params string[] targetIds)
        {
            var targets = new HashSet<string>(targetIds ?? Array.Empty<string>(), StringComparer.Ordinal) { "*" };
            return entries
                .Where(entry => entry.EffectType == effectType && targets.Contains(entry.TargetId))
                .Sum(entry => entry.Amount);
        }

        public double Multiplier(string effectType, params string[] targetIds)
        {
            return Math.Max(0d, 1d + Additive(effectType, targetIds));
        }
    }
}
