using System.Linq;
using System.Collections;
using Civic.Simulation;
using Civic.Simulation.Modules;
using Civic.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Civic.UI.Tests
{
    public sealed class CivicHudPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            CivicMetaSession.ResetForTests();
            CivicFeatureRuntime.ResetForMainMenu();
        }

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
            yield return new WaitForSeconds(1.1f);
            Assert.That(controller.Simulation.Snapshot.Science.ToDouble(), Is.GreaterThan(initialScience));
            controller.View.ResourcesPanelButton.onClick.Invoke();
            yield return null;
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

            controller.Simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);
            var researchedBefore = controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched);
            controller.View.TechnologiesPanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.View.EraTabRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.EraTabLabels.Any(label => label.gameObject.activeInHierarchy && label.text.Contains("원시")), Is.True);
            Assert.That(controller.View.TechnologyActionRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.TechnologyActionInfoLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            var researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));
            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));

            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(
                controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched),
                Is.GreaterThan(researchedBefore));
            Assert.That(controller.View.EraTabLabels.Any(label => label.gameObject.activeInHierarchy && label.text.Contains("고대")), Is.True);

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));

            var nextEraTabButton = controller.View.EraTabButtons.First(button => button.gameObject.activeSelf && button.interactable);
            nextEraTabButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("ancient"));
            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("ancient"));

            var previousEraTabButton = controller.View.EraTabButtons.First(button => button.gameObject.activeSelf && button.interactable);
            previousEraTabButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));
            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));

            Assert.DoesNotThrow(() => controller.View.FoodToggleButton.onClick.Invoke());
            Assert.That(controller.Simulation.Snapshot.Resources.Any(resource => resource.Category == ResourceCategory.Aggregate), Is.True);
        }

        [UnityTest]
        public IEnumerator CivicHud_AllModulesUseSeparateDomainPanels()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var modulePanel = controller.ModulePanelView;
            Assert.That(modulePanel.HasRequiredReferences, Is.True);
            Assert.That(modulePanel.DomainPanels.Count, Is.EqualTo(8));
            Assert.That(modulePanel.DomainPanels.Select(item => item.FeatureId), Is.EquivalentTo(CivicFeatureRegistry.Features.Select(item => item.Id)));
            Assert.That(controller.OverlayView.HasRequiredReferences, Is.True);
            controller.ToggleDebugPanel();
            yield return null;
            Assert.That(controller.OverlayView.IsDebugPanelOpen, Is.True);
            controller.ToggleDebugPanel();
            yield return null;
            Assert.That(controller.OverlayView.IsDebugPanelOpen, Is.False);

            controller.View.ResourcesPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Resources));
            Assert.That(controller.View.DetailPanelRoot.activeSelf, Is.True);
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);

            modulePanel.OpenButton.onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.True);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));
            Assert.That(controller.View.DetailPanelRoot.activeSelf, Is.False);
            Assert.That(modulePanel.DomainPanels.Count(item => item.PanelRoot.activeSelf), Is.EqualTo(1));
            Assert.That(modulePanel.DomainPanels.Single(item => item.PanelRoot.activeSelf).Rows.Any(row => row.gameObject.activeSelf), Is.True);

            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Buildings));
            controller.View.DetailCloseButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));

            modulePanel.OpenButton.onClick.Invoke();
            yield return null;
            controller.ProcessEscape();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.False);
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.True);
            controller.OverlayView.ContinueButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator CivicHud_DebugQuickCommandsAndInstantToggleWorkInIsolation()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller, Is.Not.Null);
            controller.ToggleDebugPanel();
            yield return null;

            var wheatBeforeDebugGrant = controller.Simulation.State.Resources["wheat"];
            var prestigeBeforeDebugGrant = controller.ModuleRuntime.MetaProgress.PrestigePoints;
            controller.OverlayView.DebugGrantResourcesButton.onClick.Invoke();
            controller.OverlayView.DebugResearchAllButton.onClick.Invoke();
            controller.OverlayView.DebugGrantPrestigeButton.onClick.Invoke();
            controller.OverlayView.DebugInstantActionsToggle.isOn = true;
            yield return null;

            Assert.That(controller.Simulation.State.Resources["wheat"], Is.EqualTo(wheatBeforeDebugGrant + CivicNumber.FromDouble(9999d)));
            Assert.That(controller.Simulation.State.ResearchedTechnologyIds.Count, Is.EqualTo(controller.Simulation.Data.Technologies.Count));
            Assert.That(controller.ModuleRuntime.MetaProgress.PrestigePoints, Is.EqualTo(prestigeBeforeDebugGrant + 9999));
            Assert.That(controller.ModuleRuntime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics).DebugInstantActionsEnabled, Is.True);
            Assert.That(controller.ModuleRuntime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders).DebugInstantActionsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator CivicHud_ModuleCardsPoliticsTabsAndImpossibleNationFilterWork()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var modulePanel = controller.ModulePanelView;
            modulePanel.OpenPanel();

            var civilizationIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.StartCivilizations);
            modulePanel.TabButtons[civilizationIndex].onClick.Invoke();
            yield return null;
            var civilizationPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.StartCivilizations);
            var civilizationRows = civilizationPanel.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(civilizationRows, Has.Length.EqualTo(1));
            Assert.That(civilizationRows[0].InfoLabel.text, Does.Contain("현재 문명"));
            Assert.That(civilizationRows[0].DescriptionRoot.activeSelf, Is.True);
            Assert.That(civilizationRows[0].DescriptionLabel.text, Is.Not.Empty);

            var politicsIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.Politics);
            modulePanel.TabButtons[politicsIndex].onClick.Invoke();
            yield return null;
            var politicsPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.Politics);
            Assert.That(politicsPanel.CategoryTabRows.Count(row => row.activeSelf), Is.EqualTo(5));
            Assert.That(politicsPanel.CategoryTabLabels.Where(label => label.gameObject.activeInHierarchy).All(label => label.text.Contains("현행:")), Is.True);
            Assert.That(modulePanel.SelectedPoliticsCategory, Is.EqualTo("government"));
            politicsPanel.CategoryTabButtons[1].onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.SelectedPoliticsCategory, Is.EqualTo("franchise"));
            Assert.That(politicsPanel.Rows.Where(row => row.gameObject.activeSelf).All(row => !row.InfoLabel.text.Contains("[government]")), Is.True);

            var nationIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.NationFormation);
            modulePanel.TabButtons[nationIndex].onClick.Invoke();
            yield return null;
            var nationPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.NationFormation);
            var hiddenImpossibleCount = nationPanel.Rows.Count(row => row.gameObject.activeSelf);
            nationPanel.ImpossibleFilterToggle.isOn = true;
            yield return null;
            Assert.That(nationPanel.Rows.Count(row => row.gameObject.activeSelf), Is.GreaterThan(hiddenImpossibleCount));
            Assert.That(nationPanel.Rows.Where(row => row.gameObject.activeSelf).All(row => row.DescriptionRoot.activeSelf), Is.True);

            controller.View.TooltipView.Show("tooltip lifecycle", Vector2.zero);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True);
            modulePanel.ClosePanel();
            yield return null;
            Assert.That(controller.View.TooltipView.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator CivicHud_EventPopupCanCloseReopenAndRecordsChoiceHistory()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var events = controller.ModuleRuntime.GetModule<Civic.Simulation.Modules.CivicEventModule>(CivicFeatureRegistry.Events);
            if (events.Queue.Count == 0) Assert.That(events.DebugQueueEvent(events.Definitions[0].Id), Is.True);
            yield return null;

            Assert.That(controller.OverlayView.EventAlertButton.gameObject.activeSelf, Is.True);
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.True);
            var queueBefore = events.Queue.Count;
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.False);
            Assert.That(events.Queue.Count, Is.EqualTo(queueBefore));
            controller.OverlayView.EventAlertButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.True);

            var historyBefore = events.History.Count;
            var choice = controller.OverlayView.EventChoiceButtons.First(button => button.gameObject.activeSelf && button.interactable);
            choice.onClick.Invoke();
            yield return null;
            Assert.That(events.History.Count, Is.EqualTo(historyBefore + 1));
            Assert.That(events.History.Last().AppliedResults, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CivicHud_WithoutPrestigeCanReturnToMainMenuFromEscapePopup()
        {
            CivicFeatureRuntime.ResetForMainMenu();
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller.ModuleRuntime.GetModule<Civic.Simulation.Modules.CivicPrestigeModule>(CivicFeatureRegistry.Prestige), Is.Null);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.True);
            controller.OverlayView.MainMenuButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }
    }
}
