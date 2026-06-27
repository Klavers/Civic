using System;
using UnityEngine;

namespace Civic.Simulation
{
    [CreateAssetMenu(fileName = "CivicGameDataSource", menuName = "Civic/Game Data Source")]
    public sealed class CivicGameDataSource : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Project/Data/CivicGameDataSource.asset";

        [SerializeField] private TextAsset resourcesCsv;
        [SerializeField] private TextAsset buildingsCsv;
        [SerializeField] private TextAsset technologiesCsv;
        [SerializeField] private TextAsset technologyEffectsCsv;
        [SerializeField] private TextAsset erasCsv;
        [SerializeField] private TextAsset initialStateCsv;

        public TextAsset ResourcesCsv => resourcesCsv;
        public TextAsset BuildingsCsv => buildingsCsv;
        public TextAsset TechnologiesCsv => technologiesCsv;
        public TextAsset TechnologyEffectsCsv => technologyEffectsCsv;
        public TextAsset ErasCsv => erasCsv;
        public TextAsset InitialStateCsv => initialStateCsv;

        public CivicGameData LoadGameData()
        {
            return CivicGameDataLoader.Load(
                RequireText(resourcesCsv, "resourcesCsv"),
                RequireText(buildingsCsv, "buildingsCsv"),
                RequireText(technologiesCsv, "technologiesCsv"),
                RequireText(technologyEffectsCsv, "technologyEffectsCsv"),
                RequireText(erasCsv, "erasCsv"),
                RequireText(initialStateCsv, "initialStateCsv"));
        }

        private static string RequireText(TextAsset asset, string label)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"CivicGameDataSource is missing {label}.");
            }

            return asset.text;
        }
    }
}
