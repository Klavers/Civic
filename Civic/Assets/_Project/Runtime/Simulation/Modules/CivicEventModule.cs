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
        public CivicEventHistoryEntry(string eventId, string eventTitleKo, string choiceId, string choiceTextKo, double occurredAt, IReadOnlyList<string> appliedResults)
        {
            EventId = eventId;
            EventTitleKo = eventTitleKo;
            ChoiceId = choiceId;
            ChoiceTextKo = choiceTextKo;
            OccurredAt = occurredAt;
            AppliedResults = appliedResults ?? Array.Empty<string>();
        }

        public string EventId { get; }
        public string EventTitleKo { get; }
        public string ChoiceId { get; }
        public string ChoiceTextKo { get; }
        public double OccurredAt { get; }
        public IReadOnlyList<string> AppliedResults { get; }
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
        public int ProvisionalEffectCount => content.Effects.Count(item => item.EffectType == CivicProvisionalEffect.Planned);
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
            var appliedResults = ApplyChoice(queued.Definition, choice);
            queue.Remove(queued);
            history.Add(new CivicEventHistoryEntry(eventId, queued.Definition.TitleKo, choiceId, choice.TextKo, Context.Telemetry.ElapsedSeconds, appliedResults));
            cooldowns[eventId] = queued.Definition.CooldownSeconds * Context.Modifiers.Multiplier(CivicModifierEffectTypes.EventCooldownMultiplier, eventId, queued.Definition.Category, "*");
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

        public string DescribeChoice(string choiceId)
        {
            var choice = content.Choices.FirstOrDefault(item => item.Id == choiceId);
            if (choice == null) return string.Empty;
            var descriptions = content.Effects.Where(item => item.ChoiceId == choiceId).Select(DescribeEffect).ToArray();
            var requirement = string.IsNullOrEmpty(choice.RequirementMetricId)
                ? string.Empty
                : $"조건: {choice.RequirementMetricId} {choice.RequirementComparator} {choice.RequirementValue:0.##}";
            return string.Join("\n", new[] { requirement }.Concat(descriptions).Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        public bool DebugQueueEvent(string eventId)
        {
            var definition = content.Events.FirstOrDefault(item => item.Id == eventId);
            if (definition == null || queue.Count >= QueueLimit || queue.Any(item => item.Definition.Id == eventId)) return false;
            QueueEvent(definition, "DEBUG 강제 발생");
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
                var weightMultiplier = Context.Modifiers.Multiplier(CivicModifierEffectTypes.EventWeightMultiplier, definition.Id, definition.Category, "*");
                var chance = Math.Min(0.95d, (definition.BaseWeight + accumulatedWeights[definition.Id]) * weightMultiplier);
                if (pity || DeterministicRoll(definition.Id) < chance)
                {
                    QueueEvent(definition, pity ? "누적 조건 확정" : "조건부 판정");
                }
                else
                {
                    accumulatedWeights[definition.Id] = Math.Min(0.95d, accumulatedWeights[definition.Id] + definition.BaseWeight * weightMultiplier);
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

        private IReadOnlyList<string> ApplyChoice(CivicEventDefinition definition, CivicEventChoiceDefinition choice)
        {
            var applied = new List<string>();
            foreach (var effect in content.Effects.Where(item => item.ChoiceId == choice.Id))
            {
                var resolved = effect.Resolve();
                if (resolved.EffectType == "resourceGrant")
                {
                    var before = Context.Simulation.State.Resources.TryGetValue(resolved.TargetId, out var current) ? current.ToDouble() : 0d;
                    Context.Simulation.GrantResource(resolved.TargetId, CivicNumber.FromDouble(resolved.Amount));
                    var after = Context.Simulation.State.Resources.TryGetValue(resolved.TargetId, out current) ? current.ToDouble() : before;
                    applied.Add($"{resolved.TargetId}: {before:0.##} → {after:0.##}");
                }
                else if (resolved.EffectType == "prestigeGrant") { Context.MetaProgress.PrestigePoints += (long)Math.Round(resolved.Amount); applied.Add($"환생 포인트 {resolved.Amount:+0.##;-0.##;0}"); }
                else if (resolved.EffectType == "politicalCapitalGrant") { Context.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics)?.GrantPoliticalCapital(resolved.Amount); applied.Add($"정치력 {resolved.Amount:+0.##;-0.##;0}"); }
                else if (resolved.EffectType == "reformProgressAdd") { Context.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics)?.AddReformProgress(resolved.Amount); applied.Add($"개혁 진행 {resolved.Amount:+0.##;-0.##;0}"); }
                else if (resolved.EffectType == "wonderProgressAdd") { Context.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders)?.AddProgressFraction(resolved.Amount); applied.Add($"불가사의 진행 {resolved.Amount:+0.##;-0.##;0}"); }
                else if (resolved.EffectType == "flagSet") { Context.Telemetry.SetExternalMetric("event.flag." + resolved.TargetId, resolved.Amount); applied.Add($"플래그 {resolved.TargetId}={resolved.Amount:0.##}"); }
                else if (!resolved.IsProvisional && !DirectModifierEffects.Contains(resolved.EffectType))
                {
                    if (!inactiveEffects.Contains(effect)) inactiveEffects.Add(effect);
                    applied.Add("현재 런타임 미지원: " + DescribeResolvedEffect(resolved));
                }
                else { ApplyModifier(definition.Id, choice.Id, effect, resolved); applied.Add(DescribeResolvedEffect(resolved)); }
            }
            return applied;
        }

        private string DescribeEffect(CivicEventEffectDefinition effect) => DescribeResolvedEffect(effect.Resolve());

        private static string DescribeResolvedEffect(CivicResolvedModuleEffect resolved)
        {
            var duration = resolved.Duration > 0d ? $" · {resolved.Duration:0.#}초" : " · 지속";
            return $"{resolved.EffectType}({resolved.TargetId}) {resolved.Amount:+0.##;-0.##;0}{duration}";
        }

        private void ApplyModifier(string eventId, string choiceId, CivicEventEffectDefinition effect, CivicResolvedModuleEffect resolved)
        {
            var group = string.IsNullOrEmpty(effect.StackGroup) ? resolved.EffectType + ":" + resolved.TargetId : effect.StackGroup;
            var sourceId = eventId + ":" + group;
            var capGroup = string.IsNullOrEmpty(resolved.CapGroup) ? group : resolved.CapGroup;
            Context.Modifiers.ReplaceSource("event", sourceId, new[] { new CivicModifierEntry("event", sourceId, resolved.EffectType, resolved.TargetId, resolved.Amount, capGroup) });
            if (resolved.Duration > 0d) timedEffects[sourceId] = resolved.Duration;
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
