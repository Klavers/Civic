using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Features
{
    public sealed class CivicFeatureMatrixCase
    {
        public CivicFeatureMatrixCase(string name, IEnumerable<string> requestedIds)
        {
            Name = name;
            RequestedIds = (requestedIds ?? Array.Empty<string>()).ToArray();
        }

        public string Name { get; }
        public IReadOnlyList<string> RequestedIds { get; }
    }

    public static class CivicFeatureMatrix
    {
        public static IReadOnlyList<CivicFeatureMatrixCase> CreateDefaultCases()
        {
            var featureIds = CivicFeatureRegistry.Features.Select(feature => feature.Id).ToArray();
            var cases = new List<CivicFeatureMatrixCase>
            {
                new CivicFeatureMatrixCase("Baseline", Array.Empty<string>())
            };

            cases.AddRange(featureIds.Select(featureId =>
                new CivicFeatureMatrixCase("EachModule:" + featureId, new[] { featureId })));

            for (var left = 0; left < featureIds.Length; left++)
            {
                for (var right = left + 1; right < featureIds.Length; right++)
                {
                    cases.Add(new CivicFeatureMatrixCase(
                        $"Pairwise:{featureIds[left]}+{featureIds[right]}",
                        new[] { featureIds[left], featureIds[right] }));
                }
            }

            cases.Add(new CivicFeatureMatrixCase("AllOn", featureIds));
            return cases;
        }
    }
}
