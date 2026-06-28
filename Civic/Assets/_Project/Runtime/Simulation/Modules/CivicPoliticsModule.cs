using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public sealed class CivicReformSnapshot
    {
        public CivicReformSnapshot(string targetInstitutionId, double progress, double support, double resistance, bool paused)
        {
            TargetInstitutionId = targetInstitutionId;
            Progress = progress;
            Support = support;
            Resistance = resistance;
            Paused = paused;
        }

        public string TargetInstitutionId { get; }
        public double Progress { get; }
        public double Support { get; }
        public double Resistance { get; }
        public bool Paused { get; }
    }

    public sealed class CivicPoliticsModule : CivicGameplayModuleBase
    {
        private const double NeutralLegitimacy = 50d;
        private const double NeutralSupport = 0.50d;
        private const double BasePoliticalCapitalPerSecond = 1d;
        private readonly CivicPoliticsContent content;
        private readonly Dictionary<string, string> activeByCategory = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> reformCountByCategory = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<CivicInstitutionEffectDefinition> inactiveEffects = new List<CivicInstitutionEffectDefinition>();
        private string targetInstitutionId;
        private double reformProgress;

        public CivicPoliticsModule(CivicPoliticsContent content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public override string FeatureId => CivicFeatureRegistry.Politics;
        public IReadOnlyDictionary<string, string> ActiveByCategory => activeByCategory;
        public IReadOnlyList<CivicInstitutionDefinition> Definitions => content.Institutions;
        public IReadOnlyList<CivicInstitutionEffectDefinition> InactiveEffects => inactiveEffects;
        public double Legitimacy { get; private set; } = NeutralLegitimacy;
        public double PoliticalCapital { get; private set; }
        public double FatigueRemaining { get; private set; }
        public CivicReformSnapshot Reform { get; private set; }

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            foreach (var group in content.Institutions.GroupBy(item => item.Category, StringComparer.Ordinal))
            {
                activeByCategory[group.Key] = group.Single(item => item.IsDefault).Id;
                reformCountByCategory[group.Key] = 0;
            }
            RebuildActiveEffects();
            PublishMetrics();
        }

        public override void AfterAdvance(double seconds)
        {
            var elapsed = Math.Max(0d, seconds);
            FatigueRemaining = Math.Max(0d, FatigueRemaining - elapsed);
            PoliticalCapital += elapsed * BasePoliticalCapitalPerSecond * PoliticalCapitalMultiplier();
            ApplyInstitutionUpkeep(elapsed);
            RecalculateLegitimacy();
            if (!string.IsNullOrEmpty(targetInstitutionId)) AdvanceReform(elapsed);
            PublishMetrics();
        }

        public bool IsUnlocked(string institutionId)
        {
            var institution = content.Institutions.FirstOrDefault(item => item.Id == institutionId);
            if (institution == null) return false;
            var unlocks = content.Unlocks.Where(item => item.InstitutionId == institutionId).ToArray();
            var direct = unlocks.Where(item => string.IsNullOrEmpty(item.AlternativeGroup));
            if (direct.Any(unlock => !UnlockSatisfied(unlock))) return false;
            return unlocks.Where(item => !string.IsNullOrEmpty(item.AlternativeGroup))
                .GroupBy(item => item.AlternativeGroup, StringComparer.Ordinal)
                .All(group => group.Any(UnlockSatisfied));
        }

        private bool UnlockSatisfied(CivicInstitutionUnlockDefinition unlock) => CivicConditionEvaluator.Compare(Context.Telemetry.GetMetric(unlock.MetricId, Context.MetaProgress), unlock.Comparator, unlock.Value);

        public bool TryPropose(string institutionId)
        {
            if (!string.IsNullOrEmpty(targetInstitutionId) || FatigueRemaining > 0d || !IsUnlocked(institutionId)) return false;
            var institution = content.Institutions.First(item => item.Id == institutionId);
            if (activeByCategory.TryGetValue(institution.Category, out var current) && current == institutionId) return false;
            if (PoliticalCapital + 1e-9d < institution.PoliticalCost || Context.Simulation.State.Resources["treasury"].ToDouble() + 1e-9d < institution.TreasuryCost) return false;
            PoliticalCapital -= institution.PoliticalCost;
            Context.Simulation.State.Resources["treasury"] -= CivicNumber.FromDouble(institution.TreasuryCost);
            targetInstitutionId = institutionId;
            reformProgress = 0d;
            Reform = new CivicReformSnapshot(institutionId, 0d, NeutralSupport, Resistance(), false);
            return true;
        }

        public bool CancelReform()
        {
            if (string.IsNullOrEmpty(targetInstitutionId)) return false;
            targetInstitutionId = string.Empty;
            reformProgress = 0d;
            Reform = null;
            return true;
        }

        public void GrantPoliticalCapital(double amount)
        {
            PoliticalCapital = Math.Max(0d, PoliticalCapital + amount);
            PublishMetrics();
        }

        public bool AddReformProgress(double percentagePoints)
        {
            if (string.IsNullOrEmpty(targetInstitutionId) || percentagePoints == 0d) return false;
            var institution = content.Institutions.First(item => item.Id == targetInstitutionId);
            var previous = reformProgress;
            reformProgress = Math.Max(0d, Math.Min(100d, reformProgress + percentagePoints));
            PublishThreshold(previous, reformProgress, 25d);
            PublishThreshold(previous, reformProgress, 50d);
            PublishThreshold(previous, reformProgress, 75d);
            if (reformProgress >= 100d - 1e-9d) CompleteReform(institution);
            else Reform = new CivicReformSnapshot(targetInstitutionId, reformProgress, NeutralSupport, Resistance(), false);
            PublishMetrics();
            return true;
        }

        private void AdvanceReform(double seconds)
        {
            var institution = content.Institutions.First(item => item.Id == targetInstitutionId);
            var resistance = Resistance();
            var paused = Legitimacy <= 0d || resistance >= 100d;
            if (!paused)
            {
                var speedMultiplier = Math.Max(0.1d, (Legitimacy - resistance + 100d) / 100d) * ReformSpeedMultiplier();
                var previous = reformProgress;
                reformProgress = Math.Min(100d, reformProgress + seconds * 100d / institution.ReformSeconds * speedMultiplier);
                PublishThreshold(previous, reformProgress, 25d);
                PublishThreshold(previous, reformProgress, 50d);
                PublishThreshold(previous, reformProgress, 75d);
                if (reformProgress >= 100d - 1e-9d)
                {
                    CompleteReform(institution);
                    return;
                }
            }

            Reform = new CivicReformSnapshot(targetInstitutionId, reformProgress, NeutralSupport, resistance, paused);
        }

        private void CompleteReform(CivicInstitutionDefinition institution)
        {
            activeByCategory[institution.Category] = institution.Id;
            reformCountByCategory[institution.Category]++;
            FatigueRemaining = institution.FatigueSeconds;
            targetInstitutionId = string.Empty;
            reformProgress = 0d;
            Reform = null;
            RebuildActiveEffects();
            Context.Simulation.RefreshSnapshot();
        }

        private void RebuildActiveEffects()
        {
            foreach (var institution in content.Institutions) Context.Modifiers.RemoveSource("institution", institution.Id);
            inactiveEffects.Clear();
            foreach (var institutionId in activeByCategory.Values)
            {
                foreach (var effect in content.Effects.Where(item => item.InstitutionId == institutionId && string.IsNullOrEmpty(item.CostType)))
                {
                    if (IsPoliticsOnlyEffect(effect.EffectType) || effect.EffectType == "planned")
                    {
                        if (effect.EffectType == "planned") inactiveEffects.Add(effect);
                        continue;
                    }
                    Context.Modifiers.Add(new CivicModifierEntry("institution", institutionId, effect.EffectType, effect.TargetId, effect.Amount, effect.CapGroup));
                }
            }
        }

        private void ApplyInstitutionUpkeep(double seconds)
        {
            if (seconds <= 0d) return;
            var gdpRatio = activeByCategory.Values
                .SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.CostType == "gdpRatio"))
                .Sum(item => item.Amount);
            if (gdpRatio <= 0d) return;
            var cost = Context.Simulation.Snapshot.Gdp.ToDouble() * gdpRatio * seconds;
            var treasury = Context.Simulation.State.Resources["treasury"].ToDouble();
            Context.Simulation.State.Resources["treasury"] = CivicNumber.FromDouble(Math.Max(0d, treasury - cost));
        }

        private void RecalculateLegitimacy()
        {
            var add = activeByCategory.Values.SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.EffectType == "legitimacyAdd")).Sum(item => item.Amount);
            var floor = activeByCategory.Values.SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.EffectType == "legitimacyFloor")).Select(item => item.Amount).DefaultIfEmpty(0d).Max();
            Legitimacy = Math.Max(floor, Math.Min(100d, NeutralLegitimacy + add));
        }

        private double PoliticalCapitalMultiplier() => Math.Max(0d, 1d + activeByCategory.Values.SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.EffectType == "politicalCapitalMultiplier")).Sum(item => item.Amount));
        private double ReformSpeedMultiplier() => Math.Max(0d, 1d + activeByCategory.Values.SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.EffectType == "reformSpeedMultiplier")).Sum(item => item.Amount));
        private double Resistance() => Math.Max(0d, Math.Min(100d, 50d - NeutralSupport * 50d + activeByCategory.Values.SelectMany(id => content.Effects.Where(item => item.InstitutionId == id && item.EffectType == "reformResistanceAdd")).Sum(item => item.Amount)));
        private static bool IsPoliticsOnlyEffect(string effectType) => effectType == "legitimacyAdd" || effectType == "legitimacyFloor" || effectType == "politicalCapitalMultiplier" || effectType == "reformSpeedMultiplier" || effectType == "reformResistanceAdd";

        private void PublishThreshold(double previous, double current, double threshold)
        {
            if (previous < threshold && current >= threshold) Context.Telemetry.SetExternalMetric("reform.threshold." + (int)threshold, 1d);
        }

        private void PublishMetrics()
        {
            foreach (var institution in content.Institutions)
            {
                Context.Telemetry.SetExternalMetric("institution.active." + institution.Id, activeByCategory.Values.Contains(institution.Id) ? 1d : 0d);
                Context.Telemetry.SetExternalMetric("institution.unlocked." + institution.Id, IsUnlocked(institution.Id) ? 1d : 0d);
                Context.Telemetry.SetExternalMetric("reform.target." + institution.Id, targetInstitutionId == institution.Id ? 1d : 0d);
            }
            foreach (var pair in reformCountByCategory) Context.Telemetry.SetExternalMetric("reform.count." + pair.Key, pair.Value);
            Context.Telemetry.SetExternalMetric("politics.legitimacy", Legitimacy);
            Context.Telemetry.SetExternalMetric("politics.capital", PoliticalCapital);
            Context.Telemetry.SetExternalMetric("politics.resistance", Resistance());
            Context.Telemetry.SetExternalMetric("reform.active", string.IsNullOrEmpty(targetInstitutionId) ? 0d : 1d);
            Context.Telemetry.SetExternalMetric("reform.progress", reformProgress);
        }
    }
}
