using System;
using System.Collections.Generic;
using System.Linq;
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
    public static class MainMenuPrefabGenerator
    {
        public const string GeneratedFolder = "Assets/_Project/Prefabs/UI/Generated";
        public const string EditableFolder = "Assets/_Project/Prefabs/UI";
        public const string MainMenuBasePath = GeneratedFolder + "/CivicMainMenu_Base.prefab";
        public const string MainMenuPath = EditableFolder + "/CivicMainMenu.prefab";
        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Civic/Main Menu/Generate")]
        private static void GenerateFromMenu()
        {
            try
            {
                GenerateAll();
                EditorUtility.DisplayDialog(
                    "Civic Main Menu Generate",
                    "메인 메뉴 Base Prefab과 Scene을 생성·검증했습니다. 기존 사용자 Variant는 보존했습니다.",
                    "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic Main Menu Generate Failed", exception.Message, "OK");
                throw;
            }
        }

        public static void GenerateAll()
        {
            GenerateAssetsAt(GeneratedFolder, EditableFolder);
            InstallInMainMenuScene();
            MainMenuPrefabValidator.ValidateAll();
            Debug.Log("CIVIC_MAIN_MENU_GENERATION_OK");
        }

        public static void GenerateAssetsAt(string generatedFolder, string editableFolder)
        {
            RequireAssetFolder(generatedFolder);
            RequireAssetFolder(editableFolder);
            EnsureFolder(generatedFolder);
            EnsureFolder(editableFolder);

            var basePath = generatedFolder + "/CivicMainMenu_Base.prefab";
            var variantPath = editableFolder + "/CivicMainMenu.prefab";
            var basePrefab = BuildMainMenuBase(basePath);
            CreateVariantIfMissing(basePrefab, variantPath, "CivicMainMenu");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void InstallInMainMenuScene()
        {
            var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
            if (menuPrefab == null)
            {
                throw new InvalidOperationException($"Main menu prefab is missing: {MainMenuPath}");
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                throw new InvalidOperationException("Save the currently modified scene before generating the main menu scene.");
            }

            if (activeScene.path != MainMenuScenePath)
            {
                activeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null
                    ? EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var existingController = UnityEngine.Object.FindFirstObjectByType<CivicMainMenuController>();
            if (existingController == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, activeScene);
                instance.name = "CivicMainMenu";
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
            else
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(existingController.gameObject);
                if (source != menuPrefab)
                {
                    throw new InvalidOperationException("MainMenu scene contains a CivicMainMenuController that is not the editable CivicMainMenu Variant.");
                }
            }

            if (activeScene.path != MainMenuScenePath || activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene, MainMenuScenePath);
            }

            EnsureBuildSettings();
        }

        private static GameObject BuildMainMenuBase(string assetPath)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            var root = loaded
                ? PrefabUtility.LoadPrefabContents(assetPath)
                : new GameObject("CivicMainMenu_Base", typeof(RectTransform), typeof(Image));

            try
            {
                root.name = "CivicMainMenu_Base";
                var rootRect = RequireRectTransform(root);
                SetRect(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                GetOrAdd<Image>(root).color = new Color(0.025f, 0.04f, 0.055f, 1f);

                var canvasObject = GetOrCreateChild(root.transform, "Canvas", typeof(RectTransform));
                SetRect(canvasObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var canvas = GetOrAdd<Canvas>(canvasObject);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                GetOrAdd<GraphicRaycaster>(canvasObject);
                var scaler = GetOrAdd<CanvasScaler>(canvasObject);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                var background = GetOrCreateChild(canvasObject.transform, "Background", typeof(RectTransform), typeof(Image));
                SetStretchRect(background.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
                background.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.055f, 1f);

                var mainPanel = GetOrCreateChild(background.transform, "MainPanel", typeof(RectTransform), typeof(Image));
                SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 570f));
                mainPanel.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.14f, 0.98f);

                var title = CreateText(mainPanel.transform, "Title", "CIVIC", 54, TextAnchor.MiddleCenter);
                SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(620f, 90f));
                var subtitle = CreateText(mainPanel.transform, "Subtitle", "문명 경제 Idle", 24, TextAnchor.MiddleCenter);
                subtitle.color = new Color(0.68f, 0.76f, 0.86f, 1f);
                SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(620f, 48f));
                var openFeaturePanelButton = CreateButton(mainPanel.transform, "OpenFeaturePanelButton", "새 게임 / 모듈 설정");
                SetRect(openFeaturePanelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(420f, 72f));
                var quitButton = CreateButton(mainPanel.transform, "QuitButton", "종료");
                SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(420f, 72f));

                var featurePanel = GetOrCreateChild(background.transform, "FeaturePanel", typeof(RectTransform), typeof(Image));
                SetRect(featurePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 940f));
                featurePanel.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.14f, 0.99f);

                var featureTitle = CreateText(featurePanel.transform, "FeatureTitle", "새 게임 모듈 설정", 34, TextAnchor.MiddleLeft);
                SetRect(featureTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(1080f, 58f));
                var summaryLabel = CreateText(featurePanel.transform, "SelectionSummaryLabel", "선택 모듈 0개 / 자동 연계 0개", 20, TextAnchor.MiddleLeft);
                SetRect(summaryLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(1080f, 38f));

                var scroll = CreateScrollArea(featurePanel.transform, CivicFeatureRegistry.Features.Count * 78f);
                var rows = CreateFeatureRows(scroll.content, CivicFeatureRegistry.Features.Count);

                var civilizationSection = GetOrCreateChild(featurePanel.transform, "CivilizationSection", typeof(RectTransform), typeof(Image));
                SetRect(civilizationSection.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 262f), new Vector2(1080f, 90f));
                civilizationSection.GetComponent<Image>().color = new Color(0.045f, 0.065f, 0.09f, 1f);
                var civilizationTitle = CreateText(civilizationSection.transform, "TitleLabel", "시작 문명", 18, TextAnchor.MiddleLeft);
                SetRect(civilizationTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, 20f), new Vector2(120f, 42f));
                var previousCivilizationButton = CreateButton(civilizationSection.transform, "PreviousButton", "◀");
                SetRect(previousCivilizationButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(165f, 20f), new Vector2(52f, 48f));
                var civilizationName = CreateText(civilizationSection.transform, "CivilizationNameLabel", "기본 문명", 18, TextAnchor.MiddleCenter);
                SetRect(civilizationName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(315f, 20f), new Vector2(230f, 48f));
                var nextCivilizationButton = CreateButton(civilizationSection.transform, "NextButton", "▶");
                SetRect(nextCivilizationButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(465f, 20f), new Vector2(52f, 48f));
                var civilizationDescription = CreateText(civilizationSection.transform, "CivilizationDescriptionLabel", "추가 특성 없이 시작", 15, TextAnchor.MiddleLeft);
                civilizationDescription.color = new Color(0.72f, 0.78f, 0.86f, 1f);
                SetRect(civilizationDescription.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(790f, 20f), new Vector2(570f, 56f));

                var validationLabel = CreateText(featurePanel.transform, "ValidationLabel", "구성을 시작할 수 있습니다.", 18, TextAnchor.MiddleLeft);
                SetRect(validationLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1080f, 48f));
                var noticeLabel = CreateText(featurePanel.transform, "ImplementationNoticeLabel", "모듈 기반 구현 상태", 16, TextAnchor.UpperLeft);
                noticeLabel.color = new Color(0.78f, 0.72f, 0.50f, 1f);
                SetRect(noticeLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 115f), new Vector2(1080f, 62f));
                var backButton = CreateButton(featurePanel.transform, "BackButton", "뒤로");
                SetRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(155f, 50f), new Vector2(250f, 64f));
                var startButton = CreateButton(featurePanel.transform, "StartGameButton", "이 구성으로 시작");
                SetRect(startButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-205f, 50f), new Vector2(350f, 64f));

                var eventSystemObject = GetOrCreateChild(root.transform, "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystemObject.GetComponent<EventSystem>().sendNavigationEvents = true;

                var controller = GetOrAdd<CivicMainMenuController>(root);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("mainPanel").objectReferenceValue = mainPanel;
                serialized.FindProperty("featurePanel").objectReferenceValue = featurePanel;
                serialized.FindProperty("openFeaturePanelButton").objectReferenceValue = openFeaturePanelButton;
                serialized.FindProperty("backButton").objectReferenceValue = backButton;
                serialized.FindProperty("startGameButton").objectReferenceValue = startButton;
                serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
                AssignArray(serialized, "featureRows", rows);
                serialized.FindProperty("selectionSummaryLabel").objectReferenceValue = summaryLabel;
                serialized.FindProperty("validationLabel").objectReferenceValue = validationLabel;
                serialized.FindProperty("implementationNoticeLabel").objectReferenceValue = noticeLabel;
                serialized.FindProperty("civilizationSection").objectReferenceValue = civilizationSection;
                serialized.FindProperty("previousCivilizationButton").objectReferenceValue = previousCivilizationButton;
                serialized.FindProperty("nextCivilizationButton").objectReferenceValue = nextCivilizationButton;
                serialized.FindProperty("civilizationNameLabel").objectReferenceValue = civilizationName;
                serialized.FindProperty("civilizationDescriptionLabel").objectReferenceValue = civilizationDescription;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                featurePanel.SetActive(false);
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

        private static CivicFeatureToggleRow[] CreateFeatureRows(Transform content, int count)
        {
            var expectedNames = new HashSet<string>(Enumerable.Range(1, count).Select(index => $"FeatureToggleRow{index:00}"));
            for (var index = content.childCount - 1; index >= 0; index--)
            {
                var child = content.GetChild(index);
                if (child.name.StartsWith("FeatureToggleRow", StringComparison.Ordinal) && !expectedNames.Contains(child.name))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            var rows = new CivicFeatureToggleRow[count];
            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(content, $"FeatureToggleRow{index + 1:00}", typeof(RectTransform), typeof(Image));
                SetTopStretchRect(row.GetComponent<RectTransform>(), 0f, index * 78f, 0f, 68f);
                row.GetComponent<Image>().color = new Color(0.045f, 0.065f, 0.09f, 1f);

                var name = CreateText(row.transform, "NameLabel", "모듈", 20, TextAnchor.MiddleLeft);
                SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 10f), new Vector2(270f, 48f));
                var description = CreateText(row.transform, "DescriptionLabel", "설명", 15, TextAnchor.MiddleLeft);
                description.color = new Color(0.72f, 0.78f, 0.86f, 1f);
                SetRect(description.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(555f, 10f), new Vector2(520f, 48f));
                var state = CreateText(row.transform, "StateLabel", "OFF", 18, TextAnchor.MiddleCenter);
                SetRect(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-180f, 10f), new Vector2(80f, 48f));
                var toggle = CreateToggle(row.transform, "Toggle");
                SetRect(toggle.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 10f), new Vector2(52f, 52f));

                var rowComponent = GetOrAdd<CivicFeatureToggleRow>(row);
                var serialized = new SerializedObject(rowComponent);
                serialized.FindProperty("toggle").objectReferenceValue = toggle;
                serialized.FindProperty("nameLabel").objectReferenceValue = name;
                serialized.FindProperty("descriptionLabel").objectReferenceValue = description;
                serialized.FindProperty("stateLabel").objectReferenceValue = state;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                rows[index] = rowComponent;
            }

            return rows;
        }

        private static (ScrollRect scrollRect, Transform content) CreateScrollArea(Transform parent, float contentHeight)
        {
            var scrollObject = GetOrCreateChild(parent, "FeatureScroll", typeof(RectTransform), typeof(ScrollRect));
            SetStretchRect(scrollObject.GetComponent<RectTransform>(), 50f, 145f, 50f, 320f);
            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            var viewport = GetOrCreateChild(scrollObject.transform, "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            SetStretchRect(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = GetOrCreateChild(viewport.transform, "Content", typeof(RectTransform));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            contentRect.localScale = Vector3.one;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            return (scrollRect, content.transform);
        }

        private static Toggle CreateToggle(Transform parent, string name)
        {
            var toggleObject = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            var background = toggleObject.GetComponent<Image>();
            background.color = new Color(0.15f, 0.19f, 0.24f, 1f);
            var checkmark = GetOrCreateChild(toggleObject.transform, "Checkmark", typeof(RectTransform), typeof(Image));
            SetStretchRect(checkmark.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
            checkmark.GetComponent<Image>().color = new Color(0.26f, 0.64f, 0.92f, 1f);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark.GetComponent<Image>();
            return toggle;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var buttonObject = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.36f, 0.58f, 1f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.86f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.68f, 0.80f, 0.92f, 1f);
            colors.disabledColor = new Color(0.28f, 0.31f, 0.35f, 1f);
            button.colors = colors;
            var text = CreateText(buttonObject.transform, "Label", label, 21, TextAnchor.MiddleCenter);
            SetStretchRect(text.rectTransform, 8f, 4f, 8f, 4f);
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var textObject = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Text));
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
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

        private static void EnsureBuildSettings()
        {
            var paths = new[] { MainMenuScenePath, SampleScenePath };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in paths)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset == null)
                {
                    throw new InvalidOperationException($"Build scene is missing: {path}");
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            scenes.AddRange(EditorBuildSettings.scenes.Where(scene => !paths.Contains(scene.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AssignArray<T>(SerializedObject serializedObject, string propertyName, IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            var property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static GameObject GetOrCreateChild(Transform parent, string name, params Type[] components)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                foreach (var componentType in components)
                {
                    if (existing.GetComponent(componentType) == null)
                    {
                        existing.gameObject.AddComponent(componentType);
                    }
                }

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
            if (!gameObject.TryGetComponent<RectTransform>(out var rect))
            {
                throw new InvalidOperationException($"{gameObject.name} must have a RectTransform.");
            }

            return rect;
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

        private static void SetStretchRect(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void SetTopStretchRect(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
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
