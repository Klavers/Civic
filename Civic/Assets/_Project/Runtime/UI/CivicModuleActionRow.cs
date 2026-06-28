using System;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicModuleActionRow : MonoBehaviour
    {
        [SerializeField] private Text infoLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionLabel;
        [SerializeField] private CivicTooltipTrigger tooltip;

        private string key;
        private Action<string> requested;

        public bool HasRequiredReferences => infoLabel != null && actionButton != null && actionLabel != null && tooltip != null;
        public Text InfoLabel => infoLabel;
        public Button ActionButton => actionButton;

        public void Bind(string rowKey, string info, string actionText, bool interactable, string tooltipText, Action<string> onRequested)
        {
            key = rowKey ?? string.Empty;
            requested = onRequested;
            infoLabel.text = info ?? string.Empty;
            actionLabel.text = string.IsNullOrEmpty(actionText) ? "—" : actionText;
            actionButton.interactable = interactable;
            actionButton.onClick.RemoveListener(HandleRequested);
            actionButton.onClick.AddListener(HandleRequested);
            tooltip.SetTooltipText(tooltipText ?? string.Empty);
        }

        private void OnDisable()
        {
            actionButton?.onClick.RemoveListener(HandleRequested);
        }

        private void HandleRequested()
        {
            requested?.Invoke(key);
        }
    }
}
