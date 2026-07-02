using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Civic.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicHudView : MonoBehaviour
    {
        private const int BuildingInputOutputVisibleItems = 3;
        private const int ResourceProducerSlotsPerRowFallback = 4;
        private const string PopulationId = "population";
        private const string ScienceId = "science";
        private const string TreasuryId = "treasury";
        private const string ConstructionPowerId = "construction_power";
        private const string CapitalBuildingId = "capital";
        private const double ShortageTintStartDemandSupplyRatio = 1.5d;
        private const double ShortageTintFullDemandSupplyRatio = 4d;
        private const double SurplusTintStartDemandSupplyRatio = 0.75d;
        private const double SurplusTintFullDemandSupplyRatio = 0.25d;
        private static readonly Color NeutralMarketColor = Color.white;
        private static readonly Color ShortageMarketColor = Color.red;
        private static readonly Color SurplusMarketColor = Color.yellow;

        [SerializeField] private Text populationLabel;
        [SerializeField] private Text buildingCapacityLabel;
        [SerializeField] private Text gdpLabel;
        [SerializeField] private Text treasuryLabel;
        [SerializeField] private Text constructionLabel;
        [SerializeField] private Text scienceLabel;
        [SerializeField] private Text eraLabel;
        [SerializeField] private Text shortageAlertLabel;
        [SerializeField] private Text researchAlertLabel;
        [SerializeField] private Text constructionAlertLabel;
        [SerializeField] private Text rightResourcesLabel;
        [SerializeField] private Text detailTitleLabel;
        [SerializeField] private Text detailBodyLabel;
        [SerializeField] private GameObject detailPanelRoot;
        [SerializeField] private Button detailCloseButton;
        [SerializeField] private GameObject resourceDetailPanel;
        [SerializeField] private GameObject buildingDetailPanel;
        [SerializeField] private GameObject technologyDetailPanel;
        [SerializeField] private GameObject nationDetailPanel;
        [SerializeField] private Button resourcesPanelButton;
        [SerializeField] private Button buildingsPanelButton;
        [SerializeField] private Button technologiesPanelButton;
        [SerializeField] private Button nationPanelButton;
        [SerializeField] private GameObject[] resourceDetailRows;
        [SerializeField] private Text[] resourceSummaryLabels;
        [SerializeField] private GameObject[] resourceProducerBoxes;
        [SerializeField] private Text[] resourceProducerLabels;
        [SerializeField] private CivicTooltipTrigger[] resourceProducerTooltips;
        [SerializeField] private GameObject[] buildingActionRows;
        [SerializeField] private Text[] buildingActionInfoLabels;
        [SerializeField] private Text[] buildingCountLabels;
        [SerializeField] private Text[] buildingCostLabels;
        [SerializeField] private Text[] buildingInputOutputLabels;
        [SerializeField] private CivicTooltipTrigger[] buildingInputOutputTooltips;
        [SerializeField] private Text[] buildingGdpDeltaLabels;
        [SerializeField] private Button[] buildingActionButtons;
        [SerializeField] private CivicTooltipTrigger[] buildingButtonTooltips;
        [SerializeField] private GameObject[] eraTabRows;
        [SerializeField] private Text[] eraTabLabels;
        [SerializeField] private Button[] eraTabButtons;
        [SerializeField] private GameObject[] technologyActionRows;
        [SerializeField] private Text[] technologyActionInfoLabels;
        [SerializeField] private Button[] technologyActionButtons;
        [SerializeField] private Text nationStatusLabel;
        [SerializeField] private GameObject[] nationModifierRows;
        [SerializeField] private Text[] nationModifierSummaryLabels;
        [SerializeField] private GameObject[] nationModifierDetailRoots;
        [SerializeField] private Text[] nationModifierDetailLabels;
        [SerializeField] private Button[] nationModifierExpandButtons;
        [SerializeField] private Text[] nationModifierExpandLabels;
        [SerializeField] private LayoutElement[] nationModifierLayouts;
        [SerializeField] private Button foodToggleButton;
        [SerializeField] private CivicTooltipView tooltipView;

        private string[] currentBuildingActionIds = Array.Empty<string>();
        private string[] currentEraTabIds = Array.Empty<string>();
        private string[] currentTechnologyActionIds = Array.Empty<string>();
        private IReadOnlyList<CivicNationModifierGroup> currentNationModifiers = Array.Empty<CivicNationModifierGroup>();
        private string currentNationName = string.Empty;
        private readonly HashSet<string> expandedNationModifierKeys = new HashSet<string>(StringComparer.Ordinal);

        public event Action ResourcesPanelRequested;
        public event Action BuildingsPanelRequested;
        public event Action TechnologiesPanelRequested;
        public event Action NationPanelRequested;
        public event Action DetailPanelCloseRequested;
        public event Action<string> BuildRequested;
        public event Action<string> ResearchRequested;
        public event Action<string> EraTabRequested;
        public event Action FoodToggleRequested;

        public bool HasRequiredReferences =>
            populationLabel != null &&
            buildingCapacityLabel != null &&
            gdpLabel != null &&
            treasuryLabel != null &&
            constructionLabel != null &&
            scienceLabel != null &&
            eraLabel != null &&
            shortageAlertLabel != null &&
            researchAlertLabel != null &&
            constructionAlertLabel != null &&
            rightResourcesLabel != null &&
            detailTitleLabel != null &&
            detailBodyLabel != null &&
            detailPanelRoot != null &&
            detailCloseButton != null &&
            resourceDetailPanel != null &&
            buildingDetailPanel != null &&
            technologyDetailPanel != null &&
            nationDetailPanel != null &&
            resourcesPanelButton != null &&
            buildingsPanelButton != null &&
            technologiesPanelButton != null &&
            nationPanelButton != null &&
            HasResourceRows() &&
            HasBuildingRows() &&
            HasActionRows(eraTabRows, eraTabLabels, eraTabButtons) &&
            HasActionRows(technologyActionRows, technologyActionInfoLabels, technologyActionButtons) &&
            HasNationModifierRows() &&
            foodToggleButton != null &&
            tooltipView != null &&
            tooltipView.HasRequiredReferences;

        public Button ResourcesPanelButton => resourcesPanelButton;
        public Button BuildingsPanelButton => buildingsPanelButton;
        public Button TechnologiesPanelButton => technologiesPanelButton;
        public Button NationPanelButton => nationPanelButton;
        public GameObject DetailPanelRoot => detailPanelRoot;
        public Button DetailCloseButton => detailCloseButton;
        public IReadOnlyList<GameObject> ResourceDetailRows => resourceDetailRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> ResourceSummaryLabels => resourceSummaryLabels ?? Array.Empty<Text>();
        public IReadOnlyList<GameObject> ResourceProducerBoxes => resourceProducerBoxes ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> ResourceProducerLabels => resourceProducerLabels ?? Array.Empty<Text>();
        public IReadOnlyList<CivicTooltipTrigger> ResourceProducerTooltips => resourceProducerTooltips ?? Array.Empty<CivicTooltipTrigger>();
        public IReadOnlyList<GameObject> BuildingActionRows => buildingActionRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> BuildingActionInfoLabels => buildingActionInfoLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Text> BuildingCountLabels => buildingCountLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Text> BuildingCostLabels => buildingCostLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Text> BuildingInputOutputLabels => buildingInputOutputLabels ?? Array.Empty<Text>();
        public IReadOnlyList<CivicTooltipTrigger> BuildingInputOutputTooltips => buildingInputOutputTooltips ?? Array.Empty<CivicTooltipTrigger>();
        public IReadOnlyList<Text> BuildingGdpDeltaLabels => buildingGdpDeltaLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Button> BuildingActionButtons => buildingActionButtons ?? Array.Empty<Button>();
        public IReadOnlyList<CivicTooltipTrigger> BuildingButtonTooltips => buildingButtonTooltips ?? Array.Empty<CivicTooltipTrigger>();
        public IReadOnlyList<GameObject> EraTabRows => eraTabRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> EraTabLabels => eraTabLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Button> EraTabButtons => eraTabButtons ?? Array.Empty<Button>();
        public IReadOnlyList<GameObject> TechnologyActionRows => technologyActionRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> TechnologyActionInfoLabels => technologyActionInfoLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Button> TechnologyActionButtons => technologyActionButtons ?? Array.Empty<Button>();
        public GameObject NationDetailPanel => nationDetailPanel;
        public Text NationStatusLabel => nationStatusLabel;
        public IReadOnlyList<GameObject> NationModifierRows => nationModifierRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Button> NationModifierExpandButtons => nationModifierExpandButtons ?? Array.Empty<Button>();
        public IReadOnlyList<GameObject> NationModifierDetailRoots => nationModifierDetailRoots ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> NationModifierDetailLabels => nationModifierDetailLabels ?? Array.Empty<Text>();
        public IReadOnlyList<LayoutElement> NationModifierLayouts => nationModifierLayouts ?? Array.Empty<LayoutElement>();
        public Button FoodToggleButton => foodToggleButton;
        public CivicTooltipView TooltipView => tooltipView;

        private void OnEnable()
        {
            resourcesPanelButton?.onClick.AddListener(NotifyResourcesPanelRequested);
            buildingsPanelButton?.onClick.AddListener(NotifyBuildingsPanelRequested);
            technologiesPanelButton?.onClick.AddListener(NotifyTechnologiesPanelRequested);
            nationPanelButton?.onClick.AddListener(NotifyNationPanelRequested);
            detailCloseButton?.onClick.AddListener(NotifyDetailPanelCloseRequested);
            foodToggleButton?.onClick.AddListener(NotifyFoodToggleRequested);
            BindActionButtons();
        }

        private void OnDisable()
        {
            resourcesPanelButton?.onClick.RemoveListener(NotifyResourcesPanelRequested);
            buildingsPanelButton?.onClick.RemoveListener(NotifyBuildingsPanelRequested);
            technologiesPanelButton?.onClick.RemoveListener(NotifyTechnologiesPanelRequested);
            nationPanelButton?.onClick.RemoveListener(NotifyNationPanelRequested);
            detailCloseButton?.onClick.RemoveListener(NotifyDetailPanelCloseRequested);
            foodToggleButton?.onClick.RemoveListener(NotifyFoodToggleRequested);
            ClearActionButtons(buildingActionButtons);
            ClearActionButtons(eraTabButtons);
            ClearActionButtons(technologyActionButtons);
            ClearActionButtons(nationModifierExpandButtons);
            tooltipView?.Hide();
        }

        public void Render(
            CivicGameSnapshot snapshot,
            CivicHudPanelMode panelMode,
            bool showFoodChildren,
            string selectedTechnologyEraId,
            IReadOnlyList<CivicNationModifierGroup> nationModifiers,
            string currentNationName)
        {
            if (snapshot == null)
            {
                return;
            }

            var population = FindResource(snapshot, PopulationId)?.Stockpile ?? snapshot.Population;
            var treasury = FindResource(snapshot, TreasuryId)?.Stockpile ?? snapshot.Treasury;
            var constructionPower = FindResource(snapshot, ConstructionPowerId)?.Stockpile ?? snapshot.ConstructionPower;
            var science = FindResource(snapshot, ScienceId)?.Stockpile ?? snapshot.Science;

            populationLabel.text = $"인구 {population.ToShortString()}";
            buildingCapacityLabel.text = $"건물 {snapshot.UsedPopulation.ToShortString()} / {population.ToShortString()}";
            gdpLabel.text = $"GDP {snapshot.Gdp.ToShortString()}";
            treasuryLabel.text = $"국고 {treasury.ToShortString()}";
            constructionLabel.text = $"건설력 {constructionPower.ToShortString()}";
            scienceLabel.text = $"과학 {science.ToShortString()}";
            eraLabel.text = snapshot.CurrentEraName;
            shortageAlertLabel.text = snapshot.HasShortage ? "품귀" : "품귀 없음";
            researchAlertLabel.text = snapshot.HasResearchAvailable ? "연구 가능" : "연구 대기";
            constructionAlertLabel.text = snapshot.HasConstructionBlocked ? "건설 불가" : "건설 가능";
            rightResourcesLabel.supportRichText = true;
            rightResourcesLabel.text = BuildRightResourceText(snapshot, showFoodChildren);
            RenderDetail(snapshot, panelMode, selectedTechnologyEraId, nationModifiers, currentNationName);
        }

        private static string BuildRightResourceText(CivicGameSnapshot snapshot, bool showFoodChildren)
        {
            var builder = new StringBuilder();
            builder.AppendLine("자원 요약");
            foreach (var resource in snapshot.Resources)
            {
                if (resource.Category == ResourceCategory.Element && !showFoodChildren && IsFoodChild(resource))
                {
                    continue;
                }

                if (resource.Category == ResourceCategory.Element && showFoodChildren && IsFoodChild(resource))
                {
                    builder.Append("  ");
                }

                builder.AppendLine(WrapMarketColor(
                    $"{resource.DisplayNameKo}  {resource.Stockpile.ToShortString()}  {resource.NetPerSecond.ToShortString()}/s",
                    resource));
            }

            return builder.ToString();
        }

        private static bool IsFoodChild(CivicResourceSnapshot resource)
        {
            return resource.FoodConversion > 0d;
        }

        private void RenderDetail(
            CivicGameSnapshot snapshot,
            CivicHudPanelMode panelMode,
            string selectedTechnologyEraId,
            IReadOnlyList<CivicNationModifierGroup> nationModifiers,
            string currentNationName)
        {
            detailPanelRoot.SetActive(panelMode != CivicHudPanelMode.None);
            detailBodyLabel.gameObject.SetActive(false);
            resourceDetailPanel.SetActive(panelMode == CivicHudPanelMode.Resources);
            buildingDetailPanel.SetActive(panelMode == CivicHudPanelMode.Buildings);
            technologyDetailPanel.SetActive(panelMode == CivicHudPanelMode.Technologies);
            nationDetailPanel.SetActive(panelMode == CivicHudPanelMode.Nation);

            switch (panelMode)
            {
                case CivicHudPanelMode.Resources:
                    detailTitleLabel.text = "자원 상세";
                    RenderResourceDetailRows(snapshot);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderEraTabs(snapshot, false, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, false, selectedTechnologyEraId);
                    RenderNationModifierRows(null, string.Empty);
                    break;
                case CivicHudPanelMode.Buildings:
                    detailTitleLabel.text = "건물";
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, true);
                    RenderEraTabs(snapshot, false, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, false, selectedTechnologyEraId);
                    RenderNationModifierRows(null, string.Empty);
                    break;
                case CivicHudPanelMode.Technologies:
                    detailTitleLabel.text = "기술";
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderEraTabs(snapshot, true, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, true, selectedTechnologyEraId);
                    RenderNationModifierRows(null, string.Empty);
                    break;
                case CivicHudPanelMode.Nation:
                    detailTitleLabel.text = "국가";
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderEraTabs(snapshot, false, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, false, selectedTechnologyEraId);
                    RenderNationModifierRows(nationModifiers, currentNationName);
                    break;
                case CivicHudPanelMode.None:
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderEraTabs(snapshot, false, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, false, selectedTechnologyEraId);
                    RenderNationModifierRows(null, string.Empty);
                    break;
                default:
                    detailTitleLabel.text = "자원 상세";
                    RenderResourceDetailRows(snapshot);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderEraTabs(snapshot, false, selectedTechnologyEraId);
                    RenderTechnologyActionButtons(snapshot, false, selectedTechnologyEraId);
                    RenderNationModifierRows(null, string.Empty);
                    break;
            }
        }

        private void RenderNationModifierRows(IReadOnlyList<CivicNationModifierGroup> modifiers, string nationName)
        {
            currentNationModifiers = modifiers ?? Array.Empty<CivicNationModifierGroup>();
            currentNationName = nationName ?? string.Empty;
            nationStatusLabel.text = "현재 국가: " + (string.IsNullOrEmpty(currentNationName) ? "설립된 국가 없음" : currentNationName) +
                $" · modifier 그룹 {currentNationModifiers.Count}개 · 대상 {currentNationModifiers.Sum(group => group.Targets.Count)}개";
            for (var index = 0; index < NationModifierRows.Count; index++)
            {
                var visible = index < currentNationModifiers.Count;
                NationModifierRows[index].SetActive(visible);
                if (!visible) continue;
                var group = currentNationModifiers[index];
                var expanded = expandedNationModifierKeys.Contains(group.Key);
                nationModifierSummaryLabels[index].text = group.Targets.Count == 1
                    ? $"{group.EffectName} · {group.Targets[0].TargetName} · {FormatModifierAmount(group.EffectType, group.Targets[0].Amount)}"
                    : $"{group.EffectName} · {group.Targets.Count}개 대상 · {group.ContributionCount}개 출처";
                nationModifierDetailLabels[index].text = BuildNationModifierGroupDetail(group);
                nationModifierDetailRoots[index].SetActive(expanded);
                nationModifierExpandLabels[index].text = expanded ? "−" : "+";
                var detailHeight = Math.Max(24f, nationModifierDetailLabels[index].preferredHeight + 16f);
                nationModifierLayouts[index].preferredHeight = expanded
                    ? 60f + detailHeight
                    : 52f;
            }

            var content = NationModifierRows.FirstOrDefault()?.transform.parent as RectTransform;
            if (content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private static string BuildNationModifierGroupDetail(CivicNationModifierGroup group)
        {
            var lines = new List<string>();
            foreach (var target in group.Targets)
            {
                lines.Add($"{target.TargetName}: {FormatModifierAmount(target.EffectType, target.Amount)}");
                lines.AddRange(target.Contributions.Select(contribution =>
                    $"  {SourceTypeName(contribution.SourceType)} · {contribution.SourceName}: {FormatModifierAmount(target.EffectType, contribution.Amount)}"));
            }
            return string.Join("\n", lines);
        }

        private static string FormatModifierAmount(string effectType, double amount)
        {
            var percent = effectType.EndsWith("Multiplier", StringComparison.Ordinal) || effectType == CivicModifierEffectTypes.TaxRateAdd;
            return percent ? $"{amount:+0.##%;-0.##%;0%}" : $"{amount:+0.##;-0.##;0}";
        }

        private static string SourceTypeName(string sourceType)
        {
            switch (sourceType)
            {
                case "technology": return "기술";
                case "civilization": return "시작 문명";
                case "nation": return "국가";
                case "institution": return "정치·사회체계";
                case "wonder": return "불가사의";
                case "person": return "위인 직책";
                case "personAbility": return "위인 능력";
                case "personLegacy": return "위인 유산";
                case "event": return "이벤트";
                case "achievement": return "도전과제";
                case "legacy": return "환생 유산";
                default: return sourceType;
            }
        }

        private void RenderResourceDetailRows(CivicGameSnapshot snapshot)
        {
            var resources = snapshot?.Resources
                .Where(resource => resource.Category != ResourceCategory.Aggregate)
                .Take(ResourceDetailRows.Count)
                .ToArray() ?? Array.Empty<CivicResourceSnapshot>();
            var producerSlotsPerRow = GetResourceProducerSlotsPerRow();

            for (var index = 0; index < ResourceDetailRows.Count; index++)
            {
                var row = ResourceDetailRows[index];
                var summaryLabel = ResourceSummaryLabels[index];
                if (row == null || summaryLabel == null)
                {
                    continue;
                }

                if (index >= resources.Length)
                {
                    row.SetActive(false);
                    summaryLabel.color = NeutralMarketColor;
                    ClearResourceProducerSlots(index, producerSlotsPerRow);
                    continue;
                }

                var resource = resources[index];
                row.SetActive(true);
                summaryLabel.text = BuildResourceSummary(resource);
                summaryLabel.color = GetMarketTint(resource);
                RenderResourceProducerSlots(index, snapshot, resource, producerSlotsPerRow);
            }
        }

        private void RenderResourceProducerSlots(int resourceIndex, CivicGameSnapshot snapshot, CivicResourceSnapshot resource, int producerSlotsPerRow)
        {
            var entries = BuildResourceDetailFlowEntries(snapshot, resource).ToArray();
            var visibleEntryCount = producerSlotsPerRow <= 1 || entries.Length <= producerSlotsPerRow
                ? Math.Min(entries.Length, producerSlotsPerRow)
                : producerSlotsPerRow - 1;

            for (var slot = 0; slot < producerSlotsPerRow; slot++)
            {
                var flatIndex = resourceIndex * producerSlotsPerRow + slot;
                if (flatIndex >= ResourceProducerBoxes.Count ||
                    flatIndex >= ResourceProducerLabels.Count ||
                    flatIndex >= ResourceProducerTooltips.Count)
                {
                    continue;
                }

                var box = ResourceProducerBoxes[flatIndex];
                var label = ResourceProducerLabels[flatIndex];
                var tooltip = ResourceProducerTooltips[flatIndex];
                if (box == null || label == null)
                {
                    continue;
                }

                if (slot < visibleEntryCount)
                {
                    var entry = entries[slot];
                    box.SetActive(true);
                    label.text = entry.Text;
                    label.color = entry.Tint;
                    ConfigureTooltip(tooltip, entry.Tooltip);
                    continue;
                }

                if (slot == visibleEntryCount && entries.Length > producerSlotsPerRow)
                {
                    var extraCount = entries.Length - visibleEntryCount;
                    box.SetActive(true);
                    label.text = $"외 {extraCount}개 항목";
                    label.color = NeutralMarketColor;
                    ConfigureTooltip(tooltip, BuildAllResourceFlowTooltip(entries));
                    continue;
                }

                if (slot == 0 && entries.Length == 0)
                {
                    box.SetActive(true);
                    label.text = "흐름 없음";
                    label.color = NeutralMarketColor;
                    ConfigureTooltip(tooltip, string.Empty);
                    continue;
                }

                box.SetActive(false);
                label.text = string.Empty;
                label.color = NeutralMarketColor;
                ConfigureTooltip(tooltip, string.Empty);
            }
        }

        private void ClearResourceProducerSlots(int resourceIndex, int producerSlotsPerRow)
        {
            for (var slot = 0; slot < producerSlotsPerRow; slot++)
            {
                var flatIndex = resourceIndex * producerSlotsPerRow + slot;
                if (flatIndex >= ResourceProducerBoxes.Count ||
                    flatIndex >= ResourceProducerLabels.Count ||
                    flatIndex >= ResourceProducerTooltips.Count)
                {
                    continue;
                }

                ResourceProducerBoxes[flatIndex]?.SetActive(false);
                if (ResourceProducerLabels[flatIndex] != null)
                {
                    ResourceProducerLabels[flatIndex].text = string.Empty;
                    ResourceProducerLabels[flatIndex].color = NeutralMarketColor;
                }

                ConfigureTooltip(ResourceProducerTooltips[flatIndex], string.Empty);
            }
        }

        private void RenderBuildingActionButtons(CivicGameSnapshot snapshot, bool visible)
        {
            var buildings = snapshot.Buildings
                .Where(building => building.IsBuildable)
                .Take(BuildingActionRows.Count)
                .ToArray();
            EnsureActionIdBuffers();

            for (var index = 0; index < BuildingActionRows.Count; index++)
            {
                var row = BuildingActionRows[index];
                var nameLabel = BuildingActionInfoLabels[index];
                var countLabel = BuildingCountLabels[index];
                var costLabel = BuildingCostLabels[index];
                var inputOutputLabel = BuildingInputOutputLabels[index];
                var inputOutputTooltip = BuildingInputOutputTooltips[index];
                var gdpDeltaLabel = BuildingGdpDeltaLabels[index];
                var button = BuildingActionButtons[index];
                var buttonTooltip = BuildingButtonTooltips[index];
                if (row == null ||
                    nameLabel == null ||
                    countLabel == null ||
                    costLabel == null ||
                    inputOutputLabel == null ||
                    gdpDeltaLabel == null ||
                    button == null)
                {
                    continue;
                }

                if (!visible || index >= buildings.Length)
                {
                    currentBuildingActionIds[index] = string.Empty;
                    ConfigureBuildingRow(
                        row,
                        nameLabel,
                        countLabel,
                        costLabel,
                        inputOutputLabel,
                        inputOutputTooltip,
                        gdpDeltaLabel,
                        button,
                        buttonTooltip,
                        false,
                        null,
                        NeutralMarketColor);
                    continue;
                }

                var building = buildings[index];
                currentBuildingActionIds[index] = building.Id;
                ConfigureBuildingRow(
                    row,
                    nameLabel,
                    countLabel,
                    costLabel,
                    inputOutputLabel,
                    inputOutputTooltip,
                    gdpDeltaLabel,
                    button,
                    buttonTooltip,
                    true,
                    building,
                    GetBuildingMarketTint(snapshot, building));
            }
        }

        private static void ConfigureBuildingRow(
            GameObject row,
            Text nameLabel,
            Text countLabel,
            Text costLabel,
            Text inputOutputLabel,
            CivicTooltipTrigger inputOutputTooltip,
            Text gdpDeltaLabel,
            Button button,
            CivicTooltipTrigger buttonTooltip,
            bool visible,
            CivicBuildingSnapshot building,
            Color marketTint)
        {
            row.SetActive(visible);
            if (!visible || building == null)
            {
                nameLabel.text = string.Empty;
                countLabel.text = string.Empty;
                costLabel.text = string.Empty;
                inputOutputLabel.text = string.Empty;
                gdpDeltaLabel.text = string.Empty;
                nameLabel.color = NeutralMarketColor;
                inputOutputLabel.color = NeutralMarketColor;
                gdpDeltaLabel.color = NeutralMarketColor;
                button.interactable = false;
                SetButtonLabel(button, "건설");
                ConfigureTooltip(inputOutputTooltip, string.Empty);
                ConfigureTooltip(buttonTooltip, string.Empty);
                return;
            }

            nameLabel.text = building.DisplayNameKo;
            nameLabel.color = marketTint;
            countLabel.text = building.Count.ToString(CultureInfo.InvariantCulture);
            costLabel.text = building.ConstructionCost.ToShortString();
            inputOutputLabel.text = BuildBuildingDeltaSummary(building.ResourceDeltas, out var deltaTooltip);
            inputOutputLabel.color = marketTint;
            gdpDeltaLabel.text = FormatSignedNumber(building.GdpDelta);
            gdpDeltaLabel.color = marketTint;
            button.gameObject.SetActive(true);
            button.interactable = building.CanBuild;
            SetButtonLabel(button, "건설");
            ConfigureTooltip(inputOutputTooltip, deltaTooltip);
            ConfigureTooltip(buttonTooltip, building.CanBuild ? string.Empty : building.BlockReason);
        }

        private void RenderEraTabs(CivicGameSnapshot snapshot, bool visible, string selectedEraId)
        {
            var eras = snapshot.Eras
                .Where(era => era.IsVisible)
                .OrderBy(era => era.Order)
                .Take(EraTabRows.Count)
                .ToArray();
            EnsureActionIdBuffers();

            for (var index = 0; index < EraTabRows.Count; index++)
            {
                var row = EraTabRows[index];
                var label = EraTabLabels[index];
                var button = EraTabButtons[index];
                if (row == null || label == null || button == null)
                {
                    continue;
                }

                if (!visible || index >= eras.Length)
                {
                    currentEraTabIds[index] = string.Empty;
                    row.SetActive(false);
                    label.text = string.Empty;
                    button.interactable = false;
                    SetButtonLabel(button, string.Empty);
                    continue;
                }

                var era = eras[index];
                var isSelected = era.Id == selectedEraId;
                currentEraTabIds[index] = era.Id;
                row.SetActive(true);
                label.text = era.DisplayNameKo;
                label.color = isSelected ? Color.yellow : NeutralMarketColor;
                button.interactable = !isSelected;
                SetButtonLabel(button, era.DisplayNameKo);
            }
        }

        private void RenderTechnologyActionButtons(CivicGameSnapshot snapshot, bool visible, string selectedEraId)
        {
            var technologies = snapshot.Technologies
                .Where(technology => technology.EraId == selectedEraId)
                .Take(TechnologyActionRows.Count)
                .ToArray();
            EnsureActionIdBuffers();

            for (var index = 0; index < TechnologyActionRows.Count; index++)
            {
                var row = TechnologyActionRows[index];
                var infoLabel = TechnologyActionInfoLabels[index];
                var button = TechnologyActionButtons[index];
                if (row == null || infoLabel == null || button == null)
                {
                    continue;
                }

                if (!visible || index >= technologies.Length)
                {
                    currentTechnologyActionIds[index] = string.Empty;
                    ConfigureActionRow(row, infoLabel, button, false, string.Empty, string.Empty, false);
                    continue;
                }

                var technology = technologies[index];
                var state = technology.IsResearched ? "완료" : technology.CanResearch ? "연구" : "대기";
                currentTechnologyActionIds[index] = technology.Id;
                ConfigureActionRow(
                    row,
                    infoLabel,
                    button,
                    true,
                    BuildTechnologyActionInfo(technology),
                    $"{technology.DisplayNameKo}\n{state}",
                    technology.CanResearch);
            }
        }

        private static void ConfigureActionRow(GameObject row, Text infoLabel, Button button, bool visible, string info, string label, bool interactable)
        {
            row.SetActive(visible);
            infoLabel.text = info;
            button.gameObject.SetActive(true);
            button.interactable = visible && interactable;
            SetButtonLabel(button, label);
        }

        private static string BuildTechnologyActionInfo(CivicTechnologySnapshot technology)
        {
            var state = technology.IsResearched ? "완료" : technology.CanResearch ? "연구 가능" : "대기";
            return $"{technology.DisplayNameKo} | 비용 {technology.Cost.ToShortString()} | {state}\n{technology.EffectSummary}";
        }

        private static string BuildResourceSummary(CivicResourceSnapshot resource)
        {
            var summary = $"{resource.DisplayNameKo} | 보유 {resource.Stockpile.ToShortString()} | 생산 {resource.ProducedPerSecond.ToShortString()}/s | 소비 {resource.ConsumedPerSecond.ToShortString()}/s | 순증감 {FormatSignedNumber(resource.NetPerSecond)}/s";
            if (IsGdpResource(resource))
            {
                var gdp = resource.ProducedPerSecond * resource.Price;
                return $"{summary} | 가격 x{resource.PriceMultiplier.ToString("0.##", CultureInfo.InvariantCulture)} | GDP {gdp.ToShortString()}";
            }

            return $"{summary} | GDP 제외";
        }

        private static IEnumerable<ResourceDetailFlowEntry> BuildResourceDetailFlowEntries(CivicGameSnapshot snapshot, CivicResourceSnapshot resource)
        {
            foreach (var producer in resource.Producers)
            {
                yield return new ResourceDetailFlowEntry(
                    BuildProducerText(resource, producer),
                    string.Empty,
                    GetMarketTint(resource));
            }

            foreach (var consumer in resource.Consumers)
            {
                yield return new ResourceDetailFlowEntry(
                    BuildConsumerText(consumer),
                    string.Empty);
            }

            foreach (var entry in BuildSyntheticResourceFlowEntries(snapshot, resource))
            {
                yield return entry;
            }
        }

        private static IEnumerable<ResourceDetailFlowEntry> BuildSyntheticResourceFlowEntries(CivicGameSnapshot snapshot, CivicResourceSnapshot resource)
        {
            if (resource.Id == PopulationId)
            {
                foreach (var source in BuildPopulationSources(snapshot))
                {
                    yield return new ResourceDetailFlowEntry(
                        $"{source.DisplayNameKo} x{source.Count} | {source.DisplayNameKo} 인구 총량 {source.Amount.ToShortString()}",
                        "인구 생산 건물은 건설 시 총 인구를 즉시 늘리며, 초당 생산 흐름에는 포함되지 않습니다.");
                }

                foreach (var entry in snapshot.PopulationConsumption)
                {
                    yield return new ResourceDetailFlowEntry(
                        $"{entry.BuildingDisplayNameKo} x{entry.BuildingCount} | {entry.ResourceDisplayNameKo} 수요 충족 {entry.ConsumedAmount.ToShortString()} → 활성 인구 +{entry.ProducedPopulation.ToShortString()}",
                        "인구층 소비 자원은 영구 인구를 초당 누적하지 않습니다. 현재 수요가 충족되는 동안만 해당 인구 보너스가 활성화되고, 수요가 미충족되면 보너스가 취소됩니다.");
                }
            }

            if (resource.Id == ScienceId)
            {
                foreach (var source in BuildPopulationSources(snapshot))
                {
                    yield return new ResourceDetailFlowEntry(
                        $"{source.DisplayNameKo} 인구 | +{source.Amount.ToShortString()}/s",
                        "P01 과학은 현재 전체 인구 1명당 +1/s로 계산됩니다. 이 항목은 인구 출처별 기여를 표시합니다.");
                }
            }

            if (resource.Id == TreasuryId && resource.ConsumedPerSecond > CivicNumber.Zero && resource.Consumers.Count == 0)
            {
                yield return new ResourceDetailFlowEntry(
                    $"소비처: 건설부문 운영비 | -{resource.ConsumedPerSecond.ToShortString()}/s",
                    "건설부문 treasuryCost는 일반 input 컬럼이 아니라 건설부문 운영비 계산에서 국고 소비로 반영됩니다.");
            }

            if (resource.Id == TreasuryId)
            {
                var directIncome = resource.Producers.Aggregate(CivicNumber.Zero, (sum, producer) => sum + producer.AmountPerSecond);
                var gdpTaxIncome = CivicNumber.ClampMinZero(resource.ProducedPerSecond - directIncome);
                if (gdpTaxIncome > CivicNumber.Zero)
                {
                    var taxRate = snapshot.Gdp > CivicNumber.Zero
                        ? (gdpTaxIncome / snapshot.Gdp).ToDouble()
                        : 0d;
                    yield return new ResourceDetailFlowEntry(
                        $"수입처: GDP 세금 | +{gdpTaxIncome.ToShortString()}/s",
                        $"국고 증가량은 직접 국고 생산에 GDP 세금 수입을 더해 계산합니다. 현재 GDP {snapshot.Gdp.ToShortString()} × 세율 {FormatPercent(taxRate)} = {gdpTaxIncome.ToShortString()}/s 입니다.");
                }
            }

            if (resource.Id == ConstructionPowerId && snapshot.Buildings.Any(building => building.IsBuildable && building.ConstructionCost > CivicNumber.Zero))
            {
                yield return new ResourceDetailFlowEntry(
                    "소비처: 건설 액션 | 건설 시 즉시 차감",
                    "건설력은 초당 소비가 아니라 건설 버튼을 누를 때 건물별 건설비용만큼 차감됩니다.");
            }
        }

        private static IReadOnlyList<PopulationSourceEntry> BuildPopulationSources(CivicGameSnapshot snapshot)
        {
            var population = FindResource(snapshot, PopulationId)?.Stockpile ?? snapshot.Population;
            var sources = new List<PopulationSourceEntry>();
            var attributed = CivicNumber.Zero;
            var activeBonus = snapshot.PopulationConsumption.Aggregate(
                CivicNumber.Zero,
                (sum, entry) => sum + entry.ProducedPopulation);

            var populationResource = FindResource(snapshot, PopulationId);
            foreach (var group in (populationResource?.Producers ?? Array.Empty<CivicResourceFlowSnapshot>())
                .GroupBy(producer => new { producer.BuildingDisplayNameKo, producer.BuildingCount }))
            {
                var total = group.Aggregate(CivicNumber.Zero, (sum, producer) => sum + producer.AmountPerSecond);
                if (total <= CivicNumber.Zero)
                {
                    continue;
                }

                sources.Add(new PopulationSourceEntry(group.Key.BuildingDisplayNameKo, group.Key.BuildingCount, total));
                attributed += total;
            }

            var basePopulation = CivicNumber.ClampMinZero(population - attributed - activeBonus);
            if (basePopulation > CivicNumber.Zero)
            {
                var capital = snapshot.Buildings.FirstOrDefault(building => building.Id == CapitalBuildingId && building.Count > 0);
                sources.Insert(
                    0,
                    capital != null
                        ? new PopulationSourceEntry(capital.DisplayNameKo, capital.Count, basePopulation)
                        : new PopulationSourceEntry("초기 인구", 1, basePopulation));
            }

            return sources;
        }

        private static string BuildProducerText(CivicResourceSnapshot resource, CivicResourceFlowSnapshot producer)
        {
            var gdp = IsGdpResource(resource)
                ? (producer.AmountPerSecond * resource.Price).ToShortString()
                : "-";
            return $"생산처: {producer.BuildingDisplayNameKo} x{producer.BuildingCount} | +{producer.AmountPerSecond.ToShortString()}/s | GDP {gdp}";
        }

        private static string BuildConsumerText(CivicResourceFlowSnapshot consumer)
        {
            return $"소비처: {consumer.BuildingDisplayNameKo} x{consumer.BuildingCount} | -{consumer.AmountPerSecond.ToShortString()}/s";
        }

        private static string BuildAllResourceFlowTooltip(IReadOnlyList<ResourceDetailFlowEntry> entries)
        {
            return string.Join("\n", entries.Select(entry => string.IsNullOrEmpty(entry.Tooltip) ? entry.Text : $"{entry.Text}\n  {entry.Tooltip}"));
        }

        private static bool IsGdpResource(CivicResourceSnapshot resource)
        {
            return resource.Category == ResourceCategory.Element && resource.Price > CivicNumber.Zero;
        }

        private static string WrapMarketColor(string text, CivicResourceSnapshot resource)
        {
            var tint = GetMarketTint(resource);
            if (GetMarketTintWeight(resource) <= 0d)
            {
                return text;
            }

            return $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}>{text}</color>";
        }

        private static Color GetMarketTint(CivicResourceSnapshot resource)
        {
            var weight = GetMarketTintWeight(resource);
            if (weight <= 0d)
            {
                return NeutralMarketColor;
            }

            return Color.Lerp(NeutralMarketColor, GetMarketTargetColor(resource), (float)weight);
        }

        private static Color GetMarketTargetColor(CivicResourceSnapshot resource)
        {
            var demandSupplyRatio = ToDemandSupplyRatio(resource.SupplyDemandRatio);
            return demandSupplyRatio > ShortageTintStartDemandSupplyRatio
                ? ShortageMarketColor
                : SurplusMarketColor;
        }

        private static double GetMarketTintWeight(CivicResourceSnapshot resource)
        {
            if (resource == null || resource.Category != ResourceCategory.Element)
            {
                return 0d;
            }

            var demandSupplyRatio = ToDemandSupplyRatio(resource.SupplyDemandRatio);
            if (demandSupplyRatio > ShortageTintStartDemandSupplyRatio)
            {
                return Clamp01((demandSupplyRatio - ShortageTintStartDemandSupplyRatio) / (ShortageTintFullDemandSupplyRatio - ShortageTintStartDemandSupplyRatio));
            }

            if (demandSupplyRatio < SurplusTintStartDemandSupplyRatio)
            {
                return Clamp01((SurplusTintStartDemandSupplyRatio - demandSupplyRatio) / (SurplusTintStartDemandSupplyRatio - SurplusTintFullDemandSupplyRatio));
            }

            return 0d;
        }

        private static double ToDemandSupplyRatio(double supplyDemandRatio)
        {
            if (double.IsPositiveInfinity(supplyDemandRatio))
            {
                return 0d;
            }

            if (supplyDemandRatio <= 0d)
            {
                return double.PositiveInfinity;
            }

            return 1d / supplyDemandRatio;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0d, Math.Min(1d, value));
        }

        private static string FormatPercent(double value)
        {
            return (value * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        private static Color GetBuildingMarketTint(CivicGameSnapshot snapshot, CivicBuildingSnapshot building)
        {
            if (snapshot == null || building == null)
            {
                return NeutralMarketColor;
            }

            var outputResources = building.ResourceDeltas
                .Where(delta => delta.AmountPerSecond > CivicNumber.Zero)
                .Select(delta => FindResource(snapshot, delta.ResourceId))
                .Where(resource => resource != null && resource.Category == ResourceCategory.Element)
                .ToArray();

            if (outputResources.Length == 0)
            {
                return NeutralMarketColor;
            }

            var strongest = outputResources
                .OrderByDescending(GetMarketTintWeight)
                .First();
            return GetMarketTint(strongest);
        }

        private static string BuildBuildingDeltaSummary(IReadOnlyList<CivicBuildingResourceDeltaSnapshot> deltas, out string tooltip)
        {
            if (deltas.Count == 0)
            {
                tooltip = string.Empty;
                return "-";
            }

            var entries = deltas.Select(FormatResourceDelta).ToArray();
            tooltip = entries.Length > BuildingInputOutputVisibleItems
                ? string.Join("\n", entries)
                : string.Empty;

            if (entries.Length <= BuildingInputOutputVisibleItems)
            {
                return string.Join(", ", entries);
            }

            var visibleCount = BuildingInputOutputVisibleItems - 1;
            return string.Join(", ", entries.Take(visibleCount)) + $", 외 {entries.Length - visibleCount} 항목";
        }

        private static string FormatResourceDelta(CivicBuildingResourceDeltaSnapshot delta)
        {
            return $"{delta.ResourceDisplayNameKo} {FormatSignedNumber(delta.AmountPerSecond)}/s";
        }

        private static string FormatSignedNumber(CivicNumber value)
        {
            return value > CivicNumber.Zero
                ? "+" + value.ToShortString()
                : value.ToShortString();
        }

        private static CivicResourceSnapshot FindResource(CivicGameSnapshot snapshot, string id)
        {
            return snapshot.Resources.FirstOrDefault(resource => resource.Id == id);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
            }
        }

        private static void ConfigureTooltip(CivicTooltipTrigger trigger, string text)
        {
            if (trigger != null)
            {
                trigger.SetTooltipText(text);
            }
        }

        private int GetResourceProducerSlotsPerRow()
        {
            if (ResourceDetailRows.Count == 0)
            {
                return ResourceProducerSlotsPerRowFallback;
            }

            var slotCount = ResourceProducerBoxes.Count / ResourceDetailRows.Count;
            return Math.Max(1, slotCount);
        }

        private bool HasResourceRows()
        {
            return HasSlots(resourceDetailRows) &&
                HasSlots(resourceSummaryLabels) &&
                HasSlots(resourceProducerBoxes) &&
                HasSlots(resourceProducerLabels) &&
                HasSlots(resourceProducerTooltips) &&
                resourceDetailRows.Length == resourceSummaryLabels.Length &&
                resourceProducerBoxes.Length == resourceProducerLabels.Length &&
                resourceProducerBoxes.Length == resourceProducerTooltips.Length &&
                resourceProducerBoxes.Length % resourceDetailRows.Length == 0;
        }

        private bool HasBuildingRows()
        {
            return HasSlots(buildingActionRows) &&
                HasSlots(buildingActionInfoLabels) &&
                HasSlots(buildingCountLabels) &&
                HasSlots(buildingCostLabels) &&
                HasSlots(buildingInputOutputLabels) &&
                HasSlots(buildingInputOutputTooltips) &&
                HasSlots(buildingGdpDeltaLabels) &&
                HasSlots(buildingActionButtons) &&
                HasSlots(buildingButtonTooltips) &&
                buildingActionRows.Length == buildingActionInfoLabels.Length &&
                buildingActionRows.Length == buildingCountLabels.Length &&
                buildingActionRows.Length == buildingCostLabels.Length &&
                buildingActionRows.Length == buildingInputOutputLabels.Length &&
                buildingActionRows.Length == buildingInputOutputTooltips.Length &&
                buildingActionRows.Length == buildingGdpDeltaLabels.Length &&
                buildingActionRows.Length == buildingActionButtons.Length &&
                buildingActionRows.Length == buildingButtonTooltips.Length;
        }

        private static bool HasActionRows(GameObject[] rows, Text[] infoLabels, Button[] buttons)
        {
            return HasSlots(rows) &&
                HasSlots(infoLabels) &&
                HasSlots(buttons) &&
                rows.Length == infoLabels.Length &&
                rows.Length == buttons.Length;
        }

        private bool HasNationModifierRows()
        {
            return nationStatusLabel != null &&
                HasSlots(nationModifierRows) &&
                HasSlots(nationModifierSummaryLabels) &&
                HasSlots(nationModifierDetailRoots) &&
                HasSlots(nationModifierDetailLabels) &&
                HasSlots(nationModifierExpandButtons) &&
                HasSlots(nationModifierExpandLabels) &&
                HasSlots(nationModifierLayouts) &&
                nationModifierRows.Length == nationModifierSummaryLabels.Length &&
                nationModifierRows.Length == nationModifierDetailRoots.Length &&
                nationModifierRows.Length == nationModifierDetailLabels.Length &&
                nationModifierRows.Length == nationModifierExpandButtons.Length &&
                nationModifierRows.Length == nationModifierExpandLabels.Length &&
                nationModifierRows.Length == nationModifierLayouts.Length;
        }

        private static bool HasSlots<T>(T[] values) where T : UnityEngine.Object
        {
            return values != null && values.Length > 0 && values.All(value => value != null);
        }

        private void BindActionButtons()
        {
            EnsureActionIdBuffers();
            for (var index = 0; index < BuildingActionButtons.Count; index++)
            {
                var capturedIndex = index;
                BuildingActionButtons[index]?.onClick.AddListener(() => NotifyBuildRequested(capturedIndex));
            }

            for (var index = 0; index < EraTabButtons.Count; index++)
            {
                var capturedIndex = index;
                EraTabButtons[index]?.onClick.AddListener(() => NotifyEraTabRequested(capturedIndex));
            }

            for (var index = 0; index < TechnologyActionButtons.Count; index++)
            {
                var capturedIndex = index;
                TechnologyActionButtons[index]?.onClick.AddListener(() => NotifyResearchRequested(capturedIndex));
            }

            for (var index = 0; index < NationModifierExpandButtons.Count; index++)
            {
                var capturedIndex = index;
                NationModifierExpandButtons[index]?.onClick.AddListener(() => ToggleNationModifierDetail(capturedIndex));
            }
        }

        private void EnsureActionIdBuffers()
        {
            if (currentBuildingActionIds.Length != BuildingActionRows.Count)
            {
                currentBuildingActionIds = new string[BuildingActionRows.Count];
            }

            if (currentTechnologyActionIds.Length != TechnologyActionRows.Count)
            {
                currentTechnologyActionIds = new string[TechnologyActionRows.Count];
            }

            if (currentEraTabIds.Length != EraTabRows.Count)
            {
                currentEraTabIds = new string[EraTabRows.Count];
            }
        }

        private static void ClearActionButtons(Button[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            foreach (var button in buttons)
            {
                button?.onClick.RemoveAllListeners();
            }
        }

        private void NotifyResourcesPanelRequested() => ResourcesPanelRequested?.Invoke();
        private void NotifyBuildingsPanelRequested() => BuildingsPanelRequested?.Invoke();
        private void NotifyTechnologiesPanelRequested() => TechnologiesPanelRequested?.Invoke();
        private void NotifyNationPanelRequested() => NationPanelRequested?.Invoke();
        private void NotifyDetailPanelCloseRequested() => DetailPanelCloseRequested?.Invoke();

        private void ToggleNationModifierDetail(int index)
        {
            if (index < 0 || index >= currentNationModifiers.Count) return;
            var key = currentNationModifiers[index].Key;
            if (!expandedNationModifierKeys.Add(key)) expandedNationModifierKeys.Remove(key);
            RenderNationModifierRows(currentNationModifiers, currentNationName);
        }
        private void NotifyBuildRequested(int index)
        {
            if (index >= 0 && index < currentBuildingActionIds.Length && !string.IsNullOrEmpty(currentBuildingActionIds[index]))
            {
                BuildRequested?.Invoke(currentBuildingActionIds[index]);
            }
        }

        private void NotifyResearchRequested(int index)
        {
            if (index >= 0 && index < currentTechnologyActionIds.Length && !string.IsNullOrEmpty(currentTechnologyActionIds[index]))
            {
                ResearchRequested?.Invoke(currentTechnologyActionIds[index]);
            }
        }

        private void NotifyEraTabRequested(int index)
        {
            if (index >= 0 && index < currentEraTabIds.Length && !string.IsNullOrEmpty(currentEraTabIds[index]))
            {
                EraTabRequested?.Invoke(currentEraTabIds[index]);
            }
        }

        private void NotifyFoodToggleRequested() => FoodToggleRequested?.Invoke();

        private readonly struct ResourceDetailFlowEntry
        {
            public ResourceDetailFlowEntry(string text, string tooltip)
                : this(text, tooltip, NeutralMarketColor)
            {
            }

            public ResourceDetailFlowEntry(string text, string tooltip, Color tint)
            {
                Text = text;
                Tooltip = tooltip;
                Tint = tint;
            }

            public string Text { get; }
            public string Tooltip { get; }
            public Color Tint { get; }
        }

        private readonly struct PopulationSourceEntry
        {
            public PopulationSourceEntry(string displayNameKo, int count, CivicNumber amount)
            {
                DisplayNameKo = displayNameKo;
                Count = count;
                Amount = amount;
            }

            public string DisplayNameKo { get; }
            public int Count { get; }
            public CivicNumber Amount { get; }
        }
    }

    public enum CivicHudPanelMode
    {
        None,
        Resources,
        Buildings,
        Technologies,
        Nation,
    }
}
