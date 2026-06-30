using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Simulation.Modules
{
    public sealed class CivicRunTelemetry
    {
        private readonly CivicGameSimulation simulation;
        private readonly HashSet<string> everBuiltIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> researchedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> externalMetrics = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> technologyAliases = new Dictionary<string, string>(StringComparer.Ordinal);

        public CivicRunTelemetry(CivicGameSimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            RunId = Guid.NewGuid().ToString("N");
            Observe(0d);
        }

        public string RunId { get; }
        public double ElapsedSeconds { get; private set; }
        public double PeakGdp { get; private set; }
        public double PeakPopulation { get; private set; }
        public int HighestEraOrder { get; private set; }
        public double TreasuryPositiveDuration { get; private set; }
        public bool TreasuryReachedZeroAfterAncient { get; private set; }
        public bool FoodShortageExperienced { get; private set; }
        public IReadOnlyCollection<string> EverBuiltIds => everBuiltIds;
        public IReadOnlyCollection<string> ResearchedIds => researchedIds;

        public void Observe(double elapsedSeconds)
        {
            ElapsedSeconds += Math.Max(0d, elapsedSeconds);
            var snapshot = simulation.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            PeakGdp = Math.Max(PeakGdp, snapshot.Gdp.ToDouble());
            PeakPopulation = Math.Max(PeakPopulation, snapshot.Population.ToDouble());
            var era = snapshot.Eras.FirstOrDefault(item => item.Id == snapshot.CurrentEraId);
            HighestEraOrder = Math.Max(HighestEraOrder, era?.Order ?? 0);
            TreasuryPositiveDuration = snapshot.Treasury > CivicNumber.Zero
                ? TreasuryPositiveDuration + Math.Max(0d, elapsedSeconds)
                : 0d;
            if (HighestEraOrder >= 1 && snapshot.Treasury <= CivicNumber.Zero)
            {
                TreasuryReachedZeroAfterAncient = true;
            }

            if (snapshot.Resources.Any(resource => resource.FoodConversion > 0d && resource.IsShortage))
            {
                FoodShortageExperienced = true;
            }

            foreach (var technology in snapshot.Technologies.Where(item => item.IsResearched))
            {
                researchedIds.Add(technology.Id);
            }

            foreach (var building in snapshot.Buildings.Where(item => item.Count > 0))
            {
                everBuiltIds.Add(building.Id);
            }
        }

        public void RecordBuilding(string buildingId)
        {
            if (!string.IsNullOrEmpty(buildingId))
            {
                everBuiltIds.Add(buildingId);
            }
        }

        public void RecordTechnology(string technologyId)
        {
            if (!string.IsNullOrEmpty(technologyId))
            {
                researchedIds.Add(technologyId);
            }
        }

        public void SetExternalMetric(string metricId, double value)
        {
            if (string.IsNullOrWhiteSpace(metricId))
            {
                throw new ArgumentException("Metric ID cannot be empty.", nameof(metricId));
            }

            externalMetrics[metricId] = value;
        }

        public void SetTechnologyAliases(IEnumerable<CivicTechnologyAliasDefinition> aliases)
        {
            technologyAliases.Clear();
            foreach (var alias in aliases ?? Array.Empty<CivicTechnologyAliasDefinition>())
            {
                technologyAliases[alias.SemanticTechnologyId] = alias.MappedTechnologyId;
            }
        }

        public double GetMetric(string metricId, CivicMetaProgress metaProgress)
        {
            var snapshot = simulation.Snapshot;
            if (externalMetrics.TryGetValue(metricId, out var externalValue))
            {
                return externalValue;
            }

            if (metricId == "run.peakGdp") return PeakGdp;
            if (metricId == "run.peakPopulation") return PeakPopulation;
            if (metricId == "run.highestEraOrder" || metricId == "snapshot.eraOrder") return HighestEraOrder;
            if (metricId == "run.treasuryPositiveDuration") return TreasuryPositiveDuration;
            if (metricId == "run.treasuryReachedZeroAfterAncient") return TreasuryReachedZeroAfterAncient ? 1d : 0d;
            if (metricId == "run.foodShortageExperienced") return FoodShortageExperienced ? 1d : 0d;
            if (metricId == "snapshot.population") return snapshot?.Population.ToDouble() ?? 0d;
            if (metricId == "snapshot.gdp") return snapshot?.Gdp.ToDouble() ?? 0d;
            if (metricId == "snapshot.currentEraOrder") return snapshot?.Eras.FirstOrDefault(item => item.Id == snapshot.CurrentEraId)?.Order ?? 0d;
            if (metricId == "snapshot.buildingPopulationRatio")
            {
                var population = snapshot?.Population.ToDouble() ?? 0d;
                return population <= 0d ? 0d : (snapshot?.Buildings.Sum(item => item.Count) ?? 0d) / population;
            }
            if (metricId == "snapshot.minSupplyRate")
            {
                return snapshot?.Resources.Where(resource => resource.Category == ResourceCategory.Element).Select(resource => resource.SupplyRate).DefaultIfEmpty(1d).Min() ?? 1d;
            }

            if (metricId.StartsWith("building.count.", StringComparison.Ordinal))
            {
                var buildingId = metricId.Substring("building.count.".Length);
                return snapshot?.Buildings.FirstOrDefault(item => item.Id == buildingId)?.Count ?? 0d;
            }

            if (metricId.StartsWith("building.group.", StringComparison.Ordinal))
            {
                var group = metricId.Substring("building.group.".Length);
                return snapshot?.Buildings.Where(item => IsBuildingInGroup(item.Id, group)).Sum(item => item.Count) ?? 0d;
            }

            if (metricId.StartsWith("building.ever.", StringComparison.Ordinal))
            {
                return everBuiltIds.Contains(metricId.Substring("building.ever.".Length)) ? 1d : 0d;
            }

            if (metricId.StartsWith("resource.gdpShare.", StringComparison.Ordinal))
            {
                var resourceId = metricId.Substring("resource.gdpShare.".Length);
                var resource = snapshot?.Resources.FirstOrDefault(item => item.Id == resourceId);
                var gdp = snapshot?.Gdp.ToDouble() ?? 0d;
                return resource == null || gdp <= 0d ? 0d : resource.ProducedPerSecond.ToDouble() * resource.Price.ToDouble() / gdp;
            }

            if (metricId.StartsWith("resource.stockpile.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.stockpile.".Length);
                return snapshot?.Resources.FirstOrDefault(item => item.Id == id)?.Stockpile.ToDouble() ?? 0d;
            }

            if (metricId.StartsWith("resource.stockDemandRatio.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.stockDemandRatio.".Length);
                var resource = snapshot?.Resources.FirstOrDefault(item => item.Id == id);
                if (resource == null || resource.NormalDemandPerSecond <= CivicNumber.Zero) return 0d;
                return resource.Stockpile.ToDouble() / resource.NormalDemandPerSecond.ToDouble();
            }

            if (metricId.StartsWith("resource.priceMultiplier.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.priceMultiplier.".Length);
                return snapshot?.Resources.FirstOrDefault(item => item.Id == id)?.PriceMultiplier ?? 0d;
            }

            if (metricId.StartsWith("resource.demandSupplyRatio.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.demandSupplyRatio.".Length);
                var resource = snapshot?.Resources.FirstOrDefault(item => item.Id == id);
                return resource == null || resource.SupplyDemandRatio <= 0d ? 0d : 1d / resource.SupplyDemandRatio;
            }

            if (metricId.StartsWith("resource.unlocked.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.unlocked.".Length);
                return snapshot?.Resources.Any(item => item.Id == id) == true ? 1d : 0d;
            }

            if (metricId.StartsWith("resource.supplyRate.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.supplyRate.".Length);
                return snapshot?.Resources.FirstOrDefault(item => item.Id == id)?.SupplyRate ?? 0d;
            }

            if (metricId.StartsWith("resource.net.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.net.".Length);
                return snapshot?.Resources.FirstOrDefault(item => item.Id == id)?.NetPerSecond.ToDouble() ?? 0d;
            }

            if (metricId.StartsWith("resource.produced.", StringComparison.Ordinal))
            {
                var id = metricId.Substring("resource.produced.".Length);
                return snapshot?.Resources.FirstOrDefault(item => item.Id == id)?.ProducedPerSecond.ToDouble() ?? 0d;
            }

            if (metricId == "resource.gdpShare.food")
            {
                var gdp = snapshot?.Gdp.ToDouble() ?? 0d;
                if (snapshot == null || gdp <= 0d) return 0d;
                return snapshot.Resources
                    .Where(item => item.Category == ResourceCategory.Element && item.FoodConversion > 0d)
                    .Sum(item => item.ProducedPerSecond.ToDouble() * item.Price.ToDouble()) / gdp;
            }

            if (metricId.StartsWith("technology.researched.", StringComparison.Ordinal))
            {
                var requestedId = metricId.Substring("technology.researched.".Length);
                var resolvedId = technologyAliases.TryGetValue(requestedId, out var mappedId) ? mappedId : requestedId;
                return researchedIds.Contains(resolvedId) ? 1d : 0d;
            }

            if (metricId == "technology.unresearchedPreviousCount")
            {
                var currentOrder = snapshot?.Eras.FirstOrDefault(item => item.Id == snapshot.CurrentEraId)?.Order ?? 0;
                return snapshot?.Technologies.Count(item =>
                {
                    var era = snapshot.Eras.FirstOrDefault(candidate => candidate.Id == item.EraId);
                    return !item.IsResearched && era != null && era.Order < currentOrder;
                }) ?? 0d;
            }

            if (metricId == "resource.surplusTypeCount")
            {
                return snapshot?.Resources.Count(item => item.Category == ResourceCategory.Element && item.NetPerSecond > CivicNumber.Zero) ?? 0d;
            }

            if (metricId == "resource.surplusProcessedTypeCount")
            {
                var processedIds = simulation.Data.Buildings
                    .Where(building => building.Inputs.Count > 0)
                    .SelectMany(building => building.Outputs)
                    .Select(output => output.ResourceId)
                    .ToHashSet(StringComparer.Ordinal);
                return snapshot?.Resources.Count(item => processedIds.Contains(item.Id) && item.NetPerSecond > CivicNumber.Zero) ?? 0d;
            }

            if (metricId == "snapshot.shortageResourceCount")
            {
                return snapshot?.Resources.Count(item => item.Category == ResourceCategory.Element && item.IsShortage) ?? 0d;
            }

            if (metricId.StartsWith("achievement.progress.", StringComparison.Ordinal))
            {
                return metaProgress?.HasAchievement(metricId.Substring("achievement.progress.".Length)) == true ? 1d : 0d;
            }

            if (metricId == "meta.prestigeCount") return metaProgress?.PrestigeCount ?? 0d;
            if (metricId == "meta.completedAchievementCount") return metaProgress?.CompletedAchievementIds.Count ?? 0d;
            if (metricId == "meta.eventDiscoveryCount") return metaProgress?.DiscoveredEventIds.Count ?? 0d;
            if (metricId == "meta.secretNationCount") return metaProgress?.FormedNationIds.Count(id => id.StartsWith("secret_", StringComparison.Ordinal)) ?? 0d;
            if (metricId == "meta.formedNationCount") return metaProgress?.FormedNationIds.Count ?? 0d;
            if (metricId == "meta.civilizationClassicalCount") return metaProgress?.CivilizationEraRecords.Count(item => item.HighestEraOrder >= 2) ?? 0d;
            return 0d;
        }

        private static bool IsBuildingInGroup(string buildingId, string group)
        {
            if (group == "mine") return buildingId.EndsWith("_mine", StringComparison.Ordinal) || buildingId == "quarry";
            if (group == "factory") return buildingId.EndsWith("_factory", StringComparison.Ordinal);
            if (group == "workshop") return buildingId.EndsWith("_workshop", StringComparison.Ordinal) || buildingId == "paper_mill";
            if (group == "production") return buildingId != "capital" && buildingId != "house" && buildingId != "construction_sector";
            return false;
        }
    }
}
