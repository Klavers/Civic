using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Simulation
{
    public sealed class CivicGameState
    {
        private const double BaseTaxRate = 0.10d;

        public CivicGameState(CivicGameData data)
        {
            CurrentEraId = data.StartingEra.Id;
            TaxRate = BaseTaxRate;
            Resources = data.Resources.ToDictionary(resource => resource.Id, _ => CivicNumber.Zero, StringComparer.Ordinal);
            Buildings = data.Buildings.ToDictionary(building => building.Id, _ => 0, StringComparer.Ordinal);
            ResearchedTechnologyIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in data.InitialState.Resources)
            {
                Resources[pair.Key] = pair.Value;
            }

            foreach (var pair in data.InitialState.Buildings)
            {
                Buildings[pair.Key] = pair.Value;
            }
        }

        public Dictionary<string, CivicNumber> Resources { get; }
        public Dictionary<string, int> Buildings { get; }
        public HashSet<string> ResearchedTechnologyIds { get; }
        public string CurrentEraId { get; set; }
        public double TaxRate { get; set; }
    }

    public sealed class CivicGameSimulation
    {
        private const string PopulationId = "population";
        private const string FoodId = "food";
        private const string ScienceId = "science";
        private const string TreasuryId = "treasury";
        private const string ConstructionPowerId = "construction_power";

        private CivicGameSnapshot lastSnapshot;

        public CivicGameSimulation(CivicGameData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            State = new CivicGameState(data);
            lastSnapshot = CalculateSnapshot();
        }

        public CivicGameData Data { get; }
        public CivicGameState State { get; }

        public CivicGameSnapshot Snapshot => lastSnapshot;

        public CivicGameSnapshot Advance(double seconds)
        {
            if (seconds <= 0d)
            {
                return lastSnapshot;
            }

            var rates = CalculateRates();
            ApplyRates(rates, seconds);
            lastSnapshot = CalculateSnapshot(rates);
            return lastSnapshot;
        }

        public bool TryBuild(string buildingId)
        {
            if (!Data.BuildingsById.TryGetValue(buildingId, out var building) || !CanBuild(building))
            {
                return false;
            }

            State.Resources[ConstructionPowerId] = State.Resources[ConstructionPowerId] - building.ConstructionCost;
            State.Buildings[building.Id] = State.Buildings.TryGetValue(building.Id, out var count) ? count + 1 : 1;

            if (building.Role == BuildingRole.Housing)
            {
                foreach (var output in building.Outputs)
                {
                    AddResource(output.ResourceId, output.Amount);
                }
            }

            lastSnapshot = CalculateSnapshot();
            return true;
        }

        public bool TryResearch(string technologyId)
        {
            if (!Data.TechnologiesById.TryGetValue(technologyId, out var technology) || !CanResearch(technology))
            {
                return false;
            }

            State.Resources[ScienceId] = State.Resources[ScienceId] - technology.Cost;
            State.ResearchedTechnologyIds.Add(technology.Id);
            State.TaxRate += technology.TaxRateAdd;
            if (!string.IsNullOrEmpty(technology.UnlocksEraId))
            {
                State.CurrentEraId = technology.UnlocksEraId;
            }

            lastSnapshot = CalculateSnapshot();
            return true;
        }

        public BuildingDefinition FirstBuildableBuilding()
        {
            return Data.Buildings.FirstOrDefault(CanBuild);
        }

        public TechnologyDefinition FirstResearchableTechnology()
        {
            return Data.Technologies.FirstOrDefault(CanResearch);
        }

        public bool CanBuild(BuildingDefinition building)
        {
            if (building == null || !building.IsBuildable)
            {
                return false;
            }

            if (!IsBuildingUnlocked(building))
            {
                return false;
            }

            if (State.Resources[ConstructionPowerId] < building.ConstructionCost)
            {
                return false;
            }

            return !IsBlockedByPopulationLimit(building);
        }

        public bool CanResearch(TechnologyDefinition technology)
        {
            if (technology == null || State.ResearchedTechnologyIds.Contains(technology.Id))
            {
                return false;
            }

            if (State.Resources[ScienceId] < technology.Cost)
            {
                return false;
            }

            return technology.PrerequisiteTechnologyIds.All(State.ResearchedTechnologyIds.Contains);
        }

        public CivicResourceProjection PreviewBuildResourceChange(string buildingId, string resourceId)
        {
            if (!Data.BuildingsById.TryGetValue(buildingId, out var building))
            {
                return CivicResourceProjection.Empty(resourceId);
            }

            var before = CalculateSnapshot();
            State.Buildings[building.Id] = State.Buildings.TryGetValue(building.Id, out var count) ? count + 1 : 1;
            var after = CalculateSnapshot();
            State.Buildings[building.Id] = count;

            var beforeResource = before.Resources.FirstOrDefault(resource => resource.Id == resourceId);
            var afterResource = after.Resources.FirstOrDefault(resource => resource.Id == resourceId);
            return new CivicResourceProjection(
                resourceId,
                (afterResource?.ProducedPerSecond ?? CivicNumber.Zero) - (beforeResource?.ProducedPerSecond ?? CivicNumber.Zero),
                (afterResource?.ConsumedPerSecond ?? CivicNumber.Zero) - (beforeResource?.ConsumedPerSecond ?? CivicNumber.Zero),
                (afterResource?.SupplyRate ?? 1d) - (beforeResource?.SupplyRate ?? 1d));
        }

        private CivicRateSet CalculateRates()
        {
            var normalDemand = EmptyResourceNumberMap();
            var normalOutput = EmptyResourceNumberMap();
            foreach (var building in ActiveTickBuildings())
            {
                var count = GetBuildingCount(building);
                foreach (var input in building.Inputs)
                {
                    normalDemand[input.ResourceId] = normalDemand[input.ResourceId] + input.Amount * count;
                }

                foreach (var output in building.Outputs)
                {
                    normalOutput[output.ResourceId] = normalOutput[output.ResourceId] + output.Amount * count;
                }
            }

            var supplyRates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var resource in Data.Resources)
            {
                var demand = normalDemand[resource.Id];
                if (demand <= CivicNumber.Zero)
                {
                    supplyRates[resource.Id] = 1d;
                    continue;
                }

                var available = normalOutput[resource.Id] + GetResource(resource.Id);
                supplyRates[resource.Id] = Math.Min(1d, Math.Max(0d, (available / demand).ToDouble()));
            }

            var produced = EmptyResourceNumberMap();
            var consumed = EmptyResourceNumberMap();
            var buildingEfficiencies = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var building in ActiveTickBuildings())
            {
                var efficiency = CalculateMaterialEfficiency(building, supplyRates);
                buildingEfficiencies[building.Id] = efficiency;
                var count = GetBuildingCount(building);
                foreach (var input in building.Inputs)
                {
                    consumed[input.ResourceId] = consumed[input.ResourceId] + input.Amount * count * efficiency;
                }

                foreach (var output in building.Outputs)
                {
                    produced[output.ResourceId] = produced[output.ResourceId] + output.Amount * count * efficiency;
                }
            }

            produced[ScienceId] = produced[ScienceId] + GetPopulation();

            var prices = CalculatePrices(normalDemand, produced);
            var gdp = CalculateGdp(produced, prices);
            var treasuryIncome = produced[TreasuryId] + gdp * State.TaxRate;
            var treasurySpend = CalculateConstructionTreasurySpend(buildingEfficiencies);

            if (treasurySpend > CivicNumber.Zero)
            {
                var affordableRatio = Math.Min(1d, ((GetResource(TreasuryId) + treasuryIncome) / treasurySpend).ToDouble());
                if (affordableRatio < 1d)
                {
                    RecalculateConstructionForTreasury(affordableRatio, produced, consumed, buildingEfficiencies);
                    prices = CalculatePrices(normalDemand, produced);
                    gdp = CalculateGdp(produced, prices);
                    treasuryIncome = produced[TreasuryId] + gdp * State.TaxRate;
                    treasurySpend = treasurySpend * affordableRatio;
                }
            }

            return new CivicRateSet(normalDemand, produced, consumed, prices, supplyRates, buildingEfficiencies, gdp, treasuryIncome, treasurySpend);
        }

        private void ApplyRates(CivicRateSet rates, double seconds)
        {
            foreach (var resource in Data.Resources)
            {
                if (resource.Id == TreasuryId)
                {
                    continue;
                }

                var delta = (rates.Produced[resource.Id] - rates.Consumed[resource.Id]) * seconds;
                State.Resources[resource.Id] = CivicNumber.ClampMinZero(GetResource(resource.Id) + delta);
            }

            var treasuryDelta = (rates.TreasuryIncome - rates.TreasurySpend) * seconds;
            State.Resources[TreasuryId] = CivicNumber.ClampMinZero(GetResource(TreasuryId) + treasuryDelta);
        }

        private CivicGameSnapshot CalculateSnapshot()
        {
            return CalculateSnapshot(CalculateRates());
        }

        private CivicGameSnapshot CalculateSnapshot(CivicRateSet rates)
        {
            var resources = new List<CivicResourceSnapshot>();
            foreach (var resource in Data.Resources)
            {
                var stockpile = resource.Id == FoodId ? CalculateFoodStockpile() : GetResource(resource.Id);
                var produced = resource.Id == FoodId
                    ? CalculateFoodProduced(rates.Produced)
                    : resource.Id == TreasuryId
                        ? rates.TreasuryIncome
                        : rates.Produced[resource.Id];
                var consumed = resource.Id == FoodId
                    ? CivicNumber.Zero
                    : resource.Id == TreasuryId
                        ? rates.TreasurySpend
                        : rates.Consumed[resource.Id];
                var net = produced - consumed;
                var price = rates.Prices.TryGetValue(resource.Id, out var currentPrice) ? currentPrice : CivicNumber.Zero;
                var basePrice = resource.BasePrice;
                var priceMultiplier = basePrice > CivicNumber.Zero ? (price / basePrice).ToDouble() : 0d;
                var supplyRate = rates.SupplyRates.TryGetValue(resource.Id, out var currentSupplyRate) ? currentSupplyRate : 1d;
                var supplyDemandRatio = CalculateSupplyDemandRatio(rates.Demand[resource.Id], rates.Produced[resource.Id]);
                resources.Add(new CivicResourceSnapshot(
                    resource.Id,
                    resource.DisplayNameKo,
                    resource.Category,
                    resource.FoodConversion,
                    BuildResourceFlows(resource.Id, true, rates),
                    BuildResourceFlows(resource.Id, false, rates),
                    BuildBuildProjections(resource.Id),
                    stockpile,
                    produced,
                    consumed,
                    net,
                    price,
                    priceMultiplier,
                    supplyDemandRatio,
                    supplyRate,
                    supplyRate < 0.999d));
            }

            var currentEra = Data.ErasById.TryGetValue(State.CurrentEraId, out var era) ? era.DisplayNameKo : State.CurrentEraId;
            return new CivicGameSnapshot(
                resources.OrderBy(resource => Data.ResourcesById[resource.Id].SortOrder).ToArray(),
                Data.Buildings.Select(building => BuildBuildingSnapshot(building, rates.Gdp)).OrderBy(building => Data.BuildingsById[building.Id].SortOrder).ToArray(),
                Data.Technologies.Select(BuildTechnologySnapshot).OrderBy(technology => Data.TechnologiesById[technology.Id].SortOrder).ToArray(),
                currentEra,
                GetPopulation(),
                GetUsedPopulation(),
                rates.Gdp,
                GetResource(TreasuryId),
                GetResource(ConstructionPowerId),
                GetResource(ScienceId),
                resources.Any(resource => resource.IsShortage),
                Data.Technologies.Any(CanResearch),
                Data.Buildings.Any(building =>
                    building.IsBuildable &&
                    IsBuildingUnlocked(building) &&
                    State.Resources[ConstructionPowerId] >= building.ConstructionCost &&
                    IsBlockedByPopulationLimit(building)));
        }

        private CivicBuildingSnapshot BuildBuildingSnapshot(BuildingDefinition building, CivicNumber currentGdp)
        {
            return new CivicBuildingSnapshot(
                building.Id,
                building.DisplayNameKo,
                building.IsBuildable,
                GetBuildingCount(building),
                CanBuild(building),
                GetBuildBlockReason(building),
                building.ConstructionCost,
                BuildBuildingResourceDeltas(building),
                PreviewGdpDelta(building, currentGdp));
        }

        private IReadOnlyList<CivicBuildingResourceDeltaSnapshot> BuildBuildingResourceDeltas(BuildingDefinition building)
        {
            var deltas = new Dictionary<string, CivicNumber>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (var input in building.Inputs)
            {
                AddDelta(deltas, order, input.ResourceId, CivicNumber.Zero - input.Amount);
            }

            if (building.TreasuryCost > CivicNumber.Zero)
            {
                AddDelta(deltas, order, TreasuryId, CivicNumber.Zero - building.TreasuryCost);
            }

            foreach (var output in building.Outputs)
            {
                AddDelta(deltas, order, output.ResourceId, output.Amount);
            }

            return order
                .Where(resourceId => deltas[resourceId] > CivicNumber.Zero || deltas[resourceId] < CivicNumber.Zero)
                .Select(resourceId =>
                {
                    var displayName = Data.ResourcesById.TryGetValue(resourceId, out var resource)
                        ? resource.DisplayNameKo
                        : resourceId;
                    return new CivicBuildingResourceDeltaSnapshot(resourceId, displayName, deltas[resourceId]);
                })
                .ToArray();
        }

        private CivicNumber PreviewGdpDelta(BuildingDefinition building, CivicNumber currentGdp)
        {
            State.Buildings[building.Id] = State.Buildings.TryGetValue(building.Id, out var count) ? count + 1 : 1;
            var projectedGdp = CalculateRates().Gdp;
            State.Buildings[building.Id] = count;
            return projectedGdp - currentGdp;
        }

        private static void AddDelta(IDictionary<string, CivicNumber> deltas, ICollection<string> order, string resourceId, CivicNumber amount)
        {
            if (!deltas.ContainsKey(resourceId))
            {
                deltas[resourceId] = CivicNumber.Zero;
                order.Add(resourceId);
            }

            deltas[resourceId] = deltas[resourceId] + amount;
        }

        private IReadOnlyList<CivicResourceFlowSnapshot> BuildResourceFlows(string resourceId, bool outputs, CivicRateSet rates)
        {
            var flows = new List<CivicResourceFlowSnapshot>();
            foreach (var building in ActiveTickBuildings())
            {
                var count = GetBuildingCount(building);
                var perBuilding = SumAmounts(outputs ? building.Outputs : building.Inputs, resourceId);
                if (perBuilding <= CivicNumber.Zero)
                {
                    continue;
                }

                var efficiency = rates.BuildingEfficiencies.TryGetValue(building.Id, out var currentEfficiency)
                    ? currentEfficiency
                    : 1d;
                flows.Add(new CivicResourceFlowSnapshot(
                    building.Id,
                    building.DisplayNameKo,
                    count,
                    perBuilding * count * efficiency));
            }

            return flows;
        }

        private IReadOnlyList<CivicResourceBuildProjectionSnapshot> BuildBuildProjections(string resourceId)
        {
            var projections = new List<CivicResourceBuildProjectionSnapshot>();
            foreach (var building in Data.Buildings.Where(building => building.IsBuildable && IsBuildingUnlocked(building)))
            {
                var producedDelta = SumAmounts(building.Outputs, resourceId);
                var consumedDelta = SumAmounts(building.Inputs, resourceId);
                if (producedDelta <= CivicNumber.Zero && consumedDelta <= CivicNumber.Zero)
                {
                    continue;
                }

                projections.Add(new CivicResourceBuildProjectionSnapshot(
                    building.Id,
                    building.DisplayNameKo,
                    producedDelta,
                    consumedDelta,
                    CanBuild(building),
                    GetBuildBlockReason(building)));
            }

            return projections;
        }

        private CivicTechnologySnapshot BuildTechnologySnapshot(TechnologyDefinition technology)
        {
            return new CivicTechnologySnapshot(technology.Id, technology.DisplayNameKo, technology.Cost, State.ResearchedTechnologyIds.Contains(technology.Id), CanResearch(technology), technology.EraId);
        }

        private Dictionary<string, CivicNumber> CalculatePrices(IReadOnlyDictionary<string, CivicNumber> demand, IReadOnlyDictionary<string, CivicNumber> produced)
        {
            var prices = new Dictionary<string, CivicNumber>(StringComparer.Ordinal);
            foreach (var resource in Data.Resources)
            {
                if (resource.Category != ResourceCategory.Element)
                {
                    prices[resource.Id] = CivicNumber.Zero;
                    continue;
                }

                var demandValue = demand[resource.Id];
                if (demandValue <= CivicNumber.Zero)
                {
                    prices[resource.Id] = resource.BasePrice * 0.125d;
                    continue;
                }

                var ratio = (produced[resource.Id] / demandValue).ToDouble();
                var multiplier = InterpolatePriceMultiplier(ratio);
                prices[resource.Id] = resource.BasePrice * multiplier;
            }

            return prices;
        }

        private CivicNumber CalculateGdp(IReadOnlyDictionary<string, CivicNumber> produced, IReadOnlyDictionary<string, CivicNumber> prices)
        {
            var gdp = CivicNumber.Zero;
            foreach (var resource in Data.Resources.Where(resource => resource.Category == ResourceCategory.Element))
            {
                gdp += produced[resource.Id] * prices[resource.Id];
            }

            return gdp;
        }

        private static double CalculateSupplyDemandRatio(CivicNumber demand, CivicNumber produced)
        {
            if (demand > CivicNumber.Zero)
            {
                return (produced / demand).ToDouble();
            }

            return produced > CivicNumber.Zero ? double.PositiveInfinity : 1d;
        }

        private CivicNumber CalculateConstructionTreasurySpend(IReadOnlyDictionary<string, double> buildingEfficiencies)
        {
            var spend = CivicNumber.Zero;
            foreach (var building in Data.Buildings.Where(building => building.Role == BuildingRole.Construction))
            {
                var count = GetBuildingCount(building);
                if (count <= 0)
                {
                    continue;
                }

                var efficiency = buildingEfficiencies.TryGetValue(building.Id, out var currentEfficiency)
                    ? currentEfficiency
                    : 0d;
                spend += building.TreasuryCost * count * efficiency;
            }

            return spend;
        }

        private void RecalculateConstructionForTreasury(double affordableRatio, IDictionary<string, CivicNumber> produced, IDictionary<string, CivicNumber> consumed, IReadOnlyDictionary<string, double> buildingEfficiencies)
        {
            foreach (var building in Data.Buildings.Where(building => building.Role == BuildingRole.Construction))
            {
                var count = GetBuildingCount(building);
                if (count <= 0 || !buildingEfficiencies.TryGetValue(building.Id, out var originalEfficiency))
                {
                    continue;
                }

                var removedRatio = originalEfficiency * (1d - affordableRatio);
                foreach (var input in building.Inputs)
                {
                    consumed[input.ResourceId] = consumed[input.ResourceId] - input.Amount * count * removedRatio;
                }

                foreach (var output in building.Outputs)
                {
                    produced[output.ResourceId] = produced[output.ResourceId] - output.Amount * count * removedRatio;
                }
            }
        }

        private static double InterpolatePriceMultiplier(double ratio)
        {
            if (ratio <= 0.25d)
            {
                return 2d;
            }

            if (ratio <= 1d)
            {
                return Lerp(2d, 1d, (ratio - 0.25d) / 0.75d);
            }

            if (ratio >= 4d)
            {
                return 0.125d;
            }

            return Lerp(1d, 0.125d, (ratio - 1d) / 3d);
        }

        private static double Lerp(double from, double to, double t)
        {
            return from + (to - from) * Math.Max(0d, Math.Min(1d, t));
        }

        private double CalculateMaterialEfficiency(BuildingDefinition building, IReadOnlyDictionary<string, double> supplyRates)
        {
            if (building.Inputs.Count == 0)
            {
                return 1d;
            }

            return building.Inputs.Select(input => supplyRates[input.ResourceId]).Average();
        }

        private IEnumerable<BuildingDefinition> ActiveTickBuildings()
        {
            return Data.Buildings.Where(building => building.Role != BuildingRole.Housing && GetBuildingCount(building) > 0);
        }

        private Dictionary<string, CivicNumber> EmptyResourceNumberMap()
        {
            return Data.Resources.ToDictionary(resource => resource.Id, _ => CivicNumber.Zero, StringComparer.Ordinal);
        }

        private CivicNumber CalculateFoodStockpile()
        {
            var food = CivicNumber.Zero;
            foreach (var resource in Data.Resources.Where(resource => resource.Category == ResourceCategory.Element && resource.FoodConversion > 0d))
            {
                food += GetResource(resource.Id) * resource.FoodConversion;
            }

            return food;
        }

        private CivicNumber CalculateFoodProduced(IReadOnlyDictionary<string, CivicNumber> produced)
        {
            var food = CivicNumber.Zero;
            foreach (var resource in Data.Resources.Where(resource => resource.Category == ResourceCategory.Element && resource.FoodConversion > 0d))
            {
                food += produced[resource.Id] * resource.FoodConversion;
            }

            return food;
        }

        private bool IsBuildingUnlocked(BuildingDefinition building)
        {
            return string.IsNullOrEmpty(building.UnlockedByTechnologyId) || State.ResearchedTechnologyIds.Contains(building.UnlockedByTechnologyId);
        }

        private string GetBuildBlockReason(BuildingDefinition building)
        {
            if (!building.IsBuildable)
            {
                return "건설 불가";
            }

            if (!IsBuildingUnlocked(building))
            {
                return "기술 필요";
            }

            if (State.Resources[ConstructionPowerId] < building.ConstructionCost)
            {
                return "건설력 부족";
            }

            if (IsBlockedByPopulationLimit(building))
            {
                return "인구 한도";
            }

            return string.Empty;
        }

        private CivicNumber GetPopulation()
        {
            return GetResource(PopulationId);
        }

        private bool IsBlockedByPopulationLimit(BuildingDefinition building)
        {
            return !ProducesPopulation(building) && GetUsedPopulation() >= GetPopulation();
        }

        private static bool ProducesPopulation(BuildingDefinition building)
        {
            return SumAmounts(building.Outputs, PopulationId) > CivicNumber.Zero;
        }

        private CivicNumber GetUsedPopulation()
        {
            var used = 0;
            foreach (var building in Data.Buildings)
            {
                used += GetBuildingCount(building) * building.PopulationUse;
            }

            return CivicNumber.FromDouble(used);
        }

        private int GetBuildingCount(BuildingDefinition building)
        {
            return State.Buildings.TryGetValue(building.Id, out var count) ? count : 0;
        }

        private CivicNumber GetResource(string id)
        {
            return State.Resources.TryGetValue(id, out var value) ? value : CivicNumber.Zero;
        }

        private void AddResource(string id, CivicNumber amount)
        {
            State.Resources[id] = GetResource(id) + amount;
        }

        private static CivicNumber SumAmounts(IEnumerable<ResourceAmount> amounts, string resourceId)
        {
            var total = CivicNumber.Zero;
            foreach (var amount in amounts.Where(amount => amount.ResourceId == resourceId))
            {
                total += amount.Amount;
            }

            return total;
        }
    }

    public sealed class CivicRateSet
    {
        public CivicRateSet(
            IReadOnlyDictionary<string, CivicNumber> demand,
            IReadOnlyDictionary<string, CivicNumber> produced,
            IReadOnlyDictionary<string, CivicNumber> consumed,
            IReadOnlyDictionary<string, CivicNumber> prices,
            IReadOnlyDictionary<string, double> supplyRates,
            IReadOnlyDictionary<string, double> buildingEfficiencies,
            CivicNumber gdp,
            CivicNumber treasuryIncome,
            CivicNumber treasurySpend)
        {
            Demand = demand;
            Produced = produced;
            Consumed = consumed;
            Prices = prices;
            SupplyRates = supplyRates;
            BuildingEfficiencies = buildingEfficiencies;
            Gdp = gdp;
            TreasuryIncome = treasuryIncome;
            TreasurySpend = treasurySpend;
        }

        public IReadOnlyDictionary<string, CivicNumber> Demand { get; }
        public IReadOnlyDictionary<string, CivicNumber> Produced { get; }
        public IReadOnlyDictionary<string, CivicNumber> Consumed { get; }
        public IReadOnlyDictionary<string, CivicNumber> Prices { get; }
        public IReadOnlyDictionary<string, double> SupplyRates { get; }
        public IReadOnlyDictionary<string, double> BuildingEfficiencies { get; }
        public CivicNumber Gdp { get; }
        public CivicNumber TreasuryIncome { get; }
        public CivicNumber TreasurySpend { get; }
    }

    public sealed class CivicGameSnapshot
    {
        public CivicGameSnapshot(
            IReadOnlyList<CivicResourceSnapshot> resources,
            IReadOnlyList<CivicBuildingSnapshot> buildings,
            IReadOnlyList<CivicTechnologySnapshot> technologies,
            string currentEraName,
            CivicNumber population,
            CivicNumber usedPopulation,
            CivicNumber gdp,
            CivicNumber treasury,
            CivicNumber constructionPower,
            CivicNumber science,
            bool hasShortage,
            bool hasResearchAvailable,
            bool hasConstructionBlocked)
        {
            Resources = resources;
            Buildings = buildings;
            Technologies = technologies;
            CurrentEraName = currentEraName;
            Population = population;
            UsedPopulation = usedPopulation;
            Gdp = gdp;
            Treasury = treasury;
            ConstructionPower = constructionPower;
            Science = science;
            HasShortage = hasShortage;
            HasResearchAvailable = hasResearchAvailable;
            HasConstructionBlocked = hasConstructionBlocked;
        }

        public IReadOnlyList<CivicResourceSnapshot> Resources { get; }
        public IReadOnlyList<CivicBuildingSnapshot> Buildings { get; }
        public IReadOnlyList<CivicTechnologySnapshot> Technologies { get; }
        public string CurrentEraName { get; }
        public CivicNumber Population { get; }
        public CivicNumber UsedPopulation { get; }
        public CivicNumber Gdp { get; }
        public CivicNumber Treasury { get; }
        public CivicNumber ConstructionPower { get; }
        public CivicNumber Science { get; }
        public bool HasShortage { get; }
        public bool HasResearchAvailable { get; }
        public bool HasConstructionBlocked { get; }
    }

    public sealed class CivicResourceSnapshot
    {
        public CivicResourceSnapshot(
            string id,
            string displayNameKo,
            ResourceCategory category,
            double foodConversion,
            IReadOnlyList<CivicResourceFlowSnapshot> producers,
            IReadOnlyList<CivicResourceFlowSnapshot> consumers,
            IReadOnlyList<CivicResourceBuildProjectionSnapshot> buildProjections,
            CivicNumber stockpile,
            CivicNumber producedPerSecond,
            CivicNumber consumedPerSecond,
            CivicNumber netPerSecond,
            CivicNumber price,
            double priceMultiplier,
            double supplyDemandRatio,
            double supplyRate,
            bool isShortage)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            Category = category;
            FoodConversion = foodConversion;
            Producers = producers;
            Consumers = consumers;
            BuildProjections = buildProjections;
            Stockpile = stockpile;
            ProducedPerSecond = producedPerSecond;
            ConsumedPerSecond = consumedPerSecond;
            NetPerSecond = netPerSecond;
            Price = price;
            PriceMultiplier = priceMultiplier;
            SupplyDemandRatio = supplyDemandRatio;
            SupplyRate = supplyRate;
            IsShortage = isShortage;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public ResourceCategory Category { get; }
        public double FoodConversion { get; }
        public IReadOnlyList<CivicResourceFlowSnapshot> Producers { get; }
        public IReadOnlyList<CivicResourceFlowSnapshot> Consumers { get; }
        public IReadOnlyList<CivicResourceBuildProjectionSnapshot> BuildProjections { get; }
        public CivicNumber Stockpile { get; }
        public CivicNumber ProducedPerSecond { get; }
        public CivicNumber ConsumedPerSecond { get; }
        public CivicNumber NetPerSecond { get; }
        public CivicNumber Price { get; }
        public double PriceMultiplier { get; }
        public double SupplyDemandRatio { get; }
        public double SupplyRate { get; }
        public bool IsShortage { get; }
    }

    public sealed class CivicResourceFlowSnapshot
    {
        public CivicResourceFlowSnapshot(string buildingId, string buildingDisplayNameKo, int buildingCount, CivicNumber amountPerSecond)
        {
            BuildingId = buildingId;
            BuildingDisplayNameKo = buildingDisplayNameKo;
            BuildingCount = buildingCount;
            AmountPerSecond = amountPerSecond;
        }

        public string BuildingId { get; }
        public string BuildingDisplayNameKo { get; }
        public int BuildingCount { get; }
        public CivicNumber AmountPerSecond { get; }
    }

    public sealed class CivicResourceBuildProjectionSnapshot
    {
        public CivicResourceBuildProjectionSnapshot(string buildingId, string buildingDisplayNameKo, CivicNumber producedDelta, CivicNumber consumedDelta, bool canBuild, string blockReason)
        {
            BuildingId = buildingId;
            BuildingDisplayNameKo = buildingDisplayNameKo;
            ProducedDelta = producedDelta;
            ConsumedDelta = consumedDelta;
            CanBuild = canBuild;
            BlockReason = blockReason;
        }

        public string BuildingId { get; }
        public string BuildingDisplayNameKo { get; }
        public CivicNumber ProducedDelta { get; }
        public CivicNumber ConsumedDelta { get; }
        public bool CanBuild { get; }
        public string BlockReason { get; }
    }

    public sealed class CivicBuildingSnapshot
    {
        public CivicBuildingSnapshot(
            string id,
            string displayNameKo,
            bool isBuildable,
            int count,
            bool canBuild,
            string blockReason,
            CivicNumber constructionCost,
            IReadOnlyList<CivicBuildingResourceDeltaSnapshot> resourceDeltas,
            CivicNumber gdpDelta)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            IsBuildable = isBuildable;
            Count = count;
            CanBuild = canBuild;
            BlockReason = blockReason;
            ConstructionCost = constructionCost;
            ResourceDeltas = resourceDeltas;
            GdpDelta = gdpDelta;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public bool IsBuildable { get; }
        public int Count { get; }
        public bool CanBuild { get; }
        public string BlockReason { get; }
        public CivicNumber ConstructionCost { get; }
        public IReadOnlyList<CivicBuildingResourceDeltaSnapshot> ResourceDeltas { get; }
        public CivicNumber GdpDelta { get; }
    }

    public sealed class CivicBuildingResourceDeltaSnapshot
    {
        public CivicBuildingResourceDeltaSnapshot(string resourceId, string resourceDisplayNameKo, CivicNumber amountPerSecond)
        {
            ResourceId = resourceId;
            ResourceDisplayNameKo = resourceDisplayNameKo;
            AmountPerSecond = amountPerSecond;
        }

        public string ResourceId { get; }
        public string ResourceDisplayNameKo { get; }
        public CivicNumber AmountPerSecond { get; }
    }

    public sealed class CivicTechnologySnapshot
    {
        public CivicTechnologySnapshot(string id, string displayNameKo, CivicNumber cost, bool isResearched, bool canResearch, string eraId)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            Cost = cost;
            IsResearched = isResearched;
            CanResearch = canResearch;
            EraId = eraId;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public CivicNumber Cost { get; }
        public bool IsResearched { get; }
        public bool CanResearch { get; }
        public string EraId { get; }
    }

    public readonly struct CivicResourceProjection
    {
        public CivicResourceProjection(string resourceId, CivicNumber producedDelta, CivicNumber consumedDelta, double supplyRateDelta)
        {
            ResourceId = resourceId;
            ProducedDelta = producedDelta;
            ConsumedDelta = consumedDelta;
            SupplyRateDelta = supplyRateDelta;
        }

        public string ResourceId { get; }
        public CivicNumber ProducedDelta { get; }
        public CivicNumber ConsumedDelta { get; }
        public double SupplyRateDelta { get; }

        public static CivicResourceProjection Empty(string resourceId)
        {
            return new CivicResourceProjection(resourceId, CivicNumber.Zero, CivicNumber.Zero, 0d);
        }
    }
}
