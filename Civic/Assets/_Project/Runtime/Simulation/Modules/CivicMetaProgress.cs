using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    [Serializable]
    public sealed class CivicMetaProgress
    {
        public long PrestigePoints;
        public int PrestigeCount;
        public List<string> CompletedAchievementIds = new List<string>();
        public List<string> UnlockedCivilizationIds = new List<string>();
        public List<string> DiscoveredEventIds = new List<string>();
        public List<string> FormedNationIds = new List<string>();
        public List<string> CompletedWonderIds = new List<string>();
        public List<string> DiscoveredPersonIds = new List<string>();
        public List<CivicLegacyPerkRank> LegacyPerkRanks = new List<CivicLegacyPerkRank>();
        public List<CivicCivilizationEraRecord> CivilizationEraRecords = new List<CivicCivilizationEraRecord>();

        public bool HasAchievement(string id) => CompletedAchievementIds.Contains(id);

        public int GetPerkRank(string id)
        {
            var entry = LegacyPerkRanks.Find(item => item.Id == id);
            return entry?.Rank ?? 0;
        }

        public void SetPerkRank(string id, int rank)
        {
            var entry = LegacyPerkRanks.Find(item => item.Id == id);
            if (entry == null)
            {
                LegacyPerkRanks.Add(new CivicLegacyPerkRank { Id = id, Rank = rank });
            }
            else
            {
                entry.Rank = rank;
            }
        }
    }

    [Serializable]
    public sealed class CivicLegacyPerkRank
    {
        public string Id;
        public int Rank;
    }

    [Serializable]
    public sealed class CivicCivilizationEraRecord
    {
        public string CivilizationId;
        public int HighestEraOrder;
    }

    public interface ICivicMetaProgressStore
    {
        CivicMetaProgress Load();
        void Save(CivicMetaProgress progress);
        void Delete();
    }

    public sealed class CivicInMemoryMetaProgressStore : ICivicMetaProgressStore
    {
        private CivicMetaProgress progress;

        public CivicInMemoryMetaProgressStore(CivicMetaProgress initial = null)
        {
            progress = initial ?? new CivicMetaProgress();
        }

        public CivicMetaProgress Load() => progress;

        public void Save(CivicMetaProgress value)
        {
            progress = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Delete()
        {
            progress = new CivicMetaProgress();
        }
    }

    public sealed class CivicJsonMetaProgressStore : ICivicMetaProgressStore
    {
        private const int CurrentVersion = 1;
        private readonly string path;

        [Serializable]
        private sealed class Envelope
        {
            public int Version = CurrentVersion;
            public CivicMetaProgress Progress = new CivicMetaProgress();
        }

        public CivicJsonMetaProgressStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Meta progress path cannot be empty.", nameof(path));
            this.path = Path.GetFullPath(path);
        }

        public string PathName => path;

        public CivicMetaProgress Load()
        {
            if (!File.Exists(path)) return new CivicMetaProgress();
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            if (envelope == null || envelope.Progress == null) throw new InvalidDataException("메타 진행 저장을 읽을 수 없습니다: " + path);
            if (envelope.Version != CurrentVersion) throw new InvalidDataException("지원하지 않는 메타 진행 버전입니다: " + envelope.Version);
            Normalize(envelope.Progress);
            return envelope.Progress;
        }

        public void Save(CivicMetaProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            Normalize(progress);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            var backup = path + ".bak";
            File.WriteAllText(temporary, JsonUtility.ToJson(new Envelope { Progress = progress }, true));
            if (File.Exists(path))
            {
                try { File.Replace(temporary, path, backup); }
                catch (PlatformNotSupportedException) { File.Copy(path, backup, true); File.Delete(path); File.Move(temporary, path); }
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        public void Delete()
        {
            foreach (var candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }

        private static void Normalize(CivicMetaProgress progress)
        {
            progress.CompletedAchievementIds = progress.CompletedAchievementIds ?? new List<string>();
            progress.UnlockedCivilizationIds = progress.UnlockedCivilizationIds ?? new List<string>();
            progress.DiscoveredEventIds = progress.DiscoveredEventIds ?? new List<string>();
            progress.FormedNationIds = progress.FormedNationIds ?? new List<string>();
            progress.CompletedWonderIds = progress.CompletedWonderIds ?? new List<string>();
            progress.DiscoveredPersonIds = progress.DiscoveredPersonIds ?? new List<string>();
            progress.LegacyPerkRanks = progress.LegacyPerkRanks ?? new List<CivicLegacyPerkRank>();
            progress.CivilizationEraRecords = progress.CivilizationEraRecords ?? new List<CivicCivilizationEraRecord>();
        }
    }

    public static class CivicMetaSession
    {
        private static ICivicMetaProgressStore store = CreateDefaultStore();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeStore()
        {
            store = CreateDefaultStore();
        }

        public static ICivicMetaProgressStore Store
        {
            get => store;
            set => store = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static void ResetForTests(CivicMetaProgress progress = null)
        {
            store = new CivicInMemoryMetaProgressStore(progress);
        }

        public static void UsePersistentDefault()
        {
            store = CreateDefaultStore();
        }

        private static ICivicMetaProgressStore CreateDefaultStore()
        {
            return new CivicJsonMetaProgressStore(Path.Combine(Application.persistentDataPath, "Civic", "meta-progress.json"));
        }
    }
}
