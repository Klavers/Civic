namespace Civic.Simulation.Modules
{
    public sealed class CivicMetricConditionSnapshot
    {
        public CivicMetricConditionSnapshot(
            string metricId,
            string comparator,
            double requiredValue,
            double currentValue,
            bool isSatisfied,
            string alternativeGroup = "",
            bool forbidden = false,
            double duration = 0d)
        {
            MetricId = metricId;
            Comparator = comparator;
            RequiredValue = requiredValue;
            CurrentValue = currentValue;
            IsSatisfied = isSatisfied;
            AlternativeGroup = alternativeGroup ?? string.Empty;
            Forbidden = forbidden;
            Duration = duration;
        }

        public string MetricId { get; }
        public string Comparator { get; }
        public double RequiredValue { get; }
        public double CurrentValue { get; }
        public bool IsSatisfied { get; }
        public string AlternativeGroup { get; }
        public bool Forbidden { get; }
        public double Duration { get; }
    }
}
