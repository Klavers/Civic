using System.Collections.Generic;
using System.Linq;
using Civic.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace Civic.UI.Tests
{
    public sealed class CivicGameDataAndSimulationTests
    {
        [Test]
        public void CivicNumber_TreatsPositiveFractionAsGreaterThanZero()
        {
            var value = CivicNumber.FromDouble(0.1d);

            Assert.That(value, Is.GreaterThan(CivicNumber.Zero));
            Assert.That(CivicNumber.ClampMinZero(value).ToDouble(), Is.EqualTo(0.1d).Within(0.0001d));
        }

        [Test]
        public void DefaultDataSource_LoadsCategorySeparatedCsvSheets()
        {
            var data = LoadDefaultData();

            Assert.That(data.ResourcesById.Keys, Does.Contain("food"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("wheat"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("plastic"));
            Assert.That(data.BuildingsById.Keys, Does.Contain("capital"));
            Assert.That(data.BuildingsById.Keys, Does.Contain("house"));
            Assert.That(data.TechnologiesById.Keys, Does.Contain("wood_processing"));
            Assert.That(data.TechnologiesById.Keys, Does.Contain("future_taxation"));
            Assert.That(data.TechnologyEffects.Any(effect => effect.EffectType == TechnologyEffectType.ConditionalOutputAdd), Is.True);
            Assert.That(data.TechnologyEffects.Any(effect => effect.EffectType == TechnologyEffectType.TaxRateAdd), Is.True);
            Assert.That(data.Technologies.All(technology => technology.TaxRateAdd == 0d), Is.True);
            Assert.That(data.ResourcesById.Keys, Does.Contain("coal"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("oil"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("uranium"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("sulfur"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("concrete"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("fertilizer"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("machine_parts"));
            Assert.That(data.ResourcesById.Keys, Does.Contain("explosives"));
            Assert.That(data.ErasById.Keys, Does.Contain("primitive"));
            Assert.That(data.ErasById.Keys, Does.Contain("industrial"));
            Assert.That(data.ErasById.Keys, Does.Contain("future"));
            Assert.That(data.ErasById["primitive"].Order, Is.EqualTo(0));
            Assert.That(data.ResourcesById["food"].Category, Is.EqualTo(ResourceCategory.Aggregate));
            Assert.That(data.ResourcesById["wheat"].FoodConversion, Is.EqualTo(1d));
            Assert.That(data.ResourcesById["meat"].FoodConversion, Is.EqualTo(2d));
            Assert.That(data.ResourcesById["groceries"].FoodConversion, Is.EqualTo(4d));
            Assert.That(data.ResourcesById["tools"].BasePrice.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(data.ResourcesById["tools"].IsPopulationConsumption, Is.True);
            Assert.That(data.ResourcesById["tools"].RequiredTechnologyId, Is.EqualTo("stone_toolmaking"));
            Assert.That(data.ResourcesById["stone"].IsPopulationConsumption, Is.False);
            Assert.That(data.InitialState.Resources["wheat"].ToDouble(), Is.EqualTo(5d).Within(0.0001d));
            Assert.That(data.InitialState.Technologies, Does.Contain("primitive_agriculture"));
            Assert.That(data.InitialState.Technologies, Does.Contain("primitive_architecture"));
            Assert.That(data.InitialState.Technologies, Does.Contain("wood_processing"));
            Assert.That(data.TechnologiesById["primitive_agriculture"].UnlocksEraId, Is.Empty);
            Assert.That(data.TechnologiesById["calendar"].UnlocksEraId, Is.Empty);
            Assert.That(OutputAmount(data.BuildingsById["capital"], "wheat"), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(OutputAmount(data.BuildingsById["capital"], "wood"), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(OutputAmount(data.BuildingsById["capital"], "treasury"), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(OutputAmount(data.BuildingsById["capital"], "construction_power"), Is.EqualTo(1d).Within(0.0001d));
        }

        [Test]
        public void CsvParser_SupportsHeaderAndQuotedFields()
        {
            var errors = new List<string>();
            var rows = CivicCsvParser.Parse(
                "id,displayNameKo,notes\nquoted,\"value, comma\",\"quote \"\"ok\"\"\"",
                errors,
                "quoted.csv");

            Assert.That(errors, Is.Empty);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0]["id"], Is.EqualTo("quoted"));
            Assert.That(rows[0]["displayNameKo"], Is.EqualTo("value, comma"));
            Assert.That(rows[0]["notes"], Is.EqualTo("quote \"ok\""));
        }

        [Test]
        public void Simulation_AdvanceUsesInitialStateAndKeepsResourcesNonNegative()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(Resource(simulation.Snapshot, "food").Stockpile.ToDouble(), Is.EqualTo(5d).Within(0.0001d));
            Assert.That(Resource(simulation.Snapshot, "wheat").Stockpile.ToDouble(), Is.EqualTo(5d).Within(0.0001d));

            simulation.Advance(0.5d);
            Assert.That(Resource(simulation.Snapshot, "wheat").Stockpile.ToDouble(), Is.EqualTo(5d).Within(0.0001d));

            simulation.Advance(0.5d);

            Assert.That(Resource(simulation.Snapshot, "wheat").Stockpile.ToDouble(), Is.GreaterThan(5d));
            Assert.That(Resource(simulation.Snapshot, "wood").Stockpile.ToDouble(), Is.GreaterThan(0d));
            Assert.That(simulation.Snapshot.ConstructionPower.ToDouble(), Is.GreaterThan(5d));
            Assert.That(simulation.Snapshot.Science.ToDouble(), Is.GreaterThan(0d));
            Assert.That(simulation.Snapshot.Gdp.ToDouble(), Is.GreaterThan(0d));
            Assert.That(Resource(simulation.Snapshot, "wheat").Producers.Any(flow => flow.BuildingId == "capital"), Is.True);
            Assert.That(Resource(simulation.Snapshot, "wheat").BuildProjections.Any(projection => projection.BuildingId == "wheat_farm"), Is.True);
            Assert.That(
                simulation.Snapshot.Resources.All(resource => resource.Stockpile >= CivicNumber.Zero),
                Is.True);
        }

        [Test]
        public void ResearchUnlocksResourcesAndBuildingsForSnapshot()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.Snapshot.Resources.Any(resource => resource.Id == "tools"), Is.False);
            Assert.That(simulation.Snapshot.Resources.Any(resource => resource.Id == "meat"), Is.False);
            Assert.That(simulation.Snapshot.Buildings.Any(building => building.Id == "logging_camp"), Is.True);
            Assert.That(simulation.Snapshot.Buildings.Any(building => building.Id == "house"), Is.True);
            Assert.That(simulation.Snapshot.Buildings.Any(building => building.Id == "wheat_farm"), Is.True);

            simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);
            Assert.That(simulation.TryResearch("wood_processing"), Is.False);
            Assert.That(simulation.Snapshot.Resources.Any(resource => resource.Id == "tools"), Is.False);

            Assert.That(simulation.TryResearch("stone_toolmaking"), Is.True);
            Assert.That(simulation.Snapshot.Resources.Any(resource => resource.Id == "tools"), Is.True);
        }

        [Test]
        public void EraTabsUnlockAtHalfCompletionAndNextEraResearchAdvancesWithoutRegression()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Resources["science"] = CivicNumber.FromDouble(1000d);

            Assert.That(simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));
            Assert.That(Era(simulation.Snapshot, "primitive").IsVisible, Is.True);
            Assert.That(Era(simulation.Snapshot, "ancient").IsVisible, Is.False);
            Assert.That(simulation.Snapshot.Technologies.Count(technology => technology.EraId == "primitive" && technology.IsResearched), Is.EqualTo(3));

            Assert.That(simulation.TryResearch("stone_toolmaking"), Is.True);
            Assert.That(Era(simulation.Snapshot, "ancient").IsVisible, Is.False);

            Assert.That(simulation.TryResearch("gathering_knowledge"), Is.True);
            Assert.That(Era(simulation.Snapshot, "ancient").IsVisible, Is.True);
            Assert.That(simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));

            Assert.That(simulation.TryResearch("calendar"), Is.True);
            Assert.That(simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));

            Assert.That(simulation.TryResearch("animal_domestication"), Is.True);
            Assert.That(simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));
            Assert.That(Era(simulation.Snapshot, "classical").IsVisible, Is.False);

            Assert.That(simulation.TryResearch("bronze_smelting"), Is.True);
            Assert.That(simulation.TryResearch("writing"), Is.True);
            Assert.That(simulation.TryResearch("urban_planning"), Is.True);
            Assert.That(simulation.TryResearch("brick_architecture"), Is.True);
            Assert.That(simulation.TryResearch("wheelbarrow"), Is.True);
            Assert.That(Era(simulation.Snapshot, "classical").IsVisible, Is.True);
        }

        [Test]
        public void UnlocksEraIdDoesNotAdvanceEra()
        {
            var source = LoadDefaultDataSource();
            var technologiesCsv = source.TechnologiesCsv.text.Replace(
                "primitive_agriculture,원시 농경,primitive,3,,,0,10",
                "primitive_agriculture,원시 농경,primitive,3,ancient,,0,10");
            var initialStateCsv = source.InitialStateCsv.text
                .Replace("technology,primitive_agriculture,1\r\n", string.Empty)
                .Replace("technology,primitive_agriculture,1\n", string.Empty);
            var data = CivicGameDataLoader.Load(
                source.ResourcesCsv.text,
                source.BuildingsCsv.text,
                technologiesCsv,
                source.TechnologyEffectsCsv.text,
                source.ErasCsv.text,
                initialStateCsv);
            var simulation = new CivicGameSimulation(data);
            simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);

            Assert.That(simulation.TryResearch("primitive_agriculture"), Is.True);
            Assert.That(simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));
        }

        [Test]
        public void PopulationConsumptionActivatesCurrentBonusWithoutAccumulating()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Buildings["capital"] = 0;
            simulation.State.Buildings["house"] = 100;
            simulation.State.Resources["wheat"] = CivicNumber.FromDouble(97d);
            simulation.State.Resources["wood"] = CivicNumber.FromDouble(112d);
            simulation.State.Resources["tools"] = CivicNumber.FromDouble(80d);
            simulation.State.ResearchedTechnologyIds.Add("stone_toolmaking");

            simulation.Advance(0.1d);

            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(380d).Within(0.0001d));
            Assert.That(
                simulation.Snapshot.PopulationConsumption.Sum(entry => entry.ProducedPopulation.ToDouble()),
                Is.EqualTo(277d).Within(0.0001d));

            simulation.Advance(1d);

            Assert.That(Resource(simulation.Snapshot, "wheat").Stockpile.ToDouble(), Is.EqualTo(0d).Within(0.0001d));
            Assert.That(Resource(simulation.Snapshot, "wood").Stockpile.ToDouble(), Is.EqualTo(12d).Within(0.0001d));
            Assert.That(Resource(simulation.Snapshot, "tools").Stockpile.ToDouble(), Is.EqualTo(0d).Within(0.0001d));
            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(115d).Within(0.0001d));
            Assert.That(
                simulation.Snapshot.PopulationConsumption.Sum(entry => entry.ProducedPopulation.ToDouble()),
                Is.EqualTo(12d).Within(0.0001d));

            simulation.State.Resources["wheat"] = CivicNumber.FromDouble(100d);
            simulation.State.Resources["wood"] = CivicNumber.FromDouble(100d);
            simulation.State.Resources["tools"] = CivicNumber.FromDouble(100d);
            simulation.Advance(0.1d);

            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(403d).Within(0.0001d));
        }

        [Test]
        public void FoodAggregateIncludesFoodChildConsumption()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.TryBuild("house"), Is.True);

            var food = Resource(simulation.Snapshot, "food");
            Assert.That(food.ProducedPerSecond.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(food.ConsumedPerSecond.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(food.NetPerSecond.ToDouble(), Is.EqualTo(0d).Within(0.0001d));
        }

        [Test]
        public void BuildAndResearchActionsUpdateRuntimeState()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.State.TaxRate, Is.EqualTo(0.10d).Within(0.0001d));

            Assert.That(simulation.TryBuild("tool_workshop"), Is.False);
            Assert.That(simulation.TryBuild("construction_sector"), Is.True);

            simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);
            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(10d);
            Assert.That(simulation.TryBuild("logging_camp"), Is.True);
            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "logging_camp").Count,
                Is.EqualTo(1));

            Assert.That(simulation.TryResearch("stone_toolmaking"), Is.True);
            var toolWorkshop = simulation.Snapshot.Buildings.Single(building => building.Id == "tool_workshop");
            Assert.That(toolWorkshop.ResourceDeltas.Any(delta => delta.ResourceId == "wood" && delta.AmountPerSecond.ToDouble() < 0d), Is.True);
            Assert.That(toolWorkshop.ResourceDeltas.Any(delta => delta.ResourceId == "tools" && delta.AmountPerSecond.ToDouble() > 0d), Is.True);
            Assert.That(toolWorkshop.GdpDelta.ToDouble(), Is.GreaterThan(0d));

            var constructionSector = simulation.Snapshot.Buildings.Single(building => building.Id == "construction_sector");
            Assert.That(
                constructionSector.ResourceDeltas.Any(delta => delta.ResourceId == "treasury" && delta.AmountPerSecond.ToDouble() < 0d),
                Is.True);

            Assert.That(simulation.TryResearch("gathering_knowledge"), Is.True);
            Assert.That(simulation.TryResearch("tax_system"), Is.True);
            Assert.That(
                simulation.Snapshot.Technologies.Single(technology => technology.Id == "tax_system").IsResearched,
                Is.True);
            Assert.That(simulation.State.TaxRate, Is.EqualTo(0.15d).Within(0.0001d));

            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(10d);
            Assert.That(simulation.TryBuild("tool_workshop"), Is.True);
        }

        [Test]
        public void TechnologyOutputAddAffectsProductionGdpAndResourceFlows()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Buildings["capital"] = 0;
            simulation.State.Buildings["wheat_farm"] = 1;
            simulation.State.ResearchedTechnologyIds.Add("storage_methods");

            simulation.Advance(0.1d);

            var wheat = Resource(simulation.Snapshot, "wheat");
            Assert.That(wheat.ProducedPerSecond.ToDouble(), Is.EqualTo(2d).Within(0.0001d));
            Assert.That(wheat.Producers.Any(flow => flow.BuildingDisplayNameKo.Contains("저장법")), Is.True);
            Assert.That(wheat.Producers.Sum(flow => flow.AmountPerSecond.ToDouble()), Is.EqualTo(2d).Within(0.0001d));
            Assert.That(simulation.Snapshot.Gdp.ToDouble(), Is.GreaterThan(0d));
        }

        [Test]
        public void ConditionalTechnologyEffectAddsDemandConsumptionAndBonusOutput()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Buildings["capital"] = 0;
            simulation.State.Buildings["tool_workshop"] = 1;
            simulation.State.ResearchedTechnologyIds.Add("stone_toolmaking");
            simulation.State.ResearchedTechnologyIds.Add("stone_shaping");
            simulation.State.Resources["wood"] = CivicNumber.FromDouble(100d);
            simulation.State.Resources["stone"] = CivicNumber.FromDouble(100d);

            simulation.Advance(0.1d);

            var tools = Resource(simulation.Snapshot, "tools");
            var stone = Resource(simulation.Snapshot, "stone");
            Assert.That(tools.ProducedPerSecond.ToDouble(), Is.EqualTo(2d).Within(0.0001d));
            Assert.That(stone.ConsumedPerSecond.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(tools.Producers.Any(flow => flow.BuildingDisplayNameKo.Contains("돌 다듬기")), Is.True);
            Assert.That(stone.Consumers.Any(flow => flow.BuildingDisplayNameKo.Contains("돌 다듬기")), Is.True);
        }

        [Test]
        public void PopulationOutputTechnologyEffectContributesToEffectivePopulation()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Buildings["capital"] = 0;
            simulation.State.Buildings["house"] = 1;
            simulation.State.Resources["wheat"] = CivicNumber.Zero;
            simulation.State.Resources["wood"] = CivicNumber.Zero;
            simulation.State.ResearchedTechnologyIds.Add("urban_planning");

            simulation.Advance(0.1d);

            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(5d).Within(0.0001d));
            Assert.That(Resource(simulation.Snapshot, "population").Producers.Any(flow => flow.BuildingDisplayNameKo.Contains("도시 계획")), Is.True);
        }

        [Test]
        public void GroupTargetTechnologyEffectsAreResearchableButPlannedFollowUpOnly()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            var powerGrid = simulation.Snapshot.Technologies.Single(technology => technology.Id == "power_grid");

            Assert.That(powerGrid.EffectSummary, Does.Contain("후속 구현 예정"));
            Assert.That(LoadDefaultData().TechnologyEffects.Single(effect => effect.TechnologyId == "power_grid").EffectType, Is.EqualTo(TechnologyEffectType.PlannedFollowUp));
        }

        [Test]
        public void PopulationProducingBuildingsCanBuildAtPopulationCap()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(100d);
            simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);

            Assert.That(simulation.TryBuild("logging_camp"), Is.True);
            Assert.That(simulation.TryBuild("wheat_farm"), Is.True);
            Assert.That(simulation.TryBuild("construction_sector"), Is.True);
            Assert.That(simulation.Snapshot.UsedPopulation.ToDouble(), Is.EqualTo(3d).Within(0.0001d));
            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(3d).Within(0.0001d));

            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "logging_camp").CanBuild,
                Is.False);
            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "house").CanBuild,
                Is.True);

            Assert.That(simulation.TryBuild("house"), Is.True);
            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(6d).Within(0.0001d));
        }

        [Test]
        public void NonPopulationBuildingCannotExceedIntegerPopulationLimit()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.ResearchedTechnologyIds.Add("wood_processing");
            simulation.State.BasePopulation = CivicNumber.FromDouble(27.000000000000007d);
            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(100d);
            simulation.State.Buildings["logging_camp"] = 27;
            simulation.Advance(0.1d);

            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(27d).Within(0.0001d));
            Assert.That(simulation.Snapshot.UsedPopulation.ToDouble(), Is.EqualTo(27d).Within(0.0001d));
            Assert.That(simulation.Snapshot.Buildings.Single(building => building.Id == "logging_camp").CanBuild, Is.False);
            Assert.That(simulation.TryBuild("logging_camp"), Is.False);
        }

        [Test]
        public void TreasurySnapshotIncludesConstructionSectorSpend()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.TryBuild("construction_sector"), Is.True);

            var treasury = Resource(simulation.Snapshot, "treasury");
            Assert.That(treasury.ProducedPerSecond.ToDouble(), Is.EqualTo(1d + simulation.Snapshot.Gdp.ToDouble() * simulation.State.TaxRate).Within(0.0001d));
            Assert.That(treasury.ConsumedPerSecond.ToDouble(), Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(treasury.NetPerSecond.ToDouble(), Is.EqualTo(treasury.ProducedPerSecond.ToDouble() - 0.5d).Within(0.0001d));
        }

        private static CivicGameData LoadDefaultData()
        {
            return LoadDefaultDataSource().LoadGameData();
        }

        private static CivicGameDataSource LoadDefaultDataSource()
        {
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            Assert.That(dataSource, Is.Not.Null);
            return dataSource;
        }

        private static CivicResourceSnapshot Resource(CivicGameSnapshot snapshot, string id)
        {
            return snapshot.Resources.Single(resource => resource.Id == id);
        }

        private static CivicEraSnapshot Era(CivicGameSnapshot snapshot, string id)
        {
            return snapshot.Eras.Single(era => era.Id == id);
        }

        private static double OutputAmount(BuildingDefinition building, string resourceId)
        {
            return building.Outputs.Single(output => output.ResourceId == resourceId).Amount.ToDouble();
        }
    }
}
