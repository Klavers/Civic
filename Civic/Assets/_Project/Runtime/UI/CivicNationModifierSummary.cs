using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;
using Civic.Simulation;
using Civic.Simulation.Modules;

namespace Civic.UI
{
    public sealed class CivicNationModifierContribution
    {
        public CivicNationModifierContribution(string sourceType, string sourceId, string sourceName, double amount)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            SourceName = sourceName;
            Amount = amount;
        }

        public string SourceType { get; }
        public string SourceId { get; }
        public string SourceName { get; }
        public double Amount { get; }
    }

    public sealed class CivicNationModifierSummary
    {
        public CivicNationModifierSummary(string effectType, string targetId, string effectName, string targetName, double amount, IReadOnlyList<CivicNationModifierContribution> contributions)
        {
            EffectType = effectType;
            TargetId = targetId;
            EffectName = effectName;
            TargetName = targetName;
            Amount = amount;
            Contributions = contributions;
        }

        public string Key => EffectType + "|" + TargetId;
        public string EffectType { get; }
        public string TargetId { get; }
        public string EffectName { get; }
        public string TargetName { get; }
        public double Amount { get; }
        public IReadOnlyList<CivicNationModifierContribution> Contributions { get; }
    }

    public sealed class CivicNationModifierGroup
    {
        public CivicNationModifierGroup(string effectType, string effectName, IReadOnlyList<CivicNationModifierSummary> targets)
        {
            EffectType = effectType;
            EffectName = effectName;
            Targets = targets ?? Array.Empty<CivicNationModifierSummary>();
        }

        public string Key => EffectType;
        public string EffectType { get; }
        public string EffectName { get; }
        public IReadOnlyList<CivicNationModifierSummary> Targets { get; }
        public int ContributionCount => Targets.Sum(target => target.Contributions.Count);
    }

    public static class CivicNationModifierSummaryBuilder
    {
        public const string TechnologyOutputAdd = "technologyOutputAdd";
        public const string TechnologyConditionalOutputAdd = "technologyConditionalOutputAdd";

        public static IReadOnlyList<CivicNationModifierSummary> Build(CivicModuleRuntime runtime)
        {
            if (runtime == null) return Array.Empty<CivicNationModifierSummary>();
            var groups = new Dictionary<string, MutableSummary>(StringComparer.Ordinal);
            foreach (var entry in runtime.Simulation.Modifiers.Entries)
            {
                Add(groups, entry.EffectType, entry.TargetId, entry.Amount, entry.CapGroup,
                    new CivicNationModifierContribution(entry.SourceType, entry.SourceId, ResolveSourceName(runtime, entry.SourceType, entry.SourceId), entry.Amount));
            }

            foreach (var effect in runtime.Simulation.Data.TechnologyEffects.Where(effect =>
                runtime.Simulation.State.ResearchedTechnologyIds.Contains(effect.TechnologyId) &&
                effect.EffectType != TechnologyEffectType.PlannedFollowUp))
            {
                var technologyName = runtime.Simulation.Data.TechnologiesById.TryGetValue(effect.TechnologyId, out var technology)
                    ? technology.DisplayNameKo
                    : effect.TechnologyId;
                var amount = effect.Amount.ToDouble();
                var effectType = effect.EffectType == TechnologyEffectType.TaxRateAdd
                    ? CivicModifierEffectTypes.TaxRateAdd
                    : effect.EffectType == TechnologyEffectType.ConditionalOutputAdd
                        ? TechnologyConditionalOutputAdd
                        : TechnologyOutputAdd;
                var targetId = effect.EffectType == TechnologyEffectType.TaxRateAdd
                    ? "treasury"
                    : TechnologyTarget(effect);
                Add(groups, effectType, targetId, amount, string.Empty,
                    new CivicNationModifierContribution("technology", effect.TechnologyId, technologyName, amount));
            }

            return groups.Values
                .Select(group => group.ToSummary(runtime))
                .OrderBy(summary => summary.EffectType, StringComparer.Ordinal)
                .ThenBy(summary => summary.TargetId, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<CivicNationModifierGroup> BuildGroups(CivicModuleRuntime runtime)
        {
            return Build(runtime)
                .GroupBy(summary => summary.EffectType, StringComparer.Ordinal)
                .Select(group => new CivicNationModifierGroup(
                    group.Key,
                    group.First().EffectName,
                    group.OrderBy(summary => summary.TargetName, StringComparer.Ordinal).ThenBy(summary => summary.TargetId, StringComparer.Ordinal).ToArray()))
                .OrderBy(group => group.EffectName, StringComparer.Ordinal)
                .ThenBy(group => group.EffectType, StringComparer.Ordinal)
                .ToArray();
        }

        public static string CurrentNationName(CivicModuleRuntime runtime)
        {
            var module = runtime?.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);
            if (module == null) return "국가 설립 모듈 비활성";
            if (string.IsNullOrEmpty(module.CurrentNationId)) return "설립된 국가 없음";
            return module.Definitions.FirstOrDefault(item => item.Id == module.CurrentNationId)?.DisplayNameKo ?? module.CurrentNationId;
        }

        private static string TechnologyTarget(TechnologyEffectDefinition effect)
        {
            var target = !string.IsNullOrEmpty(effect.TargetBuildingId) ? "building:" + effect.TargetBuildingId : "group:" + effect.TargetGroupId;
            if (effect.EffectType == TechnologyEffectType.ConditionalOutputAdd)
            {
                return target + "/input:" + effect.InputResourceId + "/output:" + effect.OutputResourceId;
            }
            return target + "/output:" + effect.OutputResourceId;
        }

        private static void Add(
            IDictionary<string, MutableSummary> groups,
            string effectType,
            string targetId,
            double amount,
            string capGroup,
            CivicNationModifierContribution contribution)
        {
            var key = effectType + "|" + targetId;
            if (!groups.TryGetValue(key, out var group))
            {
                group = new MutableSummary(effectType, targetId);
                groups.Add(key, group);
            }
            group.Add(amount, capGroup, contribution);
        }

        private static string ResolveSourceName(CivicModuleRuntime runtime, string sourceType, string sourceId)
        {
            if (sourceType == "nation")
            {
                return runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation)?.Definitions.FirstOrDefault(item => item.Id == sourceId)?.DisplayNameKo ?? sourceId;
            }
            if (sourceType == "institution")
            {
                return runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics)?.Definitions.FirstOrDefault(item => item.Id == sourceId)?.DisplayNameKo ?? sourceId;
            }
            if (sourceType == "wonder")
            {
                return runtime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders)?.Definitions.FirstOrDefault(item => item.Id == sourceId)?.DisplayNameKo ?? sourceId;
            }
            if (sourceType == "civilization")
            {
                return runtime.GetModule<CivicCivilizationModule>(CivicFeatureRegistry.StartCivilizations)?.Definitions.FirstOrDefault(item => item.Id == sourceId)?.DisplayNameKo ?? sourceId;
            }
            if (sourceType == "person" || sourceType == "personLegacy" || sourceType == "personAbility")
            {
                var personId = sourceId.Split(':')[0];
                var personName = runtime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople)?.Definitions.FirstOrDefault(item => item.Id == personId)?.DisplayNameKo ?? personId;
                return sourceType == "personAbility" && sourceId.Contains(":") ? personName + " · " + sourceId.Substring(sourceId.IndexOf(':') + 1) : personName;
            }
            if (sourceType == "event")
            {
                var eventId = sourceId.Split(':')[0];
                return runtime.GetModule<CivicEventModule>(CivicFeatureRegistry.Events)?.Definitions.FirstOrDefault(item => item.Id == eventId)?.TitleKo ?? eventId;
            }
            if (sourceType == "legacy")
            {
                return runtime.GetModule<CivicPrestigeModule>(CivicFeatureRegistry.Prestige)?.LegacyPerks.FirstOrDefault(item => item.Id == sourceId)?.DisplayNameKo ?? sourceId;
            }
            if (sourceType == "achievement")
            {
                return runtime.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements)?.Snapshot.FirstOrDefault(item => item.Definition.Id == sourceId)?.Definition.TitleKo ?? sourceId;
            }
            return sourceId;
        }

        private static string EffectName(string effectType)
        {
            switch (effectType)
            {
                case CivicModifierEffectTypes.ResourceOutputMultiplier: return "자원 산출";
                case CivicModifierEffectTypes.ResourceOutputAdd: return "자원 산출량";
                case CivicModifierEffectTypes.ResourceInputMultiplier: return "자원 투입";
                case CivicModifierEffectTypes.BuildingOutputMultiplier: return "건물 산출";
                case CivicModifierEffectTypes.BuildingOutputAdd: return "건물 산출량";
                case CivicModifierEffectTypes.BuildingInputMultiplier: return "건물 투입";
                case CivicModifierEffectTypes.BuildingInputAdd: return "건물 투입량";
                case CivicModifierEffectTypes.ConstructionCostMultiplier: return "건설 비용";
                case CivicModifierEffectTypes.ConstructionCostAdd: return "건설 비용 고정값";
                case CivicModifierEffectTypes.PopulationUseAdd: return "인구 사용량";
                case CivicModifierEffectTypes.PopulationBaseAdd: return "기본 인구";
                case CivicModifierEffectTypes.PopulationScienceMultiplier: return "인구 기반 과학";
                case CivicModifierEffectTypes.TechnologyCostMultiplier: return "기술 비용";
                case CivicModifierEffectTypes.TreasuryIncomeMultiplier: return "국고 수입";
                case CivicModifierEffectTypes.TaxRateAdd: return "세율";
                case CivicModifierEffectTypes.PopulationConsumptionMultiplier: return "인구층 소비";
                case CivicModifierEffectTypes.ResourcePriceFloorAdd: return "최저 가격";
                case CivicModifierEffectTypes.ResourceGdpMultiplier: return "자원 GDP";
                case CivicModifierEffectTypes.FoodConversionMultiplier: return "식량 환산";
                case CivicModifierEffectTypes.ConstructionTreasuryCostMultiplier: return "건설부문 국고 비용";
                case CivicModifierEffectTypes.WonderCostMultiplier: return "불가사의 비용";
                case CivicModifierEffectTypes.WonderProgressMultiplier: return "불가사의 건축 속도";
                case CivicModifierEffectTypes.EventWeightMultiplier: return "이벤트 발생 가중치";
                case CivicModifierEffectTypes.EventCooldownMultiplier: return "이벤트 재발 대기";
                case CivicModifierEffectTypes.PrestigeGainMultiplier: return "환생 포인트 획득";
                case CivicModifierEffectTypes.PoliticalCapitalMultiplier: return "정치력 획득";
                case CivicModifierEffectTypes.ReformCostMultiplier: return "개혁 비용";
                case CivicModifierEffectTypes.ReformSpeedMultiplier: return "개혁 속도";
                case CivicModifierEffectTypes.ReformResistanceAdd: return "개혁 저항";
                case CivicModifierEffectTypes.LegitimacyAdd: return "정당성";
                case CivicModifierEffectTypes.LivingStandardAdd: return "생활수준";
                case CivicModifierEffectTypes.PersonCandidateWeightMultiplier: return "위인 후보 가중치";
                case CivicModifierEffectTypes.PersonLegacyMultiplier: return "위인 유산";
                case CivicModifierEffectTypes.NationPreparationSpeedMultiplier: return "국가 설립 준비 속도";
                case CivicModifierEffectTypes.NationConditionDurationMultiplier: return "국가 설립 조건 기간";
                case TechnologyOutputAdd: return "기술 추가 산출";
                case TechnologyConditionalOutputAdd: return "기술 조건부 산출";
                default: return effectType;
            }
        }

        private static string TargetName(CivicModuleRuntime runtime, string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == "*") return "전체";
            if (runtime.Simulation.Data.ResourcesById.TryGetValue(targetId, out var resource)) return resource.DisplayNameKo;
            if (runtime.Simulation.Data.BuildingsById.TryGetValue(targetId, out var building)) return building.DisplayNameKo;
            if (runtime.Simulation.Data.ErasById.TryGetValue(targetId.Replace("era:", string.Empty), out var era)) return era.DisplayNameKo;
            if (targetId.StartsWith("building:", StringComparison.Ordinal))
            {
                var parts = targetId.Split('/');
                var buildingId = parts[0].Substring("building:".Length);
                var buildingName = runtime.Simulation.Data.BuildingsById.TryGetValue(buildingId, out var targetBuilding) ? targetBuilding.DisplayNameKo : buildingId;
                var inputPart = parts.FirstOrDefault(part => part.StartsWith("input:", StringComparison.Ordinal));
                var outputPart = parts.FirstOrDefault(part => part.StartsWith("output:", StringComparison.Ordinal));
                var inputName = ResourcePartName(runtime, inputPart, "input:");
                var outputName = ResourcePartName(runtime, outputPart, "output:");
                return string.IsNullOrEmpty(inputName) ? $"{buildingName} → {outputName}" : $"{buildingName} · {inputName} 투입 → {outputName}";
            }
            return targetId;
        }

        private static string ResourcePartName(CivicModuleRuntime runtime, string part, string prefix)
        {
            if (string.IsNullOrEmpty(part)) return string.Empty;
            var id = part.Substring(prefix.Length);
            return runtime.Simulation.Data.ResourcesById.TryGetValue(id, out var resource) ? resource.DisplayNameKo : id;
        }

        private sealed class MutableSummary
        {
            private readonly List<CivicNationModifierContribution> contributions = new List<CivicNationModifierContribution>();
            private readonly Dictionary<string, double> cappedAmounts = new Dictionary<string, double>(StringComparer.Ordinal);
            private double uncappedAmount;

            public MutableSummary(string effectType, string targetId)
            {
                EffectType = effectType;
                TargetId = targetId;
            }

            public string EffectType { get; }
            public string TargetId { get; }

            public void Add(double amount, string capGroup, CivicNationModifierContribution contribution)
            {
                contributions.Add(contribution);
                if (string.IsNullOrEmpty(capGroup)) uncappedAmount += amount;
                else cappedAmounts[capGroup] = cappedAmounts.TryGetValue(capGroup, out var current) ? current + amount : amount;
            }

            public CivicNationModifierSummary ToSummary(CivicModuleRuntime runtime)
            {
                var amount = uncappedAmount;
                foreach (var pair in cappedAmounts)
                {
                    amount += runtime.Simulation.Modifiers.Caps.TryGetValue(pair.Key, out var cap) ? cap.Clamp(pair.Value) : pair.Value;
                }
                return new CivicNationModifierSummary(EffectType, TargetId, EffectName(EffectType), TargetName(runtime, TargetId), amount, contributions.ToArray());
            }
        }
    }
}
