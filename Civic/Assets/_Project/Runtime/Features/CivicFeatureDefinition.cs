using System;
using System.Collections.Generic;
using System.Linq;

namespace Civic.Features
{
    public sealed class CivicFeatureDefinition
    {
        public CivicFeatureDefinition(
            string id,
            string displayName,
            string description,
            IEnumerable<string> requiresAll = null,
            IEnumerable<string> conflictsWith = null)
        {
            Id = RequireValue(id, nameof(id));
            DisplayName = RequireValue(displayName, nameof(displayName));
            Description = description ?? string.Empty;
            RequiresAll = Normalize(requiresAll);
            ConflictsWith = Normalize(conflictsWith);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> RequiresAll { get; }
        public IReadOnlyList<string> ConflictsWith { get; }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }

            return value;
        }
    }

    public sealed class CivicFeatureIntegrationDefinition
    {
        public CivicFeatureIntegrationDefinition(string id, IEnumerable<string> requiresAll)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Value cannot be empty.", nameof(id));
            }

            Id = id;
            RequiresAll = (requiresAll ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            if (RequiresAll.Count == 0)
            {
                throw new ArgumentException("An integration must require at least one feature.", nameof(requiresAll));
            }
        }

        public string Id { get; }
        public IReadOnlyList<string> RequiresAll { get; }
    }
}
