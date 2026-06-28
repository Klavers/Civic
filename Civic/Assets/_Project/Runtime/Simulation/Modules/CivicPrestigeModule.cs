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

        public CivicPrestigeModule(IReadOnlyList<CivicLegacyPerkDefinition> legacyPerks)
        {
            this.legacyPerks = legacyPerks ?? throw new ArgumentNullException(nameof(legacyPerks));
        }

        public override string FeatureId => CivicFeatureRegistry.Prestige;
        public IReadOnlyList<CivicLegacyPerkDefinition> LegacyPerks => legacyPerks;
        public bool HasPrestigedThisRun { get; private set; }

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
            return new CivicPrestigePreview(gdpScore, populationScore, eraScore, technologyScore, challengeScore, canPrestige, warning);
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
    }
}
