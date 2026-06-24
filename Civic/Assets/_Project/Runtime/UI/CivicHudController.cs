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
                view.Render(simulation.Snapshot, panelMode, showFoodChildren);
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
            simulation?.TryResearch(technologyId);

            Render();
        }
    }
}
