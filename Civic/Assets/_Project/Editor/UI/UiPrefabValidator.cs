using System;
using System.Collections.Generic;
using System.Linq;
using Civic.UI;
using TMPro;
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
            "- Canvas, input EventSystem, and Civic HUD components";

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
            var hudBase = LoadPrefab(generatedFolder + "/CivicHud_Base.prefab", errors);
            var hud = LoadPrefab(editableFolder + "/CivicHud.prefab", errors);
            var rootBase = LoadPrefab(generatedFolder + "/UIRoot_Base.prefab", errors);
            var root = LoadPrefab(editableFolder + "/UIRoot.prefab", errors);

            ValidatePrefabType(hudBase, PrefabAssetType.Regular, "CivicHud Base", errors);
            ValidatePrefabType(hud, PrefabAssetType.Variant, "CivicHud Variant", errors);
            ValidatePrefabType(rootBase, PrefabAssetType.Regular, "UIRoot Base", errors);
            ValidatePrefabType(root, PrefabAssetType.Variant, "UIRoot Variant", errors);

            ValidateVariantSource(hud, hudBase, "CivicHud", errors);
            ValidateVariantSource(root, rootBase, "UIRoot", errors);

            if (hud != null)
            {
                ValidateHud(hud, errors);
                ValidateMissingScripts(hud, "CivicHud", errors);
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

        private static void ValidateHud(GameObject hud, ICollection<string> errors)
        {
            var view = hud.GetComponent<CivicHudView>();
            var controller = hud.GetComponent<CivicHudController>();
            var modulePanel = hud.GetComponent<CivicModulePanelView>();
            var overlay = hud.GetComponent<CivicHudOverlayView>();
            if (view == null || !view.HasRequiredReferences)
            {
                errors.Add("CivicHudView is missing or has unassigned serialized references.");
            }

            if (controller == null || !controller.HasRequiredReferences || controller.View != view)
            {
                errors.Add("CivicHudController is missing, lacks required references, or is not bound to CivicHudView.");
            }

            if (modulePanel == null || !modulePanel.HasRequiredReferences || controller == null || controller.ModulePanelView != modulePanel)
            {
                errors.Add("CivicHud module panel is missing or has unassigned serialized references.");
            }

            if (overlay == null || !overlay.HasRequiredReferences || controller == null || controller.OverlayView != overlay)
            {
                errors.Add("CivicHud overlay is missing or has unassigned serialized references.");
            }

            if (controller != null && controller.DataSource == null)
            {
                errors.Add("CivicHudController is missing CivicGameDataSource.");
            }

            if (view != null && (view.BuildingActionRows.Count == 0 || view.EraTabRows.Count == 0 || view.TechnologyActionRows.Count == 0))
            {
                errors.Add("CivicHudView must have building action row, era tab, and technology action row slots.");
            }

            if (view != null && view.BuildingQuantityButtons.Count != 5)
            {
                errors.Add("CivicHudView must contain 1/5/10/25/Max building quantity buttons.");
            }

            if (view?.TooltipView != null)
            {
                var uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiPrefabGenerator.UiFontAssetPath);
                var tooltipCanvas = view.TooltipView.GetComponent<Canvas>();
                if (tooltipCanvas == null || !tooltipCanvas.overrideSorting || tooltipCanvas.sortingOrder != 200 ||
                    view.TooltipView.Cards.Count != CivicTooltipView.MaximumDepth ||
                    !Mathf.Approximately(view.TooltipView.PinDelaySeconds, 1.5f) ||
                    view.TooltipView.Cards.Any(card => card == null || card.Outline == null))
                {
                    errors.Add("Tooltip must use sorting order 200, a 12-card pool, 1.5-second pin delay, and pin-state outlines.");
                }

                if (uiFont == null ||
                    uiFont.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                    !uiFont.isMultiAtlasTexturesEnabled ||
                    TMP_Settings.defaultFontAsset != uiFont)
                {
                    errors.Add("NanumGothic SDF must be the dynamic multi-atlas TMP default font.");
                }
                else if (view.TooltipView.Cards.Any(card =>
                    card == null || card.BodyLabel == null || card.FooterLabel == null ||
                    card.BodyLabel.font != uiFont || card.FooterLabel.font != uiFont))
                {
                    errors.Add("Every Tooltip card must explicitly use NanumGothic SDF.");
                }
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(UiPrefabGenerator.UiFontSourcePath);
            if (sourceFont == null || !sourceFont.HasCharacter('\uCD9C'))
            {
                errors.Add("Civic UI font source is missing or does not contain the Korean character U+CD9C.");
            }

            if (AssetDatabase.LoadMainAssetAtPath(UiPrefabGenerator.UiFontLicensePath) == null)
            {
                errors.Add("Nanum font OFL license must be included in StreamingAssets/ThirdPartyNotices.");
            }

            if (overlay?.EventPopupRoot != null)
            {
                var eventCanvas = overlay.EventPopupRoot.GetComponent<Canvas>();
                var blocker = overlay.EventPopupRoot.GetComponent<Image>();
                if (eventCanvas == null || !eventCanvas.overrideSorting || eventCanvas.sortingOrder != 100 || blocker == null || !blocker.raycastTarget)
                {
                    errors.Add("Event popup must use sorting order 100 and a full-screen raycast blocker.");
                }
            }

            if (view != null &&
                (view.BuildingActionRows.Count != view.BuildingActionInfoLabels.Count ||
                view.BuildingActionRows.Count != view.BuildingActionButtons.Count ||
                view.EraTabRows.Count != view.EraTabLabels.Count ||
                view.EraTabRows.Count != view.EraTabButtons.Count ||
                view.TechnologyActionRows.Count != view.TechnologyActionInfoLabels.Count ||
                view.TechnologyActionRows.Count != view.TechnologyActionButtons.Count))
            {
                errors.Add("CivicHudView action row, era tab, info label, and button slot counts must match.");
            }

            if (hud.GetComponent<Image>() == null)
            {
                errors.Add("CivicHud root must have a background Image.");
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

            if (root.GetComponentInChildren<CivicHudView>(true) == null)
            {
                errors.Add("UIRoot does not contain the editable CivicHud prefab.");
            }

            if (root.transform.Find("Canvas/IdlePanel") != null)
            {
                errors.Add("UIRoot still contains the legacy IdlePanel.");
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
