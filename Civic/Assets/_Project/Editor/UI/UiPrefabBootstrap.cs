using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Civic.Editor.UI
{
    [InitializeOnLoad]
    internal static class UiPrefabBootstrap
    {
        static UiPrefabBootstrap()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                EditorApplication.delayCall += GenerateInitialAssets;
            }
        }

        private static void GenerateInitialAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabGenerator.UiRootPath) != null)
            {
                return;
            }

            try
            {
                UiPrefabGenerator.GenerateAssetsAt(
                    UiPrefabGenerator.GeneratedFolder,
                    UiPrefabGenerator.EditableFolder);

                var scene = SceneManager.GetActiveScene();
                if (scene.path == UiPrefabGenerator.SampleScenePath && !scene.isDirty)
                {
                    UiPrefabGenerator.InstallInSampleScene();
                }
                else
                {
                    Debug.Log("Civic UI prefabs were generated. Open a clean SampleScene and use Tools > Civic > UI > Generate to install them.");
                }

                UiPrefabValidator.ValidateAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
