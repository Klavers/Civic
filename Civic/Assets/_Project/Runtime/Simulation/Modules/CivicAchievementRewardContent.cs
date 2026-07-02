using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicAchievementRewardDefinition
    {
        public CivicAchievementRewardDefinition(string achievementId, string effectType, string targetId, double amount, string capGroup)
        {
            AchievementId = achievementId;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
            CapGroup = capGroup;
        }

        public string AchievementId { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
        public string CapGroup { get; }
    }

    public static class CivicAchievementRewardContentLoader
    {
        private const string ResourcePath = "CivicModules/achievement_rewards";

        public static IReadOnlyList<CivicAchievementRewardDefinition> LoadFromResources(IReadOnlyList<CivicAchievementDefinition> achievements)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new CivicDataException(new[] { "achievement_rewards.csv resource is missing." });
            return Parse(asset.text, achievements);
        }

        public static IReadOnlyList<CivicAchievementRewardDefinition> Parse(string csv, IReadOnlyList<CivicAchievementDefinition> achievements)
        {
            var errors = new List<string>();
            var result = CivicCsvParser.Parse(csv, errors, "achievement_rewards.csv")
                .Select(row => new CivicAchievementRewardDefinition(
                    Value(row, "achievementId"),
                    Value(row, "effectType"),
                    Value(row, "targetId"),
                    Number(row, "amount", errors),
                    Value(row, "capGroup")))
                .ToArray();
            var ids = new HashSet<string>((achievements ?? Array.Empty<CivicAchievementDefinition>()).Select(item => item.Id), StringComparer.Ordinal);
            foreach (var reward in result)
            {
                if (!ids.Contains(reward.AchievementId)) errors.Add("Unknown achievement reward reference: " + reward.AchievementId);
                if (string.IsNullOrWhiteSpace(reward.EffectType) || Math.Abs(reward.Amount) <= 1e-12d || string.IsNullOrWhiteSpace(reward.CapGroup)) errors.Add("Invalid achievement reward: " + reward.AchievementId);
            }
            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());
            return result;
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key) => row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
            errors.Add("achievement_rewards.csv has invalid amount: " + raw);
            return 0d;
        }
    }
}
