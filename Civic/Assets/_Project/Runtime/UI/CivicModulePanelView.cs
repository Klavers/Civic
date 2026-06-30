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

        private CivicModuleRuntime runtime;
        private bool prestigeConfirmationPending;
        private string selectedFeatureId;
        private UnityAction[] tabHandlers = Array.Empty<UnityAction>();

        public event Action<string, string> ActionRequested;

        public bool HasRequiredReferences =>
            openButton != null && panelRoot != null && closeButton != null &&
            tabRoots != null && tabButtons != null && tabLabels != null && domainPanels != null &&
            tabRoots.Length == CivicFeatureRegistry.Features.Count && tabButtons.Length == tabRoots.Length && tabLabels.Length == tabRoots.Length &&
            tabRoots.All(item => item != null) && tabButtons.All(item => item != null) && tabLabels.All(item => item != null) &&
            domainPanels.Length == CivicFeatureRegistry.Features.Count && domainPanels.All(item => item != null && item.HasRequiredReferences) &&
            domainPanels.Select(item => item.FeatureId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(CivicFeatureRegistry.Features.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal));

        public Button OpenButton => openButton;
        public GameObject PanelRoot => panelRoot;
        public IReadOnlyList<CivicModuleActionRow> Rows => domainPanels?.SelectMany(item => item.Rows).ToArray() ?? Array.Empty<CivicModuleActionRow>();
        public IReadOnlyList<CivicDomainPanelView> DomainPanels => domainPanels ?? Array.Empty<CivicDomainPanelView>();

        private void OnEnable()
        {
            if (!HasRequiredReferences) return;
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            tabHandlers = new UnityAction[tabButtons.Length];
            for (var index = 0; index < tabButtons.Length; index++)
            {
                var captured = index;
                tabHandlers[index] = () => SelectTab(captured);
                tabButtons[index].onClick.AddListener(tabHandlers[index]);
            }
        }

        private void OnDisable()
        {
            openButton?.onClick.RemoveListener(Open);
            closeButton?.onClick.RemoveListener(Close);
            for (var index = 0; index < tabButtons?.Length && index < tabHandlers.Length; index++)
            {
                if (tabHandlers[index] != null) tabButtons[index].onClick.RemoveListener(tabHandlers[index]);
            }
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

        private void Open()
        {
            panelRoot.SetActive(true);
            RenderSelected();
        }

        private void Close()
        {
            panelRoot.SetActive(false);
        }

        private void SelectTab(int index)
        {
            if (runtime == null || index < 0 || index >= CivicFeatureRegistry.Features.Count) return;
            var definition = CivicFeatureRegistry.Features[index];
            if (!runtime.Modules.ContainsKey(definition.Id)) return;
            selectedFeatureId = definition.Id;
            Bind(runtime, prestigeConfirmationPending);
        }

        private void RenderSelected()
        {
            if (runtime == null || string.IsNullOrEmpty(selectedFeatureId)) return;
            var definition = CivicFeatureRegistry.Features.First(item => item.Id == selectedFeatureId);
            var entries = BuildEntries(selectedFeatureId);
            var domainPanel = domainPanels.First(item => item.FeatureId == selectedFeatureId);
            foreach (var panel in domainPanels) panel.SetVisible(panel == domainPanel);
            domainPanel.TitleLabel.text = definition.DisplayName;
            domainPanel.StatusLabel.text = BuildDomainStatus(selectedFeatureId, entries.Count);
            for (var index = 0; index < domainPanel.Rows.Count; index++)
            {
                var visible = index < entries.Count;
                domainPanel.Rows[index].gameObject.SetActive(visible);
                if (!visible) continue;
                var entry = entries[index];
                domainPanel.Rows[index].Bind(entry.Key, entry.Info, entry.Action, entry.Interactable, entry.Tooltip, key => ActionRequested?.Invoke(selectedFeatureId, key));
            }
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
            return module.Snapshot.Select(item => new RowEntry(item.Definition.Id, $"{item.Definition.TitleKo} · {item.State} · 보상 {item.Definition.PrestigeReward}", "상태", false, item.BlockingReason)).ToList();
        }

        private List<RowEntry> CivilizationEntries()
        {
            var module = runtime.GetModule<CivicCivilizationModule>(CivicFeatureRegistry.StartCivilizations);
            return new List<RowEntry> { new RowEntry(module.ActiveCivilization.Id, $"현재 문명: {module.ActiveCivilization.DisplayNameKo} · {module.ActiveCivilization.DescriptionKo} · 임시 매핑 효과 {module.ProvisionalEffectCount}", "런 고정", false, "시작 문명은 메인 메뉴에서 선택하며 런 도중 변경할 수 없습니다. planned 효과는 P04 초기값으로 임시 적용됩니다.") };
        }

        private List<RowEntry> NationEntries()
        {
            var module = runtime.GetModule<CivicNationModule>(CivicFeatureRegistry.NationFormation);
            var result = module.Snapshot.Where(item => item.State != CivicNationCandidateState.Hidden).Select(item =>
            {
                var action = item.State == CivicNationCandidateState.Ready ? "설립" : item.State == CivicNationCandidateState.Preparing ? "취소" : item.State == CivicNationCandidateState.AwaitingCharter ? "헌장 확정" : "상태";
                var enabled = item.State == CivicNationCandidateState.Ready || item.State == CivicNationCandidateState.Preparing || item.State == CivicNationCandidateState.AwaitingCharter;
                return new RowEntry(item.Definition.Id, $"{item.Definition.DisplayNameKo} · {item.State} · 조건 {item.ConditionRatio:P0} · 준비 {item.PreparationProgress:P0}", action, enabled, item.BlockingReason);
            }).ToList();
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"현재 국가 임시 매핑 효과 {module.ProvisionalEffectCount}개", "적용 중", false, "원래 설계 ID를 유지한 P04 초기 밸런스 효과입니다."));
            return result;
        }

        private List<RowEntry> PoliticsEntries()
        {
            var module = runtime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics);
            var result = new List<RowEntry>();
            if (module.Reform != null) result.Add(new RowEntry("__cancel", $"개혁 진행: {module.Reform.TargetInstitutionId} {module.Reform.Progress:0.#}%", "개혁 취소", true, string.Empty));
            result.AddRange(module.Definitions.Select(item =>
            {
                var active = module.ActiveByCategory.TryGetValue(item.Category, out var id) && id == item.Id;
                var unlocked = module.IsUnlocked(item.Id);
                return new RowEntry(item.Id, $"{item.DisplayNameKo} · {item.Category} · 정치력 {item.PoliticalCost:0.#} · 국고 {item.TreasuryCost:0.#}", active ? "적용 중" : "개혁", !active && unlocked && module.Reform == null && module.FatigueRemaining <= 0d, unlocked ? item.DescriptionKo : "해금 조건 미충족");
            }));
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"현행 제도 임시 매핑 효과 {module.ProvisionalEffectCount}개", "적용 중", false, "정식 정치·사회 밸런스 전 P04 임시 공식으로 적용됩니다."));
            return result;
        }

        private List<RowEntry> EventEntries()
        {
            var module = runtime.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            var result = module.Queue.SelectMany(item => item.Choices.Select(choice => new RowEntry(item.Definition.Id + "|" + choice.Id, item.Definition.TitleKo + " · " + choice.TextKo, "선택", module.IsChoiceAvailable(choice.Id), item.Cause + "\n" + item.Definition.DescriptionKo))).ToList();
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
                return new RowEntry(item.Definition.Id, $"{item.Definition.DisplayNameKo} · {item.State} · 진행 {item.Progress:P0}", action, enabled, item.BlockingReason);
            }).ToList();
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"불가사의 임시 매핑 효과 {module.ProvisionalEffectCount}개", "초기값", false, "완공 시 P04 초기 수치·기간·상한으로 적용됩니다."));
            return result;
        }

        private List<RowEntry> PeopleEntries()
        {
            var module = runtime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople);
            var result = module.Candidates.Select(item => new RowEntry("recruit:" + item.Definition.Id, $"후보 {item.Definition.DisplayNameKo} · {item.Definition.ArchetypeId} · {item.RemainingSeconds:0}s", "영입", true, string.Empty)).ToList();
            result.AddRange(module.ActivePeople.Select(item => new RowEntry("ability:" + item.Definition.Id, $"활성 {item.Definition.DisplayNameKo} · {item.AssignmentId} · 임기 {item.TenureRemaining:0}s", "능력", item.AbilityUsesRemaining > 0, "남은 능력 사용 " + item.AbilityUsesRemaining)));
            result.AddRange(module.ActivePeople.Select(item => new RowEntry("assign:" + item.Definition.Id, $"배치 {item.Definition.DisplayNameKo} · 현재 {item.AssignmentId}", "배치 변경", item.Definition.AllowedAssignments.Count > 1, string.Join(" / ", item.Definition.AllowedAssignments))));
            if (module.ProvisionalEffectCount > 0) result.Add(new RowEntry("", $"위인 임시 매핑 효과 {module.ProvisionalEffectCount}개", "초기값", false, "특성·능력은 P04 초기 수치·기간·상한으로 적용됩니다."));
            if (result.Count == 0) result.Add(new RowEntry("", "현재 후보나 활성 위인이 없습니다.", "대기", false, string.Empty));
            return result;
        }

        private readonly struct RowEntry
        {
            public RowEntry(string key, string info, string action, bool interactable, string tooltip)
            {
                Key = key; Info = info; Action = action; Interactable = interactable; Tooltip = tooltip;
            }
            public string Key { get; }
            public string Info { get; }
            public string Action { get; }
            public bool Interactable { get; }
            public string Tooltip { get; }
        }
    }
}
