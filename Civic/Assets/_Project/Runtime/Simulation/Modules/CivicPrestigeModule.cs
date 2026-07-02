using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public sealed class CivicPrestigePreview
    {
        public CivicPrestigePreview(int gdpScore, int populationScore, int eraScore, int technologyScore, int challengeScore, bool canPrestige, string warning)
        {
            GdpScore = gdpScore;
            PopulationScore = populationScore;
            EraScore = eraScore;
            TechnologyScore = technologyScore;
            ChallengeScore = challengeScore;
            TotalScore = gdpScore + populationScore + eraScore + technologyScore + challengeScore;
            CanPrestige = canPrestige;
            Warning = warning;
        }

        public int GdpScore { get; }
        public int PopulationScore { get; }
        public int EraScore { get; }
        public int TechnologyScore { get; }
        public int ChallengeScore { get; }
        public int TotalScore { get; }
        public bool CanPrestige { get; }
        public string Warning { get; }
    }

    public sealed class CivicPrestigeModule : CivicGameplayModuleBase
    {
        private readonly IReadOnlyList<CivicLegacyPerkDefinition> legacyPerks;
        private readonly Dictionary<string, double> timedLegacyRemaining = new Dictionary<string, double>(StringComparer.Ordinal);

        public CivicPrestigeModule(IReadOnlyList<CivicLegacyPerkDefinition> legacyPerks)
        {
            this.legacyPerks = legacyPerks ?? throw new ArgumentNullException(nameof(legacyPerks));
        }

        public override string FeatureId => CivicFeatureRegistry.Prestige;
        public IReadOnlyList<CivicLegacyPerkDefinition> LegacyPerks => legacyPerks;
        public bool HasPrestigedThisRun { get; private set; }

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            ApplyOwnedLegacyPerks();
        }

        public override void AfterAdvance(double seconds)
        {
            foreach (var sourceId in timedLegacyRemaining.Keys.ToArray())
            {
                timedLegacyRemaining[sourceId] -= Math.Max(0d, seconds);
                if (timedLegacyRemaining[sourceId] > 0d) continue;
                Context.Modifiers.RemoveSource("legacyTimed", sourceId);
                timedLegacyRemaining.Remove(sourceId);
            }
        }

        public CivicPrestigePreview CreatePreview()
        {
            var telemetry = Context.Telemetry;
            var gdpScore = (int)Math.Floor(Math.Log10(telemetry.PeakGdp + 1d) * 4d);
            var populationScore = (int)Math.Floor(Math.Sqrt(Math.Max(0d, telemetry.PeakPopulation)));
            var eraScore = telemetry.HighestEraOrder * 5;
            var eraWeightSum = telemetry.ResearchedIds
                .Select(id => Context.Simulation.Data.TechnologiesById.TryGetValue(id, out var technology)
                    ? (Context.Simulation.Data.ErasById.TryGetValue(technology.EraId, out var era) ? era.Order + 1 : 0)
                    : 0)
                .Sum();
            var technologyScore = eraWeightSum / 5;
            var achievementModule = Context.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements);
            var challengeScore = achievementModule?.PrestigeRewardEarnedThisRun ?? 0;
            var canPrestige = telemetry.HighestEraOrder >= 1;
            var total = gdpScore + populationScore + eraScore + technologyScore + challengeScore;
            var warning = !canPrestige
                ? "고대시대 진입 후 환생할 수 있습니다."
                : total == 0 ? "예상 환생 포인트가 0입니다." : string.Empty;
            var multiplier = Context == null ? 1d : Context.Modifiers.Multiplier(CivicModifierEffectTypes.PrestigeGainMultiplier, "*");
            var preview = new CivicPrestigePreview(gdpScore, populationScore, eraScore, technologyScore, challengeScore, canPrestige, warning);
            if (Math.Abs(multiplier - 1d) <= 1e-9d) return preview;
            var adjustedChallenge = challengeScore + (int)Math.Floor(preview.TotalScore * multiplier) - preview.TotalScore;
            return new CivicPrestigePreview(gdpScore, populationScore, eraScore, technologyScore, adjustedChallenge, canPrestige, warning);
        }

        public bool TryPrestige(out int awardedPoints)
        {
            var preview = CreatePreview();
            awardedPoints = 0;
            if (!preview.CanPrestige || HasPrestigedThisRun)
            {
                return false;
            }

            awardedPoints = preview.TotalScore;
            Context.MetaProgress.PrestigePoints += awardedPoints;
            Context.MetaProgress.PrestigeCount++;
            HasPrestigedThisRun = true;
            CivicMetaSession.Store.Save(Context.MetaProgress);
            return true;
        }

        public bool TryPurchaseLegacyPerk(string perkId, out int cost, out string reason)
        {
            cost = 0;
            reason = string.Empty;
            var perk = legacyPerks.FirstOrDefault(item => item.Id == perkId);
            if (perk == null) { reason = "알 수 없는 유산입니다."; return false; }
            var rank = Context.MetaProgress.GetPerkRank(perkId);
            if (rank >= perk.MaxRank) { reason = "최대 단계입니다."; return false; }
            cost = perk.CostForNextRank(rank);
            if (cost <= 0) { reason = "단계 비용이 유효하지 않습니다."; return false; }
            if (Context.MetaProgress.PrestigePoints < cost) { reason = "환생 포인트가 부족합니다."; return false; }
            Context.MetaProgress.PrestigePoints -= cost;
            Context.MetaProgress.SetPerkRank(perkId, rank + 1);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            reason = "구매한 효과는 다음 런부터 적용됩니다.";
            return true;
        }

        private void ApplyOwnedLegacyPerks()
        {
            foreach (var perk in legacyPerks)
            {
                var rank = Math.Min(perk.MaxRank, Math.Max(0, Context.MetaProgress.GetPerkRank(perk.Id)));
                if (rank <= 0) continue;
                var amount = perk.Amount * rank;
                if (perk.EffectType == "startingResourceAdd")
                {
                    if (perk.TargetId == "population") Context.Simulation.State.BasePopulation += CivicNumber.FromDouble(amount);
                    else Context.Simulation.GrantResource(perk.TargetId, CivicNumber.FromDouble(amount));
                }
                else if (perk.EffectType == "startingUnlockedResourceAdd")
                {
                    foreach (var resource in Context.Simulation.Snapshot.Resources.Where(item => item.Category == ResourceCategory.Element)) Context.Simulation.GrantResource(resource.Id, CivicNumber.FromDouble(amount));
                }
                else
                {
                    ApplyLegacyModifier(perk, amount);
                }
            }
            Context.Simulation.RefreshSnapshot();
        }

        private void ApplyLegacyModifier(CivicLegacyPerkDefinition perk, double amount)
        {
            var effectType = CivicModifierEffectTypes.ProvisionalFlag;
            var targetId = perk.TargetId;
            var capGroup = "provisional_flag";
            if (perk.EffectType == "earlyEraConstructionCostMultiplier")
            {
                var replacements = perk.TargetId.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(eraId => new CivicModifierEntry("legacy", perk.Id, CivicModifierEffectTypes.ConstructionCostMultiplier, "era:" + eraId.Trim(), amount, "construction_cost"))
                    .ToArray();
                Context.Modifiers.ReplaceSource("legacy", perk.Id, replacements);
                return;
            }
            else if (perk.EffectType == "timedPopulationConsumptionMultiplier") { effectType = CivicModifierEffectTypes.PopulationConsumptionMultiplier; targetId = "*"; capGroup = "population_consumption"; }
            else if (perk.EffectType == "housingSupplyBuffer") { effectType = CivicModifierEffectTypes.FoodConversionMultiplier; targetId = "food"; capGroup = "food_conversion"; }
            else if (perk.EffectType == "previousEraTechnologyCostMultiplier") { effectType = CivicModifierEffectTypes.TechnologyCostMultiplier; targetId = "*"; capGroup = "technology_cost"; }
            else if (perk.EffectType == "firstEraResearchRefund") { effectType = CivicModifierEffectTypes.TechnologyCostMultiplier; targetId = "era:primitive"; amount = -Math.Abs(amount); capGroup = "technology_cost"; }
            else if (perk.EffectType == "reformCostMultiplier") { effectType = CivicModifierEffectTypes.ReformCostMultiplier; targetId = "*"; capGroup = "reform_cost"; }
            else if (perk.EffectType == "nationConditionDurationMultiplier") { effectType = CivicModifierEffectTypes.NationConditionDurationMultiplier; targetId = "*"; capGroup = "nation_condition"; }
            else if (perk.EffectType == "startingTaxRateAdd") { effectType = CivicModifierEffectTypes.TaxRateAdd; targetId = "treasury"; capGroup = "tax_rate"; }
            else if (perk.EffectType == "firstWonderCostMultiplier") { effectType = CivicModifierEffectTypes.WonderCostMultiplier; targetId = "*"; capGroup = "wonder_cost"; }
            else if (perk.EffectType == "freePersonReroll") { effectType = CivicModifierEffectTypes.PersonCandidateWeightMultiplier; targetId = "*"; amount = 0.25d * RankSafe(perk.Id); capGroup = "person_candidate"; }
            else if (perk.EffectType == "challengePrestigeMultiplier") { effectType = CivicModifierEffectTypes.PrestigeGainMultiplier; targetId = "*"; capGroup = "prestige_gain"; }

            var sourceType = perk.EffectType == "timedPopulationConsumptionMultiplier" ? "legacyTimed" : "legacy";
            Context.Modifiers.ReplaceSource(sourceType, perk.Id, new[] { new CivicModifierEntry(sourceType, perk.Id, effectType, targetId, amount, capGroup) });
            if (sourceType == "legacyTimed" && double.TryParse(perk.TargetId, out var duration)) timedLegacyRemaining[perk.Id] = duration;
        }

        private int RankSafe(string perkId) => Math.Max(1, Context.MetaProgress.GetPerkRank(perkId));
    }
}
