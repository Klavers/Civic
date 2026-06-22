using System;
using System.Collections.Generic;
using Civic.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Civic.Editor.UI
{
    public static class UiPrefabValidator
    {
        private const string ValidationSummary =
            "Validation passed.\n\n" +
            "Checked:\n" +
            "- Base and Variant prefab existence/type\n" +
            "- Base/Variant inheritance\n" +
            "- Missing scripts and serialized references\n" +
            "- Canvas, input EventSystem, and idle panel components";

        [MenuItem("Tools/Civic/UI/Validate")]
        private static void ValidateFromMenu()
        {
            try
            {
                ValidateAll();
                EditorUtility.DisplayDialog("Civic UI Validation", ValidationSummary, "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic UI Validation Failed", exception.Message, "OK");
                throw;
            }
        }

        public static void ValidateAll()
        {
            var errors = CollectErrors(
                UiPrefabGenerator.GeneratedFolder,
                UiPrefabGenerator.EditableFolder);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Civic UI validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("CIVIC_UI_VALIDATION_OK");
        }

        public static IReadOnlyList<string> CollectErrors(string generatedFolder, string editableFolder)
        {
            var errors = new List<string>();
            var panelBase = LoadPrefab(generatedFolder + "/IdlePanel_Base.prefab", errors);
            var panel = LoadPrefab(editableFolder + "/IdlePanel.prefab", errors);
            var rootBase = LoadPrefab(generatedFolder + "/UIRoot_Base.prefab", errors);
            var root = LoadPrefab(editableFolder + "/UIRoot.prefab", errors);

            ValidatePrefabType(panelBase, PrefabAssetType.Regular, "IdlePanel Base", errors);
            ValidatePrefabType(panel, PrefabAssetType.Variant, "IdlePanel Variant", errors);
            ValidatePrefabType(rootBase, PrefabAssetType.Regular, "UIRoot Base", errors);
            ValidatePrefabType(root, PrefabAssetType.Variant, "UIRoot Variant", errors);

            ValidateVariantSource(panel, panelBase, "IdlePanel", errors);
            ValidateVariantSource(root, rootBase, "UIRoot", errors);

            if (panel != null)
            {
                ValidatePanel(panel, errors);
                ValidateMissingScripts(panel, "IdlePanel", errors);
            }

            if (root != null)
            {
                ValidateRoot(root, errors);
                ValidateMissingScripts(root, "UIRoot", errors);
            }

            return errors;
        }

        private static GameObject LoadPrefab(string path, ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"Missing prefab: {path}");
            }

            return prefab;
        }

        private static void ValidatePrefabType(GameObject prefab, PrefabAssetType expected, string label, ICollection<string> errors)
        {
            if (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) != expected)
            {
                errors.Add($"{label} must be {expected}, but was {PrefabUtility.GetPrefabAssetType(prefab)}.");
            }
        }

        private static void ValidateVariantSource(GameObject variant, GameObject expectedBase, string label, ICollection<string> errors)
        {
            if (variant == null || expectedBase == null)
            {
                return;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
            if (source != expectedBase)
            {
                errors.Add($"{label} Variant does not inherit from its generated Base prefab.");
            }
        }

        private static void ValidatePanel(GameObject panel, ICollection<string> errors)
        {
            var view = panel.GetComponent<IdlePanelView>();
            var controller = panel.GetComponent<DummyIdleGameController>();
            if (view == null || !view.HasRequiredReferences)
            {
                errors.Add("IdlePanelView is missing or has unassigned serialized references.");
            }

            if (controller == null || controller.View != view)
            {
                errors.Add("DummyIdleGameController is missing or is not bound to IdlePanelView.");
            }

            if (panel.GetComponent<Image>() == null)
            {
                errors.Add("IdlePanel root must have a background Image.");
            }
        }

        private static void ValidateRoot(GameObject root, ICollection<string> errors)
        {
            if (root.GetComponent<UiRootMarker>() == null)
            {
                errors.Add("UIRoot is missing UiRootMarker.");
            }

            if (root.GetComponentInChildren<Canvas>(true) == null ||
                root.GetComponentInChildren<CanvasScaler>(true) == null ||
                root.GetComponentInChildren<GraphicRaycaster>(true) == null)
            {
                errors.Add("UIRoot Canvas is missing Canvas, CanvasScaler, or GraphicRaycaster.");
            }

            if (root.GetComponentInChildren<EventSystem>(true) == null ||
                root.GetComponentInChildren<InputSystemUIInputModule>(true) == null)
            {
                errors.Add("UIRoot EventSystem is missing InputSystemUIInputModule.");
            }

            if (root.GetComponentInChildren<IdlePanelView>(true) == null)
            {
                errors.Add("UIRoot does not contain the editable IdlePanel prefab.");
            }
        }

        private static void ValidateMissingScripts(GameObject root, string label, ICollection<string> errors)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingCount > 0)
                {
                    errors.Add($"{label}/{transform.name} has {missingCount} missing script reference(s).");
                }
            }
        }
    }
}
