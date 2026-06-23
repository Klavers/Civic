using System;
using System.Linq;
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

                var detailPanel = GetOrCreateChild(root.transform, "LeftDetailPanel", typeof(RectTransform), typeof(Image));
                SetRect(detailPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(640f, -36f), new Vector2(1040f, -140f));
                detailPanel.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.17f, 0.96f);
                var detailTitle = GetOrCreateText(detailPanel.transform, "DetailTitleLabel");
                detailTitle.text = "자원 상세";
                detailTitle.fontSize = 26;
                detailTitle.alignment = TextAnchor.MiddleLeft;
                SetRect(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -34f), new Vector2(-32f, 50f));
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

                var resourcePanel = GetOrCreateChild(detailPanel.transform, "ResourceDetailPanel", typeof(RectTransform));
                SetStretchRect(resourcePanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                var resourceScroll = CreateScrollArea(resourcePanel.transform, "ResourceScroll", resourceRowCount * 150f);
                var resourceRows = CreateResourceDetailRows(resourceScroll.Content, tooltipView, resourceRowCount, 4);

                var buildingPanel = GetOrCreateChild(detailPanel.transform, "BuildingDetailPanel", typeof(RectTransform));
                SetStretchRect(buildingPanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                CreateBuildingHeaderRow(buildingPanel.transform);
                var buildingScroll = CreateScrollArea(buildingPanel.transform, "BuildingScroll", 8 * 56f, 42f);
                var buildingActionRows = CreateBuildingActionRows(buildingScroll.Content, tooltipView, 8);

                var technologyPanel = GetOrCreateChild(detailPanel.transform, "TechnologyDetailPanel", typeof(RectTransform));
                SetStretchRect(technologyPanel.GetComponent<RectTransform>(), 16f, 86f, 16f, 18f);
                var technologyScroll = CreateScrollArea(technologyPanel.transform, "TechnologyScroll", 8 * 64f);
                var technologyActionRows = CreateDetailActionRows(technologyScroll.Content, "TechnologyActionRow", "TechnologyInfoLabel", "TechnologyResearchButton", "기술 정보", "연구", 8);

                var rightPanel = GetOrCreateChild(root.transform, "RightResourcePanel", typeof(RectTransform), typeof(Image));
                SetRect(rightPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-190f, -36f), new Vector2(360f, -140f));
                rightPanel.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.14f, 0.96f);
                var rightResources = GetOrCreateText(rightPanel.transform, "RightResourcesLabel");
                rightResources.text = "자원 요약";
                rightResources.fontSize = 18;
                rightResources.alignment = TextAnchor.UpperLeft;
                SetRect(rightResources.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, -48f), new Vector2(-36f, -96f));
                var foodToggleButton = CreateActionButton(rightPanel.transform, "FoodToggleButton", "식량 펼침", new Vector2(0f, 34f));

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
                viewObject.FindProperty("resourceDetailPanel").objectReferenceValue = resourcePanel;
                viewObject.FindProperty("buildingDetailPanel").objectReferenceValue = buildingPanel;
                viewObject.FindProperty("technologyDetailPanel").objectReferenceValue = technologyPanel;
                viewObject.FindProperty("resourcesPanelButton").objectReferenceValue = resourcesButton;
                viewObject.FindProperty("buildingsPanelButton").objectReferenceValue = buildingsButton;
                viewObject.FindProperty("technologiesPanelButton").objectReferenceValue = technologiesButton;
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
                AssignObjectArray(viewObject, "technologyActionRows", technologyActionRows.Rows);
                AssignObjectArray(viewObject, "technologyActionInfoLabels", technologyActionRows.InfoLabels);
                AssignObjectArray(viewObject, "technologyActionButtons", technologyActionRows.Buttons);
                viewObject.FindProperty("foodToggleButton").objectReferenceValue = foodToggleButton;
                viewObject.FindProperty("tooltipView").objectReferenceValue = tooltipView;
                viewObject.ApplyModifiedPropertiesWithoutUndo();

                var controller = GetOrAdd<CivicHudController>(root);
                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("view").objectReferenceValue = view;
                controllerObject.FindProperty("dataSource").objectReferenceValue = dataSource;
                controllerObject.FindProperty("simulationSpeed").doubleValue = 1d;
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
            tooltipObject.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.96f);

            var label = GetOrCreateText(tooltipObject.transform, "BodyLabel");
            label.text = "Tooltip";
            label.fontSize = 15;
            label.alignment = TextAnchor.UpperLeft;
            SetStretchRect(label.rectTransform, 12f, 10f, 12f, 10f);

            var tooltip = GetOrAdd<CivicTooltipView>(tooltipObject);
            var tooltipSerialized = new SerializedObject(tooltip);
            tooltipSerialized.FindProperty("panel").objectReferenceValue = tooltipRect;
            tooltipSerialized.FindProperty("bodyLabel").objectReferenceValue = label;
            tooltipSerialized.ApplyModifiedPropertiesWithoutUndo();
            tooltipObject.SetActive(false);
            return tooltip;
        }

        private static DetailActionRows CreateDetailActionRows(Transform parent, string rowPrefix, string infoName, string buttonPrefix, string defaultInfo, string buttonLabel, int count)
        {
            var rows = new GameObject[count];
            var infoLabels = new Text[count];
            var buttons = new Button[count];
            for (var index = 0; index < count; index++)
            {
                var row = GetOrCreateChild(parent, $"{rowPrefix}{index + 1:00}", typeof(RectTransform), typeof(Image));
                SetRect(
                    row.GetComponent<RectTransform>(),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -118f - index * 64f),
                    new Vector2(-36f, 56f));
                row.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 1f);

                var info = GetOrCreateText(row.transform, infoName);
                info.text = defaultInfo;
                info.fontSize = 17;
                info.alignment = TextAnchor.MiddleLeft;
                SetRect(info.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-72f, 0f), new Vector2(-176f, -10f));

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
