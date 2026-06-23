using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Civic.UI.Tests
{
    public sealed class DummyIdleGamePlayModeTests
    {
        [UnityTest]
        public IEnumerator IdlePanel_IncreasesCurrencyAndProcessesWorkButton()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;

            var controller = Object.FindFirstObjectByType<DummyIdleGameController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.View, Is.Not.Null);

            Canvas.ForceUpdateCanvases();
            var canvas = controller.GetComponentInParent<Canvas>();
            var panelRect = controller.GetComponent<RectTransform>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            Assert.That(panelRect.rect.width, Is.GreaterThan(0f));
            Assert.That(panelRect.rect.height, Is.GreaterThan(0f));
            Assert.That(panelRect.lossyScale.sqrMagnitude, Is.GreaterThan(0.01f));

            var initialCurrency = controller.Currency;
            yield return new WaitForSeconds(0.1f);
            Assert.That(controller.Currency, Is.GreaterThan(initialCurrency));

            var beforeClick = controller.Currency;
            controller.View.WorkButton.onClick.Invoke();
            Assert.That(controller.Currency, Is.GreaterThanOrEqualTo(beforeClick + 5d));

            var originalReward = controller.ClickReward;
            while (controller.Currency < controller.NextUpgradeCost)
            {
                controller.View.WorkButton.onClick.Invoke();
            }

            Assert.That(controller.View.UpgradeButton.interactable, Is.True);
            var upgradeCost = controller.NextUpgradeCost;
            var beforeUpgrade = controller.Currency;
            controller.View.UpgradeButton.onClick.Invoke();

            Assert.That(controller.Currency, Is.EqualTo(beforeUpgrade - upgradeCost).Within(0.001d));
            Assert.That(controller.ClickReward, Is.GreaterThan(originalReward));
            Assert.That(controller.NextUpgradeCost, Is.GreaterThan(upgradeCost));

            var beforeUpgradedClick = controller.Currency;
            controller.View.WorkButton.onClick.Invoke();
            Assert.That(controller.Currency, Is.EqualTo(beforeUpgradedClick + controller.ClickReward).Within(0.001d));
        }
    }
}
