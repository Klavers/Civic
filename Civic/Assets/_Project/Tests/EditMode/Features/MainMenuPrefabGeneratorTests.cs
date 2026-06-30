using System.Linq;
using Civic.Editor.UI;
using Civic.Features;
using Civic.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI.Tests
{
    public sealed class MainMenuPrefabGeneratorTests
    {
        private const string TestRoot = "Assets/_Project/Tests/__MainMenuGeneratorTemp";
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
        public void GenerateAssetsAt_CreatesValidBaseAndEditableVariant()
        {
            MainMenuPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);
            var errors = MainMenuPrefabValidator.CollectErrors(TestGenerated, TestEditable);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/CivicMainMenu.prefab");
            var controller = variant.GetComponent<CivicMainMenuController>();

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(controller.HasRequiredReferences, Is.True);
            Assert.That(controller.FeatureRows.Count, Is.EqualTo(CivicFeatureRegistry.Features.Count));
            Assert.That(controller.FeatureRows.All(row => row.HasRequiredReferences), Is.True);
            Assert.That(controller.CivilizationSection, Is.Not.Null);
            Assert.That(controller.OpenOptionsPanelButton, Is.Not.Null);
            Assert.That(controller.OptionsPanel, Is.Not.Null);
            Assert.That(controller.DeleteSaveDataButton, Is.Not.Null);
            Assert.That(variant.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
        }

        [Test]
        public void RegenerateBase_PreservesVariantOverrideAndDoesNotDuplicateRows()
        {
            MainMenuPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);
            var variantPath = TestEditable + "/CivicMainMenu.prefab";
            var userColor = new Color(0.34f, 0.12f, 0.46f, 1f);
            var contents = PrefabUtility.LoadPrefabContents(variantPath);
            contents.GetComponent<Image>().color = userColor;
            PrefabUtility.SaveAsPrefabAsset(contents, variantPath);
            PrefabUtility.UnloadPrefabContents(contents);

            MainMenuPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestGenerated + "/CivicMainMenu_Base.prefab");
            Assert.That(variant.GetComponent<Image>().color, Is.EqualTo(userColor));
            Assert.That(
                basePrefab.GetComponentsInChildren<CivicFeatureToggleRow>(true).Length,
                Is.EqualTo(CivicFeatureRegistry.Features.Count));
            Assert.That(MainMenuPrefabValidator.CollectErrors(TestGenerated, TestEditable), Is.Empty);
        }
    }
}
