using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;
using Civic.Simulation.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicMainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject featurePanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button openFeaturePanelButton;
        [SerializeField] private Button openOptionsPanelButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private Button deleteSaveDataButton;
        [SerializeField] private Text deleteSaveDataStatusLabel;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private CivicFeatureToggleRow[] featureRows;
        [SerializeField] private Text selectionSummaryLabel;
        [SerializeField] private Text validationLabel;
        [SerializeField] private Text implementationNoticeLabel;
        [SerializeField] private GameObject civilizationSection;
        [SerializeField] private Button previousCivilizationButton;
        [SerializeField] private Button nextCivilizationButton;
        [SerializeField] private Text civilizationNameLabel;
        [SerializeField] private Text civilizationDescriptionLabel;

        private readonly HashSet<string> requestedIds = new HashSet<string>(StringComparer.Ordinal);
        private IReadOnlyList<CivicCivilizationDefinition> allCivilizations = Array.Empty<CivicCivilizationDefinition>();
        private IReadOnlyList<CivicCivilizationDefinition> availableCivilizations = Array.Empty<CivicCivilizationDefinition>();
        private string selectedCivilizationId = CivicCivilizationModule.DefaultCivilizationId;
        private bool deleteConfirmationPending;

        public bool HasRequiredReferences =>
            mainPanel != null &&
            featurePanel != null &&
            optionsPanel != null &&
            openFeaturePanelButton != null &&
            openOptionsPanelButton != null &&
            backButton != null &&
            optionsBackButton != null &&
            deleteSaveDataButton != null &&
            deleteSaveDataStatusLabel != null &&
            startGameButton != null &&
            quitButton != null &&
            featureRows != null &&
            featureRows.Length == CivicFeatureRegistry.Features.Count &&
            featureRows.All(row => row != null && row.HasRequiredReferences) &&
            selectionSummaryLabel != null &&
            validationLabel != null &&
            implementationNoticeLabel != null &&
            civilizationSection != null &&
            previousCivilizationButton != null &&
            nextCivilizationButton != null &&
            civilizationNameLabel != null &&
            civilizationDescriptionLabel != null;

        public IReadOnlyList<CivicFeatureToggleRow> FeatureRows => featureRows ?? Array.Empty<CivicFeatureToggleRow>();
        public Button OpenFeaturePanelButton => openFeaturePanelButton;
        public Button OpenOptionsPanelButton => openOptionsPanelButton;
        public Button OptionsBackButton => optionsBackButton;
        public Button DeleteSaveDataButton => deleteSaveDataButton;
        public Button BackButton => backButton;
        public Button StartGameButton => startGameButton;
        public GameObject MainPanel => mainPanel;
        public GameObject FeaturePanel => featurePanel;
        public GameObject OptionsPanel => optionsPanel;
        public GameObject CivilizationSection => civilizationSection;
        public Button PreviousCivilizationButton => previousCivilizationButton;
        public Button NextCivilizationButton => nextCivilizationButton;
        public string SelectedCivilizationId => selectedCivilizationId;

        private void Awake()
        {
            if (CivicFeatureRuntime.IsRunLocked)
            {
                CivicFeatureRuntime.ResetForMainMenu();
            }

            requestedIds.Clear();
            foreach (var featureId in CivicFeatureRuntime.Current.RequestedIds)
            {
                requestedIds.Add(featureId);
            }
            allCivilizations = CivicCivilizationContentLoader.LoadFromResources().Civilizations;
            selectedCivilizationId = CivicRunLaunchSettings.StartingCivilizationId;
        }

        private void OnEnable()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError("CivicMainMenuController has missing prefab references.", this);
                return;
            }

            openFeaturePanelButton.onClick.AddListener(OpenFeaturePanel);
            openOptionsPanelButton.onClick.AddListener(OpenOptionsPanel);
            backButton.onClick.AddListener(OpenMainPanel);
            optionsBackButton.onClick.AddListener(OpenMainPanel);
            deleteSaveDataButton.onClick.AddListener(DeleteSaveDataRequested);
            startGameButton.onClick.AddListener(StartGame);
            quitButton.onClick.AddListener(QuitGame);
            previousCivilizationButton.onClick.AddListener(SelectPreviousCivilization);
            nextCivilizationButton.onClick.AddListener(SelectNextCivilization);
            BindRows();
            OpenMainPanel();
        }

        private void OnDisable()
        {
            if (openFeaturePanelButton != null)
            {
                openFeaturePanelButton.onClick.RemoveListener(OpenFeaturePanel);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OpenMainPanel);
            }

            if (openOptionsPanelButton != null) openOptionsPanelButton.onClick.RemoveListener(OpenOptionsPanel);
            if (optionsBackButton != null) optionsBackButton.onClick.RemoveListener(OpenMainPanel);
            if (deleteSaveDataButton != null) deleteSaveDataButton.onClick.RemoveListener(DeleteSaveDataRequested);

            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
            }
            if (previousCivilizationButton != null) previousCivilizationButton.onClick.RemoveListener(SelectPreviousCivilization);
            if (nextCivilizationButton != null) nextCivilizationButton.onClick.RemoveListener(SelectNextCivilization);
        }

        private void BindRows()
        {
            for (var index = 0; index < featureRows.Length; index++)
            {
                var definition = CivicFeatureRegistry.Features[index];
                featureRows[index].gameObject.SetActive(true);
                featureRows[index].Bind(definition, requestedIds.Contains(definition.Id), HandleFeatureChanged);
            }

            RenderResolution();
        }

        private void HandleFeatureChanged(string featureId, bool isEnabled)
        {
            if (isEnabled)
            {
                requestedIds.Add(featureId);
            }
            else
            {
                requestedIds.Remove(featureId);
            }

            RenderResolution();
        }

        private void RenderResolution()
        {
            var resolution = CivicFeatureRuntime.Preview(requestedIds);
            var civilizationValid = RenderCivilizationSelection(resolution, out var civilizationBlockingReason);
            selectionSummaryLabel.text =
                $"선택 모듈 {resolution.EnabledFeatureIds.Count}개 / 자동 연계 {resolution.EnabledIntegrationIds.Count}개";

            if (resolution.IsValid && civilizationValid)
            {
                validationLabel.text = "구성을 시작할 수 있습니다.";
                validationLabel.color = new Color(0.48f, 0.92f, 0.58f, 1f);
                startGameButton.interactable = true;
            }
            else
            {
                validationLabel.text = string.Join("\n", resolution.Errors.Concat(new[] { civilizationBlockingReason }).Where(item => !string.IsNullOrWhiteSpace(item)));
                validationLabel.color = new Color(1f, 0.46f, 0.42f, 1f);
                startGameButton.interactable = false;
            }

            implementationNoticeLabel.text =
                "모듈 구성은 이번 런 동안 고정됩니다. 필요한 연계 기능은 선택된 모듈 조합에 따라 자동 적용됩니다.";
        }

        private bool RenderCivilizationSelection(CivicFeatureResolution resolution, out string blockingReason)
        {
            var enabled = resolution.IsEnabled(CivicFeatureRegistry.StartCivilizations);
            civilizationSection.SetActive(enabled);
            if (!enabled)
            {
                selectedCivilizationId = CivicCivilizationModule.DefaultCivilizationId;
                blockingReason = string.Empty;
                return true;
            }

            availableCivilizations = allCivilizations;
            if (availableCivilizations.All(item => item.Id != selectedCivilizationId)) selectedCivilizationId = CivicCivilizationModule.DefaultCivilizationId;
            var selected = availableCivilizations.First(item => item.Id == selectedCivilizationId);
            var missing = selected.RequiredFeatureIds.Where(id => !resolution.IsEnabled(id)).ToArray();
            civilizationNameLabel.text = selected.DisplayNameKo + " · " + selected.Difficulty;
            civilizationDescriptionLabel.text = selected.DescriptionKo + (missing.Length == 0 ? "" : "\n필요 모듈: " + string.Join(", ", missing));
            previousCivilizationButton.interactable = availableCivilizations.Count > 1;
            nextCivilizationButton.interactable = availableCivilizations.Count > 1;
            blockingReason = missing.Length == 0 ? string.Empty : $"{selected.DisplayNameKo} 시작 조건 미충족: {string.Join(", ", missing)}";
            return missing.Length == 0;
        }

        private void SelectPreviousCivilization()
        {
            MoveCivilization(-1);
        }

        private void SelectNextCivilization()
        {
            MoveCivilization(1);
        }

        private void MoveCivilization(int delta)
        {
            if (availableCivilizations.Count == 0) return;
            var index = availableCivilizations.ToList().FindIndex(item => item.Id == selectedCivilizationId);
            index = (index + delta + availableCivilizations.Count) % availableCivilizations.Count;
            selectedCivilizationId = availableCivilizations[index].Id;
            RenderResolution();
        }

        private void OpenFeaturePanel()
        {
            mainPanel.SetActive(false);
            optionsPanel.SetActive(false);
            featurePanel.SetActive(true);
        }

        private void OpenOptionsPanel()
        {
            mainPanel.SetActive(false);
            featurePanel.SetActive(false);
            optionsPanel.SetActive(true);
            deleteConfirmationPending = false;
            deleteSaveDataStatusLabel.text = "메타 진행, 환생 포인트, 도전과제, 도감 기록을 삭제합니다.";
            deleteSaveDataButton.GetComponentInChildren<Text>().text = "세이브데이터 삭제";
        }

        private void OpenMainPanel()
        {
            featurePanel.SetActive(false);
            optionsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void DeleteSaveDataRequested()
        {
            if (!deleteConfirmationPending)
            {
                deleteConfirmationPending = true;
                deleteSaveDataStatusLabel.text = "삭제 후 복구할 수 없습니다. 한 번 더 누르면 삭제합니다.";
                deleteSaveDataButton.GetComponentInChildren<Text>().text = "정말 삭제";
                return;
            }

            CivicMetaSession.Store.Delete();
            deleteConfirmationPending = false;
            deleteSaveDataStatusLabel.text = "세이브데이터를 삭제했습니다.";
            deleteSaveDataButton.GetComponentInChildren<Text>().text = "세이브데이터 삭제";
        }

        private void StartGame()
        {
            var resolution = CivicFeatureRuntime.SetPending(requestedIds);
            if (!resolution.IsValid)
            {
                RenderResolution();
                return;
            }

            CivicFeatureRuntime.BeginRun();
            CivicRunLaunchSettings.RunSeed = Guid.NewGuid().GetHashCode();
            CivicRunLaunchSettings.StartingCivilizationId = resolution.IsEnabled(CivicFeatureRegistry.StartCivilizations)
                ? selectedCivilizationId
                : CivicCivilizationModule.DefaultCivilizationId;
            SceneManager.LoadScene("SampleScene");
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("CIVIC_MAIN_MENU_QUIT_REQUESTED");
#else
            Application.Quit();
#endif
        }
    }
}
