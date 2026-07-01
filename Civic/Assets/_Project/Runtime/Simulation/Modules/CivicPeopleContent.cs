using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicPersonPositionDefinition
    {
        public CivicPersonPositionDefinition(string id, string displayNameKo, string descriptionKo, int order, IReadOnlyList<string> requiredFeatureIds)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            DescriptionKo = descriptionKo;
            Order = order;
            RequiredFeatureIds = requiredFeatureIds;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string DescriptionKo { get; }
        public int Order { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
    }

    public sealed class CivicPersonDefinition
    {
        public CivicPersonDefinition(string id, string displayNameKo, string archetypeId, string rarity, string portraitId, double baseTenure, IReadOnlyList<string> allowedPositionIds, IReadOnlyList<string> requiredFeatureIds)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            ArchetypeId = archetypeId;
            Rarity = rarity;
            PortraitId = portraitId;
            BaseTenure = baseTenure;
            AllowedPositionIds = allowedPositionIds;
            RequiredFeatureIds = requiredFeatureIds;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string ArchetypeId { get; }
        public string Rarity { get; }
        public string PortraitId { get; }
        public double BaseTenure { get; }
        public IReadOnlyList<string> AllowedPositionIds { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
    }

    public sealed class CivicPersonConditionDefinition
    {
        public CivicPersonConditionDefinition(string personId, string metricId, string comparator, double value)
        {
            PersonId = personId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
        }

        public string PersonId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
    }

    public sealed class CivicPersonEffectDefinition
    {
        public CivicPersonEffectDefinition(string personId, string positionId, string effectType, string targetId, double amount, string capGroup, double duration = 0d, string runtimeEffectType = "", string runtimeTargetId = "")
        {
            PersonId = personId;
            PositionId = positionId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CapGroup = capGroup;
            Duration = duration;
            RuntimeEffectType = runtimeEffectType;
            RuntimeTargetId = runtimeTargetId;
        }

        public string PersonId { get; }
        public string PositionId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CapGroup { get; }
        public double Duration { get; }
        public string RuntimeEffectType { get; }
        public string RuntimeTargetId { get; }
        public CivicResolvedModuleEffect Resolve() => CivicProvisionalEffect.Resolve(EffectType, TargetId, RuntimeEffectType, RuntimeTargetId, Amount, Duration, CapGroup);
    }

    public sealed class CivicPersonAbilityDefinition
    {
        public CivicPersonAbilityDefinition(string personId, string id, string effectType, string targetId, double amount, double duration, int usesPerRun, string capGroup = "", string runtimeEffectType = "", string runtimeTargetId = "")
        {
            PersonId = personId;
            Id = id;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            Duration = duration;
            UsesPerRun = usesPerRun;
            CapGroup = capGroup;
            RuntimeEffectType = runtimeEffectType;
            RuntimeTargetId = runtimeTargetId;
        }

        public string PersonId { get; }
        public string Id { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public double Duration { get; }
        public int UsesPerRun { get; }
        public string CapGroup { get; }
        public string RuntimeEffectType { get; }
        public string RuntimeTargetId { get; }
        public CivicResolvedModuleEffect Resolve() => CivicProvisionalEffect.Resolve(EffectType, TargetId, RuntimeEffectType, RuntimeTargetId, Amount, Duration, CapGroup);
    }

    public sealed class CivicPeopleContent
    {
        public CivicPeopleContent(IReadOnlyList<CivicPersonPositionDefinition> positions, IReadOnlyList<CivicPersonDefinition> people, IReadOnlyList<CivicPersonConditionDefinition> conditions, IReadOnlyList<CivicPersonEffectDefinition> traits, IReadOnlyList<CivicPersonAbilityDefinition> abilities)
        {
            Positions = positions;
            People = people;
            Conditions = conditions;
            Traits = traits;
            Abilities = abilities;
        }

        public IReadOnlyList<CivicPersonPositionDefinition> Positions { get; }
        public IReadOnlyList<CivicPersonDefinition> People { get; }
        public IReadOnlyList<CivicPersonConditionDefinition> Conditions { get; }
        public IReadOnlyList<CivicPersonEffectDefinition> Traits { get; }
        public IReadOnlyList<CivicPersonAbilityDefinition> Abilities { get; }
    }

    public static class CivicPeopleContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicPeopleContent LoadFromResources()
        {
            var positions = Resources.Load<TextAsset>(Root + "person_positions");
            var people = Resources.Load<TextAsset>(Root + "people");
            var conditions = Resources.Load<TextAsset>(Root + "person_conditions");
            var traits = Resources.Load<TextAsset>(Root + "person_traits");
            var abilities = Resources.Load<TextAsset>(Root + "person_abilities");
            if (positions == null || people == null || conditions == null || traits == null || abilities == null)
            {
                throw new CivicDataException(new[] { "People CSV resources are missing." });
            }

            return Parse(positions.text, people.text, conditions.text, traits.text, abilities.text);
        }

        public static CivicPeopleContent Parse(string positionsCsv, string peopleCsv, string conditionsCsv, string traitsCsv, string abilitiesCsv)
        {
            var errors = new List<string>();
            var positions = CivicCsvParser.Parse(positionsCsv, errors, "person_positions.csv").Select(row => new CivicPersonPositionDefinition(
                Value(row, "id"), Value(row, "displayNameKo"), Value(row, "descriptionKo"), Integer(row, "order", errors, "person_positions.csv"),
                SplitIds(Value(row, "requiredFeatureIds")))).OrderBy(item => item.Order).ToArray();
            var people = CivicCsvParser.Parse(peopleCsv, errors, "people.csv").Select(row => new CivicPersonDefinition(
                Value(row, "id"), Value(row, "displayNameKo"), Value(row, "archetypeId"), Value(row, "rarity"), Value(row, "portraitId"),
                Number(row, "baseTenure", errors, "people.csv"), SplitIds(Value(row, "allowedPositionIds")), SplitIds(Value(row, "requiredFeatureIds")))).ToArray();
            var conditions = CivicCsvParser.Parse(conditionsCsv, errors, "person_conditions.csv").Select(row => new CivicPersonConditionDefinition(
                Value(row, "personId"), Value(row, "metricId"), Value(row, "comparator"), Number(row, "value", errors, "person_conditions.csv"))).ToArray();
            var traits = CivicCsvParser.Parse(traitsCsv, errors, "person_traits.csv").Select(row => new CivicPersonEffectDefinition(
                Value(row, "personId"), Value(row, "positionId"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "person_traits.csv"), Value(row, "capGroup"),
                OptionalNumber(row, "duration", errors, "person_traits.csv"), Value(row, "runtimeEffectType"), Value(row, "runtimeTargetId"))).ToArray();
            var abilities = CivicCsvParser.Parse(abilitiesCsv, errors, "person_abilities.csv").Select(row => new CivicPersonAbilityDefinition(
                Value(row, "personId"), Value(row, "id"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "person_abilities.csv"),
                Number(row, "duration", errors, "person_abilities.csv"), Integer(row, "usesPerRun", errors, "person_abilities.csv"), Value(row, "capGroup"),
                Value(row, "runtimeEffectType"), Value(row, "runtimeTargetId"))).ToArray();

            var ids = people.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var positionIds = positions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in positions.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add($"Duplicate person position ID: {duplicate.Key}");
            foreach (var duplicate in people.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add($"Duplicate person ID: {duplicate.Key}");
            foreach (var reference in conditions.Select(item => item.PersonId).Concat(traits.Select(item => item.PersonId)).Concat(abilities.Select(item => item.PersonId)))
            {
                if (!ids.Contains(reference)) errors.Add($"Unknown person reference: {reference}");
            }

            foreach (var person in people)
            {
                if (person.BaseTenure < 600d || person.BaseTenure > 1800d) errors.Add($"Person tenure must be 600-1800 seconds: {person.Id}");
                if (person.AllowedPositionIds.Count == 0) errors.Add($"Person has no position: {person.Id}");
                foreach (var positionId in person.AllowedPositionIds.Where(positionId => !positionIds.Contains(positionId))) errors.Add($"Person references unknown position: {person.Id}/{positionId}");
                foreach (var positionId in person.AllowedPositionIds.Where(positionId => !traits.Any(trait => trait.PersonId == person.Id && trait.PositionId == positionId))) errors.Add($"Person position has no effect: {person.Id}/{positionId}");
            }

            foreach (var trait in traits)
            {
                if (!positionIds.Contains(trait.PositionId)) errors.Add($"Person trait references unknown position: {trait.PersonId}/{trait.PositionId}");
                var person = people.FirstOrDefault(item => item.Id == trait.PersonId);
                if (person != null && !person.AllowedPositionIds.Contains(trait.PositionId)) errors.Add($"Person trait uses disallowed position: {trait.PersonId}/{trait.PositionId}");
            }

            foreach (var condition in conditions)
            {
                if (condition.Comparator != ">=" && condition.Comparator != "<=" && condition.Comparator != "==") errors.Add($"Unsupported person comparator: {condition.PersonId}/{condition.Comparator}");
            }

            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicPeopleContent(positions, people, conditions, traits, abilities);
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => (int)Number(row, key, errors, source);
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }

        private static double OptionalNumber(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => string.IsNullOrEmpty(Value(row, key)) ? 0d : Number(row, key, errors, source);

        private static IReadOnlyList<string> SplitIds(string value) => value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
    }
}
