using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public sealed class CivicQueuedEventSnapshot
    {
        public CivicQueuedEventSnapshot(CivicEventDefinition definition, IReadOnlyList<CivicEventChoiceDefinition> choices, string cause)
        {
            Definition = definition;
            Choices = choices;
            Cause = cause;
        }

        public CivicEventDefinition Definition { get; }
        public IReadOnlyList<CivicEventChoiceDefinition> Choices { get; }
        public string Cause { get; }
    }

    public sealed class CivicEventHistoryEntry
    {
        public CivicEventHistoryEntry(string eventId, string choiceId, double occurredAt)
        {
            EventId = eventId;
            ChoiceId = choiceId;
            OccurredAt = occurredAt;
        }

        public string EventId { get; }
        public string ChoiceId { get; }
        public double OccurredAt { get; }
    }

    public sealed class CivicEventModule : CivicGameplayModuleBase
    {
        public const double SchedulerIntervalSeconds = 5d;
        public const int QueueLimit = 3;

        private static readonly HashSet<string> DirectModifierEffects = new HashSet<string>(StringComparer.Ordinal)
        {
            CivicModifierEffectTypes.ResourceOutputMultiplier,
            CivicModifierEffectTypes.ResourceOutputAdd,
            CivicModifierEffectTypes.ResourceInputMultiplier,
            CivicModifierEffectTypes.BuildingOutputMultiplier,
            CivicModifierEffectTypes.BuildingOutputAdd,
            CivicModifierEffectTypes.BuildingInputMultiplier,
            CivicModifierEffectTypes.BuildingInputAdd,
            CivicModifierEffectTypes.ConstructionCostMultiplier,
            CivicModifierEffectTypes.ConstructionCostAdd,
            CivicModifierEffectTypes.PopulationUseAdd,
            CivicModifierEffectTypes.PopulationBaseAdd,
            CivicModifierEffectTypes.PopulationScienceMultiplier,
            CivicModifierEffectTypes.TechnologyCostMultiplier,
            CivicModifierEffectTypes.TreasuryIncomeMultiplier,
            CivicModifierEffectTypes.TaxRateAdd,
            CivicModifierEffectTypes.PopulationConsumptionMultiplier,
            CivicModifierEffectTypes.ResourcePriceFloorAdd,
            CivicModifierEffectTypes.ResourceGdpMultiplier,
            CivicModifierEffectTypes.FoodConversionMultiplier,
            CivicModifierEffectTypes.ConstructionTreasuryCostMultiplier
        };

        private readonly CivicEventContent content;
        private readonly int runSeed;
        private readonly List<CivicQueuedEventSnapshot> queue = new List<CivicQueuedEventSnapshot>();
        private readonly List<CivicEventHistoryEntry> history = new List<CivicEventHistoryEntry>();
        private readonly List<CivicEventEffectDefinition> inactiveEffects = new List<CivicEventEffectDefinition>();
        private readonly Dictionary<string, double> conditionDurations = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> eligibleDurations = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> accumulatedWeights = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> cooldowns = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> occurrenceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> timedEffects = new Dictionary<string, double>(StringComparer.Ordinal);
        private double schedulerAccumulator;
        private uint sequence;

        public CivicEventModule(CivicEventContent content, int runSeed)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.runSeed = runSeed;
        }

        public override string FeatureId => CivicFeatureRegistry.Events;
        public IReadOnlyList<CivicEventDefinition> Definitions => content.Events;
        public IReadOnlyList<CivicQueuedEventSnapshot> Queue => queue;
        public IReadOnlyList<CivicEventHistoryEntry> History => history;
        public IReadOnlyList<CivicEventEffectDefinition> InactiveEffects => inactiveEffects;
        public bool IsSimulationPaused => queue.Any(item => item.Definition.PauseByDefault);

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            foreach (var definition in content.Events)
            {
                eligibleDurations[definition.Id] = 0d;
                accumulatedWeights[definition.Id] = 0d;
                cooldowns[definition.Id] = 0d;
                occurrenceCounts[definition.Id] = 0;
            }
            UpdateConditionDurations(0d);
            PublishMetrics();
            EvaluateCertainEvents();
        }

        public override void AfterAdvance(double seconds)
        {
            var elapsed = Math.Max(0d, seconds);
            AdvanceCooldowns(elapsed);
            AdvanceTimedEffects(elapsed);
            UpdateConditionDurations(elapsed);
            schedulerAccumulator += elapsed;
            while (schedulerAccumulator + 1e-9d >= SchedulerIntervalSeconds)
            {
                schedulerAccumulator -= SchedulerIntervalSeconds;
                EvaluateScheduler();
            }
            EvaluateCertainEvents();
            PublishMetrics();
        }

        public override void OnTechnologyResearched(string technologyId)
        {
            EvaluateCertainEvents();
        }

        public bool IsChoiceAvailable(string choiceId)
        {
            var choice = content.Choices.FirstOrDefault(item => item.Id == choiceId);
            if (choice == null) return false;
            return string.IsNullOrEmpty(choice.RequirementMetricId) || CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(choice.RequirementMetricId, Context.MetaProgress), choice.RequirementComparator, choice.RequirementValue);
        }

        public bool TryChoose(string eventId, string choiceId)
        {
            var queued = queue.FirstOrDefault(item => item.Definition.Id == eventId);
            var choice = content.Choices.FirstOrDefault(item => item.EventId == eventId && item.Id == choiceId);
            if (queued == null || choice == null || !IsChoiceAvailable(choiceId)) return false;
            ApplyChoice(queued.Definition, choice);
            queue.Remove(queued);
            history.Add(new CivicEventHistoryEntry(eventId, choiceId, Context.Telemetry.ElapsedSeconds));
            cooldowns[eventId] = queued.Definition.CooldownSeconds;
            accumulatedWeights[eventId] = 0d;
            eligibleDurations[eventId] = 0d;
            Context.Telemetry.SetExternalMetric("event.seen." + eventId, 1d);
            Context.Telemetry.SetExternalMetric("event.resolved." + eventId, 1d);
            Context.Telemetry.SetExternalMetric("event.choice." + choiceId, 1d);
            if (!Context.MetaProgress.DiscoveredEventIds.Contains(eventId)) Context.MetaProgress.DiscoveredEventIds.Add(eventId);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            if (!string.IsNullOrEmpty(choice.NextEventId)) QueueEvent(content.Events.First(item => item.Id == choice.NextEventId), "연쇄: " + choiceId);
            Context.Simulation.RefreshSnapshot();
            PublishMetrics();
            return true;
        }

        private void EvaluateScheduler()
        {
            if (queue.Count >= QueueLimit) return;
            foreach (var definition in content.Events.Where(item => item.TriggerMode == "conditional").OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!CanOccur(definition) || !ConditionsSatisfied(definition))
                {
                    eligibleDurations[definition.Id] = 0d;
                    accumulatedWeights[definition.Id] = 0d;
                    continue;
                }

                eligibleDurations[definition.Id] += SchedulerIntervalSeconds;
                var pity = definition.PitySeconds > 0d && eligibleDurations[definition.Id] + 1e-9d >= definition.PitySeconds;
                var chance = Math.Min(0.95d, definition.BaseWeight + accumulatedWeights[definition.Id]);
                if (pity || DeterministicRoll(definition.Id) < chance)
                {
                    QueueEvent(definition, pity ? "누적 조건 확정" : "조건부 판정");
                }
                else
                {
                    accumulatedWeights[definition.Id] = Math.Min(0.95d, accumulatedWeights[definition.Id] + definition.BaseWeight);
                }

                if (queue.Count >= QueueLimit) break;
            }
        }

        private void EvaluateCertainEvents()
        {
            if (queue.Count >= QueueLimit) return;
            foreach (var definition in content.Events.Where(item => item.TriggerMode == "certain" || item.TriggerMode == "chain").OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (CanOccur(definition) && ConditionsSatisfied(definition)) QueueEvent(definition, definition.TriggerMode == "chain" ? "연쇄 조건 충족" : "확정 조건 충족");
                if (queue.Count >= QueueLimit) break;
            }
        }

        private bool CanOccur(CivicEventDefinition definition)
        {
            return definition.RequiredFeatureIds.All(Context.IsFeatureEnabled)
                && occurrenceCounts[definition.Id] < definition.MaxPerRun
                && cooldowns[definition.Id] <= 0d
                && queue.All(item => item.Definition.Id != definition.Id);
        }

        private bool ConditionsSatisfied(CivicEventDefinition definition)
        {
            var conditions = content.Conditions.Where(item => item.EventId == definition.Id).ToArray();
            var direct = conditions.Where(item => string.IsNullOrEmpty(item.AlternativeGroup));
            if (direct.Any(condition => !ConditionSatisfied(condition))) return false;
            return conditions.Where(item => !string.IsNullOrEmpty(item.AlternativeGroup)).GroupBy(item => item.AlternativeGroup, StringComparer.Ordinal).All(group => group.Any(ConditionSatisfied));
        }

        private bool ConditionSatisfied(CivicEventConditionDefinition condition)
        {
            var comparison = CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress), condition.Comparator, condition.Value);
            var current = condition.Forbidden ? !comparison : comparison;
            return current && conditionDurations.TryGetValue(ConditionKey(condition), out var duration) && duration + 1e-9d >= condition.Duration;
        }

        private void UpdateConditionDurations(double elapsed)
        {
            foreach (var group in content.Conditions.GroupBy(item => item.EventId, StringComparer.Ordinal))
            {
                var index = 0;
                foreach (var condition in group)
                {
                    var comparison = CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress), condition.Comparator, condition.Value);
                    var current = condition.Forbidden ? !comparison : comparison;
                    var key = ConditionKey(condition, index++);
                    conditionDurations[key] = current ? (conditionDurations.TryGetValue(key, out var previous) ? previous : 0d) + elapsed : 0d;
                }
            }
        }

        private string ConditionKey(CivicEventConditionDefinition condition)
        {
            var conditions = content.Conditions.Where(item => item.EventId == condition.EventId).ToArray();
            return ConditionKey(condition, Array.IndexOf(conditions, condition));
        }

        private static string ConditionKey(CivicEventConditionDefinition condition, int index) => condition.EventId + ":" + index;

        private void QueueEvent(CivicEventDefinition definition, string cause)
        {
            if (queue.Count >= QueueLimit || queue.Any(item => item.Definition.Id == definition.Id)) return;
            queue.Add(new CivicQueuedEventSnapshot(definition, content.Choices.Where(item => item.EventId == definition.Id).ToArray(), cause));
            occurrenceCounts[definition.Id]++;
            Context.Telemetry.SetExternalMetric("event.queued." + definition.Id, 1d);
        }

        private void ApplyChoice(CivicEventDefinition definition, CivicEventChoiceDefinition choice)
        {
            foreach (var effect in content.Effects.Where(item => item.ChoiceId == choice.Id))
            {
                if (effect.EffectType == "resourceGrant") Context.Simulation.GrantResource(effect.TargetId, CivicNumber.FromDouble(effect.Amount));
                else if (effect.EffectType == "prestigeGrant") { Context.MetaProgress.PrestigePoints += (long)Math.Round(effect.Amount); }
                else if (effect.EffectType == "politicalCapitalGrant") Context.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics)?.GrantPoliticalCapital(effect.Amount);
                else if (effect.EffectType == "reformProgressAdd") Context.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics)?.AddReformProgress(effect.Amount);
                else if (effect.EffectType == "wonderProgressAdd") Context.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders)?.AddProgressFraction(effect.Amount);
                else if (effect.EffectType == "flagSet") Context.Telemetry.SetExternalMetric("event.flag." + effect.TargetId, effect.Amount);
                else if ((effect.EffectType == "planned" || !DirectModifierEffects.Contains(effect.EffectType)) && !inactiveEffects.Contains(effect)) inactiveEffects.Add(effect);
                else ApplyModifier(definition.Id, choice.Id, effect);
            }
        }

        private void ApplyModifier(string eventId, string choiceId, CivicEventEffectDefinition effect)
        {
            var group = string.IsNullOrEmpty(effect.StackGroup) ? effect.EffectType + ":" + effect.TargetId : effect.StackGroup;
            var sourceId = eventId + ":" + group;
            Context.Modifiers.ReplaceSource("event", sourceId, new[] { new CivicModifierEntry("event", sourceId, effect.EffectType, effect.TargetId, effect.Amount, group) });
            if (effect.Duration > 0d) timedEffects[sourceId] = effect.Duration;
        }

        private void AdvanceTimedEffects(double elapsed)
        {
            foreach (var sourceId in timedEffects.Keys.ToArray())
            {
                timedEffects[sourceId] -= elapsed;
                if (timedEffects[sourceId] > 0d) continue;
                Context.Modifiers.RemoveSource("event", sourceId);
                timedEffects.Remove(sourceId);
            }
        }

        private void AdvanceCooldowns(double elapsed)
        {
            foreach (var id in cooldowns.Keys.ToArray()) cooldowns[id] = Math.Max(0d, cooldowns[id] - elapsed);
        }

        private double DeterministicRoll(string eventId)
        {
            var text = runSeed + ":" + sequence++ + ":" + eventId;
            var hash = 2166136261u;
            foreach (var character in text) { hash ^= character; hash *= 16777619u; }
            return hash / (double)uint.MaxValue;
        }

        private void PublishMetrics()
        {
            Context.Telemetry.SetExternalMetric("event.queueCount", queue.Count);
            Context.Telemetry.SetExternalMetric("event.sequence", sequence);
            Context.Telemetry.SetExternalMetric("meta.eventDiscoveryRatio", content.Events.Count == 0 ? 0d : Context.MetaProgress.DiscoveredEventIds.Count(id => content.Events.Any(item => item.Id == id)) / (double)content.Events.Count);
        }
    }
}
