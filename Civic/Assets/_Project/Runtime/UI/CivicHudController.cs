using Civic.Simulation;
using UnityEngine;

namespace Civic.UI
{
    public sealed class CivicHudController : MonoBehaviour
    {
        [SerializeField] private CivicHudView view;
        [SerializeField] private CivicGameDataSource dataSource;
        [SerializeField] private double simulationSpeed = 1d;

        private CivicGameSimulation simulation;
        private CivicHudPanelMode panelMode;
        private bool showFoodChildren;
        private string selectedTechnologyEraId;

        public CivicHudView View => view;
        public CivicGameSimulation Simulation => simulation;
        public CivicGameDataSource DataSource => dataSource;
        public bool HasRequiredReferences => view != null && dataSource != null;

        private void Awake()
        {
            if (dataSource != null)
            {
                simulation = new CivicGameSimulation(dataSource.LoadGameData());
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
        }

        private void Update()
        {
            simulation?.Advance(Time.deltaTime * simulationSpeed);
            Render();
        }

        private void Render()
        {
            if (simulation != null && view != null)
            {
                EnsureSelectedTechnologyEra(simulation.Snapshot);
                view.Render(simulation.Snapshot, panelMode, showFoodChildren, selectedTechnologyEraId);
            }
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
            simulation?.TryBuild(buildingId);

            Render();
        }

        private void ResearchRequestedTechnology(string technologyId)
        {
            if (simulation != null && simulation.TryResearch(technologyId))
            {
                selectedTechnologyEraId = simulation.Snapshot.CurrentEraId;
            }

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
