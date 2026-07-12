namespace Civic.Simulation
{
    public enum CivicBuildQuantityMode
    {
        One = 1,
        Five = 5,
        Ten = 10,
        TwentyFive = 25,
        Maximum = 0,
    }

    public sealed class CivicBuildQuote
    {
        public CivicBuildQuote(
            string buildingId,
            CivicBuildQuantityMode mode,
            int quantity,
            int maximumQuantity,
            CivicNumber unitCost,
            CivicNumber totalCost,
            bool canBuild,
            string blockReason)
        {
            BuildingId = buildingId ?? string.Empty;
            Mode = mode;
            Quantity = quantity;
            MaximumQuantity = maximumQuantity;
            UnitCost = unitCost;
            TotalCost = totalCost;
            CanBuild = canBuild;
            BlockReason = blockReason ?? string.Empty;
        }

        public string BuildingId { get; }
        public CivicBuildQuantityMode Mode { get; }
        public int Quantity { get; }
        public int MaximumQuantity { get; }
        public CivicNumber UnitCost { get; }
        public CivicNumber TotalCost { get; }
        public bool CanBuild { get; }
        public string BlockReason { get; }
    }
}
