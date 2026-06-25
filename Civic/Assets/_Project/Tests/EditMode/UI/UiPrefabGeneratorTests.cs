using Civic.Editor.UI;
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

            var errors = UiPrefabValidator.CollectErrors(TestGenerated, TestEditable);

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/CivicHud.prefab");
            var view = hud.GetComponent<CivicHudView>();
            var controller = hud.GetComponent<CivicHudController>();
            Assert.That(view.HasRequiredReferences, Is.True);
            Assert.That(controller.HasRequiredReferences, Is.True);
            Assert.That(view.ResourceDetailRows.Count, Is.EqualTo(20));
            Assert.That(view.ResourceSummaryLabels.Count, Is.EqualTo(20));
            Assert.That(view.ResourceProducerBoxes.Count, Is.EqualTo(80));
            Assert.That(view.ResourceProducerLabels.Count, Is.EqualTo(80));
            Assert.That(view.ResourceProducerTooltips.Count, Is.EqualTo(80));
            Assert.That(view.BuildingActionRows.Count, Is.EqualTo(19));
            Assert.That(view.BuildingActionInfoLabels.Count, Is.EqualTo(19));
            Assert.That(view.BuildingCountLabels.Count, Is.EqualTo(19));
            Assert.That(view.BuildingCostLabels.Count, Is.EqualTo(19));
            Assert.That(view.BuildingInputOutputLabels.Count, Is.EqualTo(19));
            Assert.That(view.BuildingInputOutputTooltips.Count, Is.EqualTo(19));
            Assert.That(view.BuildingGdpDeltaLabels.Count, Is.EqualTo(19));
            Assert.That(view.BuildingActionButtons.Count, Is.EqualTo(19));
            Assert.That(view.BuildingButtonTooltips.Count, Is.EqualTo(19));
            Assert.That(view.EraTabRows.Count, Is.EqualTo(6));
            Assert.That(view.EraTabLabels.Count, Is.EqualTo(6));
            Assert.That(view.EraTabButtons.Count, Is.EqualTo(6));
            Assert.That(view.TechnologyActionRows.Count, Is.EqualTo(11));
            Assert.That(view.TechnologyActionInfoLabels.Count, Is.EqualTo(11));
            Assert.That(view.TechnologyActionButtons.Count, Is.EqualTo(11));
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
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow06"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceDetailRow20"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "ResourceProducerBox01"), Is.EqualTo(20));
            Assert.That(CountChildrenNamed(hudBase, "BuildingHeaderRow"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingActionRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingActionRow19"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingNameLabel"), Is.EqualTo(19));
            Assert.That(CountChildrenNamed(hudBase, "BuildingInputOutputCell"), Is.EqualTo(19));
            Assert.That(CountChildrenNamed(hudBase, "BuildingBuildButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "BuildingBuildButton19"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabRow06"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "EraTabButton06"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyActionRow01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyActionRow11"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyInfoLabel"), Is.EqualTo(11));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyResearchButton01"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "TechnologyResearchButton11"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "Tooltip"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(hudBase, "FoodToggleButton"), Is.EqualTo(1));
            Assert.That(UiPrefabValidator.CollectErrors(TestGenerated, TestEditable), Is.Empty);
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
