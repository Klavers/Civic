using System.Collections;
using System.Linq;
using Civic.Features;
using Civic.Simulation.Modules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Civic.UI.Tests
{
    public sealed class MainMenuFeatureSelectionPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            CivicFeatureRuntime.ResetForMainMenu();
            CivicMetaSession.ResetForTests();
        }

        [UnityTest]
        public IEnumerator MainMenu_SelectsModulesAndLocksSelectionForSampleScene()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicMainMenuController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.HasRequiredReferences, Is.True);
            Assert.That(controller.MainPanel.activeSelf, Is.True);

            controller.OpenFeaturePanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.FeaturePanel.activeSelf, Is.True);

            controller.FeatureRows[0].Toggle.isOn = true;
            controller.FeatureRows[1].Toggle.isOn = true;
            yield return null;
            Assert.That(controller.StartGameButton.interactable, Is.True);
            controller.StartGameButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("SampleScene"));
            Assert.That(CivicFeatureRuntime.IsRunLocked, Is.True);
            Assert.That(CivicFeatureRuntime.Current.IsEnabled(CivicFeatureRegistry.Prestige), Is.True);
            Assert.That(CivicFeatureRuntime.Current.IsEnabled(CivicFeatureRegistry.Achievements), Is.True);
            Assert.That(CivicFeatureRuntime.Current.IsIntegrationEnabled("integration.achievementPrestige"), Is.True);
            var hud = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.ModuleRuntime.GetModule<CivicPrestigeModule>(CivicFeatureRegistry.Prestige), Is.Not.Null);
            Assert.That(hud.ModuleRuntime.GetModule<CivicAchievementModule>(CivicFeatureRegistry.Achievements), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MainMenu_StartCivilizationSelectionIsVisibleOnlyWhenModuleEnabled()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CivicMainMenuController>();
            controller.OpenFeaturePanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.CivilizationSection.activeSelf, Is.False);
            var row = controller.FeatureRows.Single(item => item.FeatureId == CivicFeatureRegistry.StartCivilizations);
            row.Toggle.isOn = true;
            yield return null;
            Assert.That(controller.CivilizationSection.activeSelf, Is.True);
            var before = controller.SelectedCivilizationId;
            controller.NextCivilizationButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.SelectedCivilizationId, Is.Not.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator AllOn_HudModulePanelOpensAndBindsStoredRows()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var panel = controller.ModulePanelView;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.HasRequiredReferences, Is.True);
            Assert.That(panel.OpenButton.gameObject.activeSelf, Is.True);
            panel.OpenButton.onClick.Invoke();
            yield return null;

            Assert.That(panel.PanelRoot.activeSelf, Is.True);
            Assert.That(panel.Rows.Any(row => row.gameObject.activeSelf), Is.True);
            Assert.That(panel.Rows.Any(row => row.gameObject.activeSelf && !string.IsNullOrEmpty(row.InfoLabel.text)), Is.True);
        }

        [UnityTest]
        public IEnumerator MainMenu_OptionsDeletesMetaSaveAfterTwoStepConfirmation()
        {
            CivicMetaSession.ResetForTests(new CivicMetaProgress { PrestigePoints = 77, PrestigeCount = 3 });
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicMainMenuController>();
            controller.OpenOptionsPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.OptionsPanel.activeSelf, Is.True);
            controller.DeleteSaveDataButton.onClick.Invoke();
            Assert.That(CivicMetaSession.Store.Load().PrestigePoints, Is.EqualTo(77));
            controller.DeleteSaveDataButton.onClick.Invoke();
            Assert.That(CivicMetaSession.Store.Load().PrestigePoints, Is.Zero);
            Assert.That(CivicMetaSession.Store.Load().PrestigeCount, Is.Zero);
        }
    }
}
