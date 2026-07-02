using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public enum CivicNationCandidateState
    {
        Hidden,
        Unavailable,
        Discovered,
        Ready,
        Preparing,
        AwaitingCharter,
        Current
    }

    public sealed class CivicNationCandidateSnapshot
    {
        public CivicNationCandidateSnapshot(CivicNationDefinition definition, CivicNationCandidateState state, double conditionRatio, double preparationProgress, string blockingReason)
        {
            Definition = definition;
            State = state;
            ConditionRatio = conditionRatio;
            PreparationProgress = preparationProgress;
            BlockingReason = blockingReason;
        }

        public CivicNationDefinition Definition { get; }
        public CivicNationCandidateState State { get; }
        public double ConditionRatio { get; }
        public double PreparationProgress { get; }
        public string BlockingReason { get; }
    }

    public sealed class CivicNationModule : CivicGameplayModuleBase
    {
        private readonly CivicNationContent content;
        private readonly Dictionary<string, double> preparationProgress = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<CivicNationEffectDefinition> inactiveEffects = new List<CivicNationEffectDefinition>();
        private readonly HashSet<string> debugReadyIds = new HashSet<string>(StringComparer.Ordinal);
        private string preparingNationId;
        private string awaitingCharterNationId;
        private IReadOnlyList<CivicNationCandidateSnapshot> snapshot = Array.Empty<CivicNationCandidateSnapshot>();

        public CivicNationModule(CivicNationContent content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public override string FeatureId => CivicFeatureRegistry.NationFormation;
        public string CurrentNationId { get; private set; } = string.Empty;
        public IReadOnlyList<CivicNationCandidateSnapshot> Snapshot => snapshot;
        public IReadOnlyList<CivicNationDefinition> Definitions => content.Nations;
        public IReadOnlyList<CivicNationEffectDefinition> InactiveEffects => inactiveEffects;
        public int ProvisionalEffectCount => string.IsNullOrEmpty(CurrentNationId) ? 0 : content.Effects.Count(item => item.NationId == CurrentNationId && item.EffectType == CivicProvisionalEffect.Planned);
        public bool DebugInstantActionsEnabled { get; private set; }

        public IReadOnlyList<CivicNationConditionDefinition> ConditionsFor(string nationId) => content.Conditions.Where(item => item.NationId == nationId).ToArray();
        public IReadOnlyList<CivicNationEffectDefinition> EffectsFor(string nationId) => content.Effects.Where(item => item.NationId == nationId).ToArray();
        public IReadOnlyList<CivicMetricConditionSnapshot> ConditionStatusFor(string nationId) => ConditionsFor(nationId)
            .Select(condition =>
            {
                var current = Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress);
                return new CivicMetricConditionSnapshot(
                    condition.MetricId,
                    condition.Comparator,
                    condition.Value,
                    current,
                    debugReadyIds.Contains(nationId) || CivicConditionEvaluator.Compare(current, condition.Comparator, condition.Value),
                    condition.AlternativeGroup);
            }).ToArray();

        public bool IsImpossibleThisRun(string nationId)
        {
            return !string.IsNullOrEmpty(ImpossibleReasonFor(nationId));
        }

        public string ImpossibleReasonFor(string nationId)
        {
            var nation = content.Nations.FirstOrDefault(item => item.Id == nationId);
            if (nation == null || debugReadyIds.Contains(nationId)) return string.Empty;
            var missingFeature = nation.RequiredFeatureIds.FirstOrDefault(id => !Context.IsFeatureEnabled(id));
            if (!string.IsNullOrEmpty(missingFeature)) return "런 시작 시 비활성화된 필수 모듈: " + missingFeature;

            var conditions = content.Conditions.Where(item => item.NationId == nationId).ToArray();
            var impossibleDirect = conditions
                .Where(item => string.IsNullOrEmpty(item.AlternativeGroup))
                .FirstOrDefault(IsConditionImpossibleThisRun);
            if (impossibleDirect != null) return "고정된 런 조건 불일치: " + impossibleDirect.MetricId;

            var impossibleAlternative = conditions
                .Where(item => !string.IsNullOrEmpty(item.AlternativeGroup))
                .GroupBy(item => item.AlternativeGroup, StringComparer.Ordinal)
                .FirstOrDefault(group => group.All(IsConditionImpossibleThisRun));
            return impossibleAlternative == null
                ? string.Empty
                : "대안 조건을 모두 달성할 수 없음: " + impossibleAlternative.Key;
        }

        public bool DebugSatisfyConditions(string nationId)
        {
            if (content.Nations.All(item => item.Id != nationId)) return false;
            debugReadyIds.Add(nationId);
            RebuildSnapshot();
            return true;
        }

        public void SetDebugInstantActions(bool enabled)
        {
            DebugInstantActionsEnabled = enabled;
        }

        public bool DebugFormImmediately(string nationId)
        {
            if (content.Nations.All(item => item.Id != nationId) || nationId == CurrentNationId) return false;
            debugReadyIds.Add(nationId);
            preparingNationId = string.Empty;
            awaitingCharterNationId = nationId;
            preparationProgress[nationId] = content.Nations.First(item => item.Id == nationId).PreparationSeconds;
            return TryCompleteFormation();
        }

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            context.Telemetry.SetExternalMetric("nation.formationCount", 0d);
            RebuildSnapshot();
            PublishProgressMetric();
        }

        public override void AfterAdvance(double seconds)
        {
            if (!string.IsNullOrEmpty(preparingNationId))
            {
                Context.Telemetry.SetExternalMetric("nation.preparing", 1d);
                var nation = content.Nations.First(item => item.Id == preparingNationId);
                if (ConditionsSatisfied(nation))
                {
                    var preparationSpeed = Context.Modifiers.Multiplier(CivicModifierEffectTypes.NationPreparationSpeedMultiplier, nation.Id, "*");
                    var conditionDuration = Context.Modifiers.Multiplier(CivicModifierEffectTypes.NationConditionDurationMultiplier, nation.Id, "*");
                    var effectiveSeconds = Math.Max(0d, seconds) * preparationSpeed / Math.Max(0.1d, conditionDuration);
                    preparationProgress[nation.Id] = Math.Min(nation.PreparationSeconds, preparationProgress[nation.Id] + effectiveSeconds);
                    if (preparationProgress[nation.Id] + 1e-9d >= nation.PreparationSeconds)
                    {
                        awaitingCharterNationId = nation.Id;
                        preparingNationId = string.Empty;
                        Context.Telemetry.SetExternalMetric("nation.preparing", 0d);
                    }
                }
            }

            RebuildSnapshot();
            PublishProgressMetric();
        }

        public bool TryDeclare(string nationId)
        {
            if (DebugInstantActionsEnabled) return DebugFormImmediately(nationId);
            var nation = content.Nations.FirstOrDefault(item => item.Id == nationId);
            if (nation == null || !IsAvailable(nation, out _) || !ConditionsSatisfied(nation)) return false;
            if (Context.Simulation.State.Resources["treasury"].ToDouble() + 1e-9d < nation.TreasuryCost) return false;
            Context.Simulation.GrantResource("treasury", CivicNumber.FromDouble(-nation.TreasuryCost));
            preparationProgress[nation.Id] = 0d;
            if (nation.PreparationSeconds <= 0d)
            {
                awaitingCharterNationId = nation.Id;
            }
            else
            {
                preparingNationId = nation.Id;
            }

            RebuildSnapshot();
            PublishProgressMetric();
            return true;
        }

        public bool CancelPreparation()
        {
            if (string.IsNullOrEmpty(preparingNationId)) return false;
            preparingNationId = string.Empty;
            Context.Telemetry.SetExternalMetric("nation.preparing", 0d);
            RebuildSnapshot();
            PublishProgressMetric();
            return true;
        }

        public bool TryCompleteFormation(string charterId = "default")
        {
            if (string.IsNullOrEmpty(awaitingCharterNationId)) return false;
            var nation = content.Nations.First(item => item.Id == awaitingCharterNationId);
            if (!string.IsNullOrEmpty(CurrentNationId)) Context.Modifiers.RemoveSource("nation", CurrentNationId);
            CurrentNationId = nation.Id;
            awaitingCharterNationId = string.Empty;
            ApplyEffects(nation.Id, charterId);
            if (!Context.MetaProgress.FormedNationIds.Contains(nation.Id)) Context.MetaProgress.FormedNationIds.Add(nation.Id);
            Context.Telemetry.SetExternalMetric("nation.formationCount", Context.Telemetry.GetMetric("nation.formationCount", Context.MetaProgress) + 1d);
            Context.Telemetry.SetExternalMetric("nation.current." + nation.Id, 1d);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            Context.Simulation.RefreshSnapshot();
            RebuildSnapshot();
            PublishProgressMetric();
            return true;
        }

        private void ApplyEffects(string nationId, string charterId)
        {
            foreach (var effect in content.Effects.Where(item => item.NationId == nationId && (item.CharterId == charterId || item.CharterId == "default")))
            {
                var resolved = effect.Resolve();
                Context.Modifiers.Add(new CivicModifierEntry("nation", nationId, resolved.EffectType, resolved.TargetId, resolved.Amount, resolved.CapGroup));
            }
        }

        private bool IsAvailable(CivicNationDefinition nation, out string reason)
        {
            var missing = nation.RequiredFeatureIds.FirstOrDefault(id => !Context.IsFeatureEnabled(id));
            reason = missing == null ? string.Empty : "필요 모듈 OFF: " + missing;
            return missing == null;
        }

        private bool ConditionsSatisfied(CivicNationDefinition nation)
        {
            if (debugReadyIds.Contains(nation.Id)) return true;
            var conditions = content.Conditions.Where(item => item.NationId == nation.Id).ToArray();
            var direct = conditions.Where(item => string.IsNullOrEmpty(item.AlternativeGroup));
            if (direct.Any(condition => !ConditionSatisfied(condition))) return false;
            return conditions.Where(item => !string.IsNullOrEmpty(item.AlternativeGroup))
                .GroupBy(item => item.AlternativeGroup, StringComparer.Ordinal)
                .All(group => group.Any(ConditionSatisfied));
        }

        private bool ConditionSatisfied(CivicNationConditionDefinition condition)
        {
            return CivicConditionEvaluator.Compare(
                Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress),
                condition.Comparator,
                condition.Value);
        }

        private bool IsConditionImpossibleThisRun(CivicNationConditionDefinition condition)
        {
            var current = Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress);
            if (condition.MetricId.StartsWith("identity.startingCivilization.", StringComparison.Ordinal))
            {
                return !CivicConditionEvaluator.Compare(current, condition.Comparator, condition.Value);
            }

            if (!IsMonotonicMetric(condition.MetricId)) return false;
            if (condition.Comparator == "<=") return current > condition.Value + 1e-9d;
            if (condition.Comparator == "==") return current > condition.Value + 1e-9d;
            return false;
        }

        private static bool IsMonotonicMetric(string metricId)
        {
            return metricId.StartsWith("run.", StringComparison.Ordinal) ||
                metricId.StartsWith("meta.", StringComparison.Ordinal) ||
                metricId.StartsWith("technology.researched.", StringComparison.Ordinal) ||
                metricId.StartsWith("building.ever.", StringComparison.Ordinal) ||
                metricId.StartsWith("wonder.", StringComparison.Ordinal) ||
                metricId.StartsWith("event.choice.", StringComparison.Ordinal);
        }

        private double ConditionRatio(CivicNationDefinition nation)
        {
            var conditions = content.Conditions.Where(item => item.NationId == nation.Id).ToArray();
            if (conditions.Length == 0) return 0d;
            var directSatisfied = conditions.Count(item => string.IsNullOrEmpty(item.AlternativeGroup) && ConditionSatisfied(item));
            var directCount = conditions.Count(item => string.IsNullOrEmpty(item.AlternativeGroup));
            var alternativeGroups = conditions.Where(item => !string.IsNullOrEmpty(item.AlternativeGroup)).GroupBy(item => item.AlternativeGroup).ToArray();
            var alternativeSatisfied = alternativeGroups.Count(group => group.Any(ConditionSatisfied));
            return (directSatisfied + alternativeSatisfied) / (double)Math.Max(1, directCount + alternativeGroups.Length);
        }

        private void RebuildSnapshot()
        {
            snapshot = content.Nations.Select(nation =>
            {
                if (!IsAvailable(nation, out var reason)) return new CivicNationCandidateSnapshot(nation, CivicNationCandidateState.Unavailable, 0d, 0d, reason);
                var ratio = ConditionRatio(nation);
                var progress = preparationProgress.TryGetValue(nation.Id, out var value) && nation.PreparationSeconds > 0d ? value / nation.PreparationSeconds : 0d;
                var state = nation.Id == CurrentNationId ? CivicNationCandidateState.Current
                    : nation.Id == awaitingCharterNationId ? CivicNationCandidateState.AwaitingCharter
                    : nation.Id == preparingNationId ? CivicNationCandidateState.Preparing
                    : ConditionsSatisfied(nation) ? CivicNationCandidateState.Ready
                    : ratio >= 0.70d ? CivicNationCandidateState.Discovered
                    : CivicNationCandidateState.Hidden;
                var impossibleReason = ImpossibleReasonFor(nation.Id);
                var blockingReason = state == CivicNationCandidateState.Ready
                    ? string.Empty
                    : string.IsNullOrEmpty(impossibleReason) ? "조건 미충족" : "이번 런 달성 불가: " + impossibleReason;
                return new CivicNationCandidateSnapshot(nation, state, ratio, progress, blockingReason);
            }).ToArray();
        }

        private void PublishProgressMetric()
        {
            if (string.IsNullOrEmpty(preparingNationId))
            {
                Context.Telemetry.SetExternalMetric("nation.preparationProgress", 0d);
                return;
            }
            var nation = content.Nations.First(item => item.Id == preparingNationId);
            Context.Telemetry.SetExternalMetric("nation.preparationProgress", nation.PreparationSeconds <= 0d ? 1d : preparationProgress[preparingNationId] / nation.PreparationSeconds);
        }
    }
}
