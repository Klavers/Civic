using System;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class IdlePanelView : MonoBehaviour
    {
        [SerializeField] private Text currencyLabel;
        [SerializeField] private Text rateLabel;
        [SerializeField] private Button workButton;
        [SerializeField] private Text workButtonLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Text upgradeButtonLabel;

        public event Action WorkRequested;
        public event Action UpgradeRequested;

        public Text CurrencyLabel => currencyLabel;
        public Text RateLabel => rateLabel;
        public Button WorkButton => workButton;
        public Button UpgradeButton => upgradeButton;
        public bool HasRequiredReferences =>
            currencyLabel != null &&
            rateLabel != null &&
            workButton != null &&
            workButtonLabel != null &&
            upgradeButton != null &&
            upgradeButtonLabel != null;

        private void OnEnable()
        {
            if (workButton != null)
            {
                workButton.onClick.AddListener(NotifyWorkRequested);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(NotifyUpgradeRequested);
            }
        }

        private void OnDisable()
        {
            if (workButton != null)
            {
                workButton.onClick.RemoveListener(NotifyWorkRequested);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(NotifyUpgradeRequested);
            }
        }

        public void Render(double currency, double coinsPerSecond)
        {
            if (currencyLabel != null)
            {
                currencyLabel.text = $"Coins: {currency:0}";
            }

            if (rateLabel != null)
            {
                rateLabel.text = $"Per second: {coinsPerSecond:0.0}";
            }
        }

        public void Render(double currency, double coinsPerSecond, double clickReward, double nextUpgradeCost)
        {
            Render(currency, coinsPerSecond);

            if (rateLabel != null)
            {
                rateLabel.text = $"Per second: {coinsPerSecond:0.0}  |  Work: +{clickReward:0}";
            }

            if (workButtonLabel != null)
            {
                workButtonLabel.text = $"Work (+{clickReward:0})";
            }

            if (upgradeButtonLabel != null)
            {
                upgradeButtonLabel.text = $"Upgrade Work\nCost: {nextUpgradeCost:0}";
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = currency >= nextUpgradeCost;
            }
        }

        private void NotifyWorkRequested()
        {
            WorkRequested?.Invoke();
        }

        private void NotifyUpgradeRequested()
        {
            UpgradeRequested?.Invoke();
        }
    }
}
