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
        public const string WonderCostMultiplier = "wonderCostMultiplier";
        public const string WonderProgressMultiplier = "wonderProgressMultiplier";
        public const string EventWeightMultiplier = "eventWeightMultiplier";
        public const string EventCooldownMultiplier = "eventCooldownMultiplier";
        public const string PrestigeGainMultiplier = "prestigeGainMultiplier";
        public const string PoliticalCapitalMultiplier = "politicalCapitalMultiplier";
        public const string ReformCostMultiplier = "reformCostMultiplier";
        public const string ReformSpeedMultiplier = "reformSpeedMultiplier";
        public const string ReformResistanceAdd = "reformResistanceAdd";
        public const string LegitimacyAdd = "legitimacyAdd";
        public const string LivingStandardAdd = "livingStandardAdd";
        public const string PersonCandidateWeightMultiplier = "personCandidateWeightMultiplier";
        public const string PersonLegacyMultiplier = "personLegacyMultiplier";
        public const string NationPreparationSpeedMultiplier = "nationPreparationSpeedMultiplier";
        public const string NationConditionDurationMultiplier = "nationConditionDurationMultiplier";
        public const string ProvisionalFlag = "provisionalFlag";
    }

    public sealed class CivicModifierCapDefinition
    {
        public CivicModifierCapDefinition(string id, double minimum, double maximum)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Cap ID cannot be empty.", nameof(id));
            if (minimum > maximum) throw new ArgumentException("Cap minimum cannot exceed maximum.", nameof(minimum));
            Id = id;
            Minimum = minimum;
            Maximum = maximum;
        }

        public string Id { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public double Clamp(double value) => Math.Max(Minimum, Math.Min(Maximum, value));
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
        private readonly Dictionary<string, CivicModifierCapDefinition> caps = new Dictionary<string, CivicModifierCapDefinition>(StringComparer.Ordinal);

        public IReadOnlyList<CivicModifierEntry> Entries => entries;
        public IReadOnlyDictionary<string, CivicModifierCapDefinition> Caps => caps;

        public void ConfigureCaps(IEnumerable<CivicModifierCapDefinition> definitions)
        {
            caps.Clear();
            foreach (var definition in definitions ?? Array.Empty<CivicModifierCapDefinition>())
            {
                if (caps.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate modifier cap: " + definition.Id, nameof(definitions));
                caps.Add(definition.Id, definition);
            }
        }

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
            var matched = entries.Where(entry => entry.EffectType == effectType && targets.Contains(entry.TargetId)).ToArray();
            var total = matched.Where(entry => string.IsNullOrEmpty(entry.CapGroup)).Sum(entry => entry.Amount);
            foreach (var group in matched.Where(entry => !string.IsNullOrEmpty(entry.CapGroup)).GroupBy(entry => entry.CapGroup, StringComparer.Ordinal))
            {
                var sum = group.Sum(entry => entry.Amount);
                total += caps.TryGetValue(group.Key, out var cap) ? cap.Clamp(sum) : sum;
            }

            return total;
        }

        public double Multiplier(string effectType, params string[] targetIds)
        {
            return Math.Max(0d, 1d + Additive(effectType, targetIds));
        }
    }
}
