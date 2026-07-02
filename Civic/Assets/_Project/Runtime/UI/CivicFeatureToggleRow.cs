using System;
using Civic.Features;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicFeatureToggleRow : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text stateLabel;

        private string featureId;
        private Action<string, bool> changed;

        public bool HasRequiredReferences =>
            toggle != null && nameLabel != null && descriptionLabel != null && stateLabel != null;
        public Toggle Toggle => toggle;
        public string FeatureId => featureId;

        public void Bind(CivicFeatureDefinition definition, bool isEnabled, Action<string, bool> onChanged)
        {
            if (definition == null || !HasRequiredReferences)
            {
                return;
            }

            featureId = definition.Id;
            changed = onChanged;
            nameLabel.text = definition.DisplayName;
            descriptionLabel.text = definition.Description;
            toggle.onValueChanged.RemoveListener(HandleValueChanged);
            toggle.SetIsOnWithoutNotify(isEnabled);
            toggle.onValueChanged.AddListener(HandleValueChanged);
            UpdateState(isEnabled);
        }

        private void HandleValueChanged(bool isEnabled)
        {
            UpdateState(isEnabled);
            changed?.Invoke(featureId, isEnabled);
        }

        private void UpdateState(bool isEnabled)
        {
            stateLabel.text = isEnabled ? "ON" : "OFF";
            stateLabel.color = isEnabled
                ? new Color(0.48f, 0.92f, 0.58f, 1f)
                : new Color(0.62f, 0.65f, 0.70f, 1f);
        }
    }
}
