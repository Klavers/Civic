using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;
using Civic.Simulation;
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
        public const string CivicHudBasePath = GeneratedFolder + "/CivicHud_Base.prefab";
        public const string CivicHudPath = EditableFolder + "/CivicHud.prefab";
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
                    "Civic HUD Base Prefabs were regenerated and validated. Existing user Variants were preserved.",
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
            CivicDataValidator.ValidateAll();
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

            var hudBasePath = generatedFolder + "/CivicHud_Base.prefab";
            var hudPath = editableFolder + "/CivicHud.prefab";
            var rootBasePath = generatedFolder + "/UIRoot_Base.prefab";
            var rootPath = editableFolder + "/UIRoot.prefab";
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            if (dataSource == null)
            {
                throw new InvalidOperationException($"Missing data source asset: {CivicGameDataSource.DefaultAssetPath}");
            }

            var hudBase = BuildCivicHudBase(hudBasePath, dataSource);
            var hudVariant = CreateVariantIfMissing(hudBase, hudPath, "CivicHud");
            var rootBase = BuildUiRootBase(rootBasePath, hudVariant);
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
                throw new InvalidOperationException("Save the currently modified scene before installing the Civic HUD.");
            }

            if (activeScene.path != SampleScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            var existingRoot = UnityEngine.Object.FindFirstObjectByType<UiRootMarker>();
            if (existingRoot != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(existingRoot.gameObject);
                if (source == rootPrefab)
                {
                    return;
                }

                if (activeScene.isDirty)
                {
                    throw new InvalidOperationException("Save or discard SampleScene changes before replacing the previous UI root.");
                }

                UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, activeScene);
            instance.name = "UIRoot";
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        private static GameObject BuildCivicHudBase(string assetPath, CivicGameDataSource dataSource)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            var root = loaded
                ? PrefabUtility.LoadPrefabContents(assetPath)
                : new GameObject("CivicHud_Base", typeof(RectTransform));

            try
            {
                root.name = "CivicHud_Base";
                var rootRect = RequireRectTransform(root);
                SetRect(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var background = GetOrAdd<Image>(root);
                background.color = new Color(0.06f, 0.08f, 0.10f, 0.96f);

                var topBar = GetOrCreateChild(root.transform, "TopBar", typeof(RectTransform), typeof(Image));
                SetRect(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(0f, 72f));
                topBar.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.98f);

                var population = CreateTopText(topBar.transform, "PopulationLabel", "인구 3", 80f);
                var buildings = CreateTopText(topBar.transform, "BuildingCapacityLabel", "건물 0 / 3", 250f);
                var gdp = CreateTopText(topBar.transform, "GdpLabel", "GDP 0", 450f);
                var treasury = CreateTopText(topBar.transform, "TreasuryLabel", "국고 0", 610f);
                var construction = CreateTopText(topBar.transform, "ConstructionLabel", "건설력 5", 790f);
                var science = CreateTopText(topBar.transform, "ScienceLabel", "과학 0", 980f);
                var era = CreateTopText(topBar.transform, "EraLabel", "원시시대", 1160f);

                var alerts = GetOrCreateChild(root.transform, "TopAlerts", typeof(RectTransform));
                SetRect(alerts.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-270f, -36f), new Vector2(500f, 64f));
                var shortageAlert = CreateAlertText(alerts.transform, "ShortageAlertLabel", "품귀 없음", -160f);
                var researchAlert = CreateAlertText(alerts.transform, "ResearchAlertLabel", "연구 대기", 0f);
                var constructionAlert = CreateAlertText(alerts.transform, "ConstructionAlertLabel", "건설 가능", 160f);

                var sideBar = GetOrCreateChild(root.transform, "LeftSidebar", typeof(RectTransform), typeof(Image));
                SetRect(sideBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(80f, -72f));
                sideBar.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.98f);
                var resourcesButton = CreateSidebarButton(sideBar.transform, "ResourcesPanelButton", "자원", -90f);
                var buildingsButton = CreateSidebarButton(sideBar.transform, "BuildingsPanelButton", "건물", -180f);
                var technologiesButton = CreateSidebarButton(sideBar.transform, "TechnologiesPanelButton", "기술", -270f);
                var nationButton = CreateSidebarButton(sideBar.transform, "NationPanelButton", "국가", -360f);
                var modulesButton = CreateSidebarButton(sideBar.transform, "ModulesPanelButton", "모듈", -450f);

                var detailPanel = GetOrCreateChild(root.transform, "LeftDetailPanel", typeof(RectTransform), typeof(Image));
                SetRect(detailPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(640f, -36f), new Vector2(1040f, -140f));
                detailPanel.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.17f, 0.96f);
                var detailTitle = GetOrCreateText(detailPanel.transform, "DetailTitleLabel");
                detailTitle.text = "자원 상세";
                detailTitle.fontSize = 26;
                detailTitle.alignment = TextAnchor.MiddleLeft;
                SetRect(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -34f), new Vector2(-32f, 50f));
                var detailCloseButton = GetOrCreateButton(detailPanel.transform, "DetailCloseButton");
                SetRect(detailCloseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -34f), new Vector2(100f, 44f));
                detailCloseButton.GetComponent<Image>().color = new Color(0.24f, 0.29f, 0.36f, 1f);
                var detailCloseLabel = GetOrCreateText(detailCloseButton.transform, "Label");
                detailCloseLabel.text = "닫기";
                detailCloseLabel.fontSize = 17;
                detailCloseLabel.alignment = TextAnchor.MiddleCenter;
                SetRect(detailCloseLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var detailBody = GetOrCreateText(detailPanel.transform, "DetailBodyLabel");
                detailBody.text = "데이터 로드 대기";
                detailBody.fontSize = 18;
                detailBody.alignment = TextAnchor.UpperLeft;
                SetRect(detailBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, -98f), new Vector2(-32f, -130f));
                detailBody.gameObject.SetActive(false);

                DestroyChildIfExists(detailPanel.transform, "BuildButton");
                DestroyChildIfExists(detailPanel.transform, "ResearchButton");
                for (var index = 0; index < 8; index++)
                {
                    DestroyChildIfExists(detailPanel.transform, $"BuildingBuildButton{index + 1:00}");
                    DestroyChildIfExists(detailPanel.transform, $"TechnologyResearchButton{index + 1:00}");
                    DestroyChildIfExists(detailPanel.transform, $"BuildingActionRow{index + 1:00}");
                    DestroyChildIfExists(detailPanel.transform, $"TechnologyActionRow{index + 1:00}");
                }

                var tooltipView = CreateTooltipView(root.transform);
                var gameData = dataSource.LoadGameData();
                var resourceRowCount = Math.Max(
                    1,
                    gameData.Resources.Count(resource => resource.Category != ResourceCategory.Aggregate));
                var buildingRowCount = Math.Max(
                    1,
                    gameData.Buildings.Count(building => building.IsBuildable));
                var eraTabCount = Math.Max(1, gameData.Eras.Count);
                var technologyRowCount = Math.Max(
                    1,
                    gameData.Technologies
                        .GroupBy(technology => technology.EraId)
                        .Select(group => group.Count())
                        .DefaultIfEmpty(1)
                        .Max());

                var resourcePanel = GetOrCreateChild(detailPanel.transform, "ResourceDetailPanel", typeof(RectTransform));
                SetStretchRect(resourcePanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                var resourceScroll = CreateScrollArea(resourcePanel.transform, "ResourceScroll", resourceRowCount * 150f);
                var resourceRows = CreateResourceDetailRows(resourceScroll.Content, tooltipView, resourceRowCount, 4);

                var buildingPanel = GetOrCreateChild(detailPanel.transform, "BuildingDetailPanel", typeof(RectTransform));
                SetStretchRect(buildingPanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                CreateBuildingHeaderRow(buildingPanel.transform);
                var buildingScroll = CreateScrollArea(buildingPanel.transform, "BuildingScroll", buildingRowCount * 56f, 42f);
                var buildingActionRows = CreateBuildingActionRows(buildingScroll.Content, tooltipView, buildingRowCount);

                var technologyPanel = GetOrCreateChild(detailPanel.transform, "TechnologyDetailPanel", typeof(RectTransform));
                SetStretchRect(technologyPanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                var eraTabs = CreateEraTabRows(technologyPanel.transform, eraTabCount);
                var technologyScroll = CreateScrollArea(technologyPanel.transform, "TechnologyScroll", technologyRowCount * 84f, 52f);
                var technologyActionRows = CreateDetailActionRows(technologyScroll.Content, "TechnologyActionRow", "TechnologyInfoLabel", "TechnologyResearchButton", "기술 정보", "연구", technologyRowCount);

                const int nationModifierRowCount = 64;
                var nationPanel = GetOrCreateChild(detailPanel.transform, "NationDetailPanel", typeof(RectTransform));
                SetStretchRect(nationPanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                var nationStatus = GetOrCreateText(nationPanel.transform, "NationStatusLabel");
                nationStatus.text = "현재 국가: 설립된 국가 없음";
                nationStatus.fontSize = 17;
                nationStatus.alignment = TextAnchor.MiddleLeft;
                SetRect(nationStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -24f), new Vector2(-24f, 40f));
                var nationScroll = CreateScrollArea(nationPanel.transform, "NationModifierScroll", nationModifierRowCount * 56f, 52f);
                var nationModifierRows = CreateNationModifierRows(nationScroll.Content, nationModifierRowCount);

                var rightPanel = GetOrCreateChild(root.transform, "RightResourcePanel", typeof(RectTransform), typeof(Image));
                SetRect(rightPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-190f, -36f), new Vector2(360f, -140f));
                rightPanel.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.14f, 0.96f);
                var rightResources = GetOrCreateText(rightPanel.transform, "RightResourcesLabel");
                rightResources.text = "자원 요약";
                rightResources.fontSize = 18;
                rightResources.alignment = TextAnchor.UpperLeft;
                SetRect(rightResources.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, -48f), new Vector2(-36f, -96f));
                var foodToggleButton = CreateActionButton(rightPanel.transform, "FoodToggleButton", "식량 펼침", new Vector2(0f, 34f));
                var modulePanelView = CreateModulePanel(root, modulesButton, tooltipView);
                var overlayView = CreateHudOverlayView(root, tooltipView);
                tooltipView.transform.SetAsLastSibling();

                var view = GetOrAdd<CivicHudView>(root);
                var viewObject = new SerializedObject(view);
                viewObject.FindProperty("populationLabel").objectReferenceValue = population;
                viewObject.FindProperty("buildingCapacityLabel").objectReferenceValue = buildings;
                viewObject.FindProperty("gdpLabel").objectReferenceValue = gdp;
                viewObject.FindProperty("treasuryLabel").objectReferenceValue = treasury;
                viewObject.FindProperty("constructionLabel").objectReferenceValue = construction;
                viewObject.FindProperty("scienceLabel").objectReferenceValue = science;
                viewObject.FindProperty("eraLabel").objectReferenceValue = era;
                viewObject.FindProperty("shortageAlertLabel").objectReferenceValue = shortageAlert;
                viewObject.FindProperty("researchAlertLabel").objectReferenceValue = researchAlert;
                viewObject.FindProperty("constructionAlertLabel").objectReferenceValue = constructionAlert;
                viewObject.FindProperty("rightResourcesLabel").objectReferenceValue = rightResources;
                viewObject.FindProperty("detailTitleLabel").objectReferenceValue = detailTitle;
                viewObject.FindProperty("detailBodyLabel").objectReferenceValue = detailBody;
                viewObject.FindProperty("detailPanelRoot").objectReferenceValue = detailPanel;
                viewObject.FindProperty("detailCloseButton").objectReferenceValue = detailCloseButton;
                viewObject.FindProperty("resourceDetailPanel").objectReferenceValue = resourcePanel;
                viewObject.FindProperty("buildingDetailPanel").objectReferenceValue = buildingPanel;
                viewObject.FindProperty("technologyDetailPanel").objectReferenceValue = technologyPanel;
                viewObject.FindProperty("nationDetailPanel").objectReferenceValue = nationPanel;
                viewObject.FindProperty("resourcesPanelButton").objectReferenceValue = resourcesButton;
                viewObject.FindProperty("buildingsPanelButton").objectReferenceValue = buildingsButton;
                viewObject.FindProperty("technologiesPanelButton").objectReferenceValue = technologiesButton;
                viewObject.FindProperty("nationPanelButton").objectReferenceValue = nationButton;
                AssignObjectArray(viewObject, "resourceDetailRows", resourceRows.Rows);
                AssignObjectArray(viewObject, "resourceSummaryLabels", resourceRows.SummaryLabels);
                AssignObjectArray(viewObject, "resourceProducerBoxes", resourceRows.ProducerBoxes);
                AssignObjectArray(viewObject, "resourceProducerLabels", resourceRows.ProducerLabels);
                AssignObjectArray(viewObject, "resourceProducerTooltips", resourceRows.ProducerTooltips);
                AssignObjectArray(viewObject, "buildingActionRows", buildingActionRows.Rows);
                AssignObjectArray(viewObject, "buildingActionInfoLabels", buildingActionRows.NameLabels);
                AssignObjectArray(viewObject, "buildingCountLabels", buildingActionRows.CountLabels);
                AssignObjectArray(viewObject, "buildingCostLabels", buildingActionRows.CostLabels);
                AssignObjectArray(viewObject, "buildingInputOutputLabels", buildingActionRows.InputOutputLabels);
                AssignObjectArray(viewObject, "buildingInputOutputTooltips", buildingActionRows.InputOutputTooltips);
                AssignObjectArray(viewObject, "buildingGdpDeltaLabels", buildingActionRows.GdpDeltaLabels);
                AssignObjectArray(viewObject, "buildingActionButtons", buildingActionRows.Buttons);
                AssignObjectArray(viewObject, "buildingButtonTooltips", buildingActionRows.ButtonTooltips);
                AssignObjectArray(viewObject, "eraTabRows", eraTabs.Rows);
                AssignObjectArray(viewObject, "eraTabLabels", eraTabs.InfoLabels);
                AssignObjectArray(viewObject, "eraTabButtons", eraTabs.Buttons);
                AssignObjectArray(viewObject, "technologyActionRows", technologyActionRows.Rows);
                AssignObjectArray(viewObject, "technologyActionInfoLabels", technologyActionRows.InfoLabels);
                AssignObjectArray(viewObject, "technologyActionButtons", technologyActionRows.Buttons);
                viewObject.FindProperty("nationStatusLabel").objectReferenceValue = nationStatus;
                AssignObjectArray(viewObject, "nationModifierRows", nationModifierRows.Rows);
                AssignObjectArray(viewObject, "nationModifierSummaryLabels", nationModifierRows.SummaryLabels);
                AssignObjectArray(viewObject, "nationModifierDetailRoots", nationModifierRows.DetailRoots);
                AssignObjectArray(viewObject, "nationModifierDetailLabels", nationModifierRows.DetailLabels);
                AssignObjectArray(viewObject, "nationModifierExpandButtons", nationModifierRows.ExpandButtons);
                AssignObjectArray(viewObject, "nationModifierExpandLabels", nationModifierRows.ExpandLabels);
                AssignObjectArray(viewObject, "nationModifierLayouts", nationModifierRows.Layouts);
                viewObject.FindProperty("foodToggleButton").objectReferenceValue = foodToggleButton;
                viewObject.FindProperty("tooltipView").objectReferenceValue = tooltipView;
                viewObject.ApplyModifiedPropertiesWithoutUndo();

                var controller = GetOrAdd<CivicHudController>(root);
                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("view").objectReferenceValue = view;
                controllerObject.FindProperty("dataSource").objectReferenceValue = dataSource;
                controllerObject.FindProperty("simulationSpeed").doubleValue = 1d;
                controllerObject.FindProperty("modulePanelView").objectReferenceValue = modulePanelView;
                controllerObject.FindProperty("overlayView").objectReferenceValue = overlayView;
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

        private static GameObject BuildUiRootBase(string assetPath, GameObject hudVariant)
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
                SetRect(canvasObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                DestroyChildIfExists(canvasObject.transform, "IdlePanel");
                if (canvasObject.transform.Find("CivicHud") == null)
                {
                    var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudVariant, canvasObject.transform);
                    hudInstance.name = "CivicHud";
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

        private static ScrollArea CreateScrollArea(Transform parent, string name, float contentHeight, float topOffset = 0f)
        {
            var scrollObject = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(ScrollRect));
            SetStretchRect(scrollObject.GetComponent<RectTransform>(), 0f, topOffset, 0f, 0f);
            var scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            var viewport = GetOrCreateChild(scrollObject.transform, "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            SetStretchRect(viewport.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            var mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

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
            return new ScrollArea(scrollObject, content.transform);
        }

        private static ResourceDetailRows CreateResourceDetailRows(Transform parent, CivicTooltipView tooltipView, int count, int producerSlotsPerRow)
        {
            var rows = new GameObject[count];
            var summaryLabels = new Text[count];
            var producerBoxes = new GameObject[count * producerSlotsPerRow];
            var producerLabels = new Text[count * producerSlotsPerRow];
            var producerTooltips = new CivicTooltipTrigger[count * producerSlotsPerRow];

            for (var rowIndex = 0; rowIndex < count; rowIndex++)
            {
                var row = GetOrCreateChild(parent, $"ResourceDetailRow{rowIndex + 1:00}", typeof(RectTransform), typeof(Image));
                SetTopStretchRect(row.GetComponent<RectTransform>(), 0f, rowIndex * 150f, 0f, 140f);
                row.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

                var summary = GetOrCreateText(row.transform, "ResourceSummaryLabel");
                summary.text = "자원 요약";
                summary.fontSize = 17;
                summary.alignment = TextAnchor.MiddleLeft;
                SetStretchRect(summary.rectTransform, 14f, 8f, 14f, 98f);
                rows[rowIndex] = row;
                summaryLabels[rowIndex] = summary;

                for (var slot = 0; slot < producerSlotsPerRow; slot++)
                {
                    var flatIndex = rowIndex * producerSlotsPerRow + slot;
                    var box = GetOrCreateChild(row.transform, $"ResourceProducerBox{slot + 1:00}", typeof(RectTransform), typeof(Image));
                    SetTopStretchRect(box.GetComponent<RectTransform>(), 18f, 44f + slot * 22f, 18f, 20f);
                    box.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.14f, 1f);
                    var trigger = GetOrAdd<CivicTooltipTrigger>(box);
                    trigger.AssignTooltipView(tooltipView);

                    var label = GetOrCreateText(box.transform, "Label");
                    label.text = "생산처";
                    label.fontSize = 14;
                    label.alignment = TextAnchor.MiddleLeft;
                    SetStretchRect(label.rectTransform, 8f, 0f, 8f, 0f);

                    producerBoxes[flatIndex] = box;
                    producerLabels[flatIndex] = label;
                    producerTooltips[flatIndex] = trigger;
                }

                row.SetActive(false);
            }

            return new ResourceDetailRows(rows, summaryLabels, producerBoxes, producerLabels, producerTooltips);
        }

        private static BuildingActionRows CreateBuildingActionRows(Transform parent, CivicTooltipView tooltipView, int count)
        {
            var rows = new GameObject[count];
            var nameLabels = new Text[count];
            var countLabels = new Text[count];
            var costLabels = new Text[count];
            var inputOutputLabels = new Text[count];
            var inputOutputTooltips = new CivicTooltipTrigger[count];
            var gdpDeltaLabels = new Text[count];
            var buttons = new Button[count];
            var buttonTooltips = new CivicTooltipTrigger[count];

            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(parent, $"BuildingActionRow{index + 1:00}", typeof(RectTransform), typeof(Image));
                SetTopStretchRect(row.GetComponent<RectTransform>(), 0f, index * 56f, 0f, 50f);
                row.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

                var name = CreateColumnText(row.transform, "BuildingNameLabel", "건물명", 10f, 150f, TextAnchor.MiddleLeft);
                var currentCount = CreateColumnText(row.transform, "BuildingCountLabel", "0", 165f, 58f, TextAnchor.MiddleCenter);
                var cost = CreateColumnText(row.transform, "BuildingCostLabel", "0", 232f, 92f, TextAnchor.MiddleCenter);

                var inputOutputCell = GetOrCreateChild(row.transform, "BuildingInputOutputCell", typeof(RectTransform), typeof(Image));
                SetLeftRect(inputOutputCell.GetComponent<RectTransform>(), 334f, 300f, 42f);
                inputOutputCell.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.13f, 1f);
                var inputOutputTrigger = GetOrAdd<CivicTooltipTrigger>(inputOutputCell);
                inputOutputTrigger.AssignTooltipView(tooltipView);
                var inputOutput = GetOrCreateText(inputOutputCell.transform, "Label");
                inputOutput.text = "투입/산출";
                inputOutput.fontSize = 15;
                inputOutput.alignment = TextAnchor.MiddleLeft;
                SetStretchRect(inputOutput.rectTransform, 8f, 0f, 8f, 0f);

                var gdp = CreateColumnText(row.transform, "BuildingGdpDeltaLabel", "+0", 646f, 110f, TextAnchor.MiddleCenter);

                var button = GetOrCreateButton(row.transform, $"BuildingBuildButton{index + 1:00}");
                SetRightRect(button.GetComponent<RectTransform>(), 8f, 154f, 44f);
                button.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.58f, 1f);
                ConfigureButtonColors(button);
                var buttonTrigger = GetOrAdd<CivicTooltipTrigger>(button.gameObject);
                buttonTrigger.AssignTooltipView(tooltipView);
                var buttonText = GetOrCreateText(button.transform, "Label");
                buttonText.text = "건설";
                buttonText.fontSize = 16;
                buttonText.alignment = TextAnchor.MiddleCenter;
                SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                row.SetActive(false);
                rows[index] = row;
                nameLabels[index] = name;
                countLabels[index] = currentCount;
                costLabels[index] = cost;
                inputOutputLabels[index] = inputOutput;
                inputOutputTooltips[index] = inputOutputTrigger;
                gdpDeltaLabels[index] = gdp;
                buttons[index] = button;
                buttonTooltips[index] = buttonTrigger;
            }

            return new BuildingActionRows(rows, nameLabels, countLabels, costLabels, inputOutputLabels, inputOutputTooltips, gdpDeltaLabels, buttons, buttonTooltips);
        }

        private static void CreateBuildingHeaderRow(Transform parent)
        {
            var header = GetOrCreateChild(parent, "BuildingHeaderRow", typeof(RectTransform), typeof(Image));
            SetTopStretchRect(header.GetComponent<RectTransform>(), 0f, 0f, 0f, 36f);
            header.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.07f, 1f);
            CreateColumnText(header.transform, "HeaderBuildingNameLabel", "건물명", 10f, 150f, TextAnchor.MiddleLeft);
            CreateColumnText(header.transform, "HeaderBuildingCountLabel", "개수", 165f, 58f, TextAnchor.MiddleCenter);
            CreateColumnText(header.transform, "HeaderBuildingCostLabel", "건설비용", 232f, 92f, TextAnchor.MiddleCenter);
            CreateColumnText(header.transform, "HeaderBuildingInputOutputLabel", "투입/산출", 334f, 300f, TextAnchor.MiddleLeft);
            CreateColumnText(header.transform, "HeaderBuildingGdpDeltaLabel", "GDP 변화", 646f, 110f, TextAnchor.MiddleCenter);
        }

        private static Text CreateColumnText(Transform parent, string name, string text, float x, float width, TextAnchor alignment)
        {
            var label = GetOrCreateText(parent, name);
            label.text = text;
            label.fontSize = 16;
            label.alignment = alignment;
            SetLeftRect(label.rectTransform, x, width, 34f);
            return label;
        }

        private static CivicTooltipView CreateTooltipView(Transform parent)
        {
            var tooltipObject = GetOrCreateChild(parent, "Tooltip", typeof(RectTransform), typeof(Image));
            var tooltipRect = tooltipObject.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0f, 1f);
            tooltipRect.anchorMax = new Vector2(0f, 1f);
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.anchoredPosition = Vector2.zero;
            tooltipRect.sizeDelta = new Vector2(360f, 160f);
            tooltipRect.localScale = Vector3.one;
            var tooltipImage = tooltipObject.GetComponent<Image>();
            tooltipImage.color = new Color(0.02f, 0.025f, 0.03f, 0.96f);
            tooltipImage.raycastTarget = false;
            var tooltipCanvasGroup = GetOrAdd<CanvasGroup>(tooltipObject);
            tooltipCanvasGroup.interactable = false;
            tooltipCanvasGroup.blocksRaycasts = false;

            var label = GetOrCreateText(tooltipObject.transform, "BodyLabel");
            label.text = "Tooltip";
            label.fontSize = 15;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            SetStretchRect(label.rectTransform, 12f, 10f, 12f, 10f);

            var tooltip = GetOrAdd<CivicTooltipView>(tooltipObject);
            var tooltipSerialized = new SerializedObject(tooltip);
            tooltipSerialized.FindProperty("panel").objectReferenceValue = tooltipRect;
            tooltipSerialized.FindProperty("bodyLabel").objectReferenceValue = label;
            tooltipSerialized.ApplyModifiedPropertiesWithoutUndo();
            tooltipObject.SetActive(false);
            return tooltip;
        }

        private static DetailActionRows CreateEraTabRows(Transform parent, int count)
        {
            var rows = new GameObject[count];
            var labels = new Text[count];
            var buttons = new Button[count];
            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(parent, $"EraTabRow{index + 1:00}", typeof(RectTransform));
                var width = 112f;
                SetRect(
                    row.GetComponent<RectTransform>(),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(58f + index * (width + 8f), -24f),
                    new Vector2(width, 38f));

                var button = GetOrCreateButton(row.transform, $"EraTabButton{index + 1:00}");
                SetRect(button.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                button.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
                ConfigureButtonColors(button);

                var label = GetOrCreateText(button.transform, "Label");
                label.text = "시대";
                label.fontSize = 15;
                label.alignment = TextAnchor.MiddleCenter;
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                row.SetActive(false);
                rows[index] = row;
                labels[index] = label;
                buttons[index] = button;
            }

            return new DetailActionRows(rows, labels, buttons);
        }

        private static DetailActionRows CreateDetailActionRows(Transform parent, string rowPrefix, string infoName, string buttonPrefix, string defaultInfo, string buttonLabel, int count)
        {
            var rows = new GameObject[count];
            var infoLabels = new Text[count];
            var buttons = new Button[count];
            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(parent, $"{rowPrefix}{index + 1:00}", typeof(RectTransform), typeof(Image));
                SetTopStretchRect(row.GetComponent<RectTransform>(), 0f, index * 84f, 36f, 76f);
                row.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

                var info = GetOrCreateText(row.transform, infoName);
                info.text = defaultInfo;
                info.fontSize = 15;
                info.alignment = TextAnchor.MiddleLeft;
                SetRect(info.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-72f, 0f), new Vector2(-176f, -12f));

                var button = GetOrCreateButton(row.transform, $"{buttonPrefix}{index + 1:00}");
                SetRect(
                    button.GetComponent<RectTransform>(),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-84f, 0f),
                    new Vector2(150f, 46f));
                button.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.58f, 1f);
                ConfigureButtonColors(button);

                var text = GetOrCreateText(button.transform, "Label");
                text.text = buttonLabel;
                text.fontSize = 15;
                text.alignment = TextAnchor.MiddleCenter;
                SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                row.SetActive(false);
                rows[index] = row;
                infoLabels[index] = info;
                buttons[index] = button;
            }

            return new DetailActionRows(rows, infoLabels, buttons);
        }

        private static NationModifierRows CreateNationModifierRows(Transform parent, int count)
        {
            var verticalLayout = GetOrAdd<VerticalLayoutGroup>(parent.gameObject);
            verticalLayout.padding = new RectOffset(0, 24, 0, 8);
            verticalLayout.spacing = 6f;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            var fitter = GetOrAdd<ContentSizeFitter>(parent.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rows = new GameObject[count];
            var summaryLabels = new Text[count];
            var detailRoots = new GameObject[count];
            var detailLabels = new Text[count];
            var expandButtons = new Button[count];
            var expandLabels = new Text[count];
            var layouts = new LayoutElement[count];
            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(parent, $"NationModifierRow{index + 1:00}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.GetComponent<Image>().color = new Color(0.045f, 0.065f, 0.09f, 1f);
                var layout = row.GetComponent<LayoutElement>();
                layout.minHeight = 52f;
                layout.preferredHeight = 52f;

                var summary = GetOrCreateText(row.transform, "SummaryLabel");
                summary.text = "통합 modifier";
                summary.fontSize = 15;
                summary.alignment = TextAnchor.MiddleLeft;
                SetRect(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -26f), new Vector2(-60f, 44f));

                var expandButton = GetOrCreateButton(row.transform, "ExpandButton");
                SetRect(expandButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(40f, 40f));
                expandButton.GetComponent<Image>().color = new Color(0.18f, 0.31f, 0.46f, 1f);
                ConfigureButtonColors(expandButton);
                var expandLabel = GetOrCreateText(expandButton.transform, "Label");
                expandLabel.text = "+";
                expandLabel.fontSize = 22;
                expandLabel.alignment = TextAnchor.MiddleCenter;
                SetRect(expandLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var detailRoot = GetOrCreateChild(row.transform, "Detail", typeof(RectTransform), typeof(Image));
                SetStretchRect(detailRoot.GetComponent<RectTransform>(), 12f, 52f, 12f, 8f);
                detailRoot.GetComponent<Image>().color = new Color(0.075f, 0.105f, 0.145f, 1f);
                var detailLabel = GetOrCreateText(detailRoot.transform, "Label");
                detailLabel.text = "세부 기여 내역";
                detailLabel.fontSize = 14;
                detailLabel.alignment = TextAnchor.UpperLeft;
                detailLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                detailLabel.verticalOverflow = VerticalWrapMode.Overflow;
                SetStretchRect(detailLabel.rectTransform, 10f, 8f, 10f, 8f);
                detailRoot.SetActive(false);
                row.SetActive(false);

                rows[index] = row;
                summaryLabels[index] = summary;
                detailRoots[index] = detailRoot;
                detailLabels[index] = detailLabel;
                expandButtons[index] = expandButton;
                expandLabels[index] = expandLabel;
                layouts[index] = layout;
            }

            return new NationModifierRows(rows, summaryLabels, detailRoots, detailLabels, expandButtons, expandLabels, layouts);
        }

        private static void AssignObjectArray<T>(SerializedObject serializedObject, string propertyName, T[] values)
            where T : UnityEngine.Object
        {
            var property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private readonly struct DetailActionRows
        {
            public DetailActionRows(GameObject[] rows, Text[] infoLabels, Button[] buttons)
            {
                Rows = rows;
                InfoLabels = infoLabels;
                Buttons = buttons;
            }

            public GameObject[] Rows { get; }
            public Text[] InfoLabels { get; }
            public Button[] Buttons { get; }
        }

        private readonly struct NationModifierRows
        {
            public NationModifierRows(
                GameObject[] rows,
                Text[] summaryLabels,
                GameObject[] detailRoots,
                Text[] detailLabels,
                Button[] expandButtons,
                Text[] expandLabels,
                LayoutElement[] layouts)
            {
                Rows = rows;
                SummaryLabels = summaryLabels;
                DetailRoots = detailRoots;
                DetailLabels = detailLabels;
                ExpandButtons = expandButtons;
                ExpandLabels = expandLabels;
                Layouts = layouts;
            }

            public GameObject[] Rows { get; }
            public Text[] SummaryLabels { get; }
            public GameObject[] DetailRoots { get; }
            public Text[] DetailLabels { get; }
            public Button[] ExpandButtons { get; }
            public Text[] ExpandLabels { get; }
            public LayoutElement[] Layouts { get; }
        }

        private readonly struct ScrollArea
        {
            public ScrollArea(GameObject root, Transform content)
            {
                Root = root;
                Content = content;
            }

            public GameObject Root { get; }
            public Transform Content { get; }
        }

        private readonly struct ResourceDetailRows
        {
            public ResourceDetailRows(
                GameObject[] rows,
                Text[] summaryLabels,
                GameObject[] producerBoxes,
                Text[] producerLabels,
                CivicTooltipTrigger[] producerTooltips)
            {
                Rows = rows;
                SummaryLabels = summaryLabels;
                ProducerBoxes = producerBoxes;
                ProducerLabels = producerLabels;
                ProducerTooltips = producerTooltips;
            }

            public GameObject[] Rows { get; }
            public Text[] SummaryLabels { get; }
            public GameObject[] ProducerBoxes { get; }
            public Text[] ProducerLabels { get; }
            public CivicTooltipTrigger[] ProducerTooltips { get; }
        }

        private readonly struct BuildingActionRows
        {
            public BuildingActionRows(
                GameObject[] rows,
                Text[] nameLabels,
                Text[] countLabels,
                Text[] costLabels,
                Text[] inputOutputLabels,
                CivicTooltipTrigger[] inputOutputTooltips,
                Text[] gdpDeltaLabels,
                Button[] buttons,
                CivicTooltipTrigger[] buttonTooltips)
            {
                Rows = rows;
                NameLabels = nameLabels;
                CountLabels = countLabels;
                CostLabels = costLabels;
                InputOutputLabels = inputOutputLabels;
                InputOutputTooltips = inputOutputTooltips;
                GdpDeltaLabels = gdpDeltaLabels;
                Buttons = buttons;
                ButtonTooltips = buttonTooltips;
            }

            public GameObject[] Rows { get; }
            public Text[] NameLabels { get; }
            public Text[] CountLabels { get; }
            public Text[] CostLabels { get; }
            public Text[] InputOutputLabels { get; }
            public CivicTooltipTrigger[] InputOutputTooltips { get; }
            public Text[] GdpDeltaLabels { get; }
            public Button[] Buttons { get; }
            public CivicTooltipTrigger[] ButtonTooltips { get; }
        }

        private static Text CreateTopText(Transform parent, string name, string text, float x)
        {
            var label = GetOrCreateText(parent, name);
            label.text = text;
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(170f, 48f));
            return label;
        }

        private static Text CreateAlertText(Transform parent, string name, string text, float x)
        {
            var label = GetOrCreateText(parent, name);
            label.text = text;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.84f, 0.35f, 1f);
            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 48f));
            return label;
        }

        private static Button CreateSidebarButton(Transform parent, string name, string label, float y)
        {
            var button = GetOrCreateButton(parent, name);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(64f, 64f));
            button.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.32f, 1f);
            var text = GetOrCreateText(button.transform, "Label");
            text.text = label;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static CivicModulePanelView CreateModulePanel(GameObject root, Button openButton, CivicTooltipView tooltipView)
        {
            var panel = GetOrCreateChild(root.transform, "ModulePanel", typeof(RectTransform), typeof(Image));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(640f, -36f), new Vector2(1040f, -140f));
            panel.GetComponent<Image>().color = new Color(0.075f, 0.10f, 0.14f, 0.995f);
            panel.transform.SetAsLastSibling();

            foreach (var legacyName in new[] { "TitleLabel", "StatusLabel", "ModuleScroll" })
            {
                var legacy = panel.transform.Find(legacyName);
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            }
            var closeButton = GetOrCreateButton(panel.transform, "CloseButton");
            SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -36f), new Vector2(100f, 48f));
            closeButton.GetComponent<Image>().color = new Color(0.24f, 0.29f, 0.36f, 1f);
            var closeLabel = GetOrCreateText(closeButton.transform, "Label");
            closeLabel.text = "닫기";
            closeLabel.fontSize = 18;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(closeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var tabRoots = new GameObject[CivicFeatureRegistry.Features.Count];
            var tabButtons = new Button[tabRoots.Length];
            var tabLabels = new Text[tabRoots.Length];
            var tabArea = GetOrCreateChild(panel.transform, "TabArea", typeof(RectTransform));
            SetRect(tabArea.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(-36f, 96f));
            for (var index = 0; index < tabRoots.Length; index++)
            {
                var tab = GetOrCreateChild(tabArea.transform, $"ModuleTab{index + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
                var column = index % 4;
                var row = index / 4;
                SetRect(tab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f + column * 250f, -26f - row * 50f), new Vector2(230f, 42f));
                tab.GetComponent<Image>().color = new Color(0.16f, 0.24f, 0.34f, 1f);
                var button = tab.GetComponent<Button>();
                button.targetGraphic = tab.GetComponent<Image>();
                ConfigureButtonColors(button);
                var label = GetOrCreateText(tab.transform, "Label");
                label.text = "모듈";
                label.fontSize = 15;
                label.alignment = TextAnchor.MiddleCenter;
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                tabRoots[index] = tab;
                tabButtons[index] = button;
                tabLabels[index] = label;
            }

            var domainPanels = new CivicDomainPanelView[CivicFeatureRegistry.Features.Count];
            for (var index = 0; index < domainPanels.Length; index++)
            {
                var definition = CivicFeatureRegistry.Features[index];
                var rowCount = definition.Id == CivicFeatureRegistry.Events ? 64
                    : definition.Id == CivicFeatureRegistry.Politics || definition.Id == CivicFeatureRegistry.GreatPeople ? 24
                    : 18;
                domainPanels[index] = CreateDomainPanel(panel.transform, definition, tooltipView, rowCount, index);
            }

            var view = GetOrAdd<CivicModulePanelView>(root);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("openButton").objectReferenceValue = openButton;
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            AssignObjectArray(serialized, "tabRoots", tabRoots);
            AssignObjectArray(serialized, "tabButtons", tabButtons);
            AssignObjectArray(serialized, "tabLabels", tabLabels);
            AssignObjectArray(serialized, "domainPanels", domainPanels);
            serialized.FindProperty("tooltipView").objectReferenceValue = tooltipView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
            return view;
        }

        private static CivicHudOverlayView CreateHudOverlayView(GameObject root, CivicTooltipView tooltipView)
        {
            var eventAlertButton = GetOrCreateButton(root.transform, "EventAlertButton");
            SetRect(eventAlertButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(220f, 46f));
            eventAlertButton.GetComponent<Image>().color = new Color(0.55f, 0.24f, 0.18f, 1f);
            var eventAlertLabel = GetOrCreateText(eventAlertButton.transform, "Label");
            eventAlertLabel.text = "이벤트";
            eventAlertLabel.fontSize = 18;
            eventAlertLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(eventAlertLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var exitPopup = CreateModalPanel(root.transform, "ExitPopup", new Vector2(520f, 300f));
            var exitTitle = GetOrCreateText(exitPopup.transform, "TitleLabel");
            exitTitle.text = "게임 메뉴";
            exitTitle.fontSize = 30;
            exitTitle.alignment = TextAnchor.MiddleCenter;
            SetRect(exitTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -54f), new Vector2(-40f, 60f));
            var exitNotice = GetOrCreateText(exitPopup.transform, "NoticeLabel");
            exitNotice.text = "메인 메뉴로 이동하면 현재 런은 종료됩니다.";
            exitNotice.fontSize = 18;
            exitNotice.alignment = TextAnchor.MiddleCenter;
            SetRect(exitNotice.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, 32f), new Vector2(-40f, 60f));
            var continueButton = CreateModalButton(exitPopup.transform, "ContinueButton", "계속 플레이", new Vector2(-125f, -86f), new Vector2(220f, 58f));
            var mainMenuButton = CreateModalButton(exitPopup.transform, "MainMenuButton", "메인 메뉴로", new Vector2(125f, -86f), new Vector2(220f, 58f));

            var eventPopup = CreateModalPanel(root.transform, "EventPopup", new Vector2(860f, 580f));
            var eventTitle = GetOrCreateText(eventPopup.transform, "TitleLabel");
            eventTitle.text = "이벤트";
            eventTitle.fontSize = 30;
            eventTitle.alignment = TextAnchor.MiddleLeft;
            SetRect(eventTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -48f), new Vector2(-110f, 58f));
            var eventCloseButton = CreateModalButton(eventPopup.transform, "CloseButton", "닫기", new Vector2(370f, 244f), new Vector2(100f, 46f));
            var eventCause = GetOrCreateText(eventPopup.transform, "CauseLabel");
            eventCause.text = "발생 원인";
            eventCause.fontSize = 16;
            eventCause.color = new Color(0.74f, 0.80f, 0.88f, 1f);
            eventCause.alignment = TextAnchor.MiddleLeft;
            SetRect(eventCause.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -100f), new Vector2(-64f, 40f));
            var eventDescription = GetOrCreateText(eventPopup.transform, "DescriptionLabel");
            eventDescription.text = "이벤트 설명";
            eventDescription.fontSize = 19;
            eventDescription.alignment = TextAnchor.UpperLeft;
            SetRect(eventDescription.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(32f, -154f), new Vector2(-64f, -156f));
            var eventChoiceButtons = new Button[3];
            var eventChoiceLabels = new Text[3];
            var eventChoiceTooltips = new CivicTooltipTrigger[3];
            for (var index = 0; index < 3; index++)
            {
                var button = CreateModalButton(eventPopup.transform, $"ChoiceButton{index + 1:00}", $"선택지 {index + 1}", new Vector2(0f, 70f - index * 82f), new Vector2(780f, 64f));
                var trigger = GetOrAdd<CivicTooltipTrigger>(button.gameObject);
                trigger.AssignTooltipView(tooltipView);
                eventChoiceButtons[index] = button;
                eventChoiceLabels[index] = button.transform.Find("Label").GetComponent<Text>();
                eventChoiceTooltips[index] = trigger;
            }

            var debugPanel = CreateModalPanel(root.transform, "DebugPanel", new Vector2(980f, 720f));
            DestroyChildIfExists(debugPanel.transform, "DescriptionLabel");
            var debugTitle = GetOrCreateText(debugPanel.transform, "TitleLabel");
            debugTitle.text = "DEBUG · Backquote(`)";
            debugTitle.fontSize = 28;
            debugTitle.alignment = TextAnchor.MiddleLeft;
            SetRect(debugTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -48f), new Vector2(-120f, 56f));
            var debugCloseButton = CreateModalButton(debugPanel.transform, "CloseButton", "닫기", new Vector2(430f, 320f), new Vector2(100f, 44f));

            var debugGrantResourcesButton = CreateModalButton(debugPanel.transform, "GrantResourcesButton", "해금 자원 +9999", new Vector2(-310f, 235f), new Vector2(280f, 52f));
            var debugResearchAllButton = CreateModalButton(debugPanel.transform, "ResearchAllButton", "모든 기술 연구", new Vector2(0f, 235f), new Vector2(280f, 52f));
            var debugGrantPrestigeButton = CreateModalButton(debugPanel.transform, "GrantPrestigeButton", "환생 포인트 +9999", new Vector2(310f, 235f), new Vector2(280f, 52f));

            var instantRow = GetOrCreateChild(debugPanel.transform, "InstantActionsRow", typeof(RectTransform));
            SetRect(instantRow.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 172f), new Vector2(900f, 44f));
            var instantToggleObject = GetOrCreateChild(instantRow.transform, "Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            SetRect(instantToggleObject.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(30f, 30f));
            var instantToggleImage = instantToggleObject.GetComponent<Image>();
            instantToggleImage.color = new Color(0.16f, 0.24f, 0.34f, 1f);
            var instantCheckmark = GetOrCreateChild(instantToggleObject.transform, "Checkmark", typeof(RectTransform), typeof(Image));
            SetStretchRect(instantCheckmark.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);
            var instantCheckmarkImage = instantCheckmark.GetComponent<Image>();
            instantCheckmarkImage.color = new Color(0.45f, 0.78f, 0.95f, 1f);
            var instantToggle = instantToggleObject.GetComponent<Toggle>();
            instantToggle.targetGraphic = instantToggleImage;
            instantToggle.graphic = instantCheckmarkImage;
            var instantLabel = GetOrCreateText(instantRow.transform, "Label");
            instantLabel.text = "사회구조/체계 · 국가 · 불가사의 즉시 적용/완공";
            instantLabel.fontSize = 17;
            instantLabel.alignment = TextAnchor.MiddleLeft;
            SetStretchRect(instantLabel.rectTransform, 48f, 0f, 8f, 0f);

            var previousDomain = CreateModalButton(debugPanel.transform, "PreviousDomainButton", "◀", new Vector2(-390f, 102f), new Vector2(56f, 48f));
            var nextDomain = CreateModalButton(debugPanel.transform, "NextDomainButton", "▶", new Vector2(390f, 102f), new Vector2(56f, 48f));
            var domainLabel = GetOrCreateText(debugPanel.transform, "DomainLabel");
            domainLabel.text = "모듈";
            domainLabel.fontSize = 22;
            domainLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(domainLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 102f), new Vector2(700f, 48f));
            var previousTarget = CreateModalButton(debugPanel.transform, "PreviousTargetButton", "◀", new Vector2(-390f, 38f), new Vector2(56f, 48f));
            var nextTarget = CreateModalButton(debugPanel.transform, "NextTargetButton", "▶", new Vector2(390f, 38f), new Vector2(56f, 48f));
            var targetLabel = GetOrCreateText(debugPanel.transform, "TargetLabel");
            targetLabel.text = "대상";
            targetLabel.fontSize = 20;
            targetLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(targetLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(700f, 48f));
            var debugDescriptionBox = GetOrCreateChild(debugPanel.transform, "DescriptionBox", typeof(RectTransform), typeof(Image));
            SetRect(debugDescriptionBox.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -85f), new Vector2(900f, 150f));
            debugDescriptionBox.GetComponent<Image>().color = new Color(0.075f, 0.105f, 0.145f, 1f);
            var debugDescription = GetOrCreateText(debugDescriptionBox.transform, "DescriptionLabel");
            debugDescription.text = "활성 모듈의 테스트 상태를 강제로 준비합니다.";
            debugDescription.fontSize = 17;
            debugDescription.alignment = TextAnchor.UpperLeft;
            debugDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            debugDescription.verticalOverflow = VerticalWrapMode.Truncate;
            SetStretchRect(debugDescription.rectTransform, 18f, 14f, 18f, 14f);
            var debugActionButton = CreateModalButton(debugPanel.transform, "ActionButton", "실행", new Vector2(0f, -285f), new Vector2(360f, 58f));
            var debugActionLabel = debugActionButton.transform.Find("Label").GetComponent<Text>();

            var view = GetOrAdd<CivicHudOverlayView>(root);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("exitPopupRoot").objectReferenceValue = exitPopup;
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            serialized.FindProperty("eventAlertButton").objectReferenceValue = eventAlertButton;
            serialized.FindProperty("eventAlertLabel").objectReferenceValue = eventAlertLabel;
            serialized.FindProperty("eventPopupRoot").objectReferenceValue = eventPopup;
            serialized.FindProperty("eventTitleLabel").objectReferenceValue = eventTitle;
            serialized.FindProperty("eventDescriptionLabel").objectReferenceValue = eventDescription;
            serialized.FindProperty("eventCauseLabel").objectReferenceValue = eventCause;
            serialized.FindProperty("eventCloseButton").objectReferenceValue = eventCloseButton;
            AssignObjectArray(serialized, "eventChoiceButtons", eventChoiceButtons);
            AssignObjectArray(serialized, "eventChoiceLabels", eventChoiceLabels);
            AssignObjectArray(serialized, "eventChoiceTooltips", eventChoiceTooltips);
            serialized.FindProperty("debugPanelRoot").objectReferenceValue = debugPanel;
            serialized.FindProperty("debugPreviousDomainButton").objectReferenceValue = previousDomain;
            serialized.FindProperty("debugNextDomainButton").objectReferenceValue = nextDomain;
            serialized.FindProperty("debugDomainLabel").objectReferenceValue = domainLabel;
            serialized.FindProperty("debugPreviousTargetButton").objectReferenceValue = previousTarget;
            serialized.FindProperty("debugNextTargetButton").objectReferenceValue = nextTarget;
            serialized.FindProperty("debugTargetLabel").objectReferenceValue = targetLabel;
            serialized.FindProperty("debugDescriptionLabel").objectReferenceValue = debugDescription;
            serialized.FindProperty("debugActionButton").objectReferenceValue = debugActionButton;
            serialized.FindProperty("debugActionLabel").objectReferenceValue = debugActionLabel;
            serialized.FindProperty("debugCloseButton").objectReferenceValue = debugCloseButton;
            serialized.FindProperty("debugGrantResourcesButton").objectReferenceValue = debugGrantResourcesButton;
            serialized.FindProperty("debugResearchAllButton").objectReferenceValue = debugResearchAllButton;
            serialized.FindProperty("debugGrantPrestigeButton").objectReferenceValue = debugGrantPrestigeButton;
            serialized.FindProperty("debugInstantActionsToggle").objectReferenceValue = instantToggle;
            serialized.FindProperty("debugInstantActionsLabel").objectReferenceValue = instantLabel;
            serialized.FindProperty("tooltipView").objectReferenceValue = tooltipView;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            eventAlertButton.gameObject.SetActive(false);
            exitPopup.SetActive(false);
            eventPopup.SetActive(false);
            debugPanel.SetActive(false);
            exitPopup.transform.SetAsLastSibling();
            eventPopup.transform.SetAsLastSibling();
            debugPanel.transform.SetAsLastSibling();
            return view;
        }

        private static GameObject CreateModalPanel(Transform parent, string name, Vector2 size)
        {
            var panel = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Image));
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.105f, 0.995f);
            return panel;
        }

        private static Button CreateModalButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var button = GetOrCreateButton(parent, name);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            button.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.58f, 1f);
            ConfigureButtonColors(button);
            var text = GetOrCreateText(button.transform, "Label");
            text.text = label;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static CivicDomainPanelView CreateDomainPanel(
            Transform parent,
            CivicFeatureDefinition definition,
            CivicTooltipView tooltipView,
            int rowCount,
            int domainIndex)
        {
            var root = GetOrCreateChild(parent, $"DomainPanel{domainIndex + 1:00}", typeof(RectTransform), typeof(Image));
            SetStretchRect(root.GetComponent<RectTransform>(), 18f, 205f, 18f, 18f);
            root.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.105f, 0.98f);

            var title = GetOrCreateText(root.transform, "TitleLabel");
            title.text = definition.DisplayName;
            title.fontSize = 26;
            title.alignment = TextAnchor.MiddleLeft;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -28f), new Vector2(-36f, 44f));
            var status = GetOrCreateText(root.transform, "StatusLabel");
            status.text = "도메인 상태";
            status.fontSize = 15;
            status.color = new Color(0.70f, 0.78f, 0.88f, 1f);
            status.alignment = TextAnchor.MiddleLeft;
            SetRect(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -66f), new Vector2(-36f, 30f));

            GameObject categoryTabRoot = null;
            var categoryTabRows = Array.Empty<GameObject>();
            var categoryTabButtons = Array.Empty<Button>();
            var categoryTabLabels = Array.Empty<Text>();
            if (definition.Id == CivicFeatureRegistry.Politics || definition.Id == CivicFeatureRegistry.GreatPeople)
            {
                var categoryTabCapacity = definition.Id == CivicFeatureRegistry.Politics ? 8 : 2;
                categoryTabRoot = GetOrCreateChild(root.transform, "CategoryTabArea", typeof(RectTransform));
                SetRect(categoryTabRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -116f), new Vector2(-36f, 52f));
                categoryTabRows = new GameObject[categoryTabCapacity];
                categoryTabButtons = new Button[categoryTabCapacity];
                categoryTabLabels = new Text[categoryTabCapacity];
                for (var index = 0; index < categoryTabCapacity; index++)
                {
                    var tab = GetOrCreateChild(categoryTabRoot.transform, $"CategoryTab{index + 1:00}", typeof(RectTransform), typeof(Image), typeof(Button));
                    SetRect(tab.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(98f + index * 196f, 0f), new Vector2(188f, 46f));
                    tab.GetComponent<Image>().color = new Color(0.18f, 0.29f, 0.42f, 1f);
                    var button = tab.GetComponent<Button>();
                    button.targetGraphic = tab.GetComponent<Image>();
                    ConfigureButtonColors(button);
                    var label = GetOrCreateText(tab.transform, "Label");
                    label.text = definition.Id == CivicFeatureRegistry.Politics ? "정치 그룹" : "위인 탭";
                    label.fontSize = 13;
                    label.alignment = TextAnchor.MiddleCenter;
                    SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    categoryTabRows[index] = tab;
                    categoryTabButtons[index] = button;
                    categoryTabLabels[index] = label;
                }
            }

            GameObject impossibleFilterRoot = null;
            Toggle impossibleFilterToggle = null;
            Text impossibleFilterLabel = null;
            if (definition.Id == CivicFeatureRegistry.NationFormation)
            {
                impossibleFilterRoot = GetOrCreateChild(root.transform, "ImpossibleFilter", typeof(RectTransform));
                SetRect(impossibleFilterRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -116f), new Vector2(-36f, 44f));
                var toggleObject = GetOrCreateChild(impossibleFilterRoot.transform, "Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
                SetRect(toggleObject.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(28f, 28f));
                var toggleImage = toggleObject.GetComponent<Image>();
                toggleImage.color = new Color(0.16f, 0.24f, 0.34f, 1f);
                var checkmark = GetOrCreateChild(toggleObject.transform, "Checkmark", typeof(RectTransform), typeof(Image));
                SetStretchRect(checkmark.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);
                var checkmarkImage = checkmark.GetComponent<Image>();
                checkmarkImage.color = new Color(0.45f, 0.78f, 0.95f, 1f);
                impossibleFilterToggle = toggleObject.GetComponent<Toggle>();
                impossibleFilterToggle.targetGraphic = toggleImage;
                impossibleFilterToggle.graphic = checkmarkImage;
                impossibleFilterLabel = GetOrCreateText(impossibleFilterRoot.transform, "Label");
                impossibleFilterLabel.text = "달성불가 조건의 문명·국가도 표시";
                impossibleFilterLabel.fontSize = 15;
                impossibleFilterLabel.alignment = TextAnchor.MiddleLeft;
                SetStretchRect(impossibleFilterLabel.rectTransform, 52f, 0f, 12f, 0f);
            }

            GameObject filterRoot = null;
            var filterRows = Array.Empty<GameObject>();
            var filterToggles = Array.Empty<Toggle>();
            var filterLabels = Array.Empty<Text>();
            if (definition.Id == CivicFeatureRegistry.GreatPeople)
            {
                const int filterCount = 3;
                filterRoot = GetOrCreateChild(root.transform, "FilterArea", typeof(RectTransform));
                SetRect(filterRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -168f), new Vector2(-36f, 44f));
                filterRows = new GameObject[filterCount];
                filterToggles = new Toggle[filterCount];
                filterLabels = new Text[filterCount];
                for (var index = 0; index < filterCount; index++)
                {
                    var filterRow = GetOrCreateChild(filterRoot.transform, $"Filter{index + 1:00}", typeof(RectTransform));
                    SetRect(filterRow.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f + index * 310f, 0f), new Vector2(300f, 38f));
                    var toggleObject = GetOrCreateChild(filterRow.transform, "Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
                    SetRect(toggleObject.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
                    var toggleImage = toggleObject.GetComponent<Image>();
                    toggleImage.color = new Color(0.16f, 0.24f, 0.34f, 1f);
                    var checkmark = GetOrCreateChild(toggleObject.transform, "Checkmark", typeof(RectTransform), typeof(Image));
                    SetStretchRect(checkmark.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);
                    var checkmarkImage = checkmark.GetComponent<Image>();
                    checkmarkImage.color = new Color(0.45f, 0.78f, 0.95f, 1f);
                    var toggle = toggleObject.GetComponent<Toggle>();
                    toggle.targetGraphic = toggleImage;
                    toggle.graphic = checkmarkImage;
                    toggle.isOn = true;
                    var label = GetOrCreateText(filterRow.transform, "Label");
                    label.text = "위인 필터";
                    label.fontSize = 14;
                    label.alignment = TextAnchor.MiddleLeft;
                    SetStretchRect(label.rectTransform, 42f, 0f, 4f, 0f);
                    filterRows[index] = filterRow;
                    filterToggles[index] = toggle;
                    filterLabels[index] = label;
                }
                filterRoot.SetActive(false);
            }

            var usesDescriptionCards = definition.Id == CivicFeatureRegistry.StartCivilizations ||
                definition.Id == CivicFeatureRegistry.NationFormation ||
                definition.Id == CivicFeatureRegistry.Politics ||
                definition.Id == CivicFeatureRegistry.Wonders ||
                definition.Id == CivicFeatureRegistry.GreatPeople;
            var usesPersonChoices = definition.Id == CivicFeatureRegistry.GreatPeople;
            var rowStride = usesPersonChoices ? 208f : usesDescriptionCards ? 160f : 72f;
            var controlsTopOffset = filterRoot != null ? 210f : categoryTabRoot != null || impossibleFilterRoot != null ? 150f : 92f;
            var scroll = CreateScrollArea(root.transform, "DomainScroll", rowCount * rowStride, controlsTopOffset);
            var rows = new CivicModuleActionRow[rowCount];
            var expectedNames = new HashSet<string>(Enumerable.Range(1, rowCount).Select(index => $"DomainActionRow{index:00}"));
            for (var index = scroll.Content.childCount - 1; index >= 0; index--)
            {
                var child = scroll.Content.GetChild(index);
                if (child.name.StartsWith("DomainActionRow", StringComparison.Ordinal) && !expectedNames.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            for (var index = 0; index < rowCount; index++)
            {
                var row = GetOrCreateChild(scroll.Content, $"DomainActionRow{index + 1:00}", typeof(RectTransform), typeof(Image));
                SetTopStretchRect(row.GetComponent<RectTransform>(), 0f, index * rowStride, 0f, usesPersonChoices ? 200f : usesDescriptionCards ? 152f : 64f);
                row.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.08f, 1f);
                var info = GetOrCreateText(row.transform, "InfoLabel");
                info.text = definition.DisplayName + " 항목";
                info.fontSize = 16;
                info.alignment = TextAnchor.MiddleLeft;
                SetStretchRect(info.rectTransform, 14f, 6f, 200f, usesPersonChoices ? 142f : usesDescriptionCards ? 94f : 6f);
                var action = GetOrCreateButton(row.transform, "ActionButton");
                SetRect(
                    action.GetComponent<RectTransform>(),
                    usesDescriptionCards ? new Vector2(1f, 1f) : new Vector2(1f, 0.5f),
                    usesDescriptionCards ? new Vector2(1f, 1f) : new Vector2(1f, 0.5f),
                    usesDescriptionCards ? new Vector2(-92f, -29f) : new Vector2(-92f, 0f),
                    new Vector2(160f, 48f));
                action.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.58f, 1f);
                var actionLabel = GetOrCreateText(action.transform, "Label");
                actionLabel.text = "실행";
                actionLabel.fontSize = 17;
                actionLabel.alignment = TextAnchor.MiddleCenter;
                SetRect(actionLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var tooltip = GetOrAdd<CivicTooltipTrigger>(action.gameObject);
                tooltip.AssignTooltipView(tooltipView);
                var descriptionRoot = GetOrCreateChild(row.transform, "Description", typeof(RectTransform), typeof(Image));
                SetStretchRect(descriptionRoot.GetComponent<RectTransform>(), 0f, 64f, 0f, 0f);
                descriptionRoot.GetComponent<Image>().color = new Color(0.075f, 0.105f, 0.145f, 1f);
                var descriptionLabel = GetOrCreateText(descriptionRoot.transform, "Label");
                descriptionLabel.text = definition.DisplayName + " 설명과 효과";
                descriptionLabel.fontSize = 14;
                descriptionLabel.alignment = TextAnchor.UpperLeft;
                descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                descriptionLabel.verticalOverflow = VerticalWrapMode.Truncate;
                SetStretchRect(descriptionLabel.rectTransform, 12f, 7f, 12f, 7f);
                descriptionRoot.SetActive(usesDescriptionCards);

                GameObject choiceRoot = null;
                var choiceButtons = Array.Empty<Button>();
                var choiceLabels = Array.Empty<Text>();
                var choiceTooltips = Array.Empty<CivicTooltipTrigger>();
                if (usesPersonChoices)
                {
                    const int choiceCount = 4;
                    choiceRoot = GetOrCreateChild(row.transform, "PositionChoices", typeof(RectTransform));
                    SetRect(choiceRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(-16f, 40f));
                    choiceButtons = new Button[choiceCount];
                    choiceLabels = new Text[choiceCount];
                    choiceTooltips = new CivicTooltipTrigger[choiceCount];
                    for (var choiceIndex = 0; choiceIndex < choiceCount; choiceIndex++)
                    {
                        var choice = GetOrCreateButton(choiceRoot.transform, $"PositionButton{choiceIndex + 1:00}");
                        SetRect(choice.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(114f + choiceIndex * 230f, 0f), new Vector2(220f, 36f));
                        choice.GetComponent<Image>().color = new Color(0.18f, 0.29f, 0.42f, 1f);
                        var choiceLabel = GetOrCreateText(choice.transform, "Label");
                        choiceLabel.text = "직책";
                        choiceLabel.fontSize = 13;
                        choiceLabel.alignment = TextAnchor.MiddleCenter;
                        SetRect(choiceLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        var choiceTooltip = GetOrAdd<CivicTooltipTrigger>(choice.gameObject);
                        choiceTooltip.AssignTooltipView(tooltipView);
                        choiceButtons[choiceIndex] = choice;
                        choiceLabels[choiceIndex] = choiceLabel;
                        choiceTooltips[choiceIndex] = choiceTooltip;
                    }
                    choiceRoot.SetActive(false);
                }
                var component = GetOrAdd<CivicModuleActionRow>(row);
                var serializedRow = new SerializedObject(component);
                serializedRow.FindProperty("infoLabel").objectReferenceValue = info;
                serializedRow.FindProperty("descriptionRoot").objectReferenceValue = descriptionRoot;
                serializedRow.FindProperty("descriptionLabel").objectReferenceValue = descriptionLabel;
                serializedRow.FindProperty("actionButton").objectReferenceValue = action;
                serializedRow.FindProperty("actionLabel").objectReferenceValue = actionLabel;
                serializedRow.FindProperty("tooltip").objectReferenceValue = tooltip;
                serializedRow.FindProperty("choiceRoot").objectReferenceValue = choiceRoot;
                AssignObjectArray(serializedRow, "choiceButtons", choiceButtons);
                AssignObjectArray(serializedRow, "choiceLabels", choiceLabels);
                AssignObjectArray(serializedRow, "choiceTooltips", choiceTooltips);
                serializedRow.ApplyModifiedPropertiesWithoutUndo();
                rows[index] = component;
            }

            var view = GetOrAdd<CivicDomainPanelView>(root);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("featureId").stringValue = definition.Id;
            serialized.FindProperty("panelRoot").objectReferenceValue = root;
            serialized.FindProperty("titleLabel").objectReferenceValue = title;
            serialized.FindProperty("statusLabel").objectReferenceValue = status;
            AssignObjectArray(serialized, "rows", rows);
            serialized.FindProperty("categoryTabRoot").objectReferenceValue = categoryTabRoot;
            AssignObjectArray(serialized, "categoryTabRows", categoryTabRows);
            AssignObjectArray(serialized, "categoryTabButtons", categoryTabButtons);
            AssignObjectArray(serialized, "categoryTabLabels", categoryTabLabels);
            serialized.FindProperty("impossibleFilterRoot").objectReferenceValue = impossibleFilterRoot;
            serialized.FindProperty("impossibleFilterToggle").objectReferenceValue = impossibleFilterToggle;
            serialized.FindProperty("impossibleFilterLabel").objectReferenceValue = impossibleFilterLabel;
            serialized.FindProperty("filterRoot").objectReferenceValue = filterRoot;
            AssignObjectArray(serialized, "filterRows", filterRows);
            AssignObjectArray(serialized, "filterToggles", filterToggles);
            AssignObjectArray(serialized, "filterLabels", filterLabels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(domainIndex == 0);
            return view;
        }

        private static Button CreateActionButton(Transform parent, string name, string label, Vector2 position)
        {
            var button = GetOrCreateButton(parent, name);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(180f, 56f));
            button.GetComponent<Image>().color = new Color(0.20f, 0.36f, 0.58f, 1f);
            ConfigureButtonColors(button);
            var text = GetOrCreateText(button.transform, "Label");
            text.text = label;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Text GetOrCreateText(Transform parent, string name)
        {
            var child = GetOrCreateChild(parent, name, typeof(RectTransform));
            var text = GetOrAdd<Text>(child);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private static Button GetOrCreateButton(Transform parent, string name)
        {
            var child = GetOrCreateChild(parent, name, typeof(RectTransform));
            var image = GetOrAdd<Image>(child);
            var button = GetOrAdd<Button>(child);
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            return button;
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 0.96f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.28f, 0.31f, 0.35f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
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

        private static void DestroyChildIfExists(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
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

        private static void SetLeftRect(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void SetRightRect(RectTransform rect, float right, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-right, 0f);
            rect.sizeDelta = new Vector2(width, height);
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
