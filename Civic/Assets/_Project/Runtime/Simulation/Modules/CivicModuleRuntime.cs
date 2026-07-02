using System;
using System.Collections.Generic;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public sealed class CivicModuleRuntime
    {
        private readonly Dictionary<string, ICivicGameplayModule> modules = new Dictionary<string, ICivicGameplayModule>(StringComparer.Ordinal);
        private readonly CivicModuleContext context;

        public CivicModuleRuntime(
            CivicGameSimulation simulation,
            CivicFeatureResolution features,
            ICivicMetaProgressStore metaStore = null,
            CivicModuleContent content = null,
            CivicCivilizationContent civilizationContent = null,
            CivicPoliticsContent politicsContent = null,
            CivicNationContent nationContent = null,
            CivicWonderContent wonderContent = null,
            CivicPeopleContent peopleContent = null,
            CivicEventContent eventContent = null,
            CivicModuleBalanceContent balanceContent = null,
            IReadOnlyList<CivicAchievementRewardDefinition> achievementRewards = null)
        {
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            Features = features ?? throw new ArgumentNullException(nameof(features));
            if (!features.IsValid)
            {
                throw new ArgumentException("Feature resolution must be valid.", nameof(features));
            }

            metaStore = metaStore ?? CivicMetaSession.Store;
            MetaProgress = metaStore.Load() ?? new CivicMetaProgress();
            Telemetry = new CivicRunTelemetry(simulation);
            if (features.EnabledFeatureIds.Count > 0)
            {
                var balances = balanceContent ?? CivicModuleBalanceContentLoader.LoadFromResources();
                balances.ValidateAgainst(simulation.Data);
                simulation.Modifiers.ConfigureCaps(balances.ModifierCaps);
                Telemetry.SetTechnologyAliases(balances.TechnologyAliases);
            }
            if (features.IsEnabled(CivicFeatureRegistry.StartCivilizations))
            {
                var civilizations = civilizationContent ?? CivicCivilizationContentLoader.LoadFromResources();
                modules.Add(
                    CivicFeatureRegistry.StartCivilizations,
                    new CivicCivilizationModule(civilizations, CivicRunLaunchSettings.StartingCivilizationId));
            }

            // Politics publishes institution metrics consumed by nations, achievements,
            // wonders, and people. Keep it ahead of those dependent modules.
            if (features.IsEnabled(CivicFeatureRegistry.Politics))
            {
                var politics = politicsContent ?? CivicPoliticsContentLoader.LoadFromResources();
                modules.Add(CivicFeatureRegistry.Politics, new CivicPoliticsModule(politics));
            }

            if (features.IsEnabled(CivicFeatureRegistry.NationFormation))
            {
                var nations = nationContent ?? CivicNationContentLoader.LoadFromResources();
                modules.Add(CivicFeatureRegistry.NationFormation, new CivicNationModule(nations));
            }

            if (features.IsEnabled(CivicFeatureRegistry.Wonders))
            {
                var wonders = wonderContent ?? CivicWonderContentLoader.LoadFromResources();
                modules.Add(CivicFeatureRegistry.Wonders, new CivicWonderModule(wonders));
            }

            if (features.IsEnabled(CivicFeatureRegistry.GreatPeople))
            {
                var people = peopleContent ?? CivicPeopleContentLoader.LoadFromResources();
                modules.Add(CivicFeatureRegistry.GreatPeople, new CivicPeopleModule(people));
            }

            if (features.IsEnabled(CivicFeatureRegistry.Events))
            {
                var events = eventContent ?? CivicEventContentLoader.LoadFromResources();
                modules.Add(CivicFeatureRegistry.Events, new CivicEventModule(events, CivicRunLaunchSettings.RunSeed));
            }

            if (features.IsEnabled(CivicFeatureRegistry.Prestige))
            {
                var perks = content?.LegacyPerks ?? CivicModuleContentLoader.LoadLegacyPerksFromResources();
                modules.Add(CivicFeatureRegistry.Prestige, new CivicPrestigeModule(perks));
            }

            if (features.IsEnabled(CivicFeatureRegistry.Achievements))
            {
                var achievements = content?.Achievements ?? CivicModuleContentLoader.LoadAchievementsFromResources();
                achievementRewards = achievementRewards ?? CivicAchievementRewardContentLoader.LoadFromResources(achievements);
                modules.Add(CivicFeatureRegistry.Achievements, new CivicAchievementModule(achievements, achievementRewards));
            }

            context = new CivicModuleContext(simulation, features, MetaProgress, Telemetry, modules);
            foreach (var module in modules.Values)
            {
                module.Initialize(context);
            }

            Telemetry.Observe(0d);
            foreach (var module in modules.Values)
            {
                module.AfterAdvance(0d);
            }
        }

        public CivicGameSimulation Simulation { get; }
        public CivicFeatureResolution Features { get; }
        public CivicMetaProgress MetaProgress { get; }
        public CivicRunTelemetry Telemetry { get; }
        public IReadOnlyDictionary<string, ICivicGameplayModule> Modules => modules;

        public CivicGameSnapshot Advance(double seconds)
        {
            var eventModule = GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            if (eventModule?.IsSimulationPaused == true)
            {
                return Simulation.Snapshot;
            }

            foreach (var module in modules.Values)
            {
                module.BeforeAdvance(seconds);
            }

            var snapshot = Simulation.Advance(seconds);
            Telemetry.Observe(seconds);
            foreach (var module in modules.Values)
            {
                module.AfterAdvance(seconds);
            }

            return snapshot;
        }

        public bool TryBuild(string buildingId)
        {
            if (!Simulation.TryBuild(buildingId))
            {
                return false;
            }

            Telemetry.RecordBuilding(buildingId);
            Telemetry.Observe(0d);
            foreach (var module in modules.Values)
            {
                module.OnBuildingConstructed(buildingId);
            }

            return true;
        }

        public bool TryResearch(string technologyId)
        {
            if (!Simulation.TryResearch(technologyId))
            {
                return false;
            }

            Telemetry.RecordTechnology(technologyId);
            Telemetry.Observe(0d);
            foreach (var module in modules.Values)
            {
                module.OnTechnologyResearched(technologyId);
            }

            return true;
        }

        public T GetModule<T>(string featureId) where T : class, ICivicGameplayModule
        {
            return modules.TryGetValue(featureId, out var module) ? module as T : null;
        }
    }
}
