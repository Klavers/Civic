using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civic.Features
{
    [Serializable]
    public sealed class CivicModulePayloadHeader
    {
        public string ModuleId;
        public int ModuleVersion = 1;
        public int PayloadVersion = 1;
    }

    [Serializable]
    public sealed class CivicRunFeatureSaveHeader
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = CurrentFormatVersion;
        public int RunSeed = 1;
        public List<string> RequestedFeatureIds = new List<string>();
        public List<string> ResolvedFeatureIds = new List<string>();
        public List<string> ResolvedIntegrationIds = new List<string>();
        public List<CivicModulePayloadHeader> ModulePayloads = new List<CivicModulePayloadHeader>();

        public static CivicRunFeatureSaveHeader Capture(CivicFeatureResolution resolution, int runSeed = 1)
        {
            if (resolution == null || !resolution.IsValid) throw new ArgumentException("A valid feature resolution is required.", nameof(resolution));
            return new CivicRunFeatureSaveHeader
            {
                RequestedFeatureIds = resolution.RequestedIds.ToList(),
                RunSeed = runSeed,
                ResolvedFeatureIds = resolution.EnabledFeatureIds.ToList(),
                ResolvedIntegrationIds = resolution.EnabledIntegrationIds.ToList(),
                ModulePayloads = resolution.EnabledFeatureIds
                    .Concat(resolution.EnabledIntegrationIds)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => new CivicModulePayloadHeader { ModuleId = id })
                    .ToList()
            };
        }

        public CivicFeatureResolution ValidateAndResolve()
        {
            if (FormatVersion != CurrentFormatVersion) throw new InvalidOperationException("지원하지 않는 런 저장 feature 형식 버전입니다: " + FormatVersion);
            EnsureUnique(RequestedFeatureIds, "requested feature");
            EnsureUnique(ResolvedFeatureIds, "resolved feature");
            EnsureUnique(ResolvedIntegrationIds, "resolved integration");
            EnsureUnique(ModulePayloads.Select(item => item?.ModuleId), "module payload");
            if (ModulePayloads.Any(item => item == null || item.ModuleVersion <= 0 || item.PayloadVersion <= 0)) throw new InvalidOperationException("유효하지 않은 module payload header가 있습니다.");

            var resolution = CivicFeatureResolver.Resolve(RequestedFeatureIds);
            if (!resolution.IsValid) throw new InvalidOperationException(string.Join("\n", resolution.Errors));
            EnsureSameSet(ResolvedFeatureIds, resolution.EnabledFeatureIds, "resolved feature");
            EnsureSameSet(ResolvedIntegrationIds, resolution.EnabledIntegrationIds, "resolved integration");
            var expectedModules = resolution.EnabledFeatureIds.Concat(resolution.EnabledIntegrationIds);
            EnsureSameSet(ModulePayloads.Select(item => item.ModuleId), expectedModules, "module payload");
            return resolution;
        }

        public string ToJson(bool prettyPrint = false)
        {
            ValidateAndResolve();
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static CivicRunFeatureSaveHeader FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Run feature save JSON cannot be empty.", nameof(json));
            var header = JsonUtility.FromJson<CivicRunFeatureSaveHeader>(json) ?? throw new InvalidOperationException("런 저장 feature header를 읽을 수 없습니다.");
            header.RequestedFeatureIds = header.RequestedFeatureIds ?? new List<string>();
            header.ResolvedFeatureIds = header.ResolvedFeatureIds ?? new List<string>();
            header.ResolvedIntegrationIds = header.ResolvedIntegrationIds ?? new List<string>();
            header.ModulePayloads = header.ModulePayloads ?? new List<CivicModulePayloadHeader>();
            header.ValidateAndResolve();
            return header;
        }

        private static void EnsureUnique(IEnumerable<string> ids, string label)
        {
            var values = (ids ?? Array.Empty<string>()).ToArray();
            if (values.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException(label + " ID가 비어 있습니다.");
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidOperationException(label + " ID가 중복되었습니다.");
        }

        private static void EnsureSameSet(IEnumerable<string> saved, IEnumerable<string> expected, string label)
        {
            if (!new HashSet<string>(saved ?? Array.Empty<string>(), StringComparer.Ordinal).SetEquals(expected ?? Array.Empty<string>()))
            {
                throw new InvalidOperationException("저장된 " + label + " 집합이 현재 resolver 결과와 일치하지 않습니다.");
            }
        }
    }
}
