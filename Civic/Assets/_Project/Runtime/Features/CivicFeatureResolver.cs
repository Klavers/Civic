using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Features
{
    public sealed class CivicFeatureResolution
    {
        private readonly HashSet<string> enabledFeatureLookup;
        private readonly HashSet<string> enabledIntegrationLookup;

        internal CivicFeatureResolution(
            IEnumerable<string> requestedIds,
            IEnumerable<string> enabledFeatureIds,
            IEnumerable<string> enabledIntegrationIds,
            IEnumerable<string> errors)
        {
            RequestedIds = Sort(requestedIds);
            EnabledFeatureIds = Sort(enabledFeatureIds);
            EnabledIntegrationIds = Sort(enabledIntegrationIds);
            Errors = (errors ?? Array.Empty<string>()).Distinct().OrderBy(value => value).ToArray();
            enabledFeatureLookup = new HashSet<string>(EnabledFeatureIds, StringComparer.Ordinal);
            enabledIntegrationLookup = new HashSet<string>(EnabledIntegrationIds, StringComparer.Ordinal);
        }

        public IReadOnlyList<string> RequestedIds { get; }
        public IReadOnlyList<string> EnabledFeatureIds { get; }
        public IReadOnlyList<string> EnabledIntegrationIds { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;

        public bool IsEnabled(string featureId) => enabledFeatureLookup.Contains(featureId);
        public bool IsIntegrationEnabled(string integrationId) => enabledIntegrationLookup.Contains(integrationId);

        private static IReadOnlyList<string> Sort(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static class CivicFeatureResolver
    {
        public static CivicFeatureResolution Resolve(
            IEnumerable<CivicFeatureDefinition> definitions,
            IEnumerable<CivicFeatureIntegrationDefinition> integrations,
            IEnumerable<string> requestedIds)
        {
            var featureList = (definitions ?? Array.Empty<CivicFeatureDefinition>()).ToArray();
            var integrationList = (integrations ?? Array.Empty<CivicFeatureIntegrationDefinition>()).ToArray();
            var requested = new HashSet<string>(requestedIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var errors = new List<string>();
            var definitionsById = new Dictionary<string, CivicFeatureDefinition>(StringComparer.Ordinal);

            foreach (var definition in featureList)
            {
                if (definition == null || !definitionsById.TryAdd(definition.Id, definition))
                {
                    errors.Add(definition == null
                        ? "기능 정의에 null 항목이 있습니다."
                        : $"기능 ID가 중복되었습니다: {definition.Id}");
                }
            }

            foreach (var requestedId in requested)
            {
                if (!definitionsById.ContainsKey(requestedId))
                {
                    errors.Add($"등록되지 않은 기능입니다: {requestedId}");
                }
            }

            var enabled = new HashSet<string>(requested.Where(definitionsById.ContainsKey), StringComparer.Ordinal);
            foreach (var featureId in enabled.OrderBy(value => value, StringComparer.Ordinal))
            {
                var definition = definitionsById[featureId];
                foreach (var requiredId in definition.RequiresAll)
                {
                    if (!definitionsById.ContainsKey(requiredId))
                    {
                        errors.Add($"{featureId}의 필수 기능이 등록되지 않았습니다: {requiredId}");
                    }
                    else if (!enabled.Contains(requiredId))
                    {
                        errors.Add($"{featureId}을(를) 사용하려면 {requiredId}이(가) 필요합니다.");
                    }
                }

                foreach (var conflictId in definition.ConflictsWith)
                {
                    if (!definitionsById.ContainsKey(conflictId))
                    {
                        errors.Add($"{featureId}의 충돌 기능이 등록되지 않았습니다: {conflictId}");
                    }
                    else if (enabled.Contains(conflictId))
                    {
                        errors.Add($"{featureId}과(와) {conflictId}은(는) 동시에 사용할 수 없습니다.");
                    }
                }
            }

            var integrationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var integration in integrationList)
            {
                if (integration == null)
                {
                    errors.Add("연계 기능 정의에 null 항목이 있습니다.");
                    continue;
                }

                if (!integrationIds.Add(integration.Id))
                {
                    errors.Add($"연계 기능 ID가 중복되었습니다: {integration.Id}");
                    continue;
                }

                foreach (var requiredId in integration.RequiresAll)
                {
                    if (!definitionsById.ContainsKey(requiredId))
                    {
                        errors.Add($"{integration.Id}의 필수 기능이 등록되지 않았습니다: {requiredId}");
                    }
                }
            }

            var activeIntegrations = errors.Count == 0
                ? integrationList
                    .Where(integration => integration != null && integration.RequiresAll.All(enabled.Contains))
                    .Select(integration => integration.Id)
                : Array.Empty<string>();

            return new CivicFeatureResolution(requested, enabled, activeIntegrations, errors);
        }

        public static CivicFeatureResolution Resolve(IEnumerable<string> requestedIds)
        {
            return Resolve(CivicFeatureRegistry.Features, CivicFeatureRegistry.Integrations, requestedIds);
        }
    }
}
