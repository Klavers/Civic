using System.Linq;
using System.Collections;
using Civic.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Civic.UI.Tests
{
    public sealed class CivicHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator CivicHud_AdvancesSimulationAndProcessesPrimaryButtons()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.View, Is.Not.Null);
            Assert.That(controller.Simulation, Is.Not.Null);

            Canvas.ForceUpdateCanvases();
            var canvas = controller.GetComponentInParent<Canvas>();
            var hudRect = controller.GetComponent<RectTransform>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            Assert.That(hudRect.rect.width, Is.GreaterThan(0f));
            Assert.That(hudRect.rect.height, Is.GreaterThan(0f));
            Assert.That(hudRect.lossyScale.sqrMagnitude, Is.GreaterThan(0.01f));

            var initialScience = controller.Simulation.Snapshot.Science.ToDouble();
            yield return new WaitForSeconds(0.25f);
            Assert.That(controller.Simulation.Snapshot.Science.ToDouble(), Is.GreaterThan(initialScience));
            Assert.That(controller.View.ResourceDetailRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.ResourceSummaryLabels.Any(label => label.text.Contains("소비") && label.text.Contains("순증감")), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.text.Contains("수도 인구 총량")), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.text.Contains("건설 액션")), Is.True);
            Assert.That(controller.View.ResourceProducerTooltips.All(trigger => trigger.HasTooltipView), Is.True);

            var buildingsBeforeBuild = controller.Simulation.Snapshot.Buildings.Sum(building => building.Count);
            var constructionBeforeBuild = controller.Simulation.Snapshot.ConstructionPower.ToDouble();
            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.View.BuildingActionRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.BuildingActionInfoLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingInputOutputLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingGdpDeltaLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingButtonTooltips.All(trigger => trigger.HasTooltipView), Is.True);
            var buildButton = controller.View.BuildingActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            buildButton.onClick.Invoke();
            yield return null;

            Assert.That(
                controller.Simulation.Snapshot.Buildings.Sum(building => building.Count),
                Is.GreaterThan(buildingsBeforeBuild));
            Assert.That(
                controller.Simulation.Snapshot.ConstructionPower.ToDouble(),
                Is.LessThan(constructionBeforeBuild));

            var elapsed = 0f;
            while (!controller.Simulation.Snapshot.HasResearchAvailable && elapsed < 2f)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.That(controller.Simulation.Snapshot.HasResearchAvailable, Is.True);

            var researchedBefore = controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched);
            controller.View.TechnologiesPanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.View.TechnologyActionRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.TechnologyActionInfoLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            var researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(
                controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched),
                Is.GreaterThan(researchedBefore));

            Assert.DoesNotThrow(() => controller.View.FoodToggleButton.onClick.Invoke());
            Assert.That(controller.Simulation.Snapshot.Resources.Any(resource => resource.Category == ResourceCategory.Aggregate), Is.True);
        }
    }
}
