using System;

namespace Civic.Simulation.Modules
{
    public static class CivicConditionEvaluator
    {
        public static bool Compare(double current, string comparator, double expected)
        {
            if (comparator == ">=") return current >= expected;
            if (comparator == "<=") return current <= expected;
            if (comparator == "==") return Math.Abs(current - expected) <= 1e-9d;
            throw new ArgumentException($"Unsupported comparator: {comparator}", nameof(comparator));
        }
    }
}
