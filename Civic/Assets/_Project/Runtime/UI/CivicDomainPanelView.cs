using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        public string FeatureId => featureId;
        public GameObject PanelRoot => panelRoot;
        public Text TitleLabel => titleLabel;
        public Text StatusLabel => statusLabel;
        public IReadOnlyList<CivicModuleActionRow> Rows => rows ?? Array.Empty<CivicModuleActionRow>();
        public bool HasRequiredReferences =>
            !string.IsNullOrWhiteSpace(featureId) && panelRoot != null && titleLabel != null && statusLabel != null &&
            rows != null && rows.Length >= 15 && rows.All(row => row != null && row.HasRequiredReferences);

        public void SetVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }
    }
}
