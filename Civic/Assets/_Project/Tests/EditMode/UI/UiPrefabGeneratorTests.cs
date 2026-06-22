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
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/IdlePanel.prefab");
            var view = panel.GetComponent<IdlePanelView>();
            Assert.That(view.HasRequiredReferences, Is.True);
            Assert.That(view.UpgradeButton, Is.Not.Null);
            Assert.That(
                PrefabUtility.GetPrefabAssetType(panel),
                Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(
                PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(TestEditable + "/UIRoot.prefab")),
                Is.EqualTo(PrefabAssetType.Variant));
        }

        [Test]
        public void RegenerateBase_PreservesUserVariantOverride()
        {
            UiPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);
            var variantPath = TestEditable + "/IdlePanel.prefab";
            var userColor = new Color(0.62f, 0.18f, 0.42f, 1f);

            var contents = PrefabUtility.LoadPrefabContents(variantPath);
            contents.GetComponent<Image>().color = userColor;
            PrefabUtility.SaveAsPrefabAsset(contents, variantPath);
            PrefabUtility.UnloadPrefabContents(contents);

            UiPrefabGenerator.GenerateAssetsAt(TestGenerated, TestEditable);

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(variant.GetComponent<Image>().color, Is.EqualTo(userColor));

            var panelBase = AssetDatabase.LoadAssetAtPath<GameObject>(TestGenerated + "/IdlePanel_Base.prefab");
            Assert.That(CountChildrenNamed(panelBase, "WorkButton"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(panelBase, "UpgradeButton"), Is.EqualTo(1));
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
