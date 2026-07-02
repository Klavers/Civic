using System;
using Civic.Features;
using UnityEditor;
using UnityEngine;

namespace Civic.Editor.UI
{
    public static class CivicFeatureMatrixValidator
    {
        [MenuItem("Tools/Civic/Features/Validate Matrix")]
        private static void ValidateFromMenu()
        {
            try
            {
                var count = ValidateAll();
                EditorUtility.DisplayDialog("Civic Feature Matrix", $"{count}개 기능 조합 검증을 통과했습니다.", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Civic Feature Matrix Failed", exception.Message, "OK");
                throw;
            }
        }

        public static int ValidateAll()
        {
            var cases = CivicFeatureMatrix.CreateDefaultCases();
            foreach (var matrixCase in cases)
            {
                var resolution = CivicFeatureResolver.Resolve(matrixCase.RequestedIds);
                if (!resolution.IsValid)
                {
                    throw new InvalidOperationException($"{matrixCase.Name}: {string.Join("; ", resolution.Errors)}");
                }
            }

            Debug.Log($"CIVIC_FEATURE_MATRIX_OK cases={cases.Count}");
            return cases.Count;
        }
    }
}
