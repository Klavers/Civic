using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicDomainPanelView : MonoBehaviour
    {
        [SerializeField] private string featureId;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private CivicModuleActionRow[] rows;
        [SerializeField] private GameObject categoryTabRoot;
        [SerializeField] private GameObject[] categoryTabRows;
        [SerializeField] private Button[] categoryTabButtons;
        [SerializeField] private Text[] categoryTabLabels;
        [SerializeField] private GameObject impossibleFilterRoot;
        [SerializeField] private Toggle impossibleFilterToggle;
        [SerializeField] private Text impossibleFilterLabel;

        private UnityAction[] categoryTabHandlers = Array.Empty<UnityAction>();

        public event Action<int> CategoryTabRequested;
        public event Action<bool> ImpossibleFilterChanged;

        public string FeatureId => featureId;
        public GameObject PanelRoot => panelRoot;
        public Text TitleLabel => titleLabel;
        public Text StatusLabel => statusLabel;
        public IReadOnlyList<CivicModuleActionRow> Rows => rows ?? Array.Empty<CivicModuleActionRow>();
        public IReadOnlyList<GameObject> CategoryTabRows => categoryTabRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Button> CategoryTabButtons => categoryTabButtons ?? Array.Empty<Button>();
        public IReadOnlyList<Text> CategoryTabLabels => categoryTabLabels ?? Array.Empty<Text>();
        public GameObject ImpossibleFilterRoot => impossibleFilterRoot;
        public Toggle ImpossibleFilterToggle => impossibleFilterToggle;
        public bool HasRequiredReferences =>
            !string.IsNullOrWhiteSpace(featureId) && panelRoot != null && titleLabel != null && statusLabel != null &&
            rows != null && rows.Length >= 15 && rows.All(row => row != null && row.HasRequiredReferences) &&
            HasValidCategoryTabReferences() && HasValidImpossibleFilterReferences();

        private void OnEnable()
        {
            if (categoryTabButtons != null)
            {
                categoryTabHandlers = new UnityAction[categoryTabButtons.Length];
                for (var index = 0; index < categoryTabButtons.Length; index++)
                {
                    var captured = index;
                    categoryTabHandlers[index] = () => CategoryTabRequested?.Invoke(captured);
                    categoryTabButtons[index]?.onClick.AddListener(categoryTabHandlers[index]);
                }
            }

            impossibleFilterToggle?.onValueChanged.AddListener(NotifyImpossibleFilterChanged);
        }

        private void OnDisable()
        {
            for (var index = 0; index < categoryTabButtons?.Length && index < categoryTabHandlers.Length; index++)
            {
                if (categoryTabHandlers[index] != null) categoryTabButtons[index]?.onClick.RemoveListener(categoryTabHandlers[index]);
            }

            impossibleFilterToggle?.onValueChanged.RemoveListener(NotifyImpossibleFilterChanged);
        }

        public void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        public void ConfigureCategoryTabs(IReadOnlyList<string> labels, int selectedIndex)
        {
            var count = labels?.Count ?? 0;
            if (categoryTabRoot != null) categoryTabRoot.SetActive(count > 0);
            for (var index = 0; index < CategoryTabRows.Count; index++)
            {
                var visible = index < count;
                CategoryTabRows[index].SetActive(visible);
                if (!visible) continue;
                CategoryTabLabels[index].text = labels[index];
                CategoryTabButtons[index].interactable = index != selectedIndex;
            }
        }

        public void ConfigureImpossibleFilter(bool visible, bool value, string label)
        {
            if (impossibleFilterRoot == null) return;
            impossibleFilterRoot.SetActive(visible);
            if (!visible) return;
            impossibleFilterLabel.text = label ?? string.Empty;
            impossibleFilterToggle.SetIsOnWithoutNotify(value);
        }

        private bool HasValidCategoryTabReferences()
        {
            if (categoryTabRoot == null)
            {
                return (categoryTabRows == null || categoryTabRows.Length == 0) &&
                    (categoryTabButtons == null || categoryTabButtons.Length == 0) &&
                    (categoryTabLabels == null || categoryTabLabels.Length == 0);
            }

            return categoryTabRows != null && categoryTabButtons != null && categoryTabLabels != null &&
                categoryTabRows.Length > 0 && categoryTabRows.Length == categoryTabButtons.Length && categoryTabRows.Length == categoryTabLabels.Length &&
                categoryTabRows.All(item => item != null) && categoryTabButtons.All(item => item != null) && categoryTabLabels.All(item => item != null);
        }

        private bool HasValidImpossibleFilterReferences()
        {
            return impossibleFilterRoot == null
                ? impossibleFilterToggle == null && impossibleFilterLabel == null
                : impossibleFilterToggle != null && impossibleFilterLabel != null;
        }

        private void NotifyImpossibleFilterChanged(bool value) => ImpossibleFilterChanged?.Invoke(value);
    }
}
