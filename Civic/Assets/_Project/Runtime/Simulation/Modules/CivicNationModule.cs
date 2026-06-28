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
        public IReadOnlyList<CivicNationEffectDefinition> InactiveEffects => inactiveEffects;

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
                    preparationProgress[nation.Id] = Math.Min(nation.PreparationSeconds, preparationProgress[nation.Id] + Math.Max(0d, seconds));
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
                if (effect.EffectType == "planned")
                {
                    if (!inactiveEffects.Contains(effect)) inactiveEffects.Add(effect);
                    continue;
                }

                Context.Modifiers.Add(new CivicModifierEntry("nation", nationId, effect.EffectType, effect.TargetId, effect.Amount, effect.CapGroup));
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
                return new CivicNationCandidateSnapshot(nation, state, ratio, progress, state == CivicNationCandidateState.Ready ? string.Empty : "조건 미충족");
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
