using System;
using System.Linq;
using System.Collections;
using Civic.Simulation;
using Civic.Simulation.Modules;
using Civic.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Civic.UI.Tests
{
    public sealed class CivicHudPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            CivicMetaSession.ResetForTests();
            CivicFeatureRuntime.ResetForMainMenu();
        }

        [UnityTest]
        public IEnumerator CivicHud_ProfileUpdateCostBySubsystem()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            using (var update = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Civic.Hud.Update", 64))
            using (var advance = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Civic.Hud.Advance", 64))
            using (var snapshot = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Civic.Simulation.CalculateSnapshot", 64))
            using (var core = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Civic.Hud.CoreRender", 64))
            using (var modules = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Civic.Hud.ModuleRender", 64))
            {
                SceneManager.LoadScene("SampleScene");
                yield return null;
                yield return null;

                var controller = Object.FindFirstObjectByType<CivicHudController>();
                Assert.That(controller, Is.Not.Null);
                var initialSnapshot = System.Diagnostics.Stopwatch.StartNew();
                controller.Simulation.RefreshSnapshot();
                initialSnapshot.Stop();
                Debug.Log($"CIVIC_HUD_PROFILE_INITIAL_SNAPSHOT elapsed={initialSnapshot.Elapsed.TotalMilliseconds:0.###}ms · researched={controller.Simulation.State.ResearchedTechnologyIds.Count} · resources={controller.Simulation.Snapshot.Resources.Count} · buildings={controller.Simulation.Snapshot.Buildings.Count}");
                controller.Simulation.DebugResearchAllTechnologies();
                foreach (var buildingId in controller.Simulation.State.Buildings.Keys.ToArray())
                {
                    controller.Simulation.State.Buildings[buildingId] = 100;
                }
                var stressSnapshot = System.Diagnostics.Stopwatch.StartNew();
                controller.Simulation.RefreshSnapshot();
                stressSnapshot.Stop();
                Debug.Log($"CIVIC_HUD_PROFILE_STRESS_SNAPSHOT elapsed={stressSnapshot.Elapsed.TotalMilliseconds:0.###}ms · researched={controller.Simulation.State.ResearchedTechnologyIds.Count} · resources={controller.Simulation.Snapshot.Resources.Count} · buildings={controller.Simulation.Snapshot.Buildings.Count}");
                yield return null;

                var closed = new double[20][];
                for (var frame = 0; frame < closed.Length; frame++)
                {
                    yield return null;
                    closed[frame] = ProfileFrame(update, advance, snapshot, core, modules);
                }

                controller.ModulePanelView.OpenPanel();
                var open = new double[20][];
                for (var frame = 0; frame < open.Length; frame++)
                {
                    yield return null;
                    open[frame] = ProfileFrame(update, advance, snapshot, core, modules);
                }

                Debug.Log(ProfileSummary("CIVIC_HUD_PROFILE_CLOSED", closed));
                Debug.Log(ProfileSummary("CIVIC_HUD_PROFILE_OPEN", open));
                Assert.That(closed.Any(sample => sample[0] > 0d), Is.True);
                Assert.That(open.Any(sample => sample[0] > 0d), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CivicHud_AdvancesSimulationAndProcessesPrimaryButtons()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.View, Is.Not.Null);
            Assert.That(controller.Simulation, Is.Not.Null);

            Canvas.ForceUpdateCanvases();
            var canvas = controller.GetComponentInParent<Canvas>();
            var hudRect = controller.GetComponent<RectTransform>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            Assert.That(hudRect.rect.width, Is.GreaterThan(0f));
            Assert.That(hudRect.rect.height, Is.GreaterThan(0f));
            Assert.That(hudRect.lossyScale.sqrMagnitude, Is.GreaterThan(0.01f));

            var initialScience = controller.Simulation.Snapshot.Science.ToDouble();
            yield return new WaitForSeconds(1.1f);
            Assert.That(controller.Simulation.Snapshot.Science.ToDouble(), Is.GreaterThan(initialScience));
            controller.View.ResourcesPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.View.ResourceDetailRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.ResourceSummaryLabels.Any(label => label.text.Contains("소비") && label.text.Contains("순증감")), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.text.Contains("수도 인구 총량")), Is.True);
            Assert.That(controller.View.ResourceProducerLabels.Any(label => label.text.Contains("건설 액션")), Is.True);
            Assert.That(controller.View.ResourceProducerTooltips.All(trigger => trigger.HasTooltipView), Is.True);

            var buildingsBeforeBuild = controller.Simulation.Snapshot.Buildings.Sum(building => building.Count);
            var constructionBeforeBuild = controller.Simulation.Snapshot.ConstructionPower.ToDouble();
            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.View.BuildingActionRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.BuildingActionInfoLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingInputOutputLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingGdpDeltaLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            Assert.That(controller.View.BuildingButtonTooltips.All(trigger => trigger.HasTooltipView), Is.True);
            var buildButton = controller.View.BuildingActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            buildButton.onClick.Invoke();
            yield return null;

            Assert.That(
                controller.Simulation.Snapshot.Buildings.Sum(building => building.Count),
                Is.GreaterThan(buildingsBeforeBuild));
            Assert.That(
                controller.Simulation.Snapshot.ConstructionPower.ToDouble(),
                Is.LessThan(constructionBeforeBuild));

            var elapsed = 0f;
            while (!controller.Simulation.Snapshot.HasResearchAvailable && elapsed < 2f)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.That(controller.Simulation.Snapshot.HasResearchAvailable, Is.True);

            controller.Simulation.State.Resources["science"] = CivicNumber.FromDouble(100d);
            var researchedBefore = controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched);
            controller.View.TechnologiesPanelButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.View.EraTabRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.EraTabLabels.Any(label => label.gameObject.activeInHierarchy && label.text.Contains("원시")), Is.True);
            Assert.That(controller.View.TechnologyActionRows.Any(row => row.activeSelf), Is.True);
            Assert.That(controller.View.TechnologyActionInfoLabels.Any(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)), Is.True);
            var researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));
            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));

            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(
                controller.Simulation.Snapshot.Technologies.Count(technology => technology.IsResearched),
                Is.GreaterThan(researchedBefore));
            Assert.That(controller.View.EraTabLabels.Any(label => label.gameObject.activeInHierarchy && label.text.Contains("고대")), Is.True);

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("primitive"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));

            var nextEraTabButton = controller.View.EraTabButtons.First(button => button.gameObject.activeSelf && button.interactable);
            nextEraTabButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("ancient"));
            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("ancient"));

            var previousEraTabButton = controller.View.EraTabButtons.First(button => button.gameObject.activeSelf && button.interactable);
            previousEraTabButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));
            researchButton = controller.View.TechnologyActionButtons.First(button => button.gameObject.activeSelf && button.interactable);
            researchButton.onClick.Invoke();
            yield return null;

            Assert.That(controller.Simulation.Snapshot.CurrentEraId, Is.EqualTo("ancient"));
            Assert.That(controller.SelectedTechnologyEraId, Is.EqualTo("primitive"));

            Assert.DoesNotThrow(() => controller.View.FoodToggleButton.onClick.Invoke());
            Assert.That(controller.Simulation.Snapshot.Resources.Any(resource => resource.Category == ResourceCategory.Aggregate), Is.True);
        }

        [UnityTest]
        public IEnumerator CivicHud_AllModulesUseSeparateDomainPanels()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var modulePanel = controller.ModulePanelView;
            Assert.That(modulePanel.HasRequiredReferences, Is.True);
            Assert.That(modulePanel.DomainPanels.Count, Is.EqualTo(8));
            Assert.That(modulePanel.DomainPanels.Select(item => item.FeatureId), Is.EquivalentTo(CivicFeatureRegistry.Features.Select(item => item.Id)));
            Assert.That(controller.OverlayView.HasRequiredReferences, Is.True);
            controller.ToggleDebugPanel();
            yield return null;
            Assert.That(controller.OverlayView.IsDebugPanelOpen, Is.True);
            controller.ToggleDebugPanel();
            yield return null;
            Assert.That(controller.OverlayView.IsDebugPanelOpen, Is.False);

            controller.View.ResourcesPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Resources));
            Assert.That(controller.View.DetailPanelRoot.activeSelf, Is.True);
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);

            modulePanel.OpenButton.onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.True);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));
            Assert.That(controller.View.DetailPanelRoot.activeSelf, Is.False);
            Assert.That(modulePanel.DomainPanels.Count(item => item.PanelRoot.activeSelf), Is.EqualTo(1));
            Assert.That(modulePanel.DomainPanels.Single(item => item.PanelRoot.activeSelf).Rows.Any(row => row.gameObject.activeSelf), Is.True);

            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Buildings));
            controller.View.DetailCloseButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));

            controller.View.NationPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Nation));
            Assert.That(controller.View.NationDetailPanel.activeSelf, Is.True);
            Assert.That(controller.View.NationStatusLabel.text, Does.StartWith("현재 국가:"));
            var visibleNationRows = controller.View.NationModifierRows.Where(row => row.activeSelf).ToArray();
            Assert.That(visibleNationRows, Is.Not.Empty);
            controller.View.NationModifierExpandButtons[0].onClick.Invoke();
            yield return null;
            Assert.That(controller.View.NationModifierDetailRoots[0].activeSelf, Is.True);
            Assert.That(controller.View.NationModifierDetailLabels[0].text, Does.Contain("\n"));
            Assert.That(controller.View.NationModifierLayouts[0].preferredHeight, Is.GreaterThan(52f));
            Assert.That(controller.View.NationModifierRows[0].GetComponent<RectTransform>().rect.height, Is.EqualTo(controller.View.NationModifierLayouts[0].preferredHeight).Within(1f));
            Assert.That(controller.View.TooltipView.DoesNotBlockRaycasts, Is.True);
            controller.View.DetailCloseButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));

            modulePanel.OpenButton.onClick.Invoke();
            yield return null;
            controller.ProcessEscape();
            yield return null;
            Assert.That(modulePanel.PanelRoot.activeSelf, Is.False);
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.False);
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.True);
            controller.OverlayView.ContinueButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator CivicHud_DebugQuickCommandsAndInstantToggleWorkInIsolation()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller, Is.Not.Null);
            controller.ToggleDebugPanel();
            yield return null;

            var wheatBeforeDebugGrant = controller.Simulation.State.Resources["wheat"];
            var prestigeBeforeDebugGrant = controller.ModuleRuntime.MetaProgress.PrestigePoints;
            controller.OverlayView.DebugGrantResourcesButton.onClick.Invoke();
            controller.OverlayView.DebugResearchAllButton.onClick.Invoke();
            controller.OverlayView.DebugGrantPrestigeButton.onClick.Invoke();
            controller.OverlayView.DebugInstantActionsToggle.isOn = true;
            yield return null;

            Assert.That(controller.Simulation.State.Resources["wheat"], Is.EqualTo(wheatBeforeDebugGrant + CivicNumber.FromDouble(9999d)));
            Assert.That(controller.Simulation.State.ResearchedTechnologyIds.Count, Is.EqualTo(controller.Simulation.Data.Technologies.Count));
            Assert.That(controller.ModuleRuntime.MetaProgress.PrestigePoints, Is.EqualTo(prestigeBeforeDebugGrant + 9999));
            Assert.That(controller.ModuleRuntime.GetModule<CivicPoliticsModule>(CivicFeatureRegistry.Politics).DebugInstantActionsEnabled, Is.True);
            Assert.That(controller.ModuleRuntime.GetModule<CivicWonderModule>(CivicFeatureRegistry.Wonders).DebugInstantActionsEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator CivicHud_ModuleCardsPoliticsTabsAndImpossibleNationFilterWork()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var modulePanel = controller.ModulePanelView;
            modulePanel.OpenPanel();

            var civilizationIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.StartCivilizations);
            modulePanel.TabButtons[civilizationIndex].onClick.Invoke();
            yield return null;
            var civilizationPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.StartCivilizations);
            var civilizationRows = civilizationPanel.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(civilizationRows, Has.Length.EqualTo(1));
            Assert.That(civilizationRows[0].InfoLabel.text, Does.Contain("현재 문명"));
            Assert.That(civilizationRows[0].DescriptionRoot.activeSelf, Is.True);
            Assert.That(civilizationRows[0].DescriptionLabel.text, Is.Not.Empty);

            var politicsIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.Politics);
            modulePanel.TabButtons[politicsIndex].onClick.Invoke();
            yield return null;
            var politicsPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.Politics);
            Assert.That(politicsPanel.CategoryTabRows.Count(row => row.activeSelf), Is.EqualTo(5));
            Assert.That(politicsPanel.CategoryTabLabels.Where(label => label.gameObject.activeInHierarchy).All(label => label.text.Contains("현행:")), Is.True);
            Assert.That(modulePanel.SelectedPoliticsCategory, Is.EqualTo("government"));
            politicsPanel.CategoryTabButtons[1].onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.SelectedPoliticsCategory, Is.EqualTo("franchise"));
            Assert.That(politicsPanel.Rows.Where(row => row.gameObject.activeSelf).All(row => !row.InfoLabel.text.Contains("[government]")), Is.True);

            var nationIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.NationFormation);
            modulePanel.TabButtons[nationIndex].onClick.Invoke();
            yield return null;
            var nationPanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.NationFormation);
            var hiddenImpossibleCount = nationPanel.Rows.Count(row => row.gameObject.activeSelf);
            nationPanel.ImpossibleFilterToggle.isOn = true;
            yield return null;
            Assert.That(nationPanel.Rows.Count(row => row.gameObject.activeSelf), Is.GreaterThan(hiddenImpossibleCount));
            Assert.That(nationPanel.Rows.Where(row => row.gameObject.activeSelf).All(row => row.DescriptionRoot.activeSelf), Is.True);

            var peopleIndex = CivicFeatureRegistry.Features.ToList().FindIndex(item => item.Id == CivicFeatureRegistry.GreatPeople);
            modulePanel.TabButtons[peopleIndex].onClick.Invoke();
            yield return null;
            var peoplePanel = modulePanel.DomainPanels.Single(item => item.FeatureId == CivicFeatureRegistry.GreatPeople);
            Assert.That(modulePanel.SelectedPeopleTab, Is.Zero);
            Assert.That(peoplePanel.CategoryTabRows.Count(row => row.activeSelf), Is.EqualTo(2));
            Assert.That(peoplePanel.Rows.Count(row => row.gameObject.activeSelf), Is.EqualTo(4));
            Assert.That(peoplePanel.FilterRoot.activeSelf, Is.False);

            peoplePanel.CategoryTabButtons[1].onClick.Invoke();
            yield return null;
            Assert.That(modulePanel.SelectedPeopleTab, Is.EqualTo(1));
            Assert.That(peoplePanel.FilterRoot.activeSelf, Is.True);
            Assert.That(peoplePanel.FilterToggles.All(toggle => toggle.isOn), Is.True);

            var people = controller.ModuleRuntime.GetModule<CivicPeopleModule>(CivicFeatureRegistry.GreatPeople);
            var offeredId = people.Definitions.First().Id;
            var offeredAbilityName = people.AbilitiesFor(offeredId).First().DisplayNameKo;
            Assert.That(people.DebugOfferCandidate(offeredId), Is.True);
            yield return new WaitForSeconds(0.25f);
            var candidateRow = peoplePanel.Rows.First(row => row.gameObject.activeSelf && row.InfoLabel.text.Contains("후보"));
            candidateRow.ActionButton.onClick.Invoke();
            yield return null;
            var recruitedRow = peoplePanel.Rows.First(row => row.gameObject.activeSelf && row.InfoLabel.text.Contains("영입 ·"));
            Assert.That(recruitedRow.ChoiceTooltips.All(trigger => !string.IsNullOrWhiteSpace(trigger.TooltipText)), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(400f, 400f) };
            recruitedRow.ChoiceTooltips[0].OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.26f);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True);
            modulePanel.Bind(controller.ModuleRuntime, false);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True, "매 프레임 재바인딩 중에도 직책 tooltip이 유지되어야 한다.");
            recruitedRow.ChoiceTooltips[0].OnPointerExit(pointer);
            recruitedRow.ChoiceButtons[0].onClick.Invoke();
            yield return null;

            peoplePanel.CategoryTabButtons[0].onClick.Invoke();
            yield return null;
            var occupiedPositionRow = peoplePanel.Rows.First(row => row.gameObject.activeSelf && row.InfoLabel.text.Contains("임기"));
            Assert.That(occupiedPositionRow.ActionTooltip.TooltipText, Is.Not.Empty);
            Assert.That(occupiedPositionRow.ActionTooltip.TooltipText, Does.Contain(offeredAbilityName));
            Assert.That(occupiedPositionRow.DescriptionLabel.text, Does.Contain(offeredAbilityName));
            Assert.That(occupiedPositionRow.DescriptionLabel.text, Does.Contain("액티브 능력"));
            occupiedPositionRow.ActionTooltip.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.26f);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True);
            modulePanel.Bind(controller.ModuleRuntime, false);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True, "능력 발동 tooltip도 재바인딩 중 유지되어야 한다.");
            occupiedPositionRow.ActionTooltip.OnPointerExit(pointer);

            peoplePanel.CategoryTabButtons[1].onClick.Invoke();
            yield return null;
            peoplePanel.FilterToggles[2].isOn = false;
            yield return null;
            Assert.That(peoplePanel.Rows.Where(row => row.gameObject.activeSelf).All(row => !row.InfoLabel.text.Contains("영입 ·")), Is.True);

            controller.View.TooltipView.Show("tooltip lifecycle", Vector2.zero);
            Assert.That(controller.View.TooltipView.IsVisible, Is.True);
            modulePanel.ClosePanel();
            yield return null;
            Assert.That(controller.View.TooltipView.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator CivicHud_TooltipSupportsNestedGraceAndPinnedLifecycle()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var tooltip = controller.View.TooltipView;
            var sourceObject = new GameObject("TooltipPinTestSource", typeof(RectTransform), typeof(CivicTooltipTrigger));
            sourceObject.transform.SetParent(controller.View.transform, false);
            var trigger = sourceObject.GetComponent<CivicTooltipTrigger>();
            trigger.AssignTooltipView(tooltip);
            trigger.SetTooltipText("Tooltip pin lifecycle test");
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(500f, 500f) };

            trigger.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.26f);
            Assert.That(tooltip.IsVisible, Is.True);
            yield return new WaitForSecondsRealtime(1.55f);
            Assert.That(tooltip.IsPinned, Is.True, "같은 Tooltip chain을 1.5초 이상 hover하면 자동 고정되어야 한다.");
            trigger.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tooltip.IsVisible, Is.True, "고정 Tooltip은 source를 벗어나도 유지되어야 한다.");
            controller.ProcessEscape();
            Assert.That(tooltip.IsVisible, Is.False, "ESC는 다른 패널보다 고정 Tooltip을 먼저 닫아야 한다.");

            trigger.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.26f);
            var middleClick = new PointerEventData(EventSystem.current)
            {
                position = pointer.position,
                button = PointerEventData.InputButton.Middle,
            };
            tooltip.Cards[0].OnPointerClick(middleClick);
            Assert.That(tooltip.IsPinned, Is.True, "가운데 버튼으로 Tooltip을 즉시 고정할 수 있어야 한다.");
            trigger.OnPointerExit(pointer);
            tooltip.Cards[0].OnPointerClick(middleClick);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tooltip.IsVisible, Is.False, "가운데 버튼으로 고정을 해제한 뒤 pointer가 밖에 있으면 grace 후 닫혀야 한다.");

            tooltip.Show("<link=\"concept_tax_rate\"><u>세율</u></link>", pointer.position);
            yield return null;
            var parent = tooltip.Cards[0];
            Canvas.ForceUpdateCanvases();
            parent.BodyLabel.ForceMeshUpdate();
            var link = parent.BodyLabel.textInfo.linkInfo[0];
            var character = parent.BodyLabel.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            var localCenter = (character.bottomLeft + character.topRight) * 0.5f;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, parent.BodyLabel.transform.TransformPoint(localCenter));
            parent.OnPointerEnter(pointer);
            parent.OnPointerMove(pointer);
            yield return new WaitForSecondsRealtime(0.26f);
            Assert.That(tooltip.Cards[1].gameObject.activeSelf, Is.True);
            parent.OnPointerExit(pointer);
            tooltip.Cards[1].OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(tooltip.Cards[1].gameObject.activeSelf, Is.True, "부모 link에서 자식 Tooltip으로 이동하는 동안 grace가 자식 카드를 보존해야 한다.");
            tooltip.Hide();
            Object.Destroy(sourceObject);
        }

        [UnityTest]
        public IEnumerator CivicHud_EventPopupCanCloseReopenAndRecordsChoiceHistory()
        {
            CivicFeatureRuntime.ConfigureAndBeginForTests(CivicFeatureRegistry.Features.Select(item => item.Id));
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            var events = controller.ModuleRuntime.GetModule<Civic.Simulation.Modules.CivicEventModule>(CivicFeatureRegistry.Events);
            if (events.Queue.Count == 0) Assert.That(events.DebugQueueEvent(events.Definitions[0].Id), Is.True);
            yield return new WaitForSeconds(0.25f);

            Assert.That(controller.OverlayView.EventAlertButton.gameObject.activeSelf, Is.True);
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.True);
            var queueBefore = events.Queue.Count;
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.False);
            Assert.That(events.Queue.Count, Is.EqualTo(queueBefore));
            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Buildings));
            controller.OverlayView.EventAlertButton.onClick.Invoke();
            yield return null;
            Assert.That(controller.OverlayView.IsEventPopupOpen, Is.True);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Buildings));
            Assert.That(controller.OverlayView.EventPopupRoot.GetComponent<Canvas>().sortingOrder, Is.EqualTo(100));
            Assert.That(controller.View.TooltipView.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
            Assert.That(controller.OverlayView.EventChoiceEffectLabels.Where(label => label.gameObject.activeInHierarchy).All(label => !string.IsNullOrWhiteSpace(label.text)), Is.True);

            var historyBefore = events.History.Count;
            var choice = controller.OverlayView.EventChoiceButtons.First(button => button.gameObject.activeSelf && button.interactable);
            choice.onClick.Invoke();
            yield return null;
            Assert.That(events.History.Count, Is.EqualTo(historyBefore + 1));
            Assert.That(events.History.Last().AppliedResults, Is.Not.Null);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.Buildings));
        }

        [UnityTest]
        public IEnumerator CivicHud_BuildQuantityToggleExecutesAtomicBatch()
        {
            CivicFeatureRuntime.ResetForMainMenu();
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            controller.Simulation.State.BasePopulation = CivicNumber.FromDouble(100d);
            controller.Simulation.State.Resources["construction_power"] = CivicNumber.FromDouble(1000d);
            controller.Simulation.RefreshSnapshot();
            controller.View.BuildingsPanelButton.onClick.Invoke();
            yield return null;
            controller.View.BuildingQuantityButtons[1].onClick.Invoke();
            yield return null;

            Assert.That(controller.BuildQuantityMode, Is.EqualTo(CivicBuildQuantityMode.Five));
            var index = controller.View.BuildingActionInfoLabels.ToList().FindIndex(label => label.gameObject.activeInHierarchy && label.text == "벌목장");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var before = controller.Simulation.State.Buildings["logging_camp"];
            Assert.That(controller.View.BuildingActionButtons[index].interactable, Is.True);
            Assert.That(controller.View.BuildingActionButtons[index].GetComponentInChildren<Text>().text, Does.Contain("×5"));

            controller.View.BuildingActionButtons[index].onClick.Invoke();
            yield return null;

            Assert.That(controller.Simulation.State.Buildings["logging_camp"], Is.EqualTo(before + 5));
        }

        [UnityTest]
        public IEnumerator CivicHud_WithoutPrestigeCanReturnToMainMenuFromEscapePopup()
        {
            CivicFeatureRuntime.ResetForMainMenu();
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<CivicHudController>();
            Assert.That(controller.ModuleRuntime.GetModule<Civic.Simulation.Modules.CivicPrestigeModule>(CivicFeatureRegistry.Prestige), Is.Null);
            Assert.That(controller.PanelMode, Is.EqualTo(CivicHudPanelMode.None));
            controller.ProcessEscape();
            yield return null;
            Assert.That(controller.OverlayView.IsExitPopupOpen, Is.True);
            controller.OverlayView.MainMenuButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }

        private static double[] ProfileFrame(params ProfilerRecorder[] recorders)
        {
            return recorders.Select(recorder => recorder.LastValue / 1_000_000d).ToArray();
        }

        private static string ProfileSummary(string prefix, double[][] samples)
        {
            var names = new[] { "update", "advance", "snapshot", "coreHud", "moduleHud" };
            return prefix + " " + string.Join(" | ", names.Select((name, index) =>
                $"{name}={samples.Average(sample => sample[index]):0.###}ms"));
        }
    }
}
