using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicCivilizationDefinition
    {
        public CivicCivilizationDefinition(string id, string displayNameKo, string conceptId, string difficulty, string descriptionKo, IReadOnlyList<string> requiredFeatureIds)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            ConceptId = conceptId;
            Difficulty = difficulty;
            DescriptionKo = descriptionKo;
            RequiredFeatureIds = requiredFeatureIds;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string ConceptId { get; }
        public string Difficulty { get; }
        public string DescriptionKo { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
    }

    public sealed class CivicCivilizationEffectDefinition
    {
        public CivicCivilizationEffectDefinition(string civilizationId, string effectType, string targetId, double amount, string capGroup, double duration = 0d, string runtimeEffectType = "", string runtimeTargetId = "")
        {
            CivilizationId = civilizationId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CapGroup = capGroup;
            Duration = duration;
            RuntimeEffectType = runtimeEffectType;
            RuntimeTargetId = runtimeTargetId;
        }

        public string CivilizationId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CapGroup { get; }
        public double Duration { get; }
        public string RuntimeEffectType { get; }
        public string RuntimeTargetId { get; }
        public CivicResolvedModuleEffect Resolve() => CivicProvisionalEffect.Resolve(EffectType, TargetId, RuntimeEffectType, RuntimeTargetId, Amount, Duration, CapGroup);
    }

    public sealed class CivicCivilizationStartDefinition
    {
        public CivicCivilizationStartDefinition(string civilizationId, string kind, string targetId, double amount)
        {
            CivilizationId = civilizationId;
            Kind = kind;
            TargetId = targetId;
            Amount = amount;
        }

        public string CivilizationId { get; }
        public string Kind { get; }
        public string TargetId { get; }
        public double Amount { get; }
    }

    public sealed class CivicCivilizationContent
    {
        public CivicCivilizationContent(
            IReadOnlyList<CivicCivilizationDefinition> civilizations,
            IReadOnlyList<CivicCivilizationEffectDefinition> effects,
            IReadOnlyList<CivicCivilizationStartDefinition> starts)
        {
            Civilizations = civilizations;
            Effects = effects;
            Starts = starts;
        }

        public IReadOnlyList<CivicCivilizationDefinition> Civilizations { get; }
        public IReadOnlyList<CivicCivilizationEffectDefinition> Effects { get; }
        public IReadOnlyList<CivicCivilizationStartDefinition> Starts { get; }
    }

    public static class CivicCivilizationContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicCivilizationContent LoadFromResources()
        {
            var definitions = Resources.Load<TextAsset>(Root + "civilizations");
            var effects = Resources.Load<TextAsset>(Root + "civilization_effects");
            var starts = Resources.Load<TextAsset>(Root + "civilization_start");
            if (definitions == null || effects == null || starts == null)
            {
                throw new CivicDataException(new[] { "Civilization CSV resources are missing." });
            }

            return Parse(definitions.text, effects.text, starts.text);
        }

        public static CivicCivilizationContent Parse(string definitionsCsv, string effectsCsv, string startsCsv)
        {
            var errors = new List<string>();
            var definitions = CivicCsvParser.Parse(definitionsCsv, errors, "civilizations.csv")
                .Select(row => new CivicCivilizationDefinition(
                    Value(row, "id"),
                    Value(row, "displayNameKo"),
                    Value(row, "conceptId"),
                    Value(row, "difficulty"),
                    Value(row, "descriptionKo"),
                    SplitIds(Value(row, "requiredFeatureIds"))))
                .ToArray();
            var effectDefinitions = CivicCsvParser.Parse(effectsCsv, errors, "civilization_effects.csv")
                .Select(row => new CivicCivilizationEffectDefinition(
                    Value(row, "civilizationId"),
                    Value(row, "effectType"),
                    Value(row, "targetId"),
                    Number(row, "amount", errors, "civilization_effects.csv"),
                    Value(row, "capGroup"),
                    OptionalNumber(row, "duration", errors, "civilization_effects.csv"),
                    Value(row, "runtimeEffectType"),
                    Value(row, "runtimeTargetId")))
                .ToArray();
            var startDefinitions = CivicCsvParser.Parse(startsCsv, errors, "civilization_start.csv")
                .Select(row => new CivicCivilizationStartDefinition(
                    Value(row, "civilizationId"),
                    Value(row, "kind"),
                    Value(row, "targetId"),
                    Number(row, "amount", errors, "civilization_start.csv")))
                .ToArray();

            var ids = definitions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in definitions.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate civilization ID: {duplicate.Key}");
            }

            foreach (var reference in effectDefinitions.Select(item => item.CivilizationId).Concat(startDefinitions.Select(item => item.CivilizationId)))
            {
                if (!ids.Contains(reference))
                {
                    errors.Add($"Unknown civilization reference: {reference}");
                }
            }

            if (!ids.Contains(CivicCivilizationModule.DefaultCivilizationId))
            {
                errors.Add("The default civilization definition is missing.");
            }

            if (errors.Count > 0)
            {
                throw new CivicDataException(errors.ToArray());
            }

            return new CivicCivilizationContent(definitions, effectDefinitions, startDefinitions);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }

        private static double OptionalNumber(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => string.IsNullOrEmpty(Value(row, key)) ? 0d : Number(row, key, errors, source);

        private static IReadOnlyList<string> SplitIds(string value) => value
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
