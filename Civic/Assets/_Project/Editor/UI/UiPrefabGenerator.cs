using System;
using System.IO;
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
    public static class UiPrefabGenerator
    {
        public const string GeneratedFolder = "Assets/_Project/Prefabs/UI/Generated";
        public const string EditableFolder = "Assets/_Project/Prefabs/UI";
        public const string IdlePanelBasePath = GeneratedFolder + "/IdlePanel_Base.prefab";
        public const string IdlePanelPath = EditableFolder + "/IdlePanel.prefab";
        public const string UiRootBasePath = GeneratedFolder + "/UIRoot_Base.prefab";
        public const string UiRootPath = EditableFolder + "/UIRoot.prefab";
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Civic/UI/Generate")]
        private static void GenerateFromMenu()
        {
            try
            {
                GenerateAll();
                EditorUtility.DisplayDialog(
                    "Civic UI Generate",
                    "UI Base Prefabs were regenerated and validated. Existing user Variants were preserved.",
                    "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic UI Generate Failed", exception.Message, "OK");
                throw;
            }
        }

        public static void GenerateAll()
        {
            GenerateAssetsAt(GeneratedFolder, EditableFolder);
            InstallInSampleScene();
            UiPrefabValidator.ValidateAll();
            Debug.Log("CIVIC_UI_GENERATION_OK");
        }

        public static void GenerateAssetsAt(string generatedFolder, string editableFolder)
        {
            RequireAssetFolder(generatedFolder);
            RequireAssetFolder(editableFolder);
            EnsureFolder(generatedFolder);
            EnsureFolder(editableFolder);

            var panelBasePath = generatedFolder + "/IdlePanel_Base.prefab";
            var panelPath = editableFolder + "/IdlePanel.prefab";
            var rootBasePath = generatedFolder + "/UIRoot_Base.prefab";
            var rootPath = editableFolder + "/UIRoot.prefab";

            var panelBase = BuildIdlePanelBase(panelBasePath);
            var panelVariant = CreateVariantIfMissing(panelBase, panelPath, "IdlePanel");
            var rootBase = BuildUiRootBase(rootBasePath, panelVariant);
            CreateVariantIfMissing(rootBase, rootPath, "UIRoot");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void InstallInSampleScene()
        {
            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            if (rootPrefab == null)
            {
                throw new InvalidOperationException($"UI root prefab is missing: {UiRootPath}");
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty && activeScene.path != SampleScenePath)
            {
                throw new InvalidOperationException("Save the currently modified scene before installing the dummy UI.");
            }

            if (activeScene.path != SampleScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            if (UnityEngine.Object.FindFirstObjectByType<UiRootMarker>() != null)
            {
                return;
            }

            if (activeScene.isDirty)
            {
                throw new InvalidOperationException("Save or discard SampleScene changes before installing the dummy UI.");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, activeScene);
            instance.name = "UIRoot";
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        private static GameObject BuildIdlePanelBase(string assetPath)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            var root = loaded
                ? PrefabUtility.LoadPrefabContents(assetPath)
                : new GameObject("IdlePanel_Base", typeof(RectTransform));

            try
            {
                root.name = "IdlePanel_Base";
                var rootRect = RequireRectTransform(root);
                SetRect(rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 420f));

                var background = GetOrAdd<Image>(root);
                background.color = new Color(0.10f, 0.13f, 0.18f, 0.97f);

                var title = GetOrCreateText(root.transform, "Title");
                title.text = "Civic — Dummy Idle";
                title.fontSize = 34;
                title.alignment = TextAnchor.MiddleCenter;
                title.color = Color.white;
                SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(540f, 60f));

                var currency = GetOrCreateText(root.transform, "CurrencyLabel");
                currency.text = "Coins: 0";
                currency.fontSize = 30;
                currency.alignment = TextAnchor.MiddleCenter;
                currency.color = new Color(0.98f, 0.82f, 0.28f, 1f);
                SetRect(currency.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(520f, 54f));

                var rate = GetOrCreateText(root.transform, "RateLabel");
                rate.text = "Per second: 1.0";
                rate.fontSize = 22;
                rate.alignment = TextAnchor.MiddleCenter;
                rate.color = new Color(0.72f, 0.78f, 0.88f, 1f);
                SetRect(rate.rectTransform, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(520f, 44f));

                var workButton = GetOrCreateButton(root.transform, "WorkButton");
                SetRect(workButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(-145f, 0f), new Vector2(250f, 72f));
                var buttonImage = workButton.GetComponent<Image>();
                buttonImage.color = new Color(0.18f, 0.50f, 0.78f, 1f);
                workButton.targetGraphic = buttonImage;

                var buttonLabel = GetOrCreateText(workButton.transform, "Label");
                buttonLabel.text = "Work (+5)";
                buttonLabel.fontSize = 26;
                buttonLabel.alignment = TextAnchor.MiddleCenter;
                buttonLabel.color = Color.white;
                SetRect(buttonLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var upgradeButton = GetOrCreateButton(root.transform, "UpgradeButton");
                SetRect(upgradeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(145f, 0f), new Vector2(250f, 72f));
                var upgradeButtonImage = upgradeButton.GetComponent<Image>();
                upgradeButtonImage.color = new Color(0.20f, 0.62f, 0.38f, 1f);
                upgradeButton.targetGraphic = upgradeButtonImage;

                var upgradeButtonLabel = GetOrCreateText(upgradeButton.transform, "Label");
                upgradeButtonLabel.text = "Upgrade Work\nCost: 20";
                upgradeButtonLabel.fontSize = 22;
                upgradeButtonLabel.alignment = TextAnchor.MiddleCenter;
                upgradeButtonLabel.color = Color.white;
                SetRect(upgradeButtonLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var view = GetOrAdd<IdlePanelView>(root);
                var viewObject = new SerializedObject(view);
                viewObject.FindProperty("currencyLabel").objectReferenceValue = currency;
                viewObject.FindProperty("rateLabel").objectReferenceValue = rate;
                viewObject.FindProperty("workButton").objectReferenceValue = workButton;
                viewObject.FindProperty("workButtonLabel").objectReferenceValue = buttonLabel;
                viewObject.FindProperty("upgradeButton").objectReferenceValue = upgradeButton;
                viewObject.FindProperty("upgradeButtonLabel").objectReferenceValue = upgradeButtonLabel;
                viewObject.ApplyModifiedPropertiesWithoutUndo();

                var controller = GetOrAdd<DummyIdleGameController>(root);
                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("view").objectReferenceValue = view;
                controllerObject.FindProperty("startingCurrency").doubleValue = 0d;
                controllerObject.FindProperty("coinsPerSecond").doubleValue = 1d;
                controllerObject.FindProperty("clickReward").doubleValue = 5d;
                controllerObject.FindProperty("upgradeBaseCost").doubleValue = 20d;
                controllerObject.FindProperty("upgradeCostGrowth").doubleValue = 1.75d;
                controllerObject.FindProperty("clickRewardIncrement").doubleValue = 5d;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                if (loaded)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject BuildUiRootBase(string assetPath, GameObject panelVariant)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            var root = loaded
                ? PrefabUtility.LoadPrefabContents(assetPath)
                : new GameObject("UIRoot_Base", typeof(UiRootMarker));

            try
            {
                root.name = "UIRoot_Base";
                GetOrAdd<UiRootMarker>(root);

                var canvasObject = GetOrCreateChild(root.transform, "Canvas", typeof(RectTransform));
                var canvas = GetOrAdd<Canvas>(canvasObject);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                GetOrAdd<GraphicRaycaster>(canvasObject);
                var scaler = GetOrAdd<CanvasScaler>(canvasObject);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var canvasRect = canvasObject.GetComponent<RectTransform>();
                SetRect(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var panel = canvasObject.transform.Find("IdlePanel");
                if (panel == null)
                {
                    var panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelVariant, canvasObject.transform);
                    panelInstance.name = "IdlePanel";
                }

                var eventSystemObject = GetOrCreateChild(root.transform, "EventSystem");
                GetOrAdd<EventSystem>(eventSystemObject);
                GetOrAdd<InputSystemUIInputModule>(eventSystemObject);

                return PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                if (loaded)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject CreateVariantIfMissing(GameObject basePrefab, string assetPath, string instanceName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = instanceName;
                return PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Text GetOrCreateText(Transform parent, string name)
        {
            var child = GetOrCreateChild(parent, name, typeof(RectTransform));
            var text = GetOrAdd<Text>(child);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        private static Button GetOrCreateButton(Transform parent, string name)
        {
            var child = GetOrCreateChild(parent, name, typeof(RectTransform));
            GetOrAdd<Image>(child);
            return GetOrAdd<Button>(child);
        }

        private static GameObject GetOrCreateChild(Transform parent, string name, params Type[] components)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();
        }

        private static RectTransform RequireRectTransform(GameObject gameObject)
        {
            if (!gameObject.TryGetComponent<RectTransform>(out var rectTransform))
            {
                throw new InvalidOperationException($"{gameObject.name} must have a RectTransform.");
            }

            return rectTransform;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private static void EnsureFolder(string assetFolder)
        {
            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static void RequireAssetFolder(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains(".."))
            {
                throw new ArgumentException($"Asset folder must be under Assets: {path}", nameof(path));
            }
        }
    }
}
