using System;
using System.Linq;
using System.IO;
using Civic.Features;
using NUnit.Framework;
using UnityEditor;

namespace Civic.Simulation.Modules.Tests
{
    public sealed class CivicMetaModulesTests
    {
        private CivicGameData gameData;

        [SetUp]
        public void SetUp()
        {
            CivicMetaSession.ResetForTests();
            CivicRunLaunchSettings.Reset();
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            Assert.That(dataSource, Is.Not.Null);
            gameData = dataSource.LoadGameData();
        }

        [Test]
        public void ModuleContent_LoadsAllProposedPrestigePerksAndAchievements()
        {
            var content = CivicModuleContentLoader.LoadFromResources();

            Assert.That(content.LegacyPerks.Count, Is.EqualTo(15));
            Assert.That(content.Achievements.Count, Is.EqualTo(15));
            Assert.That(content.LegacyPerks.Select(item => item.Id), Is.Unique);
            Assert.That(content.Achievements.Select(item => item.Id), Is.Unique);
        }

        [Test]
        public void JsonMetaProgressStore_RoundTripsAndCreatesBackupOnReplacement()
        {
            var directory = Path.Combine(Path.GetTempPath(), "CivicMetaTests", System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "meta.json");
            try
            {
                var store = new CivicJsonMetaProgressStore(path);
                var progress = new CivicMetaProgress { PrestigePoints = 7, PrestigeCount = 2 };
                progress.CompletedAchievementIds.Add("test_achievement");
                store.Save(progress);
                progress.PrestigePoints = 9;
                store.Save(progress);
                var loaded = store.Load();

                Assert.That(loaded.PrestigePoints, Is.EqualTo(9));
                Assert.That(loaded.PrestigeCount, Is.EqualTo(2));
                Assert.That(loaded.CompletedAchievementIds, Contains.Item("test_achievement"));
                Assert.That(File.Exists(path + ".bak"), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void JsonMetaProgressStore_DeleteRemovesPrimaryBackupAndTemporaryFiles()
        {
            var directory = Path.Combine(Path.GetTempPath(), "CivicMetaTests", System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "meta.json");
            try
            {
                var store = new CivicJsonMetaProgressStore(path);
                store.Save(new CivicMetaProgress { PrestigePoints = 1 });
                store.Save(new CivicMetaProgress { PrestigePoints = 2 });
                File.WriteAllText(path + ".tmp", "temporary");
                store.Delete();

                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(path + ".bak"), Is.False);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                Assert.That(store.Load().PrestigePoints, Is.Zero);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CivilizationContent_LoadsDefaultAndFifteenProposedCivilizations()
        {
            var content = CivicCivilizationContentLoader.LoadFromResources();

            Assert.That(content.Civilizations.Count, Is.EqualTo(16));
            Assert.That(content.Civilizations.Select(item => item.Id), Contains.Item(CivicCivilizationModule.DefaultCivilizationId));
            Assert.That(content.Civilizations.Select(item => item.Id), Contains.Item("arpadel"));
            Assert.That(content.Civilizations.Select(item => item.Id), Is.Unique);
        }

        [Test]
        public void NationContent_LoadsFifteenProposedNations()
        {
            var content = CivicNationContentLoader.LoadFromResources();

            Assert.That(content.Nations.Count, Is.EqualTo(15));
            Assert.That(content.Nations.Select(item => item.Id), Is.Unique);
            Assert.That(content.Nations.Select(item => item.Id), Contains.Item("arpadel_aqueduct_kingdom"));
            Assert.That(content.Nations.Select(item => item.Id), Contains.Item("secret_star_successor"));
        }

        [Test]
        public void WonderContent_LoadsFifteenProposedWonders()
        {
            var content = CivicWonderContentLoader.LoadFromResources();

            Assert.That(content.Wonders.Count, Is.EqualTo(15));
            Assert.That(content.Wonders.Select(item => item.Id), Is.Unique);
            Assert.That(content.Costs.All(item => item.Amount > 0d && item.DeliveryRate > 0d), Is.True);
        }

        [Test]
        public void PeopleContent_LoadsFifteenProposedPeople()
        {
            var content = CivicPeopleContentLoader.LoadFromResources();

            Assert.That(content.Positions.Count, Is.EqualTo(4));
            Assert.That(content.Positions.Select(item => item.Id), Is.EquivalentTo(new[] { "leader", "security", "science", "society_economy" }));
            Assert.That(content.People.Count, Is.EqualTo(15));
            Assert.That(content.People.Select(item => item.Id), Is.Unique);
            Assert.That(content.People.All(item => item.BaseTenure >= 600d && item.BaseTenure <= 1800d), Is.True);
            Assert.That(content.People.All(person => person.AllowedPositionIds.Count == 4), Is.True);
            Assert.That(content.People.All(person => person.AllowedPositionIds.All(positionId => content.Traits.Any(trait => trait.PersonId == person.Id && trait.PositionId == positionId))), Is.True);
        }

        [Test]
        public void PoliticsContent_LoadsFiveCategoriesAndFifteenProposedInstitutions()
        {
            var content = CivicPoliticsContentLoader.LoadFromResources();

            Assert.That(content.Institutions.Count, Is.EqualTo(15));
            Assert.That(content.Institutions.Select(item => item.Category).Distinct().Count(), Is.EqualTo(5));
            Assert.That(content.Institutions.GroupBy(item => item.Category).All(group => group.Count(item => item.IsDefault) == 1), Is.True);
            Assert.That(content.Institutions.Select(item => item.Id), Is.Unique);
        }

        [Test]
        public void EventContent_LoadsFifteenProposedEventsAndThreeChoicesEach()
        {
            var content = CivicEventContentLoader.LoadFromResources();

            Assert.That(content.Events.Count, Is.EqualTo(15));
            Assert.That(content.Choices.Count, Is.EqualTo(45));
            Assert.That(content.Events.Select(item => item.Id), Is.Unique);
            Assert.That(content.Choices.Select(item => item.Id), Is.Unique);
            Assert.That(content.Events.All(item => content.Choices.Count(choice => choice.EventId == item.Id) == 3), Is.True);
        }

        [Test]
        public void BaselineRuntime_HasNoModulesAndPreservesSimulationResult()
        {
            var direct = new CivicGameSimulation(gameData);
            var wrappedSimulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(wrappedSimulation, CivicFeatureResolver.Resolve(new string[0]));

            direct.Advance(2d);
            runtime.Advance(2d);

            Assert.That(runtime.Modules, Is.Empty);
            Assert.That(wrappedSimulation.Snapshot.Gdp, Is.EqualTo(direct.Snapshot.Gdp));
            Assert.That(wrappedSimulation.Snapshot.Population, Is.EqualTo(direct.Snapshot.Population));
            Assert.That(
                wrappedSimulation.Snapshot.Resources.Select(item => item.Stockpile),
                Is.EqualTo(direct.Snapshot.Resources.Select(item => item.Stockpile)));
        }

        [Test]
        public void FeatureMatrix_AllThirtyEightCasesConstructAndAdvanceWithExactModuleSet()
        {
            foreach (var testCase in CivicFeatureMatrix.CreateDefaultCases())
            {
                CivicMetaSession.ResetForTests();
                CivicRunLaunchSettings.Reset();
                var resolution = CivicFeatureResolver.Resolve(testCase.RequestedIds);
                var runtime = new CivicModuleRuntime(new CivicGameSimulation(gameData), resolution);

                Assert.That(runtime.Modules.Keys, Is.EquivalentTo(resolution.EnabledFeatureIds), testCase.Name);
                Assert.DoesNotThrow(() => runtime.Advance(1d), testCase.Name);
                Assert.That(runtime.Simulation.Snapshot.Resources.All(item => item.Stockpile >= CivicNumber.Zero), Is.True, testCase.Name);
            }
        }

        [Test]
        public void AllOnRuntime_AdvancesTenMinutesWithoutNegativeResourcesOrDuplicateExactModifiers()
        {
            var resolution = CivicFeatureResolver.Resolve(CivicFeatureRegistry.Features.Select(item => item.Id));
            var runtime = new CivicModuleRuntime(new CivicGameSimulation(gameData), resolution);

            Assert.DoesNotThrow(() => runtime.Advance(600d));
            Assert.That(runtime.Modules.Count, Is.EqualTo(8));
            Assert.That(runtime.Simulation.Snapshot.Resources.All(item => item.Stockpile >= CivicNumber.Zero), Is.True);
            Assert.That(runtime.Simulation.Modifiers.Entries
                .GroupBy(item => new { item.SourceType, item.SourceId, item.EffectType, item.TargetId })
                .All(group => group.Count() == 1), Is.True);
        }

        [Test]
        public void AchievementModule_CompletesMetricDrivenAchievementOnce()
        {
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Achievements }));
            simulation.State.BasePopulation = CivicNumber.FromDouble(100d);

            runtime.Advance(1d);
            runtime.Advance(1d);

            var module = runtime.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements);
            Assert.That(module, Is.Not.Null);
            Assert.That(module.CompletedThisRun, Contains.Item("thriving_settlement"));
            Assert.That(runtime.MetaProgress.CompletedAchievementIds.Count(id => id == "thriving_settlement"), Is.EqualTo(1));
            Assert.That(module.PrestigeRewardEarnedThisRun, Is.EqualTo(3));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "achievement" && item.SourceId == "thriving_settlement" && item.EffectType == CivicModifierEffectTypes.PopulationBaseAdd), Is.True);
        }

        [Test]
        public void AchievementModule_HidesCrossModuleAchievementWhenDependencyIsOff()
        {
            var runtime = new CivicModuleRuntime(
                new CivicGameSimulation(gameData),
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Achievements }));
            var module = runtime.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements);

            var crossModule = module.Snapshot.Single(item => item.Definition.Id == "age_of_reform");
            Assert.That(crossModule.State, Is.EqualTo(CivicAchievementState.Unavailable));
            Assert.That(crossModule.BlockingReason, Does.Contain(CivicFeatureRegistry.Politics));
        }

        [Test]
        public void CivilizationModule_AppliesStartingStateAndSourceTrackedModifiers()
        {
            CivicRunLaunchSettings.StartingCivilizationId = "arpadel";
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.StartCivilizations }));

            var module = runtime.GetModule<CivicCivilizationModule>(CivicFeatureRegistry.StartCivilizations);
            Assert.That(module.ActiveCivilization.Id, Is.EqualTo("arpadel"));
            Assert.That(simulation.State.Resources["wheat"].ToDouble(), Is.GreaterThanOrEqualTo(15d));
            Assert.That(
                simulation.Modifiers.Entries.All(entry => entry.SourceType == "civilization" && entry.SourceId == "arpadel"),
                Is.True);
            Assert.That(
                simulation.Snapshot.Resources.Single(resource => resource.Id == "wheat").ProducedPerSecond.ToDouble(),
                Is.GreaterThan(1d));
        }

        [Test]
        public void CivilizationModule_RejectsSelectionWhoseRequiredModuleIsOff()
        {
            CivicRunLaunchSettings.StartingCivilizationId = "mirato";
            Assert.Throws<CivicDataException>(() => new CivicModuleRuntime(
                new CivicGameSimulation(gameData),
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.StartCivilizations })));
        }

        [Test]
        public void PrestigeModule_UsesRunPeaksAndChallengeRewardWithoutDoubleGrant()
        {
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[]
                {
                    CivicFeatureRegistry.Prestige,
                    CivicFeatureRegistry.Achievements
                }));
            simulation.State.BasePopulation = CivicNumber.FromDouble(100d);
            simulation.State.CurrentEraId = "ancient";
            simulation.State.Resources["treasury"] = CivicNumber.FromDouble(100d);

            runtime.Advance(1d);

            var prestige = runtime.GetModule<CivicPrestigeModule>(CivicFeatureRegistry.Prestige);
            var preview = prestige.CreatePreview();
            Assert.That(preview.CanPrestige, Is.True);
            Assert.That(preview.ChallengeScore, Is.EqualTo(3));
            Assert.That(preview.PopulationScore, Is.EqualTo(10));
            Assert.That(prestige.TryPrestige(out var awarded), Is.True);
            Assert.That(runtime.MetaProgress.PrestigePoints, Is.EqualTo(awarded));
            Assert.That(runtime.MetaProgress.PrestigeCount, Is.EqualTo(1));
        }

        [Test]
        public void NationModule_DeclaresPreparesAndAppliesDefaultCharter()
        {
            const string nations = "id,displayNameKo,conceptId,tier,preparationSeconds,treasuryCost,secret,requiredFeatureIds,descriptionKo\n" +
                "test_nation,시험국,test,1,1,0,false,,시험 국가\n";
            const string conditions = "nationId,metricId,comparator,value,alternativeGroup\n" +
                "test_nation,snapshot.population,>=,3,\n";
            const string effects = "nationId,charterId,effectType,targetId,amount,capGroup\n" +
                "test_nation,default,resourceOutputMultiplier,wheat,0.1,resource_output\n";
            var content = CivicNationContentLoader.Parse(nations, conditions, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.NationFormation }),
                nationContent: content);
            var module = runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);

            Assert.That(module.Snapshot.Single().State, Is.EqualTo(CivicNationCandidateState.Ready));
            Assert.That(module.TryDeclare("test_nation"), Is.True);
            runtime.Advance(1d);
            Assert.That(module.Snapshot.Single().State, Is.EqualTo(CivicNationCandidateState.AwaitingCharter));
            Assert.That(module.TryCompleteFormation(), Is.True);
            Assert.That(module.CurrentNationId, Is.EqualTo("test_nation"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "nation" && item.SourceId == "test_nation"), Is.True);
        }

        [Test]
        public void NationModule_DebugInstantFormationBypassesConditionsCostAndPreparation()
        {
            const string nations = "id,displayNameKo,conceptId,tier,preparationSeconds,treasuryCost,secret,requiredFeatureIds,descriptionKo\n" +
                "locked_nation,잠긴 국가,test,1,999,99999,false,,시험 국가\n";
            const string conditions = "nationId,metricId,comparator,value,alternativeGroup\n" +
                "locked_nation,snapshot.population,>=,999999,\n";
            const string effects = "nationId,charterId,effectType,targetId,amount,capGroup\n" +
                "locked_nation,default,resourceOutputMultiplier,wheat,0.1,resource_output\n";
            var content = CivicNationContentLoader.Parse(nations, conditions, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.NationFormation }),
                nationContent: content);
            var module = runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);

            Assert.That(module.DebugFormImmediately("locked_nation"), Is.True);
            Assert.That(module.CurrentNationId, Is.EqualTo("locked_nation"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "nation" && item.SourceId == "locked_nation"), Is.True);
        }

        [Test]
        public void NationModule_MarksMismatchedStartingCivilizationAsImpossibleThisRun()
        {
            CivicRunLaunchSettings.StartingCivilizationId = "arpadel";
            const string nations = "id,displayNameKo,conceptId,tier,preparationSeconds,treasuryCost,secret,requiredFeatureIds,descriptionKo\n" +
                "other_origin,타 문명 계승국,test,1,1,0,false,,다른 시작 문명 전용 국가\n";
            const string conditions = "nationId,metricId,comparator,value,alternativeGroup\n" +
                "other_origin,identity.startingCivilization.thalassa,==,1,\n";
            const string effects = "nationId,charterId,effectType,targetId,amount,capGroup\n";
            var content = CivicNationContentLoader.Parse(nations, conditions, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.StartCivilizations, CivicFeatureRegistry.NationFormation }),
                nationContent: content);
            var module = runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);

            Assert.That(module.IsImpossibleThisRun("other_origin"), Is.True);
            Assert.That(module.ImpossibleReasonFor("other_origin"), Does.Contain("identity.startingCivilization.thalassa"));
            Assert.That(module.Snapshot.Single().BlockingReason, Does.Contain("이번 런 달성 불가"));
        }

        [Test]
        public void PoliticsModule_CompletesOneReformAndReplacesCategoryModifier()
        {
            const string institutions = "id,category,displayNameKo,descriptionKo,order,isDefault,politicalCost,treasuryCost,reformSeconds,fatigueSeconds\n" +
                "default_rule,government,default,default,0,true,0,0,1,0\n" +
                "alternate_rule,government,alternate,alternate,1,false,0,0,1,0\n";
            const string unlocks = "institutionId,metricId,comparator,value,alternativeGroup\n";
            const string effects = "institutionId,effectType,targetId,amount,costType,capGroup\n" +
                "default_rule,resourceOutputMultiplier,wheat,0.1,,resource_output\n" +
                "alternate_rule,resourceOutputMultiplier,wheat,0.25,,resource_output\n";
            var content = CivicPoliticsContentLoader.Parse(institutions, unlocks, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Politics }),
                politicsContent: content);
            var module = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);

            Assert.That(module.ActiveByCategory["government"], Is.EqualTo("default_rule"));
            Assert.That(module.TryPropose("alternate_rule"), Is.True);
            runtime.Advance(1d);

            Assert.That(module.Reform, Is.Null);
            Assert.That(module.ActiveByCategory["government"], Is.EqualTo("alternate_rule"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "institution" && item.SourceId == "alternate_rule" && Math.Abs(item.Amount - 0.25d) < 1e-9d), Is.True);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceId == "default_rule"), Is.False);
        }

        [Test]
        public void PoliticsModule_DebugInstantApplyBypassesUnlockCostAndDuration()
        {
            const string institutions = "id,category,displayNameKo,descriptionKo,order,isDefault,politicalCost,treasuryCost,reformSeconds,fatigueSeconds\n" +
                "default_rule,government,default,default,0,true,0,0,1,0\n" +
                "locked_rule,government,locked,locked,1,false,50,50,600,600\n";
            const string unlocks = "institutionId,metricId,comparator,value,alternativeGroup\n" +
                "locked_rule,snapshot.population,>=,999999,\n";
            const string effects = "institutionId,effectType,targetId,amount,costType,capGroup\n" +
                "locked_rule,resourceOutputMultiplier,wheat,0.25,,resource_output\n";
            var content = CivicPoliticsContentLoader.Parse(institutions, unlocks, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Politics }),
                politicsContent: content);
            var module = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);

            Assert.That(module.IsUnlocked("locked_rule"), Is.False);
            Assert.That(module.DebugApplyImmediately("locked_rule"), Is.True);
            Assert.That(module.ActiveByCategory["government"], Is.EqualTo("locked_rule"));
            Assert.That(module.Reform, Is.Null);
            Assert.That(module.FatigueRemaining, Is.Zero);
        }

        [Test]
        public void RunTelemetry_TracksConsecutivePositiveTreasuryAndUnlockedResources()
        {
            var simulation = new CivicGameSimulation(gameData);
            var telemetry = new CivicRunTelemetry(simulation);
            simulation.State.Resources["treasury"] = CivicNumber.FromDouble(10d);
            simulation.RefreshSnapshot();

            telemetry.Observe(3d);

            Assert.That(telemetry.GetMetric("run.treasuryPositiveDuration", new CivicMetaProgress()), Is.EqualTo(3d));
            Assert.That(telemetry.GetMetric("resource.unlocked.wheat", new CivicMetaProgress()), Is.EqualTo(1d));
            simulation.State.Resources["treasury"] = CivicNumber.Zero;
            simulation.RefreshSnapshot();
            telemetry.Observe(1d);
            Assert.That(telemetry.GetMetric("run.treasuryPositiveDuration", new CivicMetaProgress()), Is.Zero);
        }

        [Test]
        public void EventModule_PausesForCertainEventAndAppliesSelectedImmediateEffect()
        {
            const string events = "id,category,titleKo,descriptionKo,triggerMode,baseWeight,pitySeconds,cooldownSeconds,maxPerRun,pauseByDefault,requiredFeatureIds\n" +
                "test_event,test,test,test,certain,0,0,0,1,true,\n";
            const string conditions = "eventId,metricId,comparator,value,duration,forbidden,alternativeGroup\n" +
                "test_event,snapshot.population,>=,0,0,false,\n";
            const string choices = "eventId,choiceId,textKo,requirementMetricId,requirementComparator,requirementValue,nextEventId\n" +
                "test_event,test_choice,choose,,,,\n";
            const string effects = "choiceId,effectType,targetId,amount,duration,stackGroup\n" +
                "test_choice,resourceGrant,wheat,2,0,\n";
            var content = CivicEventContentLoader.Parse(events, conditions, choices, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Events }),
                eventContent: content);
            var module = runtime.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            var before = simulation.State.Resources["wheat"];
            var elapsedBefore = runtime.Telemetry.ElapsedSeconds;

            Assert.That(module.Queue.Count, Is.EqualTo(1));
            runtime.Advance(10d);
            Assert.That(runtime.Telemetry.ElapsedSeconds, Is.EqualTo(elapsedBefore));
            Assert.That(module.TryChoose("test_event", "test_choice"), Is.True);
            Assert.That(simulation.State.Resources["wheat"], Is.EqualTo(before + CivicNumber.FromDouble(2d)));
            Assert.That(runtime.MetaProgress.DiscoveredEventIds, Contains.Item("test_event"));
            Assert.That(module.History.Single().EventTitleKo, Is.EqualTo("test"));
            Assert.That(module.History.Single().ChoiceTextKo, Is.EqualTo("choose"));
            Assert.That(module.History.Single().AppliedResults, Has.Some.Contains("wheat"));
        }

        [Test]
        public void EventModule_ConditionalSchedulerUsesFiveSecondTickAndTimedModifierExpires()
        {
            const string events = "id,category,titleKo,descriptionKo,triggerMode,baseWeight,pitySeconds,cooldownSeconds,maxPerRun,pauseByDefault,requiredFeatureIds\n" +
                "test_event,test,test,test,conditional,1,0,0,1,false,\n";
            const string conditions = "eventId,metricId,comparator,value,duration,forbidden,alternativeGroup\n" +
                "test_event,snapshot.population,>=,0,0,false,\n";
            const string choices = "eventId,choiceId,textKo,requirementMetricId,requirementComparator,requirementValue,nextEventId\n" +
                "test_event,test_choice,choose,,,,\n";
            const string effects = "choiceId,effectType,targetId,amount,duration,stackGroup\n" +
                "test_choice,resourceOutputMultiplier,wheat,0.2,10,test_stack\n";
            var content = CivicEventContentLoader.Parse(events, conditions, choices, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Events }),
                eventContent: content);
            var module = runtime.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);

            runtime.Advance(4d);
            Assert.That(module.Queue, Is.Empty);
            runtime.Advance(1d);
            Assert.That(module.Queue.Count, Is.EqualTo(1));
            Assert.That(module.TryChoose("test_event", "test_choice"), Is.True);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "event" && Math.Abs(item.Amount - 0.2d) < 1e-9d), Is.True);
            runtime.Advance(10d);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "event"), Is.False);
        }

        [Test]
        public void EventContent_RejectsCircularFollowupChain()
        {
            const string events = "id,category,titleKo,descriptionKo,triggerMode,baseWeight,pitySeconds,cooldownSeconds,maxPerRun,pauseByDefault,requiredFeatureIds\n" +
                "event_a,test,a,a,chain,0,0,0,1,true,\n" +
                "event_b,test,b,b,chain,0,0,0,1,true,\n";
            const string conditions = "eventId,metricId,comparator,value,duration,forbidden,alternativeGroup\n";
            const string choices = "eventId,choiceId,textKo,requirementMetricId,requirementComparator,requirementValue,nextEventId\n" +
                "event_a,choice_a,a,,,,event_b\n" +
                "event_b,choice_b,b,,,,event_a\n";
            const string effects = "choiceId,effectType,targetId,amount,duration,stackGroup\n";

            Assert.Throws<CivicDataException>(() => CivicEventContentLoader.Parse(events, conditions, choices, effects));
        }

        [Test]
        public void WonderModule_DeliversCompletesAndAppliesModifier()
        {
            const string wonders = "id,displayNameKo,conceptId,eraId,technologyId,upfrontTreasury,upkeepType,upkeepAmount,requiredFeatureIds\n" +
                "test_wonder,시험 불가사의,test,primitive,,0,none,0,\n";
            const string costs = "wonderId,resourceId,amount,deliveryRate\n" +
                "test_wonder,wheat,1,1\n";
            const string conditions = "wonderId,metricId,comparator,value\n";
            const string effects = "wonderId,effectType,targetId,amount,duration,capGroup\n" +
                "test_wonder,resourceOutputMultiplier,wheat,0.1,0,resource_output\n";
            var content = CivicWonderContentLoader.Parse(wonders, costs, conditions, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Wonders }),
                wonderContent: content);
            var module = runtime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders);

            Assert.That(module.TryStart("test_wonder"), Is.True);
            runtime.Advance(1d);
            Assert.That(module.CompletedIds, Contains.Item("test_wonder"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "wonder" && item.SourceId == "test_wonder"), Is.True);
            Assert.That(runtime.MetaProgress.CompletedWonderIds, Contains.Item("test_wonder"));
        }

        [Test]
        public void WonderModule_DebugInstantCompleteBypassesConditionsAndConstruction()
        {
            const string wonders = "id,displayNameKo,conceptId,eraId,technologyId,upfrontTreasury,upkeepType,upkeepAmount,requiredFeatureIds\n" +
                "locked_wonder,잠긴 불가사의,test,future,future_taxation,99999,none,0,\n";
            const string costs = "wonderId,resourceId,amount,deliveryRate\n" +
                "locked_wonder,wheat,99999,0.01\n";
            const string conditions = "wonderId,metricId,comparator,value\n" +
                "locked_wonder,snapshot.population,>=,999999\n";
            const string effects = "wonderId,effectType,targetId,amount,duration,capGroup\n" +
                "locked_wonder,resourceOutputMultiplier,wheat,0.1,0,resource_output\n";
            var content = CivicWonderContentLoader.Parse(wonders, costs, conditions, effects);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Wonders }),
                wonderContent: content);
            var module = runtime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders);

            Assert.That(module.DebugCompleteImmediately("locked_wonder"), Is.True);
            Assert.That(module.CompletedIds, Contains.Item("locked_wonder"));
            Assert.That(module.ActiveProjectId, Is.Empty);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "wonder" && item.SourceId == "locked_wonder"), Is.True);
        }

        [Test]
        public void PeopleModule_RecruitsUsesAbilityAndLeavesReducedLegacy()
        {
            const string positions = "id,displayNameKo,descriptionKo,order,requiredFeatureIds\n" +
                "leader,국가지도자,국가 전반을 담당합니다.,0,\n" +
                "science,과학연구담당관,과학과 기술 연구를 담당합니다.,1,\n";
            const string people = "id,displayNameKo,archetypeId,rarity,portraitId,baseTenure,allowedPositionIds,requiredFeatureIds\n" +
                "test_person,시험 인물,scholar,common,test,600,leader;science,\n";
            const string conditions = "personId,metricId,comparator,value\n";
            const string traits = "personId,positionId,effectType,targetId,amount,capGroup\n" +
                "test_person,leader,resourceOutputMultiplier,wheat,0.2,resource_output\n" +
                "test_person,science,resourceOutputMultiplier,wheat,0.1,resource_output\n";
            const string abilities = "personId,id,effectType,targetId,amount,duration,usesPerRun\n" +
                "test_person,grant_wheat,resourceGrant,wheat,2,0,1\n";
            var content = CivicPeopleContentLoader.Parse(positions, people, conditions, traits, abilities);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.GreatPeople }),
                peopleContent: content);
            var module = runtime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople);
            var wheatBefore = simulation.State.Resources["wheat"];

            Assert.That(module.Candidates.Count, Is.EqualTo(1));
            Assert.That(module.TryRecruit("test_person"), Is.True);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "person"), Is.False);
            Assert.That(module.TryUseAbility("test_person"), Is.False);
            Assert.That(module.TryAssign("test_person", "science"), Is.True);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "person" && Math.Abs(item.Amount - 0.1d) < 1e-9d), Is.True);
            Assert.That(module.TryUseAbility("test_person"), Is.True);
            Assert.That(simulation.State.Resources["wheat"], Is.EqualTo(wheatBefore + CivicNumber.FromDouble(2d)));
            Assert.That(module.TryUnassign("test_person"), Is.True);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "person"), Is.False);
            Assert.That(module.TryAssign("test_person", "leader"), Is.True);
            runtime.Advance(600d);
            Assert.That(module.RetiredIds, Contains.Item("test_person"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "personLegacy" && Math.Abs(item.Amount - 0.02d) < 1e-9d), Is.True);
        }

        [Test]
        public void PeopleModule_AssigningOccupiedPositionMovesPreviousPersonToUnassigned()
        {
            const string positions = "id,displayNameKo,descriptionKo,order,requiredFeatureIds\n" +
                "leader,국가지도자,국가 전반을 담당합니다.,0,\n" +
                "science,과학연구담당관,과학과 기술 연구를 담당합니다.,1,\n";
            const string people = "id,displayNameKo,archetypeId,rarity,portraitId,baseTenure,allowedPositionIds,requiredFeatureIds\n" +
                "person_a,인물 A,scholar,common,a,600,leader;science,\n" +
                "person_b,인물 B,scholar,common,b,600,leader;science,\n";
            const string conditions = "personId,metricId,comparator,value\n";
            const string traits = "personId,positionId,effectType,targetId,amount,capGroup\n" +
                "person_a,leader,resourceOutputMultiplier,wheat,0.1,resource_output\n" +
                "person_a,science,resourceOutputMultiplier,wheat,0.01,resource_output\n" +
                "person_b,leader,resourceOutputMultiplier,wheat,0.2,resource_output\n" +
                "person_b,science,resourceOutputMultiplier,wheat,0.02,resource_output\n";
            const string abilities = "personId,id,effectType,targetId,amount,duration,usesPerRun\n";
            var content = CivicPeopleContentLoader.Parse(positions, people, conditions, traits, abilities);
            var simulation = new CivicGameSimulation(gameData);
            var runtime = new CivicModuleRuntime(
                simulation,
                CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.GreatPeople }),
                peopleContent: content);
            var module = runtime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople);

            Assert.That(module.TryRecruit("person_a"), Is.True);
            Assert.That(module.TryRecruit("person_b"), Is.True);
            Assert.That(module.TryAssign("person_a", "leader"), Is.True);
            Assert.That(module.TryAssign("person_b", "leader"), Is.True);
            Assert.That(module.ActivePeople.Single(item => item.Definition.Id == "person_a").AssignmentId, Is.Empty);
            Assert.That(module.PositionSnapshots.Single(item => item.Definition.Id == "leader").Occupant.Definition.Id, Is.EqualTo("person_b"));
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "person" && item.SourceId == "person_a"), Is.False);
            Assert.That(simulation.Modifiers.Entries.Any(item => item.SourceType == "person" && item.SourceId == "person_b" && Math.Abs(item.Amount - 0.2d) < 1e-9d), Is.True);
        }
    }
}
