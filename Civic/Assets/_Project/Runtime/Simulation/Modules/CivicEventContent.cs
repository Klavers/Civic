using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicEventDefinition
    {
        public CivicEventDefinition(string id, string category, string titleKo, string descriptionKo, string triggerMode, double baseWeight, double pitySeconds, double cooldownSeconds, int maxPerRun, bool pauseByDefault, IReadOnlyList<string> requiredFeatureIds)
        {
            Id = id;
            Category = category;
            TitleKo = titleKo;
            DescriptionKo = descriptionKo;
            TriggerMode = triggerMode;
            BaseWeight = baseWeight;
            PitySeconds = pitySeconds;
            CooldownSeconds = cooldownSeconds;
            MaxPerRun = maxPerRun;
            PauseByDefault = pauseByDefault;
            RequiredFeatureIds = requiredFeatureIds;
        }

        public string Id { get; }
        public string Category { get; }
        public string TitleKo { get; }
        public string DescriptionKo { get; }
        public string TriggerMode { get; }
        public double BaseWeight { get; }
        public double PitySeconds { get; }
        public double CooldownSeconds { get; }
        public int MaxPerRun { get; }
        public bool PauseByDefault { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
    }

    public sealed class CivicEventConditionDefinition
    {
        public CivicEventConditionDefinition(string eventId, string metricId, string comparator, double value, double duration, bool forbidden, string alternativeGroup)
        {
            EventId = eventId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
            Duration = duration;
            Forbidden = forbidden;
            AlternativeGroup = alternativeGroup;
        }

        public string EventId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
        public double Duration { get; }
        public bool Forbidden { get; }
        public string AlternativeGroup { get; }
    }

    public sealed class CivicEventChoiceDefinition
    {
        public CivicEventChoiceDefinition(string eventId, string id, string textKo, string requirementMetricId, string requirementComparator, double requirementValue, string nextEventId)
        {
            EventId = eventId;
            Id = id;
            TextKo = textKo;
            RequirementMetricId = requirementMetricId;
            RequirementComparator = requirementComparator;
            RequirementValue = requirementValue;
            NextEventId = nextEventId;
        }

        public string EventId { get; }
        public string Id { get; }
        public string TextKo { get; }
        public string RequirementMetricId { get; }
        public string RequirementComparator { get; }
        public double RequirementValue { get; }
        public string NextEventId { get; }
    }

    public sealed class CivicEventEffectDefinition
    {
        public CivicEventEffectDefinition(string choiceId, string effectType, string targetId, double amount, double duration, string stackGroup)
        {
            ChoiceId = choiceId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            Duration = duration;
            StackGroup = stackGroup;
        }

        public string ChoiceId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public double Duration { get; }
        public string StackGroup { get; }
    }

    public sealed class CivicEventContent
    {
        public CivicEventContent(IReadOnlyList<CivicEventDefinition> events, IReadOnlyList<CivicEventConditionDefinition> conditions, IReadOnlyList<CivicEventChoiceDefinition> choices, IReadOnlyList<CivicEventEffectDefinition> effects)
        {
            Events = events;
            Conditions = conditions;
            Choices = choices;
            Effects = effects;
        }

        public IReadOnlyList<CivicEventDefinition> Events { get; }
        public IReadOnlyList<CivicEventConditionDefinition> Conditions { get; }
        public IReadOnlyList<CivicEventChoiceDefinition> Choices { get; }
        public IReadOnlyList<CivicEventEffectDefinition> Effects { get; }
    }

    public static class CivicEventContentLoader
    {
        private const string Root = "CivicModules/";

        public static CivicEventContent LoadFromResources()
        {
            var events = Resources.Load<TextAsset>(Root + "events");
            var conditions = Resources.Load<TextAsset>(Root + "event_conditions");
            var choices = Resources.Load<TextAsset>(Root + "event_choices");
            var effects = Resources.Load<TextAsset>(Root + "event_effects");
            if (events == null || conditions == null || choices == null || effects == null)
            {
                throw new CivicDataException(new[] { "Event CSV resources are missing." });
            }

            return Parse(events.text, conditions.text, choices.text, effects.text);
        }

        public static CivicEventContent Parse(string eventsCsv, string conditionsCsv, string choicesCsv, string effectsCsv)
        {
            var errors = new List<string>();
            var events = CivicCsvParser.Parse(eventsCsv, errors, "events.csv").Select(row => new CivicEventDefinition(
                Value(row, "id"), Value(row, "category"), Value(row, "titleKo"), Value(row, "descriptionKo"), Value(row, "triggerMode"),
                Number(row, "baseWeight", errors, "events.csv"), Number(row, "pitySeconds", errors, "events.csv"), Number(row, "cooldownSeconds", errors, "events.csv"),
                Integer(row, "maxPerRun", errors, "events.csv"), Boolean(row, "pauseByDefault", errors, "events.csv"), SplitIds(Value(row, "requiredFeatureIds")))).ToArray();
            var conditions = CivicCsvParser.Parse(conditionsCsv, errors, "event_conditions.csv").Select(row => new CivicEventConditionDefinition(
                Value(row, "eventId"), Value(row, "metricId"), Value(row, "comparator"), Number(row, "value", errors, "event_conditions.csv"),
                Number(row, "duration", errors, "event_conditions.csv"), Boolean(row, "forbidden", errors, "event_conditions.csv"), Value(row, "alternativeGroup"))).ToArray();
            var choices = CivicCsvParser.Parse(choicesCsv, errors, "event_choices.csv").Select(row => new CivicEventChoiceDefinition(
                Value(row, "eventId"), Value(row, "choiceId"), Value(row, "textKo"), Value(row, "requirementMetricId"), Value(row, "requirementComparator"),
                Number(row, "requirementValue", errors, "event_choices.csv"), Value(row, "nextEventId"))).ToArray();
            var effects = CivicCsvParser.Parse(effectsCsv, errors, "event_effects.csv").Select(row => new CivicEventEffectDefinition(
                Value(row, "choiceId"), Value(row, "effectType"), Value(row, "targetId"), Number(row, "amount", errors, "event_effects.csv"),
                Number(row, "duration", errors, "event_effects.csv"), Value(row, "stackGroup"))).ToArray();
            Validate(events, conditions, choices, effects, errors);
            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return new CivicEventContent(events, conditions, choices, effects);
        }

        private static void Validate(IReadOnlyList<CivicEventDefinition> events, IReadOnlyList<CivicEventConditionDefinition> conditions, IReadOnlyList<CivicEventChoiceDefinition> choices, IReadOnlyList<CivicEventEffectDefinition> effects, ICollection<string> errors)
        {
            var eventIds = events.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var choiceIds = choices.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var duplicate in events.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add("Duplicate event ID: " + duplicate.Key);
            foreach (var duplicate in choices.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) errors.Add("Duplicate event choice ID: " + duplicate.Key);
            foreach (var definition in events)
            {
                if (string.IsNullOrEmpty(definition.Id) || (definition.TriggerMode != "certain" && definition.TriggerMode != "conditional" && definition.TriggerMode != "chain")) errors.Add("Invalid event definition: " + definition.Id);
                if (definition.BaseWeight < 0d || definition.BaseWeight > 1d || definition.PitySeconds < 0d || definition.CooldownSeconds < 0d || definition.MaxPerRun <= 0) errors.Add("Invalid event timing: " + definition.Id);
                if (!choices.Any(item => item.EventId == definition.Id)) errors.Add("Event has no choices: " + definition.Id);
            }
            foreach (var condition in conditions)
            {
                if (!eventIds.Contains(condition.EventId)) errors.Add("Unknown event condition owner: " + condition.EventId);
                if (condition.Comparator != ">=" && condition.Comparator != "<=" && condition.Comparator != "==") errors.Add("Unsupported event comparator: " + condition.EventId + "/" + condition.Comparator);
                if (condition.Duration < 0d) errors.Add("Event condition duration cannot be negative: " + condition.EventId);
            }
            foreach (var choice in choices)
            {
                if (!eventIds.Contains(choice.EventId)) errors.Add("Unknown event choice owner: " + choice.EventId);
                if (!string.IsNullOrEmpty(choice.NextEventId) && !eventIds.Contains(choice.NextEventId)) errors.Add("Unknown chained event: " + choice.NextEventId);
                if (!string.IsNullOrEmpty(choice.RequirementMetricId) && choice.RequirementComparator != ">=" && choice.RequirementComparator != "<=" && choice.RequirementComparator != "==") errors.Add("Unsupported choice comparator: " + choice.Id);
            }
            foreach (var effect in effects)
            {
                if (!choiceIds.Contains(effect.ChoiceId)) errors.Add("Unknown event effect choice: " + effect.ChoiceId);
                if (string.IsNullOrEmpty(effect.EffectType) || effect.Duration < 0d || double.IsNaN(effect.Amount) || double.IsInfinity(effect.Amount)) errors.Add("Invalid event effect: " + effect.ChoiceId);
            }
            DetectChainCycles(events, choices, errors);
        }

        private static void DetectChainCycles(IReadOnlyList<CivicEventDefinition> events, IReadOnlyList<CivicEventChoiceDefinition> choices, ICollection<string> errors)
        {
            var edges = choices.Where(item => !string.IsNullOrEmpty(item.NextEventId)).GroupBy(item => item.EventId).ToDictionary(group => group.Key, group => group.Select(item => item.NextEventId).Distinct().ToArray(), StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in events)
            {
                if (HasCycle(definition.Id, edges, visiting, visited)) errors.Add("Event chain contains a cycle at: " + definition.Id);
            }
        }

        private static bool HasCycle(string id, IReadOnlyDictionary<string, string[]> edges, ISet<string> visiting, ISet<string> visited)
        {
            if (visiting.Contains(id)) return true;
            if (visited.Contains(id)) return false;
            visiting.Add(id);
            if (edges.TryGetValue(id, out var nextIds))
            {
                foreach (var next in nextIds) if (HasCycle(next, edges, visiting, visited)) return true;
            }
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source) => (int)Number(row, key, errors, source);
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (string.IsNullOrEmpty(raw)) return 0d;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add(source + " has invalid number " + key + ": " + raw); return 0d;
        }
        private static bool Boolean(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key); if (bool.TryParse(raw, out var value)) return value;
            errors.Add(source + " has invalid boolean " + key + ": " + raw); return false;
        }
        private static IReadOnlyList<string> SplitIds(string raw) => string.IsNullOrWhiteSpace(raw) ? Array.Empty<string>() : raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
    }
}
