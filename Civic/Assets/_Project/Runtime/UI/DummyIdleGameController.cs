using System;
using UnityEngine;

namespace Civic.UI
{
    public sealed class DummyIdleGameController : MonoBehaviour
    {
        [SerializeField] private IdlePanelView view;
        [SerializeField] private double startingCurrency;
        [SerializeField] private double coinsPerSecond = 1d;
        [SerializeField] private double clickReward = 5d;
        [SerializeField] private double upgradeBaseCost = 20d;
        [SerializeField] private double upgradeCostGrowth = 1.75d;
        [SerializeField] private double clickRewardIncrement = 5d;

        private double currency;
        private double nextUpgradeCost;

        public double Currency => currency;
        public double CoinsPerSecond => coinsPerSecond;
        public double ClickReward => clickReward;
        public double NextUpgradeCost => nextUpgradeCost;
        public IdlePanelView View => view;

        private void Awake()
        {
            currency = startingCurrency;
            nextUpgradeCost = Math.Max(0d, upgradeBaseCost);
        }

        private void OnEnable()
        {
            if (view != null)
            {
                view.WorkRequested += ApplyClickReward;
                view.UpgradeRequested += PurchaseClickUpgrade;
                Render();
            }
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.WorkRequested -= ApplyClickReward;
                view.UpgradeRequested -= PurchaseClickUpgrade;
            }
        }

        private void Update()
        {
            currency += coinsPerSecond * Time.deltaTime;
            Render();
        }

        private void ApplyClickReward()
        {
            currency += clickReward;
            Render();
        }

        private void PurchaseClickUpgrade()
        {
            if (currency < nextUpgradeCost)
            {
                return;
            }

            currency -= nextUpgradeCost;
            clickReward += Math.Max(0d, clickRewardIncrement);
            nextUpgradeCost = Math.Ceiling(nextUpgradeCost * Math.Max(1d, upgradeCostGrowth));
            Render();
        }

        private void Render()
        {
            view?.Render(currency, coinsPerSecond, clickReward, nextUpgradeCost);
        }
    }
}
