using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Simulation
{
    public static class CivicEffectText
    {
        public static readonly IReadOnlyList<string> RequiredEffectTypes = new string[]
        {
            CivicModifierEffectTypes.ResourceOutputMultiplier, CivicModifierEffectTypes.ResourceOutputAdd,
            CivicModifierEffectTypes.ResourceInputMultiplier, CivicModifierEffectTypes.BuildingOutputMultiplier,
            CivicModifierEffectTypes.BuildingOutputAdd, CivicModifierEffectTypes.BuildingInputMultiplier,
            CivicModifierEffectTypes.BuildingInputAdd, CivicModifierEffectTypes.ConstructionCostMultiplier,
            CivicModifierEffectTypes.ConstructionCostAdd, CivicModifierEffectTypes.PopulationUseAdd,
            CivicModifierEffectTypes.PopulationBaseAdd, CivicModifierEffectTypes.PopulationScienceMultiplier,
            CivicModifierEffectTypes.TechnologyCostMultiplier, CivicModifierEffectTypes.TreasuryIncomeMultiplier,
            CivicModifierEffectTypes.TaxRateAdd, CivicModifierEffectTypes.PopulationConsumptionMultiplier,
            CivicModifierEffectTypes.ResourcePriceFloorAdd, CivicModifierEffectTypes.ResourceGdpMultiplier,
            CivicModifierEffectTypes.FoodConversionMultiplier, CivicModifierEffectTypes.ConstructionTreasuryCostMultiplier,
            CivicModifierEffectTypes.WonderCostMultiplier, CivicModifierEffectTypes.WonderProgressMultiplier,
            CivicModifierEffectTypes.EventWeightMultiplier, CivicModifierEffectTypes.EventCooldownMultiplier,
            CivicModifierEffectTypes.PrestigeGainMultiplier, CivicModifierEffectTypes.PoliticalCapitalMultiplier,
            CivicModifierEffectTypes.ReformCostMultiplier, CivicModifierEffectTypes.ReformSpeedMultiplier,
            CivicModifierEffectTypes.ReformResistanceAdd, CivicModifierEffectTypes.LegitimacyAdd,
            CivicModifierEffectTypes.LivingStandardAdd, CivicModifierEffectTypes.PersonCandidateWeightMultiplier,
            CivicModifierEffectTypes.PersonLegacyMultiplier, CivicModifierEffectTypes.NationPreparationSpeedMultiplier,
            CivicModifierEffectTypes.NationConditionDurationMultiplier, CivicModifierEffectTypes.ProvisionalFlag,
            "resourceGrant", "prestigeGrant", "politicalCapitalGrant", "reformProgressAdd", "wonderProgressAdd", "flagSet",
            "outputAdd", "conditionalOutputAdd", "taxRateAdd", "plannedFollowUp",
            "startingResourceAdd", "earlyEraConstructionCostMultiplier", "startingUnlockedResourceAdd",
            "timedPopulationConsumptionMultiplier", "housingSupplyBuffer", "previousEraTechnologyCostMultiplier",
            "firstEraResearchRefund", "startingTaxRateAdd", "firstWonderCostMultiplier", "freePersonReroll",
            "challengePrestigeMultiplier",
        };

        public static string Describe(string effectType, string targetId, double amount, double duration = 0d, CivicGameData data = null, bool richText = false)
        {
            var catalog = CivicLocalizationService.Catalog;
            var key = "effect." + (effectType ?? string.Empty);
            if (!catalog.Contains(key)) return effectType ?? string.Empty;
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["VALUE"] = amount,
                ["TARGET"] = ResolveTarget(data, targetId),
                ["DURATION"] = duration,
            };
            var result = catalog.Resolve(key, arguments, richText);
            if (duration > 0d)
            {
                result += catalog.Resolve("effect.duration", arguments, richText);
            }
            return result;
        }

        public static string DescribeTechnologyEffect(TechnologyEffectDefinition effect, CivicGameData data, bool richText = false)
        {
            if (effect == null) return string.Empty;
            var catalog = CivicLocalizationService.Catalog;
            var effectType = TechnologyEffectId(effect.EffectType);
            var key = "effect." + effectType;
            if (!catalog.Contains(key)) return effectType;
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["VALUE"] = effect.Amount,
                ["TARGET"] = ResolveTarget(data, effect.TargetBuildingId),
                ["BUILDING"] = ResolveTarget(data, effect.TargetBuildingId),
                ["INPUT"] = ResolveTarget(data, effect.InputResourceId),
                ["OUTPUT"] = ResolveTarget(data, effect.OutputResourceId),
                ["DURATION"] = 0d,
            };
            return catalog.Resolve(key, arguments, richText);
        }

        public static string TechnologyEffectId(TechnologyEffectType effectType)
        {
            return effectType switch
            {
                TechnologyEffectType.OutputAdd => "outputAdd",
                TechnologyEffectType.ConditionalOutputAdd => "conditionalOutputAdd",
                TechnologyEffectType.TaxRateAdd => "taxRateAdd",
                TechnologyEffectType.PlannedFollowUp => "plannedFollowUp",
                _ => effectType.ToString(),
            };
        }

        public static string ResolveTarget(CivicGameData data, string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == "*") return "전체";
            if (data != null)
            {
                if (data.ResourcesById.TryGetValue(targetId, out var resource)) return resource.DisplayNameKo;
                if (data.BuildingsById.TryGetValue(targetId, out var building)) return building.DisplayNameKo;
                if (data.TechnologiesById.TryGetValue(targetId, out var technology)) return technology.DisplayNameKo;
                var era = data.Eras.FirstOrDefault(item => item.Id == targetId);
                if (era != null) return era.DisplayNameKo;
            }
            return targetId;
        }
    }
}
