using System.Linq;
using Civic.Editor.UI;
using Civic.Features;
using Civic.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI.Tests
{
    public sealed class UiPrefabGeneratorTests
    {
        private const string TestRoot = "Assets/_Project/Tests/__UiPrefabGeneratorTemp";
        private const string TestGenerated = TestRoot + "/Generated";
        private const string TestEditable = TestRoot + "/Editable";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        [Test]
        public void GenerateAssetsAt_CreatesValidBaseAndVariants()
        {
            UiPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);
            var expectedBuildingRowCount = ExpectedBuildingRowCount();

            var errors = UiPrefabValidator.CollectErrors(TestGenerated, TestEditable);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/CivicHud.prefab");
            var view = hud.GetComponent<CivicHudView>();
            var controller = hud.GetComponent<CivicHudController>();
            var modulePanel = hud.GetComponent<CivicModulePanelView>();
            var overlay = hud.GetComponent<CivicHudOverlayView>();
            Assert.That(view.HasRequiredReferences, Is.True);
            Assert.That(controller.HasRequiredReferences, Is.True);
            Assert.That(modulePanel.HasRequiredReferences, Is.True);
            Assert.That(overlay.HasRequiredReferences, Is.True);
            Assert.That(modulePanel.DomainPanels.Count, Is.EqualTo(CivicFeatureRegistry.Features.Count));
            Assert.That(modulePanel.Rows.Count, Is.EqualTo(202));
            Assert.That(modulePanel.Rows.All(row => row.DescriptionRoot != null && row.DescriptionLabel != null), Is.True);
            Assert.That(modulePanel.DomainPanels.Select(item => item.FeatureId), Is.EquivalentTo(CivicFeatureRegistry.Features.Select(item => item.Id)));
            var politicsPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.Politics);
            Assert.That(politicsPanel.CategoryTabRows.Count, Is.EqualTo(8));
            Assert.That(politicsPanel.CategoryTabButtons.Count, Is.EqualTo(8));
            Assert.That(politicsPanel.CategoryTabLabels.Count, Is.EqualTo(8));
            var nationPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.NationFormation);
            Assert.That(nationPanel.ImpossibleFilterRoot, Is.Not.Null);
            Assert.That(nationPanel.ImpossibleFilterToggle, Is.Not.Null);
            var peoplePanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.GreatPeople);
            Assert.That(peoplePanel.Rows.Count, Is.EqualTo(24));
            Assert.That(peoplePanel.Rows.All(row => row.ChoiceButtons.Count == 4), Is.True);
            Assert.That(overlay.DebugGrantResourcesButton, Is.Not.Null);
            Assert.That(overlay.DebugResearchAllButton, Is.Not.Null);
            Assert.That(overlay.DebugGrantPrestigeButton, Is.Not.Null);
            Assert.That(overlay.DebugInstantActionsToggle, Is.Not.Null);
            Assert.That(overlay.DebugDescriptionLabel, Is.Not.Null);
            Assert.That(view.ResourceDetailRows.Count, Is.EqualTo(30));
            Assert.That(view.ResourceSummaryLabels.Count, Is.EqualTo(30));
            Assert.That(view.ResourceProducerBoxes.Count, Is.EqualTo(120));
            Assert.That(view.ResourceProducerLabels.Count, Is.EqualTo(120));
            Assert.That(view.ResourceProducerTooltips.Count, Is.EqualTo(120));
            Assert.That(view.BuildingActionRows.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingActionInfoLabels.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingCountLabels.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingCostLabels.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingInputOutputLabels.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingInputOutputTooltips.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingGdpDeltaLabels.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingActionButtons.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.BuildingButtonTooltips.Count, Is.EqualTo(expectedBuildingRowCount));
            Assert.That(view.EraTabRows.Count, Is.EqualTo(10));
            Assert.That(view.EraTabLabels.Count, Is.EqualTo(10));
            Assert.That(view.EraTabButtons.Count, Is.EqualTo(10));
            Assert.That(view.TechnologyActionRows.Count, Is.EqualTo(12));
            Assert.That(view.TechnologyActionInfoLabels.Count, Is.EqualTo(12));
            Assert.That(view.TechnologyActionButtons.Count, Is.EqualTo(12));
            Assert.That(view.ResourceDetailRows, Has.All.Not.Null);
            Assert.That(view.ResourceSummaryLabels, Has.All.Not.Null);
            Assert.That(view.ResourceProducerBoxes, Has.All.Not.Null);
            Assert.That(view.ResourceProducerLabels, Has.All.Not.Null);
            Assert.That(view.ResourceProducerTooltips, Has.All.Not.Null);
            Assert.That(view.BuildingActionRows, Has.All.Not.Null);
            Assert.That(view.BuildingActionInfoLabels, Has.All.Not.Null);
            Assert.That(view.BuildingCountLabels, Has.All.Not.Null);
            Assert.That(view.BuildingCostLabels, Has.All.Not.Null);
            Assert.That(view.BuildingInputOutputLabels, Has.All.Not.Null);
            Assert.That(view.BuildingInputOutputTooltips, Has.All.Not.Null);
            Assert.That(view.BuildingGdpDeltaLabels, Has.All.Not.Null);
            Assert.That(view.BuildingActionButtons, Has.All.Not.Null);
            Assert.That(view.BuildingButtonTooltips, Has.All.Not.Null);
            Assert.That(view.EraTabRows, Has.All.Not.Null);
            Assert.That(view.EraTabLabels, Has.All.Not.Null);
            Assert.That(view.EraTabButtons, Has.All.Not.Null);
            Assert.That(view.TechnologyActionRows, Has.All.Not.Null);
            Assert.That(view.TechnologyActionInfoLabels, Has.All.Not.Null);
            Assert.That(view.TechnologyActionButtons, Has.All.Not.Null);
            Assert.That(view.FoodToggleButton, Is.Not.Null);
            Assert.That(view.DetailPanelRoot, Is.Not.Null);
            Assert.That(view.DetailCloseButton, Is.Not.Null);
            Assert.That(view.TooltipView, Is.Not.Null);
            Assert.That(view.TooltipView.HasRequiredReferences, Is.True);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(hud),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(
                PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/UIRoot.prefab")),
                Is.EqualTo(PrefabAssetType.Variant));
        }

        [Test]
        public void RegenerateBase_PreservesUserVariantOverride()
        {
            UiPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);
            var expectedBuildingRowCount = ExpectedBuildingRowCount();
            var variantPath = TestEditable + "/CivicHud.prefab";
            var userColor = new Color(0.62f, 0.18f, 0.42f, 1f);

            var contents = PrefabUtility.LoadPrefabContents(variantPath);
            contents.GetComponent<Image>().color = userColor;
            PrefabUtility.SaveAsPrefabAsset(contents, variantPath);
            PrefabUtility.UnloadPrefabContents(contents);

            UiPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(variant.GetComponent<Image>().color, Is.EqualTo(userColor));

            var hudBase = AssetDatabase.LoadAssetAtPath<GameObject>(TestGenerated + "/CivicHud_Base.prefab");
            Assert.That(CountChildrenNamed(hudBase, "ResourcesPanelButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingsPanelButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologiesPanelButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ModulesPanelButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ModulePanel"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "DomainPanel01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "DomainPanel08"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "DomainActionRow01"), Is.EqualTo(8));
            Assert.That(CountChildrenNamed(hudBase, "DomainActionRow18"), Is.EqualTo(8));
            Assert.That(CountChildrenNamed(hudBase, "DomainActionRow24"), Is.EqualTo(3));
            Assert.That(CountChildrenNamed(hudBase, "DomainActionRow64"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "CategoryTabArea"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "CategoryTab01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "CategoryTab08"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ImpossibleFilter"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "Description"), Is.EqualTo(202));
            Assert.That(CountChildrenNamed(hudBase, "ModuleActionRow01"), Is.Zero);
            Assert.That(CountChildrenNamed(hudBase, "DetailCloseButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ExitPopup"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EventPopup"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "DebugPanel"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "GrantResourcesButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResearchAllButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "GrantPrestigeButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "InstantActionsRow"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "DescriptionBox"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "PositionButton01"), Is.EqualTo(24));
            Assert.That(CountChildrenNamed(hudBase, "PositionButton04"), Is.EqualTo(24));
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow06"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow30"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceProducerBox01"), Is.EqualTo(30));
            Assert.That(CountChildrenNamed(hudBase, "BuildingHeaderRow"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingActionRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, $"BuildingActionRow{expectedBuildingRowCount:00}"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingNameLabel"), Is.EqualTo(expectedBuildingRowCount));
            Assert.That(CountChildrenNamed(hudBase, "BuildingInputOutputCell"), Is.EqualTo(expectedBuildingRowCount));
            Assert.That(CountChildrenNamed(hudBase, "BuildingBuildButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, $"BuildingBuildButton{expectedBuildingRowCount:00}"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabRow10"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabButton10"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyActionRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyActionRow12"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyInfoLabel"), Is.EqualTo(12));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyResearchButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyResearchButton12"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "Tooltip"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "FoodToggleButton"), Is.EqualTo(1));
            Assert.That(UiPrefabValidator.CollectErrors(TestGenerated, TestEditable), Is.Empty);
        }

        private static int ExpectedBuildingRowCount()
        {
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            Assert.That(dataSource, Is.Not.Null);
            return Mathf.Max(1, dataSource.LoadGameData().Buildings.Count(building => building.IsBuildable));
        }

        private static int CountChildrenNamed(GameObject root, string objectName)
        {
            var count = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
