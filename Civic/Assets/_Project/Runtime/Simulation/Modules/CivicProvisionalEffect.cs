using System;
using System.Collections.Generic;

namespace Civic.Simulation.Modules
{
    public readonly struct CivicResolvedModuleEffect
    {
        public CivicResolvedModuleEffect(
            string designEffectType,
            string designTargetId,
            string runtimeEffectType,
            string runtimeTargetId,
            double amount,
            double duration,
            string capGroup)
        {
            DesignEffectType = designEffectType;
            DesignTargetId = designTargetId;
            EffectType = designEffectType == CivicProvisionalEffect.Planned ? runtimeEffectType : designEffectType;
            TargetId = designEffectType == CivicProvisionalEffect.Planned ? runtimeTargetId : designTargetId;
            Amount = amount;
            Duration = duration;
            CapGroup = capGroup;
            IsProvisional = designEffectType == CivicProvisionalEffect.Planned;
        }

        public string DesignEffectType { get; }
        public string DesignTargetId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public double Duration { get; }
        public string CapGroup { get; }
        public bool IsProvisional { get; }
    }

    public static class CivicProvisionalEffect
    {
        public const string Planned = "planned";
        private static readonly ISet<string> SupportedRuntimeEffectTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            CivicModifierEffectTypes.ResourceOutputMultiplier,
            CivicModifierEffectTypes.ResourceOutputAdd,
            CivicModifierEffectTypes.ResourceInputMultiplier,
            CivicModifierEffectTypes.BuildingOutputMultiplier,
            CivicModifierEffectTypes.BuildingOutputAdd,
            CivicModifierEffectTypes.BuildingInputMultiplier,
            CivicModifierEffectTypes.BuildingInputAdd,
            CivicModifierEffectTypes.ConstructionCostMultiplier,
            CivicModifierEffectTypes.ConstructionCostAdd,
            CivicModifierEffectTypes.PopulationUseAdd,
            CivicModifierEffectTypes.PopulationBaseAdd,
            CivicModifierEffectTypes.PopulationScienceMultiplier,
            CivicModifierEffectTypes.TechnologyCostMultiplier,
            CivicModifierEffectTypes.TreasuryIncomeMultiplier,
            CivicModifierEffectTypes.TaxRateAdd,
            CivicModifierEffectTypes.PopulationConsumptionMultiplier,
            CivicModifierEffectTypes.ResourcePriceFloorAdd,
            CivicModifierEffectTypes.ResourceGdpMultiplier,
            CivicModifierEffectTypes.FoodConversionMultiplier,
            CivicModifierEffectTypes.ConstructionTreasuryCostMultiplier,
            CivicModifierEffectTypes.WonderCostMultiplier,
            CivicModifierEffectTypes.WonderProgressMultiplier,
            CivicModifierEffectTypes.EventWeightMultiplier,
            CivicModifierEffectTypes.EventCooldownMultiplier,
            CivicModifierEffectTypes.PrestigeGainMultiplier,
            CivicModifierEffectTypes.PoliticalCapitalMultiplier,
            CivicModifierEffectTypes.ReformCostMultiplier,
            CivicModifierEffectTypes.ReformSpeedMultiplier,
            CivicModifierEffectTypes.ReformResistanceAdd,
            CivicModifierEffectTypes.LegitimacyAdd,
            CivicModifierEffectTypes.LivingStandardAdd,
            CivicModifierEffectTypes.PersonCandidateWeightMultiplier,
            CivicModifierEffectTypes.PersonLegacyMultiplier,
            CivicModifierEffectTypes.NationPreparationSpeedMultiplier,
            CivicModifierEffectTypes.NationConditionDurationMultiplier,
            CivicModifierEffectTypes.ProvisionalFlag,
            "resourceGrant", "prestigeGrant", "politicalCapitalGrant", "reformProgressAdd", "wonderProgressAdd", "flagSet"
        };

        public static CivicResolvedModuleEffect Resolve(
            string designEffectType,
            string designTargetId,
            string runtimeEffectType,
            string runtimeTargetId,
            double amount,
            double duration,
            string capGroup)
        {
            return new CivicResolvedModuleEffect(
                designEffectType,
                designTargetId,
                runtimeEffectType,
                runtimeTargetId,
                amount,
                duration,
                capGroup);
        }

        public static void Validate(
            string source,
            string designEffectType,
            string designTargetId,
            string runtimeEffectType,
            string runtimeTargetId,
            double amount,
            double duration,
            string capGroup,
            IReadOnlyDictionary<string, CivicModifierCapDefinition> caps,
            ICollection<string> errors)
        {
            if (duration < 0d) errors.Add(source + " has negative duration: " + designTargetId);
            if (designEffectType != Planned) return;
            if (Math.Abs(amount) <= 1e-12d) errors.Add(source + " planned effect has zero amount: " + designTargetId);
            if (string.IsNullOrWhiteSpace(runtimeEffectType) || string.IsNullOrWhiteSpace(runtimeTargetId)) errors.Add(source + " planned effect lacks runtime mapping: " + designTargetId);
            else if (!SupportedRuntimeEffectTypes.Contains(runtimeEffectType)) errors.Add(source + " planned effect has unsupported runtime mapping: " + designTargetId + "/" + runtimeEffectType);
            if (string.IsNullOrWhiteSpace(capGroup)) errors.Add(source + " planned effect lacks cap group: " + designTargetId);
            else if (caps != null && !caps.ContainsKey(capGroup)) errors.Add(source + " planned effect has unknown cap group: " + designTargetId + "/" + capGroup);
        }
    }
}
