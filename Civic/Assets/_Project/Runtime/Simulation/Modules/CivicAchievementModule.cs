using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public enum CivicAchievementState
    {
        Unavailable,
        InProgress,
        Failed,
        Completed
    }

    public sealed class CivicAchievementProgressSnapshot
    {
        public CivicAchievementProgressSnapshot(
            CivicAchievementDefinition definition,
            CivicAchievementState state,
            double heldSeconds,
            double requiredSeconds,
            string blockingReason)
        {
            Definition = definition;
            State = state;
            HeldSeconds = heldSeconds;
            RequiredSeconds = requiredSeconds;
            BlockingReason = blockingReason;
        }

        public CivicAchievementDefinition Definition { get; }
        public CivicAchievementState State { get; }
        public double HeldSeconds { get; }
        public double RequiredSeconds { get; }
        public string BlockingReason { get; }
    }

    public sealed class CivicAchievementModule : CivicGameplayModuleBase
    {
        private readonly IReadOnlyList<CivicAchievementDefinition> definitions;
        private readonly Dictionary<string, double> heldSeconds = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> failedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedThisRun = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<CivicAchievementProgressSnapshot> snapshot = Array.Empty<CivicAchievementProgressSnapshot>();

        public CivicAchievementModule(IReadOnlyList<CivicAchievementDefinition> definitions)
        {
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public override string FeatureId => CivicFeatureRegistry.Achievements;
        public IReadOnlyList<CivicAchievementProgressSnapshot> Snapshot => snapshot;
        public IReadOnlyCollection<string> CompletedThisRun => completedThisRun;
        public int PrestigeRewardEarnedThisRun { get; private set; }

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            Evaluate(0d);
        }

        public override void AfterAdvance(double seconds)
        {
            Evaluate(Math.Max(0d, seconds));
        }

        public override void OnBuildingConstructed(string buildingId)
        {
            Evaluate(0d);
        }

        public override void OnTechnologyResearched(string technologyId)
        {
            Evaluate(0d);
        }

        private void Evaluate(double seconds)
        {
            var snapshots = new List<CivicAchievementProgressSnapshot>(definitions.Count);
            foreach (var definition in definitions)
            {
                var unavailableFeature = definition.RequiredFeatureIds.FirstOrDefault(id => !Context.IsFeatureEnabled(id));
                if (unavailableFeature != null)
                {
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.Unavailable,
                        0d,
                        RequiredDuration(definition),
                        $"필요 모듈 OFF: {unavailableFeature}"));
                    continue;
                }

                if (Context.MetaProgress.HasAchievement(definition.Id))
                {
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.Completed,
                        RequiredDuration(definition),
                        RequiredDuration(definition),
                        string.Empty));
                    continue;
                }

                if (failedIds.Contains(definition.Id))
                {
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.Failed,
                        0d,
                        RequiredDuration(definition),
                        "금지 조건을 위반했습니다."));
                    continue;
                }

                var forbidden = definition.Conditions.FirstOrDefault(condition =>
                    condition.Forbidden && CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress), condition.Comparator, condition.Value));
                if (forbidden != null)
                {
                    failedIds.Add(definition.Id);
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.Failed,
                        0d,
                        RequiredDuration(definition),
                        $"금지 조건 위반: {forbidden.MetricId}"));
                    continue;
                }

                var requiredConditions = definition.Conditions.Where(condition => !condition.Forbidden).ToArray();
                var unsatisfied = requiredConditions.FirstOrDefault(condition =>
                    !CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress), condition.Comparator, condition.Value));
                if (unsatisfied != null)
                {
                    heldSeconds[definition.Id] = 0d;
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.InProgress,
                        0d,
                        RequiredDuration(definition),
                        $"미충족: {unsatisfied.MetricId} {unsatisfied.Comparator} {unsatisfied.Value}"));
                    continue;
                }

                var requiredDuration = RequiredDuration(definition);
                heldSeconds[definition.Id] = heldSeconds.TryGetValue(definition.Id, out var current) ? current + seconds : seconds;
                if (heldSeconds[definition.Id] + 1e-9d >= requiredDuration)
                {
                    Complete(definition);
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.Completed,
                        requiredDuration,
                        requiredDuration,
                        string.Empty));
                }
                else
                {
                    snapshots.Add(new CivicAchievementProgressSnapshot(
                        definition,
                        CivicAchievementState.InProgress,
                        heldSeconds[definition.Id],
                        requiredDuration,
                        string.Empty));
                }
            }

            snapshot = snapshots;
            Context.Telemetry.SetExternalMetric("meta.hardAchievementCount", definitions.Count(item => item.Category == "challenge" && Context.MetaProgress.HasAchievement(item.Id)));
        }

        private void Complete(CivicAchievementDefinition definition)
        {
            if (Context.MetaProgress.HasAchievement(definition.Id))
            {
                return;
            }

            Context.MetaProgress.CompletedAchievementIds.Add(definition.Id);
            completedThisRun.Add(definition.Id);
            PrestigeRewardEarnedThisRun += definition.PrestigeReward;
            CivicMetaSession.Store.Save(Context.MetaProgress);
        }

        private static double RequiredDuration(CivicAchievementDefinition definition)
        {
            return definition.Conditions.Where(condition => !condition.Forbidden).Select(condition => condition.Duration).DefaultIfEmpty(0d).Max();
        }

    }
}
