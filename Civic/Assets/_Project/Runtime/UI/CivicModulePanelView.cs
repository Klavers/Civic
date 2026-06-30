using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;
using Civic.Simulation.Modules;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicModulePanelView : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject[] tabRoots;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private Text[] tabLabels;
        [SerializeField] private CivicDomainPanelView[] domainPanels;
        [SerializeField] private CivicTooltipView tooltipView;

        private CivicModuleRuntime runtime;
        private bool prestigeConfirmationPending;
        private string selectedFeatureId;
        private string selectedPoliticsCategory;
        private bool showImpossibleNations;
        private UnityAction[] tabHandlers = Array.Empty<UnityAction>();

        public event Action<string, string> ActionRequested;
        public event Action Opened;
        public event Action Closed;

        public bool HasRequiredReferences =>
            openButton != null && panelRoot != null && closeButton != null && tooltipView != null &&
            tabRoots != null && tabButtons != null && tabLabels != null && domainPanels != null &&
            tabRoots.Length == CivicFeatureRegistry.Features.Count && tabButtons.Length == tabRoots.Length && tabLabels.Length == tabRoots.Length &&
            tabRoots.All(item => item != null) && tabButtons.All(item => item != null) && tabLabels.All(item => item != null) &&
            domainPanels.Length == CivicFeatureRegistry.Features.Count && domainPanels.All(item => item != null && item.HasRequiredReferences) &&
            domainPanels.Select(item => item.FeatureId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(CivicFeatureRegistry.Features.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal));

        public Button OpenButton => openButton;
        public GameObject PanelRoot => panelRoot;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public string SelectedFeatureId => selectedFeatureId;
        public string SelectedPoliticsCategory => selectedPoliticsCategory;
        public IReadOnlyList<Button> TabButtons => tabButtons ?? Array.Empty<Button>();
        public IReadOnlyList<CivicModuleActionRow> Rows => domainPanels?.SelectMany(item => item.Rows).ToArray() ?? Array.Empty<CivicModuleActionRow>();
        public IReadOnlyList<CivicDomainPanelView> DomainPanels => domainPanels ?? Array.Empty<CivicDomainPanelView>();

        private void OnEnable()
        {
            if (!HasRequiredReferences) return;
            openButton.onClick.AddListener(OpenPanel);
            closeButton.onClick.AddListener(ClosePanel);
            tabHandlers = new UnityAction[tabButtons.Length];
            for (var index = 0; index < tabButtons.Length; index++)
            {
                var captured = index;
                tabHandlers[index] = () => SelectTab(captured);
                tabButtons[index].onClick.AddListener(tabHandlers[index]);
            }

            var politicsPanel = DomainPanel(CivicFeatureRegistry.Politics);
            if (politicsPanel != null) politicsPanel.CategoryTabRequested += SelectPoliticsCategory;
            var nationPanel = DomainPanel(CivicFeatureRegistry.NationFormation);
            if (nationPanel != null) nationPanel.ImpossibleFilterChanged += SetShowImpossibleNations;
        }

        private void OnDisable()
        {
            openButton?.onClick.RemoveListener(OpenPanel);
            closeButton?.onClick.RemoveListener(ClosePanel);
            for (var index = 0; index < tabButtons?.Length && index < tabHandlers.Length; index++)
            {
                if (tabHandlers[index] != null) tabButtons[index].onClick.RemoveListener(tabHandlers[index]);
            }
            var politicsPanel = DomainPanel(CivicFeatureRegistry.Politics);
            if (politicsPanel != null) politicsPanel.CategoryTabRequested -= SelectPoliticsCategory;
            var nationPanel = DomainPanel(CivicFeatureRegistry.NationFormation);
            if (nationPanel != null) nationPanel.ImpossibleFilterChanged -= SetShowImpossibleNations;
            tooltipView?.Hide();
        }

        public void Bind(CivicModuleRuntime moduleRuntime, bool confirmationPending)
        {
            runtime = moduleRuntime;
            prestigeConfirmationPending = confirmationPending;
            if (!HasRequiredReferences || runtime == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                return;
            }

            var enabled = CivicFeatureRegistry.Features.Where(item => runtime.Modules.ContainsKey(item.Id)).ToArray();
            openButton.gameObject.SetActive(enabled.Length > 0);
            if (enabled.Length == 0)
            {
                panelRoot.SetActive(false);
                return;
            }

            if (string.IsNullOrEmpty(selectedFeatureId) || enabled.All(item => item.Id != selectedFeatureId)) selectedFeatureId = enabled[0].Id;
            for (var index = 0; index < tabRoots.Length; index++)
            {
                var definition = CivicFeatureRegistry.Features[index];
                var active = runtime.Modules.ContainsKey(definition.Id);
                tabRoots[index].SetActive(active);
                tabLabels[index].text = definition.DisplayName;
                tabButtons[index].interactable = active && definition.Id != selectedFeatureId;
            }
            RenderSelected();
        }

        public void OpenPanel()
        {
            panelRoot.SetActive(true);
            RenderSelected();
            Opened?.Invoke();
        }

        public void ClosePanel()
        {
            if (!IsOpen) return;
            tooltipView?.Hide();
            panelRoot.SetActive(false);
            Closed?.Invoke();
        }

        private void SelectTab(int index)
        {
            if (runtime == null || index < 0 || index >= CivicFeatureRegistry.Features.Count) return;
            var definition = CivicFeatureRegistry.Features[index];
            if (!runtime.Modules.ContainsKey(definition.Id)) return;
            tooltipView?.Hide();
            selectedFeatureId = definition.Id;
            Bind(runtime, prestigeConfirmationPending);
        }

        private void RenderSelected()
        {
            if (runtime == null || string.IsNullOrEmpty(selectedFeatureId)) return;
            var definition = CivicFeatureRegistry.Features.First(item => item.Id == selectedFeatureId);
            PrepareDomainSelection(selectedFeatureId);
            var entries = BuildEntries(selectedFeatureId);
            var domainPanel = domainPanels.First(item => item.FeatureId == selectedFeatureId);
            foreach (var panel in domainPanels) panel.SetVisible(panel == domainPanel);
            ConfigureDomainControls(domainPanel, selectedFeatureId);
            domainPanel.TitleLabel.text = definition.DisplayName;
            domainPanel.StatusLabel.text = BuildDomainStatus(selectedFeatureId, entries.Count);
            for (var index = 0; index < domainPanel.Rows.Count; index++)
            {
                var visible = index < entries.Count;
                domainPanel.Rows[index].gameObject.SetActive(visible);
                if (!visible) continue;
                var entry = entries[index];
                domainPanel.Rows[index].Bind(entry.Key, entry.Info, entry.Description, entry.Action, entry.Interactable, entry.Tooltip, key => ActionRequested?.Invoke(selectedFeatureId, key));
            }
        }

        private void PrepareDomainSelection(string featureId)
        {
            if (featureId != CivicFeatureRegistry.Politics) return;
            var module = runtime.GetModule<CivicPoliticsModule>(featureId);
            var categories = module.Definitions.Select(item => item.Category).Distinct(StringComparer.Ordinal).ToArray();
            if (categories.Length == 0)
            {
                selectedPoliticsCategory = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(selectedPoliticsCategory) || !categories.Contains(selectedPoliticsCategory, StringComparer.Ordinal))
            {
                selectedPoliticsCategory = categories[0];
            }
        }

        private void ConfigureDomainControls(CivicDomainPanelView panel, string featureId)
        {
            if (featureId == CivicFeatureRegistry.Politics)
            {
                var module = runtime.GetModule<CivicPoliticsModule>(featureId);
                var categories = module.Definitions.Select(item => item.Category).Distinct(StringComparer.Ordinal).ToArray();
                var labels = categories.Select(category =>
                {
                    var activeId = module.ActiveByCategory.TryGetValue(category, out var current) ? current : string.Empty;
                    var activeName = module.Definitions.FirstOrDefault(item => item.Id == activeId)?.DisplayNameKo ?? "없음";
                    return $"{PoliticsCategoryName(category)}\n현행: {activeName}";
                }).ToArray();
                panel.ConfigureCategoryTabs(labels, Array.IndexOf(categories, selectedPoliticsCategory));
            }
            else
            {
                panel.ConfigureCategoryTabs(Array.Empty<string>(), -1);
            }

            panel.ConfigureImpossibleFilter(
                featureId == CivicFeatureRegistry.NationFormation,
                showImpossibleNations,
                "달성불가 조건의 문명·국가도 표시");
        }

        private void SelectPoliticsCategory(int index)
        {
            var module = runtime?.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);
            var categories = module?.Definitions.Select(item => item.Category).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            if (index < 0 || index >= categories.Length) return;
            tooltipView?.Hide();
            selectedPoliticsCategory = categories[index];
            RenderSelected();
        }

        private void SetShowImpossibleNations(bool value)
        {
            tooltipView?.Hide();
            showImpossibleNations = value;
            RenderSelected();
        }

        private string BuildDomainStatus(string featureId, int entryCount)
        {
            if (featureId == CivicFeatureRegistry.Prestige) return $"환생 포인트 {runtime.MetaProgress.PrestigePoints} · {entryCount}개 항목";
            if (featureId == CivicFeatureRegistry.Achievements) return $"완료 {runtime.MetaProgress.CompletedAchievementIds.Count} · {entryCount}개 도전과제";
            if (featureId == CivicFeatureRegistry.Politics)
            {
                var politics = runtime.GetModule<CivicPoliticsModule>(featureId);
                return $"정당성 {politics.Legitimacy:0.#} · 정치력 {politics.PoliticalCapital:0.#} · 생활수준 {politics.LivingStandard:0.#} · 지지 {politics.Support:P0}";
            }
            if (featureId == CivicFeatureRegistry.Events) return $"발견 {runtime.MetaProgress.DiscoveredEventIds.Count} · {entryCount}개 선택지";
            if (featureId == CivicFeatureRegistry.Wonders) return $"완공 {runtime.MetaProgress.CompletedWonderIds.Count} · {entryCount}개 불가사의";
            if (featureId == CivicFeatureRegistry.GreatPeople) return $"발견 {runtime.MetaProgress.DiscoveredPersonIds.Count} · {entryCount}개 인물 항목";
            return $"{entryCount}개 항목 · {runtime.Features.EnabledIntegrationIds.Count}개 자동 연계 활성";
        }

        private List<RowEntry> BuildEntries(string featureId)
        {
            if (featureId == CivicFeatureRegistry.Prestige) return PrestigeEntries();
            if (featureId == CivicFeatureRegistry.Achievements) return AchievementEntries();
            if (featureId == CivicFeatureRegistry.StartCivilizations) return CivilizationEntries();
            if (featureId == CivicFeatureRegistry.NationFormation) return NationEntries();
            if (featureId == CivicFeatureRegistry.Politics) return PoliticsEntries();
            if (featureId == CivicFeatureRegistry.Events) return EventEntries();
            if (featureId == CivicFeatureRegistry.Wonders) return WonderEntries();
            if (featureId == CivicFeatureRegistry.GreatPeople) return PeopleEntries();
            return new List<RowEntry>();
        }

        private List<RowEntry> PrestigeEntries()
        {
            var module = runtime.GetModule<CivicPrestigeModule>(CivicFeatureRegistry.Prestige);
            var preview = module.CreatePreview();
            var result = new List<RowEntry>
            {
                new RowEntry("__prestige", $"예상 {preview.TotalScore}점 · GDP {preview.GdpScore} / 인구 {preview.PopulationScore} / 시대 {preview.EraScore} / 기술 {preview.TechnologyScore} / 도전 {preview.ChallengeScore}", prestigeConfirmationPending ? "최종 환생" : "환생 확인", preview.CanPrestige, preview.Warning)
            };
            result.AddRange(module.LegacyPerks.Select(perk =>
            {
                var rank = runtime.MetaProgress.GetPerkRank(perk.Id);
                var cost = perk.CostForNextRank(rank);
                var atMaximum = rank >= perk.MaxRank;
                var canAfford = !atMaximum && cost > 0 && runtime.MetaProgress.PrestigePoints >= cost;
                var action = atMaximum ? "최대" : $"구매 {cost}P";
                var tooltip = atMaximum ? "최대 단계입니다." : canAfford ? "구매 효과는 다음 런부터 적용됩니다." : "환생 포인트가 부족합니다.";
                return new RowEntry(perk.Id, $"{perk.DisplayNameKo} · {rank}/{perk.MaxRank} · {perk.EffectType} {perk.Amount:+0.##;-0.##;0}", action, canAfford, tooltip);
            }));
            return result;
        }

        private List<RowEntry> AchievementEntries()
        {
            var module = runtime.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements);
            return module.Snapshot.Select(item =>
            {
                var state = item.State == CivicAchievementState.InProgress ? "달성 가능"
                    : item.State == CivicAchievementState.ImpossibleThisRun || item.State == CivicAchievementState.Failed ? "이번 런 불가능"
                    : item.State == CivicAchievementState.Unavailable ? "필요 모듈 OFF"
                    : item.State == CivicAchievementState.Completed ? "완료" : item.State.ToString();
                var rewards = module.RewardsFor(item.Definition.Id);
                var rewardText = rewards.Count == 0 ? "영구 효과 없음" : string.Join(", ", rewards.Select(FormatAchievementReward));
                var tooltip = JoinSections(
                    item.Definition.DescriptionKo,
                    "조건\n" + FormatConditions(module.ConditionStatusFor(item.Definition.Id)),
                    $"보상\n환생 포인트 {item.Definition.PrestigeReward}\n{rewardText}",
                    item.BlockingReason);
                return new RowEntry(item.Definition.Id, $"{item.Definition.TitleKo} · {state} · 환생P {item.Definition.PrestigeReward}", "상태", false, tooltip);
            }).ToList();
        }

        private List<RowEntry> CivilizationEntries()
        {
            var module = runtime.GetModule<CivicCivilizationModule>(CivicFeatureRegistry.StartCivilizations);
            var item = module.ActiveCivilization;
            var starts = module.StartsFor(item.Id).Select(start => $"{start.Kind}:{start.TargetId} {start.Amount:+0.##;-0.##;0}").ToArray();
            var effects = module.EffectsFor(item.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)).ToArray();
            var description = JoinSections(
                item.DescriptionKo,
                "시작 상태: " + JoinInline(starts, "기본값 유지"),
                "문명 효과: " + JoinInline(effects, "추가 효과 없음"));
            var tooltip = JoinSections(description, "시작 문명은 MainMenu에서 선택하며 런 도중 변경할 수 없습니다.");
            return new List<RowEntry>
            {
                new RowEntry(item.Id, $"{item.DisplayNameKo} · 현재 문명", "적용 중", false, tooltip, description)
            };
        }

        private List<RowEntry> NationEntries()
        {
            var module = runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);
            var result = module.Snapshot.Where(item => showImpossibleNations || !module.IsImpossibleThisRun(item.Definition.Id)).Select(item =>
            {
                var action = item.State == CivicNationCandidateState.Ready ? "설립" : item.State == CivicNationCandidateState.Preparing ? "취소" : item.State == CivicNationCandidateState.AwaitingCharter ? "헌장 확정" : "상태";
                var enabled = item.State == CivicNationCandidateState.Ready || item.State == CivicNationCandidateState.Preparing || item.State == CivicNationCandidateState.AwaitingCharter;
                var conditions = FormatConditions(module.ConditionStatusFor(item.Definition.Id));
                var effects = string.Join("\n", module.EffectsFor(item.Definition.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)));
                var tooltip = JoinSections(
                    item.Definition.DescriptionKo,
                    $"Tier {item.Definition.Tier} · 준비 {item.Definition.PreparationSeconds:0.#}초 · 국고 {item.Definition.TreasuryCost:0.##}",
                    "설립 조건\n" + conditions,
                    "국가 효과\n" + effects,
                    item.BlockingReason);
                var description = JoinSections(
                    item.Definition.DescriptionKo,
                    "설립 효과: " + JoinInline(module.EffectsFor(item.Definition.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)), "추가 효과 없음"));
                return new RowEntry(item.Definition.Id, $"{item.Definition.DisplayNameKo} · {NationStateText(item.State)} · 조건 {item.ConditionRatio:P0} · 준비 {item.PreparationProgress:P0}", action, enabled, tooltip, description);
            }).ToList();
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"현재 국가 임시 매핑 효과 {module.ProvisionalEffectCount}개", "적용 중", false, "원래 설계 ID를 유지한 P04 초기 밸런스 효과입니다."));
            return result;
        }

        private List<RowEntry> PoliticsEntries()
        {
            var module = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);
            var result = new List<RowEntry>();
            var category = selectedPoliticsCategory;
            var reformDefinition = module.Reform == null ? null : module.Definitions.FirstOrDefault(item => item.Id == module.Reform.TargetInstitutionId);
            if (module.Reform != null && reformDefinition?.Category == category)
            {
                result.Add(new RowEntry("__cancel", $"개혁 진행: {module.Reform.TargetInstitutionId} {module.Reform.Progress:0.#}%", "개혁 취소", true, string.Empty, "현재 선택한 체계로 개혁 중입니다."));
            }

            var activeId = module.ActiveByCategory.TryGetValue(category, out var current) ? current : string.Empty;
            foreach (var item in module.Definitions.Where(item => item.Category == category).OrderBy(item => item.Order))
            {
                var active = activeId == item.Id;
                var unlocked = module.IsUnlocked(item.Id);
                var unlocks = FormatConditions(module.UnlockStatusFor(item.Id));
                var effectLines = module.EffectsFor(item.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)).ToArray();
                var effects = string.Join("\n", effectLines);
                var tooltip = JoinSections(item.DescriptionKo, "해금 조건\n" + unlocks, "체계 효과\n" + effects, $"개혁 시간 {item.ReformSeconds:0.#}초 · 피로 {item.FatigueSeconds:0.#}초");
                var description = JoinSections(item.DescriptionKo, "체계 효과: " + JoinInline(effectLines, "추가 효과 없음"));
                result.Add(new RowEntry(item.Id, $"{item.DisplayNameKo} · {(active ? "현행" : unlocked ? "개혁 가능" : "미해금")} · 정치력 {item.PoliticalCost:0.#} · 국고 {item.TreasuryCost:0.#}", active ? "적용 중" : "개혁", !active && unlocked && module.Reform == null && module.FatigueRemaining <= 0d, tooltip, description));
            }
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"현행 제도 임시 매핑 효과 {module.ProvisionalEffectCount}개", "적용 중", false, "정식 정치·사회 밸런스 전 P04 임시 공식으로 적용됩니다."));
            return result;
        }

        private List<RowEntry> EventEntries()
        {
            var module = runtime.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            var result = module.Queue.Select(item => new RowEntry("", $"대기 · {item.Definition.TitleKo} · {item.Cause}", "상단 알림", false, item.Definition.DescriptionKo + "\n선택지는 상단 이벤트 알림에서 확인합니다.")).ToList();
            result.AddRange(module.History.OrderByDescending(item => item.OccurredAt).Select(item => new RowEntry("", $"이력 · {item.EventTitleKo} · {item.ChoiceTextKo} · {item.OccurredAt:0}s", "해결", false, string.Join("\n", item.AppliedResults))));
            if (result.Count == 0) result.Add(new RowEntry("", $"대기 사건 없음 · 발견 {runtime.MetaProgress.DiscoveredEventIds.Count} · 이번 런 해결 {module.History.Count}", "대기", false, string.Empty));
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"이벤트 임시 매핑 효과 {module.ProvisionalEffectCount}개", "초기값", false, "선택 시 효과별 기간과 상한을 적용합니다."));
            return result;
        }

        private List<RowEntry> WonderEntries()
        {
            var module = runtime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders);
            var result = module.Snapshot.Select(item =>
            {
                var action = item.State == CivicWonderState.Available ? "착공" : item.State == CivicWonderState.Building ? "취소" : "상태";
                var enabled = item.State == CivicWonderState.Available || item.State == CivicWonderState.Building;
                var costs = module.CostsFor(item.Definition.Id);
                var costText = string.Join("\n", costs.Select(cost => $"{ResourceName(cost.ResourceId)} {cost.Amount:0.##} · 납품 {cost.DeliveryRate:0.##}/s · 현재 {Delivered(item, cost.ResourceId):0.##}"));
                var conditions = FormatConditions(module.ConditionStatusFor(item.Definition.Id));
                var effects = string.Join("\n", module.EffectsFor(item.Definition.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)));
                var upkeep = string.IsNullOrEmpty(item.Definition.UpkeepType) || item.Definition.UpkeepAmount == 0d ? "없음" : $"{item.Definition.UpkeepType} {item.Definition.UpkeepAmount:0.####}";
                var tooltip = JoinSections(
                    $"시대 {item.Definition.EraId} · 기술 {item.Definition.TechnologyId}",
                    $"착공 국고 {item.Definition.UpfrontTreasury:0.##} · 예상 최소 {module.MinimumBuildSeconds(item.Definition.Id):0.#}초 · 유지비 {upkeep}",
                    "필요 자원\n" + costText,
                    "해금 조건\n" + conditions,
                    "완공 효과\n" + effects,
                    item.BlockingReason);
                var description = JoinSections(
                    WonderConceptDescription(item.Definition.ConceptId),
                    "완공 효과: " + JoinInline(module.EffectsFor(item.Definition.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)), "추가 효과 없음"));
                return new RowEntry(item.Definition.Id, $"{item.Definition.DisplayNameKo} · {item.State} · 진행 {item.Progress:P0} · 최소 {module.MinimumBuildSeconds(item.Definition.Id):0.#}초", action, enabled, tooltip, description);
            }).ToList();
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"불가사의 임시 매핑 효과 {module.ProvisionalEffectCount}개", "초기값", false, "완공 시 P04 초기 수치·기간·상한으로 적용됩니다."));
            return result;
        }

        private List<RowEntry> PeopleEntries()
        {
            var module = runtime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople);
            var candidateIds = module.Candidates.ToDictionary(item => item.Definition.Id, StringComparer.Ordinal);
            var activeIds = module.ActivePeople.ToDictionary(item => item.Definition.Id, StringComparer.Ordinal);
            var result = module.Definitions.Select(item =>
            {
                var candidate = candidateIds.TryGetValue(item.Id, out var candidateState);
                var active = activeIds.TryGetValue(item.Id, out var activeState);
                var retired = module.RetiredIds.Contains(item.Id);
                var state = active ? $"활성 · {activeState.AssignmentId} · 임기 {activeState.TenureRemaining:0}s"
                    : candidate ? $"후보 · {candidateState.RemainingSeconds:0}s"
                    : retired ? "은퇴" : "미발견";
                var conditions = FormatConditions(module.ConditionStatusFor(item.Id));
                var traits = string.Join("\n", module.TraitsFor(item.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount * (active ? module.AssignmentScaleFor(item.Id) : 1d), effect.Duration)));
                var abilities = string.Join("\n", module.AbilitiesFor(item.Id).Select(effect => $"{effect.Id}: {FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)} · {effect.UsesPerRun}회/런"));
                var tooltip = JoinSections(
                    $"희귀도 {item.Rarity} · 직군 {item.ArchetypeId} · 기본 임기 {item.BaseTenure:0}s",
                    "발견 조건\n" + conditions,
                    "특성 효과\n" + traits,
                    "능동 능력\n" + abilities,
                    "허용 배치: " + string.Join(" / ", item.AllowedAssignments),
                    active ? $"현재 배치 {activeState.AssignmentId} · 적용 배율 {module.AssignmentScaleFor(item.Id):P0}" : string.Empty,
                    $"활성 슬롯 {module.ActivePeople.Count}/{CivicPeopleModule.ActiveSlotLimit} · 같은 배치 중복 시 100%/70%/50% 배율");
                var description = JoinSections(
                    PersonArchetypeDescription(item.ArchetypeId),
                    "상시 특성: " + JoinInline(module.TraitsFor(item.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount * (active ? module.AssignmentScaleFor(item.Id) : 1d), effect.Duration)), "없음"),
                    "능동 능력: " + JoinInline(module.AbilitiesFor(item.Id).Select(effect => FormatEffect(effect.EffectType, effect.TargetId, effect.Amount, effect.Duration)), "없음"));
                return new RowEntry(candidate ? "recruit:" + item.Id : "", $"{item.DisplayNameKo} · {state}", candidate ? "영입" : "상태", candidate && module.ActivePeople.Count < CivicPeopleModule.ActiveSlotLimit, tooltip, description);
            }).ToList();
            result.AddRange(module.ActivePeople.Select(item => new RowEntry("ability:" + item.Definition.Id, $"활성 {item.Definition.DisplayNameKo} · {item.AssignmentId} · 임기 {item.TenureRemaining:0}s", "능력", item.AbilityUsesRemaining > 0, "남은 능력 사용 " + item.AbilityUsesRemaining, "능동 능력은 상시 특성과 별개로 즉시 자원을 지급하거나 정해진 기간 동안 추가 효과를 적용합니다.")));
            result.AddRange(module.ActivePeople.Select(item => new RowEntry("assign:" + item.Definition.Id, $"배치 {item.Definition.DisplayNameKo} · 현재 {item.AssignmentId}", "배치 변경", item.Definition.AllowedAssignments.Count > 1, string.Join(" / ", item.Definition.AllowedAssignments), "배치는 현재 같은 배치에 모인 인원 수에 따라 상시 특성 배율을 100% / 70% / 50%로 조정합니다.")));
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"위인 임시 매핑 효과 {module.ProvisionalEffectCount}개", "초기값", false, "특성·능력은 P04 초기 수치·기간·상한으로 적용됩니다."));
            if (result.Count == 0) result.Add(new RowEntry("", "현재 후보나 활성 위인이 없습니다.", "대기", false, string.Empty));
            return result;
        }

        private static string FormatAchievementReward(CivicAchievementRewardDefinition reward)
        {
            return FormatEffect(reward.EffectType, reward.TargetId, reward.Amount, 0d);
        }

        private static string FormatConditions(IEnumerable<CivicMetricConditionSnapshot> conditions)
        {
            var lines = (conditions ?? Array.Empty<CivicMetricConditionSnapshot>()).Select(condition =>
            {
                var alternative = string.IsNullOrEmpty(condition.AlternativeGroup) ? string.Empty : $" [대안 {condition.AlternativeGroup}]";
                var duration = condition.Duration > 0d ? $" · {condition.Duration:0.#}초 유지" : string.Empty;
                var forbidden = condition.Forbidden ? "금지 " : string.Empty;
                return $"{(condition.IsSatisfied ? "✓" : "✕")} {forbidden}{condition.MetricId} {condition.Comparator} {condition.RequiredValue:0.##} · 현재 {condition.CurrentValue:0.##}{alternative}{duration}";
            }).ToArray();
            return lines.Length == 0 ? "조건 없음" : string.Join("\n", lines);
        }

        private static string FormatEffect(string effectType, string targetId, double amount, double duration)
        {
            var lifetime = duration > 0d ? $" · {duration:0.#}초" : " · 지속";
            return $"{effectType}({targetId}) {amount:+0.##;-0.##;0}{lifetime}";
        }

        private static string NationStateText(CivicNationCandidateState state)
        {
            switch (state)
            {
                case CivicNationCandidateState.Hidden: return "미발견";
                case CivicNationCandidateState.Unavailable: return "필요 모듈 OFF";
                case CivicNationCandidateState.Discovered: return "조건 진행 중";
                case CivicNationCandidateState.Ready: return "설립 가능";
                case CivicNationCandidateState.Preparing: return "설립 준비 중";
                case CivicNationCandidateState.AwaitingCharter: return "헌장 선택 대기";
                case CivicNationCandidateState.Current: return "현재 국가";
                default: return state.ToString();
            }
        }

        private string ResourceName(string resourceId)
        {
            return runtime?.Simulation?.Snapshot?.Resources?.FirstOrDefault(item => item.Id == resourceId)?.DisplayNameKo ?? resourceId;
        }

        private static double Delivered(CivicWonderSnapshot snapshot, string resourceId)
        {
            return snapshot.Delivered != null && snapshot.Delivered.TryGetValue(resourceId, out var value) ? value : 0d;
        }

        private static string JoinSections(params string[] sections)
        {
            return string.Join("\n\n", (sections ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private CivicDomainPanelView DomainPanel(string featureId)
        {
            return domainPanels?.FirstOrDefault(item => item != null && item.FeatureId == featureId);
        }

        private static string JoinInline(IEnumerable<string> values, string emptyValue)
        {
            var items = (values ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            return items.Length == 0 ? emptyValue : string.Join(" · ", items);
        }

        private static string PoliticsCategoryName(string category)
        {
            switch (category)
            {
                case "government": return "정부 유형";
                case "franchise": return "유권자 권리";
                case "welfare": return "복지 정책";
                case "education": return "교육 정책";
                case "economy": return "경제 체계";
                default: return category;
            }
        }

        private static string WonderConceptDescription(string conceptId)
        {
            switch (conceptId)
            {
                case "knowledge": return "지식 축적과 연구 역량을 장기적으로 강화하는 불가사의입니다.";
                case "infrastructure": return "생산망과 건설 기반을 넓히는 대규모 기반시설입니다.";
                case "politics": return "정당성·개혁·복지 운영을 강화하는 통치 상징물입니다.";
                case "culture": return "시장·문화·기록과 관련된 장기 보너스를 제공하는 불가사의입니다.";
                case "meta": return "환생과 후반 목표에 연결되는 문명 단위의 거대 계획입니다.";
                default: return "완공 후 지속 효과를 제공하는 대규모 건축 계획입니다.";
            }
        }

        private static string PersonArchetypeDescription(string archetypeId)
        {
            switch (archetypeId)
            {
                case "scholar": return "연구와 과학 진행을 전문으로 하는 학자형 위인입니다.";
                case "engineer": return "건설·불가사의·생산 시설을 전문으로 하는 기술자형 위인입니다.";
                case "politician": return "국가 설립과 정치 개혁을 전문으로 하는 정치가형 위인입니다.";
                case "entrepreneur": return "시장·공장·국고 운영을 전문으로 하는 기업가형 위인입니다.";
                case "culture": return "사건·연대기·문화 보상을 전문으로 하는 문화인형 위인입니다.";
                default: return "런 중 한정된 기간 동안 특성과 능동 능력을 제공하는 위인입니다.";
            }
        }

        private readonly struct RowEntry
        {
            public RowEntry(string key, string info, string action, bool interactable, string tooltip, string description = "")
            {
                Key = key; Info = info; Action = action; Interactable = interactable; Tooltip = tooltip; Description = description;
            }
            public string Key { get; }
            public string Info { get; }
            public string Action { get; }
            public bool Interactable { get; }
            public string Tooltip { get; }
            public string Description { get; }
        }
    }
}
