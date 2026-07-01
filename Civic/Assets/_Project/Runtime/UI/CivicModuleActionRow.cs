using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicModuleActionRow : MonoBehaviour
    {
        [SerializeField] private Text infoLabel;
        [SerializeField] private GameObject descriptionRoot;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionLabel;
        [SerializeField] private CivicTooltipTrigger tooltip;
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceLabels;
        [SerializeField] private CivicTooltipTrigger[] choiceTooltips;

        private string key;
        private Action<string> requested;
        private string[] choiceKeys = Array.Empty<string>();
        private Action<string> choiceRequested;
        private UnityAction[] choiceHandlers = Array.Empty<UnityAction>();

        public bool HasRequiredReferences =>
            infoLabel != null && descriptionRoot != null && descriptionLabel != null &&
            actionButton != null && actionLabel != null && tooltip != null && HasValidChoiceReferences();
        public Text InfoLabel => infoLabel;
        public GameObject DescriptionRoot => descriptionRoot;
        public Text DescriptionLabel => descriptionLabel;
        public Button ActionButton => actionButton;
        public IReadOnlyList<Button> ChoiceButtons => choiceButtons ?? Array.Empty<Button>();
        public IReadOnlyList<Text> ChoiceLabels => choiceLabels ?? Array.Empty<Text>();

        public void Bind(string rowKey, string info, string description, string actionText, bool interactable, string tooltipText, Action<string> onRequested)
        {
            key = rowKey ?? string.Empty;
            requested = onRequested;
            infoLabel.text = info ?? string.Empty;
            descriptionLabel.text = description ?? string.Empty;
            descriptionRoot.SetActive(!string.IsNullOrWhiteSpace(description));
            actionLabel.text = string.IsNullOrEmpty(actionText) ? "—" : actionText;
            actionButton.interactable = interactable;
            actionButton.onClick.RemoveListener(HandleRequested);
            actionButton.onClick.AddListener(HandleRequested);
            tooltip.SetTooltipText(tooltipText ?? string.Empty);
            HideChoices();
        }

        public void BindChoices(
            IReadOnlyList<string> keys,
            IReadOnlyList<string> labels,
            IReadOnlyList<bool> interactable,
            IReadOnlyList<string> tooltipTexts,
            Action<string> onRequested)
        {
            var count = Math.Min(keys?.Count ?? 0, ChoiceButtons.Count);
            choiceRequested = onRequested;
            choiceKeys = new string[ChoiceButtons.Count];
            ClearChoiceListeners();
            choiceHandlers = new UnityAction[ChoiceButtons.Count];
            if (choiceRoot != null) choiceRoot.SetActive(count > 0);
            SetDescriptionBottom(count > 0 ? 48f : 0f);
            for (var index = 0; index < ChoiceButtons.Count; index++)
            {
                var visible = index < count;
                ChoiceButtons[index].gameObject.SetActive(visible);
                if (!visible) continue;
                choiceKeys[index] = keys[index] ?? string.Empty;
                ChoiceLabels[index].text = labels != null && index < labels.Count ? labels[index] : "선택";
                ChoiceButtons[index].interactable = interactable == null || index >= interactable.Count || interactable[index];
                if (choiceTooltips != null && index < choiceTooltips.Length) choiceTooltips[index].SetTooltipText(tooltipTexts != null && index < tooltipTexts.Count ? tooltipTexts[index] : string.Empty);
                var captured = index;
                choiceHandlers[index] = () => HandleChoiceRequested(captured);
                ChoiceButtons[index].onClick.AddListener(choiceHandlers[index]);
            }
        }

        private void OnDisable()
        {
            actionButton?.onClick.RemoveListener(HandleRequested);
            ClearChoiceListeners();
        }

        private void HandleRequested()
        {
            requested?.Invoke(key);
        }

        private void HandleChoiceRequested(int index)
        {
            if (index < 0 || index >= choiceKeys.Length) return;
            choiceRequested?.Invoke(choiceKeys[index]);
        }

        private void HideChoices()
        {
            ClearChoiceListeners();
            if (choiceRoot != null) choiceRoot.SetActive(false);
            SetDescriptionBottom(0f);
        }

        private void ClearChoiceListeners()
        {
            for (var index = 0; index < choiceButtons?.Length && index < choiceHandlers.Length; index++)
            {
                if (choiceHandlers[index] != null) choiceButtons[index]?.onClick.RemoveListener(choiceHandlers[index]);
            }
        }

        private void SetDescriptionBottom(float bottom)
        {
            if (!(descriptionRoot?.transform is RectTransform rect)) return;
            var offset = rect.offsetMin;
            offset.y = bottom;
            rect.offsetMin = offset;
        }

        private bool HasValidChoiceReferences()
        {
            if (choiceRoot == null)
            {
                return (choiceButtons == null || choiceButtons.Length == 0) &&
                    (choiceLabels == null || choiceLabels.Length == 0) &&
                    (choiceTooltips == null || choiceTooltips.Length == 0);
            }

            return choiceButtons != null && choiceLabels != null && choiceTooltips != null &&
                choiceButtons.Length > 0 && choiceButtons.Length == choiceLabels.Length && choiceButtons.Length == choiceTooltips.Length;
        }
    }
}
