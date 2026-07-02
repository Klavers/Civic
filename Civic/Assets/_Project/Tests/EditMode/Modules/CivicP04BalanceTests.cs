using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;
using NUnit.Framework;
using UnityEditor;

namespace Civic.Simulation.Modules.Tests
{
    public sealed class CivicP04BalanceTests
    {
        private CivicGameData gameData;

        [SetUp]
        public void SetUp()
        {
            CivicMetaSession.ResetForTests();
            CivicRunLaunchSettings.Reset();
            gameData = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath).LoadGameData();
        }

        [Test]
        public void ProvisionalEffects_All149RowsHaveValueDurationCapAndRuntimeMapping()
        {
            var balances = CivicModuleBalanceContentLoader.LoadFromResources();
            var capIds = balances.ModifierCaps.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var effects = new List<CivicResolvedModuleEffect>();
            effects.AddRange(CivicCivilizationContentLoader.LoadFromResources().Effects.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicPoliticsContentLoader.LoadFromResources().Effects.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicNationContentLoader.LoadFromResources().Effects.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicEventContentLoader.LoadFromResources().Effects.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicWonderContentLoader.LoadFromResources().Effects.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicPeopleContentLoader.LoadFromResources().Traits.Where(IsPlanned).Select(item => item.Resolve()));
            effects.AddRange(CivicPeopleContentLoader.LoadFromResources().Abilities.Where(IsPlanned).Select(item => item.Resolve()));

            Assert.That(effects.Count, Is.EqualTo(149));
            Assert.That(effects.All(item => Math.Abs(item.Amount) > 1e-12d), Is.True);
            Assert.That(effects.All(item => item.Duration >= 0d), Is.True);
            Assert.That(effects.All(item => !string.IsNullOrWhiteSpace(item.EffectType) && !string.IsNullOrWhiteSpace(item.TargetId)), Is.True);
            Assert.That(effects.All(item => capIds.Contains(item.CapGroup)), Is.True);
        }

        [Test]
        public void LegacyPerks_HaveMonotonicRankCostsAndCanBePurchased()
        {
            var content = CivicModuleContentLoader.LoadFromResources();
            Assert.That(content.LegacyPerks.All(perk => perk.RankCosts.Count == perk.MaxRank), Is.True);
            Assert.That(content.LegacyPerks.All(perk => perk.RankCosts.Zip(perk.RankCosts.Skip(1), (left, right) => right >= left).All(value => value)), Is.True);

            var progress = new CivicMetaProgress { PrestigePoints = 10 };
            var runtime = new CivicModuleRuntime(
                new CivicGameSimulation(gameData),
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Prestige }),
                new CivicInMemoryMetaProgressStore(progress),
                content);
            var prestige = runtime.GetModule<CivicPrestigeModule>(CivicFeatureRegistry.Prestige);

            Assert.That(prestige.TryPurchaseLegacyPerk("pioneer_construction_team", out var cost, out _), Is.True);
            Assert.That(cost, Is.EqualTo(5));
            Assert.That(progress.PrestigePoints, Is.EqualTo(5));
            Assert.That(progress.GetPerkRank("pioneer_construction_team"), Is.EqualTo(1));
        }

        [Test]
        public void ModifierLedger_ClampsCombinedCapGroup()
        {
            var ledger = new CivicModifierLedger();
            ledger.ConfigureCaps(new[] { new CivicModifierCapDefinition("test", -0.25d, 0.50d) });
            ledger.Add(new CivicModifierEntry("a", "1", CivicModifierEffectTypes.ResourceOutputMultiplier, "wheat", 0.4d, "test"));
            ledger.Add(new CivicModifierEntry("b", "2", CivicModifierEffectTypes.ResourceOutputMultiplier, "wheat", 0.4d, "test"));

            Assert.That(ledger.Additive(CivicModifierEffectTypes.ResourceOutputMultiplier, "wheat"), Is.EqualTo(0.5d).Within(1e-9d));
        }

        [Test]
        public void TechnologyAliasAndLivingStandard_UseTemporaryP04Mappings()
        {
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(simulation, CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Politics }));
            simulation.State.ResearchedTechnologyIds.Add("writing");
            runtime.Telemetry.RecordTechnology("writing");
            runtime.Advance(1d);

            Assert.That(runtime.Telemetry.GetMetric("technology.researched.royal_authority", runtime.MetaProgress), Is.EqualTo(1d));
            var politics = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);
            Assert.That(politics.LivingStandard, Is.InRange(0d, 100d));
            Assert.That(runtime.Telemetry.GetMetric("snapshot.livingStandard", runtime.MetaProgress), Is.EqualTo(politics.LivingStandard).Within(1e-9d));
        }

        [Test]
        public void AchievementPermanentRewards_ApplyForCompletedMetaOnlyWhenModuleEnabled()
        {
            var content = CivicModuleContentLoader.LoadFromResources();
            var rewards = CivicAchievementRewardContentLoader.LoadFromResources(content.Achievements);
            Assert.That(rewards.Count, Is.EqualTo(10));
            var progress = new CivicMetaProgress();
            progress.CompletedAchievementIds.Add("heart_of_industry");

            var enabledSimulation = new CivicGameSimulation(gameData);
            new CivicModuleRuntime(
                enabledSimulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Achievements }),
                new CivicInMemoryMetaProgressStore(progress),
                content,
                achievementRewards: rewards);
            Assert.That(enabledSimulation.Modifiers.Additive(CivicModifierEffectTypes.ResourceGdpMultiplier, "wheat"), Is.EqualTo(0.02d).Within(1e-9d));

            var disabledSimulation = new CivicGameSimulation(gameData);
            new CivicModuleRuntime(disabledSimulation, CivicFeatureResolver.Resolve(Array.Empty<string>()), new CivicInMemoryMetaProgressStore(progress));
            Assert.That(disabledSimulation.Modifiers.Additive(CivicModifierEffectTypes.ResourceGdpMultiplier, "wheat"), Is.Zero);
        }

        [Test]
        public void Politics_HasExactlyOneActiveInstitutionPerCategoryAndMultipleCategoriesAtOnce()
        {
            var runtime = new CivicModuleRuntime(
                new CivicGameSimulation(gameData),
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Politics }));
            var politics = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);
            var categories = politics.Definitions.Select(item => item.Category).Distinct(StringComparer.Ordinal).ToArray();

            Assert.That(categories.Length, Is.EqualTo(5));
            Assert.That(politics.ActiveByCategory.Count, Is.EqualTo(categories.Length));
            Assert.That(categories.All(category => politics.Definitions.Count(item => item.Category == category && politics.ActiveByCategory[category] == item.Id) == 1), Is.True);
            Assert.That(politics.ActiveByCategory.Values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(categories.Length));
        }

        private static bool IsPlanned(CivicCivilizationEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicInstitutionEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicNationEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicEventEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicWonderEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicPersonEffectDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
        private static bool IsPlanned(CivicPersonAbilityDefinition item) => item.EffectType == CivicProvisionalEffect.Planned;
    }
}
