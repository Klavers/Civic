using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public enum CivicWonderState
    {
        Unavailable,
        Locked,
        Available,
        Building,
        Completed
    }

    public sealed class CivicWonderSnapshot
    {
        public CivicWonderSnapshot(CivicWonderDefinition definition, CivicWonderState state, double progress, string blockingReason, IReadOnlyDictionary<string, double> delivered)
        {
            Definition = definition;
            State = state;
            Progress = progress;
            BlockingReason = blockingReason;
            Delivered = delivered;
        }

        public CivicWonderDefinition Definition { get; }
        public CivicWonderState State { get; }
        public double Progress { get; }
        public string BlockingReason { get; }
        public IReadOnlyDictionary<string, double> Delivered { get; }
    }

    public sealed class CivicWonderModule : CivicGameplayModuleBase
    {
        private const double CancellationRefundRatio = 0.70d;
        private readonly CivicWonderContent content;
        private readonly Dictionary<string, double> delivered = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> completedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> timedEffectRemaining = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<CivicWonderEffectDefinition> inactiveEffects = new List<CivicWonderEffectDefinition>();
        private string activeProjectId;
        private double activeProgressBonus;
        private IReadOnlyList<CivicWonderSnapshot> snapshot = Array.Empty<CivicWonderSnapshot>();

        public CivicWonderModule(CivicWonderContent content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public override string FeatureId => CivicFeatureRegistry.Wonders;
        public string ActiveProjectId => activeProjectId;
        public IReadOnlyCollection<string> CompletedIds => completedIds;
        public IReadOnlyList<CivicWonderSnapshot> Snapshot => snapshot;
        public IReadOnlyList<CivicWonderEffectDefinition> InactiveEffects => inactiveEffects;
        public int ProvisionalEffectCount => content.Effects.Count(item => item.EffectType == CivicProvisionalEffect.Planned);

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            PublishMetrics();
            RebuildSnapshot();
        }

        public override void AfterAdvance(double seconds)
        {
            var elapsed = Math.Max(0d, seconds);
            AdvanceTimedEffects(elapsed);
            ApplyUpkeep(elapsed);
            if (!string.IsNullOrEmpty(activeProjectId))
            {
                Deliver(activeProjectId, elapsed);
                if (CalculateProgress(activeProjectId) >= 1d - 1e-9d)
                {
                    Complete(activeProjectId);
                }
            }

            Context.Simulation.RefreshSnapshot();
            PublishMetrics();
            RebuildSnapshot();
        }

        public bool TryStart(string wonderId)
        {
            if (!string.IsNullOrEmpty(activeProjectId) || completedIds.Contains(wonderId)) return false;
            var wonder = content.Wonders.FirstOrDefault(item => item.Id == wonderId);
            if (wonder == null || !IsUnlocked(wonder, out _)) return false;
            var treasury = Context.Simulation.State.Resources["treasury"].ToDouble();
            var costMultiplier = Context.Modifiers.Multiplier(CivicModifierEffectTypes.WonderCostMultiplier, wonderId, wonder.ConceptId, "*");
            var upfrontTreasury = wonder.UpfrontTreasury * costMultiplier;
            if (treasury + 1e-9d < upfrontTreasury) return false;
            Context.Simulation.State.Resources["treasury"] = CivicNumber.FromDouble(treasury - upfrontTreasury);
            activeProjectId = wonderId;
            activeProgressBonus = 0d;
            foreach (var cost in Costs(wonderId)) delivered[Key(wonderId, cost.ResourceId)] = 0d;
            Context.Simulation.RefreshSnapshot();
            PublishMetrics();
            RebuildSnapshot();
            return true;
        }

        public bool CancelActiveProject()
        {
            if (string.IsNullOrEmpty(activeProjectId)) return false;
            foreach (var cost in Costs(activeProjectId))
            {
                var amount = Delivered(activeProjectId, cost.ResourceId) * CancellationRefundRatio;
                RefundResource(cost.ResourceId, amount);
                delivered.Remove(Key(activeProjectId, cost.ResourceId));
            }

            activeProjectId = string.Empty;
            activeProgressBonus = 0d;
            Context.Simulation.RefreshSnapshot();
            PublishMetrics();
            RebuildSnapshot();
            return true;
        }

        public bool AddProgressFraction(double fraction)
        {
            if (string.IsNullOrEmpty(activeProjectId) || fraction <= 0d) return false;
            activeProgressBonus = Math.Min(1d, activeProgressBonus + fraction);
            if (CalculateProgress(activeProjectId) >= 1d - 1e-9d) Complete(activeProjectId);
            Context.Simulation.RefreshSnapshot();
            PublishMetrics();
            RebuildSnapshot();
            return true;
        }

        private void Deliver(string wonderId, double seconds)
        {
            if (seconds <= 0d) return;
            foreach (var cost in Costs(wonderId))
            {
                var current = Delivered(wonderId, cost.ResourceId);
                var effectiveCost = EffectiveCostAmount(cost, wonderId);
                var remaining = Math.Max(0d, effectiveCost - current);
                if (remaining <= 0d) continue;
                var progressMultiplier = Context.Modifiers.Multiplier(CivicModifierEffectTypes.WonderProgressMultiplier, wonderId, "*");
                var requested = Math.Min(remaining, cost.DeliveryRate * progressMultiplier * seconds);
                var consumed = ConsumeResource(cost.ResourceId, requested);
                delivered[Key(wonderId, cost.ResourceId)] = current + consumed;
            }
        }

        private double ConsumeResource(string resourceId, double requested)
        {
            if (resourceId == "food") return ConsumeFood(requested);
            if (!Context.Simulation.State.Resources.TryGetValue(resourceId, out var current)) return 0d;
            var amount = Math.Min(requested, Math.Max(0d, current.ToDouble()));
            Context.Simulation.State.Resources[resourceId] = CivicNumber.FromDouble(current.ToDouble() - amount);
            return amount;
        }

        private double ConsumeFood(double requestedFood)
        {
            var remaining = requestedFood;
            var consumedFood = 0d;
            foreach (var resource in Context.Simulation.Data.Resources.Where(item => item.Category == ResourceCategory.Element && item.FoodConversion > 0d))
            {
                var stock = Context.Simulation.State.Resources[resource.Id].ToDouble();
                var availableFood = stock * resource.FoodConversion;
                var usedFood = Math.Min(remaining, availableFood);
                if (usedFood <= 0d) continue;
                Context.Simulation.State.Resources[resource.Id] = CivicNumber.FromDouble(stock - usedFood / resource.FoodConversion);
                consumedFood += usedFood;
                remaining -= usedFood;
                if (remaining <= 1e-9d) break;
            }

            return consumedFood;
        }

        private void RefundResource(string resourceId, double amount)
        {
            if (amount <= 0d) return;
            if (resourceId == "food")
            {
                var food = Context.Simulation.Data.Resources.FirstOrDefault(item => item.Category == ResourceCategory.Element && item.FoodConversion > 0d);
                if (food != null)
                {
                    Context.Simulation.State.Resources[food.Id] += CivicNumber.FromDouble(amount / food.FoodConversion);
                }
                return;
            }

            if (Context.Simulation.State.Resources.ContainsKey(resourceId))
            {
                Context.Simulation.State.Resources[resourceId] += CivicNumber.FromDouble(amount);
            }
        }

        private void Complete(string wonderId)
        {
            completedIds.Add(wonderId);
            activeProjectId = string.Empty;
            activeProgressBonus = 0d;
            foreach (var effect in content.Effects.Where(item => item.WonderId == wonderId))
            {
                var resolved = effect.Resolve();
                var sourceType = resolved.Duration > 0d ? "wonderTimed" : "wonder";
                var sourceId = resolved.Duration > 0d ? wonderId + ":" + resolved.EffectType + ":" + resolved.TargetId : wonderId;
                Context.Modifiers.Add(new CivicModifierEntry(sourceType, sourceId, resolved.EffectType, resolved.TargetId, resolved.Amount, resolved.CapGroup));
                if (resolved.Duration > 0d) timedEffectRemaining[sourceId] = resolved.Duration;
            }

            if (!Context.MetaProgress.CompletedWonderIds.Contains(wonderId)) Context.MetaProgress.CompletedWonderIds.Add(wonderId);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            PublishMetrics();
        }

        private void AdvanceTimedEffects(double seconds)
        {
            foreach (var sourceId in timedEffectRemaining.Keys.ToArray())
            {
                timedEffectRemaining[sourceId] -= seconds;
                if (timedEffectRemaining[sourceId] > 0d) continue;
                Context.Modifiers.RemoveSource("wonderTimed", sourceId);
                timedEffectRemaining.Remove(sourceId);
            }
        }

        private void ApplyUpkeep(double seconds)
        {
            if (seconds <= 0d) return;
            var treasuryCost = 0d;
            foreach (var wonder in content.Wonders.Where(item => completedIds.Contains(item.Id)))
            {
                if (wonder.UpkeepType == "treasuryPerSecond") treasuryCost += wonder.UpkeepAmount * seconds;
                else if (wonder.UpkeepType == "gdpRatio") treasuryCost += Context.Simulation.Snapshot.Gdp.ToDouble() * wonder.UpkeepAmount * seconds;
            }

            var treasury = Context.Simulation.State.Resources["treasury"].ToDouble();
            Context.Simulation.State.Resources["treasury"] = CivicNumber.FromDouble(Math.Max(0d, treasury - treasuryCost));
        }

        private bool IsUnlocked(CivicWonderDefinition wonder, out string reason)
        {
            var missing = wonder.RequiredFeatureIds.FirstOrDefault(id => !Context.IsFeatureEnabled(id));
            if (missing != null)
            {
                reason = "필요 모듈 OFF: " + missing;
                return false;
            }

            if (!string.IsNullOrEmpty(wonder.EraId) &&
                Context.Simulation.Data.ErasById.TryGetValue(wonder.EraId, out var era) &&
                Context.Telemetry.HighestEraOrder < era.Order)
            {
                reason = "시대 필요: " + wonder.EraId;
                return false;
            }

            if (!string.IsNullOrEmpty(wonder.TechnologyId) && !Context.Simulation.State.ResearchedTechnologyIds.Contains(wonder.TechnologyId))
            {
                reason = "기술 필요: " + wonder.TechnologyId;
                return false;
            }

            var failed = content.Conditions.Where(item => item.WonderId == wonder.Id).FirstOrDefault(condition =>
                !CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress), condition.Comparator, condition.Value));
            reason = failed == null ? string.Empty : "조건 미충족: " + failed.MetricId;
            return failed == null;
        }

        private double CalculateProgress(string wonderId)
        {
            return Math.Min(1d, Costs(wonderId).Select(cost => Delivered(wonderId, cost.ResourceId) / EffectiveCostAmount(cost, wonderId)).DefaultIfEmpty(0d).Min() + (wonderId == activeProjectId ? activeProgressBonus : 0d));
        }

        private double EffectiveCostAmount(CivicWonderCostDefinition cost, string wonderId)
        {
            var wonder = content.Wonders.First(item => item.Id == wonderId);
            return Math.Max(1e-9d, cost.Amount * Context.Modifiers.Multiplier(CivicModifierEffectTypes.WonderCostMultiplier, wonderId, wonder.ConceptId, "*"));
        }

        private IEnumerable<CivicWonderCostDefinition> Costs(string wonderId) => content.Costs.Where(item => item.WonderId == wonderId);
        private double Delivered(string wonderId, string resourceId) => delivered.TryGetValue(Key(wonderId, resourceId), out var value) ? value : 0d;
        private static string Key(string wonderId, string resourceId) => wonderId + ":" + resourceId;

        private void PublishMetrics()
        {
            Context.Telemetry.SetExternalMetric("wonder.completedCount", completedIds.Count);
            Context.Telemetry.SetExternalMetric("wonder.ancientCompletedCount", completedIds.Count(id =>
                content.Wonders.First(item => item.Id == id).EraId == "ancient"));
            Context.Telemetry.SetExternalMetric("wonder.activeProgress", string.IsNullOrEmpty(activeProjectId) ? 0d : CalculateProgress(activeProjectId));
        }

        private void RebuildSnapshot()
        {
            snapshot = content.Wonders.Select(wonder =>
            {
                if (!IsUnlocked(wonder, out var reason)) return new CivicWonderSnapshot(wonder, CivicWonderState.Locked, 0d, reason, new Dictionary<string, double>());
                var state = completedIds.Contains(wonder.Id) ? CivicWonderState.Completed
                    : wonder.Id == activeProjectId ? CivicWonderState.Building
                    : CivicWonderState.Available;
                var amounts = Costs(wonder.Id).ToDictionary(cost => cost.ResourceId, cost => Delivered(wonder.Id, cost.ResourceId), StringComparer.Ordinal);
                return new CivicWonderSnapshot(wonder, state, state == CivicWonderState.Completed ? 1d : CalculateProgress(wonder.Id), string.Empty, amounts);
            }).ToArray();
        }
    }
}
