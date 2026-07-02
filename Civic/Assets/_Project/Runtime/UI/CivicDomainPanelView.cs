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
        [SerializeField] private GameObject filterRoot;
        [SerializeField] private GameObject[] filterRows;
        [SerializeField] private Toggle[] filterToggles;
        [SerializeField] private Text[] filterLabels;

        private UnityAction[] categoryTabHandlers = Array.Empty<UnityAction>();
        private UnityAction<bool>[] filterHandlers = Array.Empty<UnityAction<bool>>();

        public event Action<int> CategoryTabRequested;
        public event Action<bool> ImpossibleFilterChanged;
        public event Action<int, bool> FilterChanged;

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
        public GameObject FilterRoot => filterRoot;
        public IReadOnlyList<GameObject> FilterRows => filterRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Toggle> FilterToggles => filterToggles ?? Array.Empty<Toggle>();
        public IReadOnlyList<Text> FilterLabels => filterLabels ?? Array.Empty<Text>();
        public bool HasRequiredReferences =>
            !string.IsNullOrWhiteSpace(featureId) && panelRoot != null && titleLabel != null && statusLabel != null &&
            rows != null && rows.Length >= 15 && rows.All(row => row != null && row.HasRequiredReferences) &&
            HasValidCategoryTabReferences() && HasValidImpossibleFilterReferences() && HasValidFilterReferences();

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
            if (filterToggles != null)
            {
                filterHandlers = new UnityAction<bool>[filterToggles.Length];
                for (var index = 0; index < filterToggles.Length; index++)
                {
                    var captured = index;
                    filterHandlers[index] = value => FilterChanged?.Invoke(captured, value);
                    filterToggles[index]?.onValueChanged.AddListener(filterHandlers[index]);
                }
            }
        }

        private void OnDisable()
        {
            for (var index = 0; index < categoryTabButtons?.Length && index < categoryTabHandlers.Length; index++)
            {
                if (categoryTabHandlers[index] != null) categoryTabButtons[index]?.onClick.RemoveListener(categoryTabHandlers[index]);
            }

            impossibleFilterToggle?.onValueChanged.RemoveListener(NotifyImpossibleFilterChanged);
            for (var index = 0; index < filterToggles?.Length && index < filterHandlers.Length; index++)
            {
                if (filterHandlers[index] != null) filterToggles[index]?.onValueChanged.RemoveListener(filterHandlers[index]);
            }
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

        public void ConfigureFilters(IReadOnlyList<string> labels, IReadOnlyList<bool> values)
        {
            var count = Math.Min(labels?.Count ?? 0, values?.Count ?? 0);
            if (filterRoot != null) filterRoot.SetActive(count > 0);
            for (var index = 0; index < FilterRows.Count; index++)
            {
                var visible = index < count;
                FilterRows[index].SetActive(visible);
                if (!visible) continue;
                FilterLabels[index].text = labels[index];
                FilterToggles[index].SetIsOnWithoutNotify(values[index]);
            }
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

        private bool HasValidFilterReferences()
        {
            if (filterRoot == null)
            {
                return (filterRows == null || filterRows.Length == 0) &&
                    (filterToggles == null || filterToggles.Length == 0) &&
                    (filterLabels == null || filterLabels.Length == 0);
            }

            return filterRows != null && filterToggles != null && filterLabels != null &&
                filterRows.Length > 0 && filterRows.Length == filterToggles.Length && filterRows.Length == filterLabels.Length &&
                filterRows.All(item => item != null) && filterToggles.All(item => item != null) && filterLabels.All(item => item != null);
        }

        private void NotifyImpossibleFilterChanged(bool value) => ImpossibleFilterChanged?.Invoke(value);
    }
}
