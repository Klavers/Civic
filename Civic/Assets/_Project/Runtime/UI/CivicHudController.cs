using System;
using System.Linq;
using Civic.Simulation;
using Civic.Features;
using Civic.Simulation.Modules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Civic.UI
{
    public sealed class CivicHudController : MonoBehaviour
    {
        [SerializeField] private CivicHudView view;
        [SerializeField] private CivicGameDataSource dataSource;
        [SerializeField] private double simulationSpeed = 1d;
        [SerializeField] private CivicModulePanelView modulePanelView;
        [SerializeField] private CivicHudOverlayView overlayView;

        private CivicGameSimulation simulation;
        private CivicModuleRuntime moduleRuntime;
        private CivicHudPanelMode panelMode;
        private bool showFoodChildren;
        private string selectedTechnologyEraId;
        private bool prestigeConfirmationPending;
        private string lastAutoOpenedEventId = string.Empty;

        public CivicHudView View => view;
        public CivicGameSimulation Simulation => simulation;
        public CivicModuleRuntime ModuleRuntime => moduleRuntime;
        public CivicGameDataSource DataSource => dataSource;
        public string SelectedTechnologyEraId => selectedTechnologyEraId;
        public CivicModulePanelView ModulePanelView => modulePanelView;
        public CivicHudOverlayView OverlayView => overlayView;
        public CivicHudPanelMode PanelMode => panelMode;
        public bool HasRequiredReferences => view != null && dataSource != null && modulePanelView != null && modulePanelView.HasRequiredReferences && overlayView != null && overlayView.HasRequiredReferences;

        private void Awake()
        {
            CivicFeatureRuntime.EnsureRunStarted();

            if (dataSource != null)
            {
                simulation = new CivicGameSimulation(dataSource.LoadGameData());
                moduleRuntime = new CivicModuleRuntime(simulation, CivicFeatureRuntime.Current);
            }
        }

        private void OnEnable()
        {
            if (view == null)
            {
                return;
            }

            view.ResourcesPanelRequested += ShowResourcesPanel;
            view.BuildingsPanelRequested += ShowBuildingsPanel;
            view.TechnologiesPanelRequested += ShowTechnologiesPanel;
            view.DetailPanelCloseRequested += CloseDetailPanel;
            view.BuildRequested += BuildRequestedBuilding;
            view.ResearchRequested += ResearchRequestedTechnology;
            view.EraTabRequested += SelectTechnologyEra;
            view.FoodToggleRequested += ToggleFoodChildren;
            modulePanelView.ActionRequested += HandleModuleAction;
            modulePanelView.Opened += HandleModulePanelOpened;
            overlayView.ContinueRequested += HandleContinueRequested;
            overlayView.MainMenuRequested += ReturnToMainMenu;
            overlayView.EventAlertRequested += OpenQueuedEvent;
            overlayView.EventCloseRequested += CloseEventPopup;
            overlayView.EventChoiceRequested += HandleEventChoice;
            overlayView.DebugDomainChanged += RefreshDebugTargets;
            overlayView.DebugActionRequested += ExecuteDebugAction;
            overlayView.DebugCloseRequested += CloseDebugPanel;
            Render();
        }

        private void OnDisable()
        {
            if (view == null)
            {
                return;
            }

            view.ResourcesPanelRequested -= ShowResourcesPanel;
            view.BuildingsPanelRequested -= ShowBuildingsPanel;
            view.TechnologiesPanelRequested -= ShowTechnologiesPanel;
            view.DetailPanelCloseRequested -= CloseDetailPanel;
            view.BuildRequested -= BuildRequestedBuilding;
            view.ResearchRequested -= ResearchRequestedTechnology;
            view.EraTabRequested -= SelectTechnologyEra;
            view.FoodToggleRequested -= ToggleFoodChildren;
            if (modulePanelView != null)
            {
                modulePanelView.ActionRequested -= HandleModuleAction;
                modulePanelView.Opened -= HandleModulePanelOpened;
            }
            if (overlayView != null)
            {
                overlayView.ContinueRequested -= HandleContinueRequested;
                overlayView.MainMenuRequested -= ReturnToMainMenu;
                overlayView.EventAlertRequested -= OpenQueuedEvent;
                overlayView.EventCloseRequested -= CloseEventPopup;
                overlayView.EventChoiceRequested -= HandleEventChoice;
                overlayView.DebugDomainChanged -= RefreshDebugTargets;
                overlayView.DebugActionRequested -= ExecuteDebugAction;
                overlayView.DebugCloseRequested -= CloseDebugPanel;
            }
        }

        private void Update()
        {
            HandleKeyboardInput();
            moduleRuntime?.Advance(Time.deltaTime * simulationSpeed);
            Render();
        }

        private void Render()
        {
            if (simulation != null && view != null)
            {
                EnsureSelectedTechnologyEra(simulation.Snapshot);
                view.Render(simulation.Snapshot, panelMode, showFoodChildren, selectedTechnologyEraId);
                modulePanelView.Bind(moduleRuntime, prestigeConfirmationPending);
                RenderEventUi();
            }
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                ToggleDebugPanel();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame) ProcessEscape();
        }

        public void ProcessEscape()
        {
            if (overlayView.IsEventPopupOpen)
            {
                CloseEventPopup();
                return;
            }
            if (overlayView.IsDebugPanelOpen)
            {
                CloseDebugPanel();
                return;
            }
            if (modulePanelView.IsOpen)
            {
                modulePanelView.ClosePanel();
                return;
            }
            if (panelMode != CivicHudPanelMode.None)
            {
                CloseDetailPanel();
                return;
            }
            if (overlayView.IsExitPopupOpen) overlayView.HideExitPopup();
            else overlayView.ShowExitPopup();
        }

        private void HandleContinueRequested() => overlayView.HideExitPopup();

        private void ReturnToMainMenu()
        {
            CivicFeatureRuntime.ResetForMainMenu();
            CivicRunLaunchSettings.Reset();
            SceneManager.LoadScene("MainMenu");
        }

        private void RenderEventUi()
        {
            var events = moduleRuntime?.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            var queueCount = events?.Queue.Count ?? 0;
            overlayView.RenderEventAlert(queueCount);
            if (queueCount == 0)
            {
                lastAutoOpenedEventId = string.Empty;
                overlayView.HideEventPopup();
                return;
            }

            var queued = events.Queue[0];
            if (!overlayView.IsEventPopupOpen && queued.Definition.Id != lastAutoOpenedEventId)
            {
                lastAutoOpenedEventId = queued.Definition.Id;
                ShowEventPopup(queued, events);
            }
        }

        private void OpenQueuedEvent()
        {
            var events = moduleRuntime?.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            if (events?.Queue.Count > 0) ShowEventPopup(events.Queue[0], events);
        }

        private void ShowEventPopup(CivicQueuedEventSnapshot queued, CivicEventModule events)
        {
            modulePanelView.ClosePanel();
            panelMode = CivicHudPanelMode.None;
            overlayView.HideExitPopup();
            overlayView.ShowEvent(queued, choice => events.IsChoiceAvailable(choice.Id), choice => events.DescribeChoice(choice.Id));
        }

        private void CloseEventPopup() => overlayView.HideEventPopup();

        private void HandleEventChoice(string eventId, string choiceId)
        {
            var events = moduleRuntime?.GetModule<CivicEventModule>(CivicFeatureRegistry.Events);
            if (events?.TryChoose(eventId, choiceId) != true) return;
            overlayView.HideEventPopup();
            lastAutoOpenedEventId = string.Empty;
            Render();
        }

        public void ToggleDebugPanel()
        {
            if (overlayView.IsDebugPanelOpen)
            {
                CloseDebugPanel();
                return;
            }

            overlayView.HideExitPopup();
            overlayView.HideEventPopup();
            modulePanelView.ClosePanel();
            panelMode = CivicHudPanelMode.None;
            var domains = CivicFeatureRegistry.Features
                .Where(item => moduleRuntime != null && moduleRuntime.Modules.ContainsKey(item.Id) &&
                    (item.Id == CivicFeatureRegistry.Events || item.Id == CivicFeatureRegistry.GreatPeople ||
                     item.Id == CivicFeatureRegistry.NationFormation || item.Id == CivicFeatureRegistry.Politics ||
                     item.Id == CivicFeatureRegistry.StartCivilizations))
                .ToArray();
            overlayView.ConfigureDebugDomains(domains.Select(item => item.Id).ToArray(), domains.Select(item => item.DisplayName).ToArray());
            overlayView.ShowDebugPanel();
            RefreshDebugTargets();
        }

        private void CloseDebugPanel() => overlayView.HideDebugPanel();

        private void RefreshDebugTargets()
        {
            if (moduleRuntime == null) return;
            var featureId = overlayView.SelectedDebugDomainId;
            if (featureId == CivicFeatureRegistry.Events)
            {
                var definitions = moduleRuntime.GetModule<CivicEventModule>(featureId)?.Definitions ?? Array.Empty<CivicEventDefinition>();
                overlayView.ConfigureDebugTargets(definitions.Select(item => item.Id).ToArray(), definitions.Select(item => item.TitleKo).ToArray(), "선택한 이벤트를 조건·확률과 무관하게 대기열에 등록합니다.", "이벤트 발생");
            }
            else if (featureId == CivicFeatureRegistry.GreatPeople)
            {
                var definitions = moduleRuntime.GetModule<CivicPeopleModule>(featureId)?.Definitions ?? Array.Empty<CivicPersonDefinition>();
                overlayView.ConfigureDebugTargets(definitions.Select(item => item.Id).ToArray(), definitions.Select(item => item.DisplayNameKo).ToArray(), "선택한 인물을 발견 조건과 무관하게 영입 후보로 등록합니다.", "후보 등록");
            }
            else if (featureId == CivicFeatureRegistry.NationFormation)
            {
                var definitions = moduleRuntime.GetModule<CivicNationModule>(featureId)?.Definitions ?? Array.Empty<CivicNationDefinition>();
                overlayView.ConfigureDebugTargets(definitions.Select(item => item.Id).ToArray(), definitions.Select(item => item.DisplayNameKo).ToArray(), "선택한 국가의 설립 조건을 이번 런에서 강제로 충족 처리합니다.", "조건 충족");
            }
            else if (featureId == CivicFeatureRegistry.Politics)
            {
                var definitions = moduleRuntime.GetModule<CivicPoliticsModule>(featureId)?.Definitions ?? Array.Empty<CivicInstitutionDefinition>();
                overlayView.ConfigureDebugTargets(definitions.Select(item => item.Id).ToArray(), definitions.Select(item => item.DisplayNameKo).ToArray(), "선택한 체계를 강제 해금하고 개혁 비용을 테스트할 수 있도록 정치력·국고를 지급합니다.", "해금·비용 지급");
            }
            else if (featureId == CivicFeatureRegistry.StartCivilizations)
            {
                var definitions = moduleRuntime.GetModule<CivicCivilizationModule>(featureId)?.Definitions ?? Array.Empty<CivicCivilizationDefinition>();
                overlayView.ConfigureDebugTargets(definitions.Select(item => item.Id).ToArray(), definitions.Select(item => item.DisplayNameKo).ToArray(), "현재 feature 조합을 유지하고 선택한 시작 문명으로 SampleScene을 다시 시작합니다. 현재 런은 종료됩니다.", "런 재시작");
            }
            else
            {
                overlayView.ConfigureDebugTargets(Array.Empty<string>(), Array.Empty<string>(), "지원되는 디버그 대상이 없습니다.", "실행");
            }
        }

        private void ExecuteDebugAction()
        {
            if (moduleRuntime == null) return;
            var featureId = overlayView.SelectedDebugDomainId;
            var targetId = overlayView.SelectedDebugTargetId;
            if (string.IsNullOrEmpty(targetId)) return;
            if (featureId == CivicFeatureRegistry.Events) moduleRuntime.GetModule<CivicEventModule>(featureId)?.DebugQueueEvent(targetId);
            else if (featureId == CivicFeatureRegistry.GreatPeople) moduleRuntime.GetModule<CivicPeopleModule>(featureId)?.DebugOfferCandidate(targetId);
            else if (featureId == CivicFeatureRegistry.NationFormation) moduleRuntime.GetModule<CivicNationModule>(featureId)?.DebugSatisfyConditions(targetId);
            else if (featureId == CivicFeatureRegistry.Politics) moduleRuntime.GetModule<CivicPoliticsModule>(featureId)?.DebugUnlockAndFund(targetId);
            else if (featureId == CivicFeatureRegistry.StartCivilizations)
            {
                CivicRunLaunchSettings.StartingCivilizationId = targetId;
                SceneManager.LoadScene("SampleScene");
                return;
            }
            Render();
        }

        private void HandleModuleAction(string featureId, string key)
        {
            if (moduleRuntime == null) return;
            if (featureId == CivicFeatureRegistry.Prestige)
            {
                var module = moduleRuntime.GetModule<CivicPrestigeModule>(featureId);
                if (key != "__prestige")
                {
                    module?.TryPurchaseLegacyPerk(key, out _, out _);
                    prestigeConfirmationPending = false;
                }
                else if (!prestigeConfirmationPending)
                {
                    prestigeConfirmationPending = true;
                }
                else
                {
                    if (module != null && module.TryPrestige(out _))
                    {
                        CivicFeatureRuntime.ResetForMainMenu();
                        CivicRunLaunchSettings.Reset();
                        SceneManager.LoadScene("MainMenu");
                        return;
                    }
                }
            }
            else if (featureId == CivicFeatureRegistry.NationFormation)
            {
                var module = moduleRuntime.GetModule<CivicNationModule>(featureId);
                var candidate = module?.Snapshot.FirstOrDefault(item => item.Definition.Id == key);
                if (candidate?.State == CivicNationCandidateState.Ready) module.TryDeclare(key);
                else if (candidate?.State == CivicNationCandidateState.Preparing) module.CancelPreparation();
                else if (candidate?.State == CivicNationCandidateState.AwaitingCharter) module.TryCompleteFormation();
            }
            else if (featureId == CivicFeatureRegistry.Politics)
            {
                var module = moduleRuntime.GetModule<CivicPoliticsModule>(featureId);
                if (key == "__cancel") module?.CancelReform();
                else module?.TryPropose(key);
            }
            else if (featureId == CivicFeatureRegistry.Events)
            {
                var separator = key.IndexOf('|');
                if (separator > 0) moduleRuntime.GetModule<CivicEventModule>(featureId)?.TryChoose(key.Substring(0, separator), key.Substring(separator + 1));
            }
            else if (featureId == CivicFeatureRegistry.Wonders)
            {
                var module = moduleRuntime.GetModule<CivicWonderModule>(featureId);
                if (module?.ActiveProjectId == key) module.CancelActiveProject();
                else module?.TryStart(key);
            }
            else if (featureId == CivicFeatureRegistry.GreatPeople)
            {
                var module = moduleRuntime.GetModule<CivicPeopleModule>(featureId);
                if (key.StartsWith("recruit:", StringComparison.Ordinal)) module?.TryRecruit(key.Substring("recruit:".Length));
                else if (key.StartsWith("ability:", StringComparison.Ordinal)) module?.TryUseAbility(key.Substring("ability:".Length));
                else if (key.StartsWith("assign:", StringComparison.Ordinal) && module != null)
                {
                    var personId = key.Substring("assign:".Length);
                    var active = module.ActivePeople.FirstOrDefault(item => item.Definition.Id == personId);
                    if (active != null)
                    {
                        var assignments = active.Definition.AllowedAssignments;
                        var currentIndex = assignments.ToList().IndexOf(active.AssignmentId);
                        module.TryAssign(personId, assignments[(currentIndex + 1 + assignments.Count) % assignments.Count]);
                    }
                }
            }

            Render();
        }

        private void ShowResourcesPanel()
        {
            modulePanelView?.ClosePanel();
            panelMode = CivicHudPanelMode.Resources;
            Render();
        }

        private void ShowBuildingsPanel()
        {
            modulePanelView?.ClosePanel();
            panelMode = CivicHudPanelMode.Buildings;
            Render();
        }

        private void ShowTechnologiesPanel()
        {
            modulePanelView?.ClosePanel();
            panelMode = CivicHudPanelMode.Technologies;
            Render();
        }

        private void CloseDetailPanel()
        {
            panelMode = CivicHudPanelMode.None;
            Render();
        }

        private void HandleModulePanelOpened()
        {
            panelMode = CivicHudPanelMode.None;
            Render();
        }

        private void ToggleFoodChildren()
        {
            showFoodChildren = !showFoodChildren;
            Render();
        }

        private void BuildRequestedBuilding(string buildingId)
        {
            moduleRuntime?.TryBuild(buildingId);

            Render();
        }

        private void ResearchRequestedTechnology(string technologyId)
        {
            moduleRuntime?.TryResearch(technologyId);

            Render();
        }

        private void SelectTechnologyEra(string eraId)
        {
            selectedTechnologyEraId = eraId;
            Render();
        }

        private void EnsureSelectedTechnologyEra(CivicGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible && era.Id == selectedTechnologyEraId)
                {
                    return;
                }
            }

            selectedTechnologyEraId = snapshot.CurrentEraId;
            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible && era.Id == selectedTechnologyEraId)
                {
                    return;
                }
            }

            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible)
                {
                    selectedTechnologyEraId = era.Id;
                    return;
                }
            }
        }
    }
}
