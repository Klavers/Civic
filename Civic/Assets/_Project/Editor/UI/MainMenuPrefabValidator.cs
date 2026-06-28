using System;
using System.Collections.Generic;
using Civic.Features;
using Civic.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Civic.Editor.UI
{
    public static class MainMenuPrefabValidator
    {
        [MenuItem("Tools/Civic/Main Menu/Validate")]
        private static void ValidateFromMenu()
        {
            try
            {
                ValidateAll();
                EditorUtility.DisplayDialog("Civic Main Menu Validate", "메인 메뉴 Prefab, Scene, Build Settings 검증을 통과했습니다.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic Main Menu Validate Failed", exception.Message, "OK");
                throw;
            }
        }

        public static void ValidateAll()
        {
            var errors = CollectErrors(MainMenuPrefabGenerator.GeneratedFolder, MainMenuPrefabGenerator.EditableFolder);
            ValidateSceneAndBuildSettings(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            Debug.Log("CIVIC_MAIN_MENU_VALIDATION_OK");
        }

        public static List<string> CollectErrors(string generatedFolder, string editableFolder)
        {
            var errors = new List<string>();
            var basePath = generatedFolder + "/CivicMainMenu_Base.prefab";
            var variantPath = editableFolder + "/CivicMainMenu.prefab";
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);

            ValidatePrefab(basePrefab, "CivicMainMenu Base", PrefabAssetType.Regular, errors);
            ValidatePrefab(variant, "CivicMainMenu Variant", PrefabAssetType.Variant, errors);
            if (basePrefab != null && variant != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
                if (source != basePrefab)
                {
                    errors.Add("CivicMainMenu Variant does not inherit from its generated Base prefab.");
                }
            }

            return errors;
        }

        private static void ValidatePrefab(GameObject prefab, string label, PrefabAssetType expectedType, ICollection<string> errors)
        {
            if (prefab == null)
            {
                errors.Add(label + " is missing.");
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(prefab) != expectedType)
            {
                errors.Add($"{label} has the wrong prefab type.");
            }

            var controller = prefab.GetComponent<CivicMainMenuController>();
            if (controller == null || !controller.HasRequiredReferences)
            {
                errors.Add($"{label} is missing CivicMainMenuController references.");
            }
            else if (controller.FeatureRows.Count != CivicFeatureRegistry.Features.Count)
            {
                errors.Add($"{label} must contain one toggle row per registered feature.");
            }

            if (prefab.GetComponentInChildren<Canvas>(true) == null ||
                prefab.GetComponentInChildren<CanvasScaler>(true) == null ||
                prefab.GetComponentInChildren<GraphicRaycaster>(true) == null)
            {
                errors.Add($"{label} is missing Canvas infrastructure.");
            }

            if (prefab.GetComponentInChildren<EventSystem>(true) == null ||
                prefab.GetComponentInChildren<InputSystemUIInputModule>(true) == null)
            {
                errors.Add($"{label} is missing EventSystem or InputSystemUIInputModule.");
            }

            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingCount > 0)
                {
                    errors.Add($"{label}/{transform.name} has {missingCount} missing script reference(s).");
                }
            }
        }

        private static void ValidateSceneAndBuildSettings(ICollection<string> errors)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPrefabGenerator.MainMenuScenePath);
            if (sceneAsset == null)
            {
                errors.Add("MainMenu scene is missing.");
                return;
            }

            var previousScenePath = SceneManager.GetActiveScene().path;
            var previousSceneDirty = SceneManager.GetActiveScene().isDirty;
            if (previousSceneDirty && previousScenePath != MainMenuPrefabGenerator.MainMenuScenePath)
            {
                errors.Add("Cannot inspect MainMenu while the current scene has unsaved changes.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(MainMenuPrefabGenerator.MainMenuScenePath, OpenSceneMode.Single);
            var controllers = UnityEngine.Object.FindObjectsByType<CivicMainMenuController>(FindObjectsSortMode.None);
            if (controllers.Length != 1)
            {
                errors.Add($"MainMenu scene must contain exactly one CivicMainMenuController; found {controllers.Length}.");
            }
            else
            {
                var expectedVariant = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabGenerator.MainMenuPath);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(controllers[0].gameObject);
                if (source != expectedVariant)
                {
                    errors.Add("MainMenu scene does not instantiate the editable CivicMainMenu Variant.");
                }
            }

            if (!scene.IsValid())
            {
                errors.Add("MainMenu scene could not be opened.");
            }

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < 2 ||
                buildScenes[0].path != MainMenuPrefabGenerator.MainMenuScenePath ||
                !buildScenes[0].enabled ||
                buildScenes[1].path != MainMenuPrefabGenerator.SampleScenePath ||
                !buildScenes[1].enabled)
            {
                errors.Add("Build Settings must contain enabled MainMenu at index 0 and SampleScene at index 1.");
            }
        }
    }
}
