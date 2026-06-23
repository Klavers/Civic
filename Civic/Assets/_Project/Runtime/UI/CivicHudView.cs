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
        [SerializeField] private GameObject resourceDetailPanel;
        [SerializeField] private GameObject buildingDetailPanel;
        [SerializeField] private GameObject technologyDetailPanel;
        [SerializeField] private Button resourcesPanelButton;
        [SerializeField] private Button buildingsPanelButton;
        [SerializeField] private Button technologiesPanelButton;
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
        [SerializeField] private GameObject[] technologyActionRows;
        [SerializeField] private Text[] technologyActionInfoLabels;
        [SerializeField] private Button[] technologyActionButtons;
        [SerializeField] private Button foodToggleButton;
        [SerializeField] private CivicTooltipView tooltipView;

        private string[] currentBuildingActionIds = Array.Empty<string>();
        private string[] currentTechnologyActionIds = Array.Empty<string>();

        public event Action ResourcesPanelRequested;
        public event Action BuildingsPanelRequested;
        public event Action TechnologiesPanelRequested;
        public event Action<string> BuildRequested;
        public event Action<string> ResearchRequested;
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
            resourceDetailPanel != null &&
            buildingDetailPanel != null &&
            technologyDetailPanel != null &&
            resourcesPanelButton != null &&
            buildingsPanelButton != null &&
            technologiesPanelButton != null &&
            HasResourceRows() &&
            HasBuildingRows() &&
            HasActionRows(technologyActionRows, technologyActionInfoLabels, technologyActionButtons) &&
            foodToggleButton != null &&
            tooltipView != null &&
            tooltipView.HasRequiredReferences;

        public Button ResourcesPanelButton => resourcesPanelButton;
        public Button BuildingsPanelButton => buildingsPanelButton;
        public Button TechnologiesPanelButton => technologiesPanelButton;
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
        public IReadOnlyList<GameObject> TechnologyActionRows => technologyActionRows ?? Array.Empty<GameObject>();
        public IReadOnlyList<Text> TechnologyActionInfoLabels => technologyActionInfoLabels ?? Array.Empty<Text>();
        public IReadOnlyList<Button> TechnologyActionButtons => technologyActionButtons ?? Array.Empty<Button>();
        public Button FoodToggleButton => foodToggleButton;
        public CivicTooltipView TooltipView => tooltipView;

        private void OnEnable()
        {
            resourcesPanelButton?.onClick.AddListener(NotifyResourcesPanelRequested);
            buildingsPanelButton?.onClick.AddListener(NotifyBuildingsPanelRequested);
            technologiesPanelButton?.onClick.AddListener(NotifyTechnologiesPanelRequested);
            foodToggleButton?.onClick.AddListener(NotifyFoodToggleRequested);
            BindActionButtons();
        }

        private void OnDisable()
        {
            resourcesPanelButton?.onClick.RemoveListener(NotifyResourcesPanelRequested);
            buildingsPanelButton?.onClick.RemoveListener(NotifyBuildingsPanelRequested);
            technologiesPanelButton?.onClick.RemoveListener(NotifyTechnologiesPanelRequested);
            foodToggleButton?.onClick.RemoveListener(NotifyFoodToggleRequested);
            ClearActionButtons(buildingActionButtons);
            ClearActionButtons(technologyActionButtons);
            tooltipView?.Hide();
        }

        public void Render(CivicGameSnapshot snapshot, CivicHudPanelMode panelMode, bool showFoodChildren)
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
            rightResourcesLabel.text = BuildRightResourceText(snapshot, showFoodChildren);
            RenderDetail(snapshot, panelMode);
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

                builder.Append(resource.DisplayNameKo);
                builder.Append("  ");
                builder.Append(resource.Stockpile.ToShortString());
                builder.Append("  ");
                builder.Append(resource.NetPerSecond.ToShortString());
                builder.AppendLine("/s");
            }

            return builder.ToString();
        }

        private static bool IsFoodChild(CivicResourceSnapshot resource)
        {
            return resource.FoodConversion > 0d;
        }

        private void RenderDetail(CivicGameSnapshot snapshot, CivicHudPanelMode panelMode)
        {
            detailBodyLabel.gameObject.SetActive(false);
            resourceDetailPanel.SetActive(panelMode == CivicHudPanelMode.Resources);
            buildingDetailPanel.SetActive(panelMode == CivicHudPanelMode.Buildings);
            technologyDetailPanel.SetActive(panelMode == CivicHudPanelMode.Technologies);

            switch (panelMode)
            {
                case CivicHudPanelMode.Resources:
                    detailTitleLabel.text = "자원 상세";
                    RenderResourceDetailRows(snapshot);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderTechnologyActionButtons(snapshot, false);
                    break;
                case CivicHudPanelMode.Buildings:
                    detailTitleLabel.text = "건물";
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, true);
                    RenderTechnologyActionButtons(snapshot, false);
                    break;
                case CivicHudPanelMode.Technologies:
                    detailTitleLabel.text = "기술";
                    RenderResourceDetailRows(null);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderTechnologyActionButtons(snapshot, true);
                    break;
                default:
                    detailTitleLabel.text = "자원 상세";
                    RenderResourceDetailRows(snapshot);
                    RenderBuildingActionButtons(snapshot, false);
                    RenderTechnologyActionButtons(snapshot, false);
                    break;
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
                    ClearResourceProducerSlots(index, producerSlotsPerRow);
                    continue;
                }

                var resource = resources[index];
                row.SetActive(true);
                summaryLabel.text = BuildResourceSummary(resource);
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
                    ConfigureTooltip(tooltip, entry.Tooltip);
                    continue;
                }

                if (slot == visibleEntryCount && entries.Length > producerSlotsPerRow)
                {
                    var extraCount = entries.Length - visibleEntryCount;
                    box.SetActive(true);
                    label.text = $"외 {extraCount}개 항목";
                    ConfigureTooltip(tooltip, BuildAllResourceFlowTooltip(entries));
                    continue;
                }

                if (slot == 0 && entries.Length == 0)
                {
                    box.SetActive(true);
                    label.text = "흐름 없음";
                    ConfigureTooltip(tooltip, string.Empty);
                    continue;
                }

                box.SetActive(false);
                label.text = string.Empty;
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
                        null);
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
                    building);
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
            CivicBuildingSnapshot building)
        {
            row.SetActive(visible);
            if (!visible || building == null)
            {
                nameLabel.text = string.Empty;
                countLabel.text = string.Empty;
                costLabel.text = string.Empty;
                inputOutputLabel.text = string.Empty;
                gdpDeltaLabel.text = string.Empty;
                button.interactable = false;
                SetButtonLabel(button, "건설");
                ConfigureTooltip(inputOutputTooltip, string.Empty);
                ConfigureTooltip(buttonTooltip, string.Empty);
                return;
            }

            nameLabel.text = building.DisplayNameKo;
            countLabel.text = building.Count.ToString(CultureInfo.InvariantCulture);
            costLabel.text = building.ConstructionCost.ToShortString();
            inputOutputLabel.text = BuildBuildingDeltaSummary(building.ResourceDeltas, out var deltaTooltip);
            gdpDeltaLabel.text = FormatSignedNumber(building.GdpDelta);
            button.gameObject.SetActive(true);
            button.interactable = building.CanBuild;
            SetButtonLabel(button, "건설");
            ConfigureTooltip(inputOutputTooltip, deltaTooltip);
            ConfigureTooltip(buttonTooltip, building.CanBuild ? string.Empty : building.BlockReason);
        }

        private void RenderTechnologyActionButtons(CivicGameSnapshot snapshot, bool visible)
        {
            var technologies = snapshot.Technologies
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
            return $"{technology.DisplayNameKo} | 비용 {technology.Cost.ToShortString()} | {state}";
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
                    string.Empty);
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

            foreach (var building in snapshot.Buildings)
            {
                if (building.Count <= 0)
                {
                    continue;
                }

                var perBuildingPopulation = building.ResourceDeltas
                    .Where(delta => delta.ResourceId == PopulationId && delta.AmountPerSecond > CivicNumber.Zero)
                    .Aggregate(CivicNumber.Zero, (sum, delta) => sum + delta.AmountPerSecond);
                var total = perBuildingPopulation * building.Count;
                if (total <= CivicNumber.Zero)
                {
                    continue;
                }

                sources.Add(new PopulationSourceEntry(building.DisplayNameKo, building.Count, total));
                attributed += total;
            }

            var basePopulation = CivicNumber.ClampMinZero(population - attributed);
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
            return string.Join(", ", entries.Take(visibleCount)) + $", +{entries.Length - visibleCount}";
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

            for (var index = 0; index < TechnologyActionButtons.Count; index++)
            {
                var capturedIndex = index;
                TechnologyActionButtons[index]?.onClick.AddListener(() => NotifyResearchRequested(capturedIndex));
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

        private void NotifyFoodToggleRequested() => FoodToggleRequested?.Invoke();

        private readonly struct ResourceDetailFlowEntry
        {
            public ResourceDetailFlowEntry(string text, string tooltip)
            {
                Text = text;
                Tooltip = tooltip;
            }

            public string Text { get; }
            public string Tooltip { get; }
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
        Resources,
        Buildings,
        Technologies,
    }
}
