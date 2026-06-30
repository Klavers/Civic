using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Simulation.Modules;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicHudOverlayView : MonoBehaviour
    {
        [SerializeField] private GameObject exitPopupRoot;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button eventAlertButton;
        [SerializeField] private Text eventAlertLabel;
        [SerializeField] private GameObject eventPopupRoot;
        [SerializeField] private Text eventTitleLabel;
        [SerializeField] private Text eventDescriptionLabel;
        [SerializeField] private Text eventCauseLabel;
        [SerializeField] private Button eventCloseButton;
        [SerializeField] private Button[] eventChoiceButtons;
        [SerializeField] private Text[] eventChoiceLabels;
        [SerializeField] private CivicTooltipTrigger[] eventChoiceTooltips;
        [SerializeField] private GameObject debugPanelRoot;
        [SerializeField] private Button debugPreviousDomainButton;
        [SerializeField] private Button debugNextDomainButton;
        [SerializeField] private Text debugDomainLabel;
        [SerializeField] private Button debugPreviousTargetButton;
        [SerializeField] private Button debugNextTargetButton;
        [SerializeField] private Text debugTargetLabel;
        [SerializeField] private Text debugDescriptionLabel;
        [SerializeField] private Button debugActionButton;
        [SerializeField] private Text debugActionLabel;
        [SerializeField] private Button debugCloseButton;
        [SerializeField] private CivicTooltipView tooltipView;

        private string currentEventId = string.Empty;
        private IReadOnlyList<string> debugDomainIds = Array.Empty<string>();
        private IReadOnlyList<string> debugDomainLabels = Array.Empty<string>();
        private IReadOnlyList<string> debugTargetIds = Array.Empty<string>();
        private IReadOnlyList<string> debugTargetLabels = Array.Empty<string>();
        private int debugDomainIndex;
        private int debugTargetIndex;

        public event Action ContinueRequested;
        public event Action MainMenuRequested;
        public event Action EventAlertRequested;
        public event Action EventCloseRequested;
        public event Action<string, string> EventChoiceRequested;
        public event Action DebugDomainChanged;
        public event Action DebugActionRequested;
        public event Action DebugCloseRequested;

        public bool HasRequiredReferences =>
            exitPopupRoot != null && continueButton != null && mainMenuButton != null &&
            eventAlertButton != null && eventAlertLabel != null && eventPopupRoot != null &&
            eventTitleLabel != null && eventDescriptionLabel != null && eventCauseLabel != null && eventCloseButton != null &&
            eventChoiceButtons != null && eventChoiceLabels != null && eventChoiceTooltips != null &&
            eventChoiceButtons.Length == 3 && eventChoiceLabels.Length == 3 && eventChoiceTooltips.Length == 3 &&
            eventChoiceButtons.All(item => item != null) && eventChoiceLabels.All(item => item != null) && eventChoiceTooltips.All(item => item != null) &&
            debugPanelRoot != null && debugPreviousDomainButton != null && debugNextDomainButton != null && debugDomainLabel != null &&
            debugPreviousTargetButton != null && debugNextTargetButton != null && debugTargetLabel != null && debugDescriptionLabel != null &&
            debugActionButton != null && debugActionLabel != null && debugCloseButton != null && tooltipView != null;

        public bool IsExitPopupOpen => exitPopupRoot != null && exitPopupRoot.activeSelf;
        public bool IsEventPopupOpen => eventPopupRoot != null && eventPopupRoot.activeSelf;
        public bool IsDebugPanelOpen => debugPanelRoot != null && debugPanelRoot.activeSelf;
        public GameObject ExitPopupRoot => exitPopupRoot;
        public GameObject EventPopupRoot => eventPopupRoot;
        public GameObject DebugPanelRoot => debugPanelRoot;
        public Button ContinueButton => continueButton;
        public Button MainMenuButton => mainMenuButton;
        public Button EventAlertButton => eventAlertButton;
        public IReadOnlyList<Button> EventChoiceButtons => eventChoiceButtons ?? Array.Empty<Button>();
        public string SelectedDebugDomainId => SelectedId(debugDomainIds, debugDomainIndex);
        public string SelectedDebugTargetId => SelectedId(debugTargetIds, debugTargetIndex);

        private void OnEnable()
        {
            if (!HasRequiredReferences) return;
            continueButton.onClick.AddListener(NotifyContinue);
            mainMenuButton.onClick.AddListener(NotifyMainMenu);
            eventAlertButton.onClick.AddListener(NotifyEventAlert);
            eventCloseButton.onClick.AddListener(NotifyEventClose);
            debugPreviousDomainButton.onClick.AddListener(SelectPreviousDebugDomain);
            debugNextDomainButton.onClick.AddListener(SelectNextDebugDomain);
            debugPreviousTargetButton.onClick.AddListener(SelectPreviousDebugTarget);
            debugNextTargetButton.onClick.AddListener(SelectNextDebugTarget);
            debugActionButton.onClick.AddListener(NotifyDebugAction);
            debugCloseButton.onClick.AddListener(NotifyDebugClose);
            for (var index = 0; index < eventChoiceButtons.Length; index++)
            {
                var captured = index;
                eventChoiceButtons[index].onClick.AddListener(() => NotifyEventChoice(captured));
            }
        }

        private void OnDisable()
        {
            tooltipView?.Hide();
            continueButton?.onClick.RemoveListener(NotifyContinue);
            mainMenuButton?.onClick.RemoveListener(NotifyMainMenu);
            eventAlertButton?.onClick.RemoveListener(NotifyEventAlert);
            eventCloseButton?.onClick.RemoveListener(NotifyEventClose);
            debugPreviousDomainButton?.onClick.RemoveListener(SelectPreviousDebugDomain);
            debugNextDomainButton?.onClick.RemoveListener(SelectNextDebugDomain);
            debugPreviousTargetButton?.onClick.RemoveListener(SelectPreviousDebugTarget);
            debugNextTargetButton?.onClick.RemoveListener(SelectNextDebugTarget);
            debugActionButton?.onClick.RemoveListener(NotifyDebugAction);
            debugCloseButton?.onClick.RemoveListener(NotifyDebugClose);
            for (var index = 0; index < eventChoiceButtons?.Length; index++) eventChoiceButtons[index]?.onClick.RemoveAllListeners();
        }

        public void ShowExitPopup()
        {
            exitPopupRoot.SetActive(true);
        }

        public void HideExitPopup()
        {
            tooltipView?.Hide();
            exitPopupRoot.SetActive(false);
        }

        public void RenderEventAlert(int queueCount)
        {
            eventAlertButton.gameObject.SetActive(queueCount > 0);
            eventAlertLabel.text = queueCount > 0 ? $"이벤트 {queueCount}" : "이벤트";
        }

        public void ShowEvent(CivicQueuedEventSnapshot queued, Func<CivicEventChoiceDefinition, bool> availability, Func<CivicEventChoiceDefinition, string> tooltipFactory)
        {
            if (queued == null) return;
            currentEventId = queued.Definition.Id;
            eventTitleLabel.text = queued.Definition.TitleKo;
            eventDescriptionLabel.text = queued.Definition.DescriptionKo;
            eventCauseLabel.text = "발생 원인: " + queued.Cause;
            for (var index = 0; index < eventChoiceButtons.Length; index++)
            {
                var visible = index < queued.Choices.Count;
                eventChoiceButtons[index].gameObject.SetActive(visible);
                if (!visible) continue;
                var choice = queued.Choices[index];
                eventChoiceButtons[index].name = choice.Id;
                eventChoiceLabels[index].text = choice.TextKo;
                eventChoiceButtons[index].interactable = availability?.Invoke(choice) ?? true;
                eventChoiceTooltips[index].SetTooltipText(tooltipFactory?.Invoke(choice) ?? string.Empty);
            }
            eventPopupRoot.SetActive(true);
        }

        public void HideEventPopup()
        {
            tooltipView?.Hide();
            eventPopupRoot.SetActive(false);
        }

        public void ConfigureDebugDomains(IReadOnlyList<string> ids, IReadOnlyList<string> labels)
        {
            debugDomainIds = ids ?? Array.Empty<string>();
            debugDomainLabels = labels ?? Array.Empty<string>();
            debugDomainIndex = 0;
            RenderDebugSelectors();
        }

        public void ConfigureDebugTargets(IReadOnlyList<string> ids, IReadOnlyList<string> labels, string description, string actionLabel)
        {
            debugTargetIds = ids ?? Array.Empty<string>();
            debugTargetLabels = labels ?? Array.Empty<string>();
            debugTargetIndex = 0;
            debugDescriptionLabel.text = description ?? string.Empty;
            debugActionLabel.text = actionLabel ?? "실행";
            debugActionButton.interactable = debugTargetIds.Count > 0;
            RenderDebugSelectors();
        }

        public void ShowDebugPanel()
        {
            debugPanelRoot.SetActive(true);
        }

        public void HideDebugPanel()
        {
            tooltipView?.Hide();
            debugPanelRoot.SetActive(false);
        }

        private static string SelectedId(IReadOnlyList<string> ids, int index)
        {
            if (ids == null || index < 0 || index >= ids.Count) return string.Empty;
            return ids[index];
        }

        private void SelectPreviousDebugDomain() => MoveDebugDomain(-1);
        private void SelectNextDebugDomain() => MoveDebugDomain(1);
        private void MoveDebugDomain(int delta)
        {
            if (debugDomainIds.Count == 0) return;
            debugDomainIndex = (debugDomainIndex + delta + debugDomainIds.Count) % debugDomainIds.Count;
            RenderDebugSelectors();
            DebugDomainChanged?.Invoke();
        }

        private void SelectPreviousDebugTarget() => MoveDebugTarget(-1);
        private void SelectNextDebugTarget() => MoveDebugTarget(1);
        private void MoveDebugTarget(int delta)
        {
            if (debugTargetIds.Count == 0) return;
            debugTargetIndex = (debugTargetIndex + delta + debugTargetIds.Count) % debugTargetIds.Count;
            RenderDebugSelectors();
        }

        private void RenderDebugSelectors()
        {
            debugDomainLabel.text = debugDomainLabels.Count > debugDomainIndex && debugDomainIndex >= 0 ? debugDomainLabels[debugDomainIndex] : "대상 모듈 없음";
            debugTargetLabel.text = debugTargetLabels.Count > debugTargetIndex && debugTargetIndex >= 0 ? debugTargetLabels[debugTargetIndex] : "대상 없음";
            debugPreviousDomainButton.interactable = debugDomainIds.Count > 1;
            debugNextDomainButton.interactable = debugDomainIds.Count > 1;
            debugPreviousTargetButton.interactable = debugTargetIds.Count > 1;
            debugNextTargetButton.interactable = debugTargetIds.Count > 1;
        }

        private void NotifyContinue() => ContinueRequested?.Invoke();
        private void NotifyMainMenu() => MainMenuRequested?.Invoke();
        private void NotifyEventAlert() => EventAlertRequested?.Invoke();
        private void NotifyEventClose() => EventCloseRequested?.Invoke();
        private void NotifyEventChoice(int index)
        {
            if (index < 0 || index >= eventChoiceButtons.Length || !eventChoiceButtons[index].gameObject.activeSelf) return;
            EventChoiceRequested?.Invoke(currentEventId, eventChoiceButtons[index].name);
        }
        private void NotifyDebugAction() => DebugActionRequested?.Invoke();
        private void NotifyDebugClose() => DebugCloseRequested?.Invoke();
    }
}
