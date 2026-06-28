using System;
using Civic.Simulation;
using Civic.Simulation.Modules;
using UnityEditor;
using UnityEngine;

namespace Civic.Editor.UI
{
    public static class CivicDataValidator
    {
        [MenuItem("Tools/Civic/Data/Validate")]
        private static void ValidateFromMenu()
        {
            try
            {
                ValidateAll();
                EditorUtility.DisplayDialog("Civic Data Validation", "Data validation passed.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic Data Validation Failed", exception.Message, "OK");
                throw;
            }
        }

        public static CivicGameData ValidateAll()
        {
            var dataSource = AssetDatabase.LoadAssetAtPath<CivicGameDataSource>(CivicGameDataSource.DefaultAssetPath);
            if (dataSource == null)
            {
                throw new InvalidOperationException($"Missing data source asset: {CivicGameDataSource.DefaultAssetPath}");
            }

            var data = dataSource.LoadGameData();
            CivicModuleContentLoader.LoadFromResources();
            CivicCivilizationContentLoader.LoadFromResources();
            CivicPoliticsContentLoader.LoadFromResources();
            CivicNationContentLoader.LoadFromResources();
            CivicEventContentLoader.LoadFromResources();
            CivicWonderContentLoader.LoadFromResources();
            CivicPeopleContentLoader.LoadFromResources();
            Debug.Log("CIVIC_DATA_VALIDATION_OK");
            return data;
        }
    }
}
