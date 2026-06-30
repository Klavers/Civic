using System;
using System.Linq;
using Civic.Simulation;
using Civic.Features;
using Civic.Simulation.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Civic.UI
{
    public sealed class CivicHudController : MonoBehaviour
    {
        [SerializeField] private CivicHudView view;
        [SerializeField] private CivicGameDataSource dataSource;
        [SerializeField] private double simulationSpeed = 1d;
        [SerializeField] private CivicModulePanelView modulePanelView;

        private CivicGameSimulation simulation;
        private CivicModuleRuntime moduleRuntime;
        private CivicHudPanelMode panelMode;
        private bool showFoodChildren;
        private string selectedTechnologyEraId;
        private bool prestigeConfirmationPending;

        public CivicHudView View => view;
        public CivicGameSimulation Simulation => simulation;
        public CivicModuleRuntime ModuleRuntime => moduleRuntime;
        public CivicGameDataSource DataSource => dataSource;
        public string SelectedTechnologyEraId => selectedTechnologyEraId;
        public CivicModulePanelView ModulePanelView => modulePanelView;
        public bool HasRequiredReferences => view != null && dataSource != null && modulePanelView != null && modulePanelView.HasRequiredReferences;

        private void Awake()
        {
            CivicFeatureRuntime.EnsureRunStarted();

            if (dataSource != null)
            {
                simulation = new CivicGameSimulation(dataSource.LoadGameData());
                moduleRuntime = new CivicModuleRuntime(simulation, CivicFeatureRuntime.Current);
            }
        }

        private void OnEnable()
        {
            if (view == null)
            {
                return;
            }

            view.ResourcesPanelRequested += ShowResourcesPanel;
            view.BuildingsPanelRequested += ShowBuildingsPanel;
            view.TechnologiesPanelRequested += ShowTechnologiesPanel;
            view.BuildRequested += BuildRequestedBuilding;
            view.ResearchRequested += ResearchRequestedTechnology;
            view.EraTabRequested += SelectTechnologyEra;
            view.FoodToggleRequested += ToggleFoodChildren;
            modulePanelView.ActionRequested += HandleModuleAction;
            Render();
        }

        private void OnDisable()
        {
            if (view == null)
            {
                return;
            }

            view.ResourcesPanelRequested -= ShowResourcesPanel;
            view.BuildingsPanelRequested -= ShowBuildingsPanel;
            view.TechnologiesPanelRequested -= ShowTechnologiesPanel;
            view.BuildRequested -= BuildRequestedBuilding;
            view.ResearchRequested -= ResearchRequestedTechnology;
            view.EraTabRequested -= SelectTechnologyEra;
            view.FoodToggleRequested -= ToggleFoodChildren;
            if (modulePanelView != null) modulePanelView.ActionRequested -= HandleModuleAction;
        }

        private void Update()
        {
            moduleRuntime?.Advance(Time.deltaTime * simulationSpeed);
            Render();
        }

        private void Render()
        {
            if (simulation != null && view != null)
            {
                EnsureSelectedTechnologyEra(simulation.Snapshot);
                view.Render(simulation.Snapshot, panelMode, showFoodChildren, selectedTechnologyEraId);
                modulePanelView.Bind(moduleRuntime, prestigeConfirmationPending);
            }
        }

        private void HandleModuleAction(string featureId, string key)
        {
            if (moduleRuntime == null) return;
            if (featureId == CivicFeatureRegistry.Prestige)
            {
                var module = moduleRuntime.GetModule<CivicPrestigeModule>(featureId);
                if (key != "__prestige")
                {
                    module?.TryPurchaseLegacyPerk(key, out _, out _);
                    prestigeConfirmationPending = false;
                }
                else if (!prestigeConfirmationPending)
                {
                    prestigeConfirmationPending = true;
                }
                else
                {
                    if (module != null && module.TryPrestige(out _))
                    {
                        CivicFeatureRuntime.ResetForMainMenu();
                        CivicRunLaunchSettings.Reset();
                        SceneManager.LoadScene("MainMenu");
                        return;
                    }
                }
            }
            else if (featureId == CivicFeatureRegistry.NationFormation)
            {
                var module = moduleRuntime.GetModule<CivicNationModule>(featureId);
                var candidate = module?.Snapshot.FirstOrDefault(item => item.Definition.Id == key);
                if (candidate?.State == CivicNationCandidateState.Ready) module.TryDeclare(key);
                else if (candidate?.State == CivicNationCandidateState.Preparing) module.CancelPreparation();
                else if (candidate?.State == CivicNationCandidateState.AwaitingCharter) module.TryCompleteFormation();
            }
            else if (featureId == CivicFeatureRegistry.Politics)
            {
                var module = moduleRuntime.GetModule<CivicPoliticsModule>(featureId);
                if (key == "__cancel") module?.CancelReform();
                else module?.TryPropose(key);
            }
            else if (featureId == CivicFeatureRegistry.Events)
            {
                var separator = key.IndexOf('|');
                if (separator > 0) moduleRuntime.GetModule<CivicEventModule>(featureId)?.TryChoose(key.Substring(0, separator), key.Substring(separator + 1));
            }
            else if (featureId == CivicFeatureRegistry.Wonders)
            {
                var module = moduleRuntime.GetModule<CivicWonderModule>(featureId);
                if (module?.ActiveProjectId == key) module.CancelActiveProject();
                else module?.TryStart(key);
            }
            else if (featureId == CivicFeatureRegistry.GreatPeople)
            {
                var module = moduleRuntime.GetModule<CivicPeopleModule>(featureId);
                if (key.StartsWith("recruit:", StringComparison.Ordinal)) module?.TryRecruit(key.Substring("recruit:".Length));
                else if (key.StartsWith("ability:", StringComparison.Ordinal)) module?.TryUseAbility(key.Substring("ability:".Length));
                else if (key.StartsWith("assign:", StringComparison.Ordinal) && module != null)
                {
                    var personId = key.Substring("assign:".Length);
                    var active = module.ActivePeople.FirstOrDefault(item => item.Definition.Id == personId);
                    if (active != null)
                    {
                        var assignments = active.Definition.AllowedAssignments;
                        var currentIndex = assignments.ToList().IndexOf(active.AssignmentId);
                        module.TryAssign(personId, assignments[(currentIndex + 1 + assignments.Count) % assignments.Count]);
                    }
                }
            }

            Render();
        }

        private void ShowResourcesPanel()
        {
            panelMode = CivicHudPanelMode.Resources;
            Render();
        }

        private void ShowBuildingsPanel()
        {
            panelMode = CivicHudPanelMode.Buildings;
            Render();
        }

        private void ShowTechnologiesPanel()
        {
            panelMode = CivicHudPanelMode.Technologies;
            Render();
        }

        private void ToggleFoodChildren()
        {
            showFoodChildren = !showFoodChildren;
            Render();
        }

        private void BuildRequestedBuilding(string buildingId)
        {
            moduleRuntime?.TryBuild(buildingId);

            Render();
        }

        private void ResearchRequestedTechnology(string technologyId)
        {
            moduleRuntime?.TryResearch(technologyId);

            Render();
        }

        private void SelectTechnologyEra(string eraId)
        {
            selectedTechnologyEraId = eraId;
            Render();
        }

        private void EnsureSelectedTechnologyEra(CivicGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible && era.Id == selectedTechnologyEraId)
                {
                    return;
                }
            }

            selectedTechnologyEraId = snapshot.CurrentEraId;
            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible && era.Id == selectedTechnologyEraId)
                {
                    return;
                }
            }

            foreach (var era in snapshot.Eras)
            {
                if (era.IsVisible)
                {
                    selectedTechnologyEraId = era.Id;
                    return;
                }
            }
        }
    }
}
