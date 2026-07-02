using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public static class CivicRunLaunchSettings
    {
        public static string StartingCivilizationId { get; set; } = CivicCivilizationModule.DefaultCivilizationId;
        public static int RunSeed { get; set; } = 1;

        public static void Reset()
        {
            StartingCivilizationId = CivicCivilizationModule.DefaultCivilizationId;
            RunSeed = 1;
        }
    }

    public sealed class CivicCivilizationModule : CivicGameplayModuleBase
    {
        public const string DefaultCivilizationId = "default";

        private static readonly HashSet<string> DirectModifierEffects = new HashSet<string>(StringComparer.Ordinal)
        {
            CivicModifierEffectTypes.ResourceOutputMultiplier,
            CivicModifierEffectTypes.ResourceOutputAdd,
            CivicModifierEffectTypes.BuildingOutputMultiplier,
            CivicModifierEffectTypes.BuildingOutputAdd,
            CivicModifierEffectTypes.BuildingInputMultiplier,
            CivicModifierEffectTypes.ConstructionCostMultiplier,
            CivicModifierEffectTypes.ConstructionCostAdd,
            CivicModifierEffectTypes.PopulationUseAdd,
            CivicModifierEffectTypes.PopulationScienceMultiplier,
            CivicModifierEffectTypes.TechnologyCostMultiplier,
            CivicModifierEffectTypes.TreasuryIncomeMultiplier,
            CivicModifierEffectTypes.TaxRateAdd,
            CivicModifierEffectTypes.PopulationConsumptionMultiplier,
            CivicModifierEffectTypes.ResourcePriceFloorAdd,
            CivicModifierEffectTypes.ResourceGdpMultiplier,
            CivicModifierEffectTypes.FoodConversionMultiplier,
            CivicModifierEffectTypes.ConstructionTreasuryCostMultiplier
        };

        private readonly CivicCivilizationContent content;
        private readonly string requestedCivilizationId;
        private readonly List<CivicCivilizationEffectDefinition> inactiveEffects = new List<CivicCivilizationEffectDefinition>();

        public CivicCivilizationModule(CivicCivilizationContent content, string requestedCivilizationId)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.requestedCivilizationId = string.IsNullOrWhiteSpace(requestedCivilizationId) ? DefaultCivilizationId : requestedCivilizationId;
        }

        public override string FeatureId => CivicFeatureRegistry.StartCivilizations;
        public CivicCivilizationDefinition ActiveCivilization { get; private set; }
        public IReadOnlyList<CivicCivilizationDefinition> Definitions => content.Civilizations;
        public IReadOnlyList<CivicCivilizationEffectDefinition> InactiveEffects => inactiveEffects;
        public int ProvisionalEffectCount => ActiveCivilization == null ? 0 : content.Effects.Count(item => item.CivilizationId == ActiveCivilization.Id && item.EffectType == CivicProvisionalEffect.Planned);
        public IReadOnlyList<CivicCivilizationEffectDefinition> EffectsFor(string civilizationId) => content.Effects.Where(item => item.CivilizationId == civilizationId).ToArray();
        public IReadOnlyList<CivicCivilizationStartDefinition> StartsFor(string civilizationId) => content.Starts.Where(item => item.CivilizationId == civilizationId).ToArray();

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            ActiveCivilization = content.Civilizations.FirstOrDefault(item => item.Id == requestedCivilizationId)
                ?? throw new CivicDataException(new[] { $"Unknown starting civilization: {requestedCivilizationId}" });
            var missingFeature = ActiveCivilization.RequiredFeatureIds.FirstOrDefault(id => !context.IsFeatureEnabled(id));
            if (missingFeature != null)
            {
                throw new CivicDataException(new[] { $"{ActiveCivilization.Id} requires enabled feature: {missingFeature}" });
            }

            ApplyEffects();
            ApplyStartingState();
            context.Telemetry.SetExternalMetric("identity.startingCivilization." + ActiveCivilization.Id, 1d);
            context.Simulation.RefreshSnapshot();
        }

        private void ApplyEffects()
        {
            foreach (var effect in content.Effects.Where(item => item.CivilizationId == ActiveCivilization.Id))
            {
                var resolved = effect.Resolve();
                if (!resolved.IsProvisional && !DirectModifierEffects.Contains(resolved.EffectType))
                {
                    inactiveEffects.Add(effect);
                    continue;
                }

                Context.Modifiers.Add(new CivicModifierEntry(
                    "civilization",
                    ActiveCivilization.Id,
                    resolved.EffectType,
                    resolved.TargetId,
                    resolved.Amount,
                    resolved.CapGroup));
            }
        }

        private void ApplyStartingState()
        {
            foreach (var start in content.Starts.Where(item => item.CivilizationId == ActiveCivilization.Id))
            {
                switch (start.Kind)
                {
                    case "resource":
                        Context.Simulation.GrantResource(start.TargetId, CivicNumber.FromDouble(start.Amount));
                        break;
                    case "building":
                        Context.Simulation.GrantBuilding(start.TargetId, Math.Max(0, (int)Math.Floor(start.Amount)));
                        break;
                    case "technology":
                        if (start.Amount > 0d) Context.Simulation.GrantTechnology(start.TargetId);
                        break;
                    default:
                        throw new CivicDataException(new[] { $"Unsupported civilization start kind: {start.Kind}" });
                }
            }
        }
    }
}
