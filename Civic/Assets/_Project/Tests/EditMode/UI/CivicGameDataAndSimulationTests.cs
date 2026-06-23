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
            Assert.That(data.BuildingsById.Keys, Does.Contain("capital"));
            Assert.That(data.TechnologiesById.Keys, Does.Contain("woodworking"));
            Assert.That(data.ErasById.Keys, Does.Contain("primitive"));
            Assert.That(data.ResourcesById["food"].Category, Is.EqualTo(ResourceCategory.Aggregate));
            Assert.That(data.ResourcesById["wheat"].FoodConversion, Is.EqualTo(1d));
            Assert.That(data.InitialState.Resources["wheat"].ToDouble(), Is.EqualTo(5d).Within(0.0001d));
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

            simulation.Advance(1d);

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
        public void BuildAndResearchActionsUpdateRuntimeState()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.TryBuild("logging_camp"), Is.True);
            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "logging_camp").Count,
                Is.EqualTo(1));
            Assert.That(simulation.Snapshot.ConstructionPower.ToDouble(), Is.EqualTo(2d).Within(0.0001d));

            var toolWorkshop = simulation.Snapshot.Buildings.Single(building => building.Id == "tool_workshop");
            Assert.That(toolWorkshop.ResourceDeltas.Any(delta => delta.ResourceId == "wood" && delta.AmountPerSecond.ToDouble() < 0d), Is.True);
            Assert.That(toolWorkshop.ResourceDeltas.Any(delta => delta.ResourceId == "tools" && delta.AmountPerSecond.ToDouble() > 0d), Is.True);
            Assert.That(toolWorkshop.GdpDelta.ToDouble(), Is.GreaterThan(0d));

            var constructionSector = simulation.Snapshot.Buildings.Single(building => building.Id == "construction_sector");
            Assert.That(
                constructionSector.ResourceDeltas.Any(delta => delta.ResourceId == "treasury" && delta.AmountPerSecond.ToDouble() < 0d),
                Is.True);

            simulation.State.Resources["science"] = CivicNumber.FromDouble(3d);
            Assert.That(simulation.TryResearch("woodworking"), Is.True);
            Assert.That(
                simulation.Snapshot.Technologies.Single(technology => technology.Id == "woodworking").IsResearched,
                Is.True);

            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(10d);
            Assert.That(simulation.TryBuild("tool_workshop"), Is.True);
        }

        [Test]
        public void PopulationProducingBuildingsCanBuildAtPopulationCap()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());
            simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(100d);

            Assert.That(simulation.TryBuild("logging_camp"), Is.True);
            Assert.That(simulation.TryBuild("wheat_farm"), Is.True);
            Assert.That(simulation.TryBuild("construction_sector"), Is.True);
            Assert.That(simulation.Snapshot.UsedPopulation.ToDouble(), Is.EqualTo(3d).Within(0.0001d));
            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(3d).Within(0.0001d));

            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "logging_camp").CanBuild,
                Is.False);
            Assert.That(
                simulation.Snapshot.Buildings.Single(building => building.Id == "hut").CanBuild,
                Is.True);

            Assert.That(simulation.TryBuild("hut"), Is.True);
            Assert.That(simulation.Snapshot.Population.ToDouble(), Is.EqualTo(5d).Within(0.0001d));
        }

        [Test]
        public void TreasurySnapshotIncludesConstructionSectorSpend()
        {
            var simulation = new CivicGameSimulation(LoadDefaultData());

            Assert.That(simulation.TryBuild("construction_sector"), Is.True);

            var treasury = Resource(simulation.Snapshot, "treasury");
            Assert.That(treasury.ProducedPerSecond.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(treasury.ConsumedPerSecond.ToDouble(), Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(treasury.NetPerSecond.ToDouble(), Is.EqualTo(0.5d).Within(0.0001d));
        }

        private static CivicGameData LoadDefaultData()
        {
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            Assert.That(dataSource, Is.Not.Null);
            return dataSource.LoadGameData();
        }

        private static CivicResourceSnapshot Resource(CivicGameSnapshot snapshot, string id)
        {
            return snapshot.Resources.Single(resource => resource.Id == id);
        }

        private static double OutputAmount(BuildingDefinition building, string resourceId)
        {
            return building.Outputs.Single(output => output.ResourceId == resourceId).Amount.ToDouble();
        }
    }
}
