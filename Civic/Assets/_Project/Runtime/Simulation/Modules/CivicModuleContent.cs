using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Civic.Simulation.Modules
{
    public sealed class CivicLegacyPerkDefinition
    {
        public CivicLegacyPerkDefinition(string id, string category, string displayNameKo, int maxRank, string effectType, string targetId, double amount)
        {
            Id = id;
            Category = category;
            DisplayNameKo = displayNameKo;
            MaxRank = maxRank;
            EffectType = effectType;
            TargetId = targetId;
            Amount = amount;
        }

        public string Id { get; }
        public string Category { get; }
        public string DisplayNameKo { get; }
        public int MaxRank { get; }
        public string EffectType { get; }
        public string TargetId { get; }
        public double Amount { get; }
    }

    public sealed class CivicAchievementDefinition
    {
        public CivicAchievementDefinition(
            string id,
            string category,
            string titleKo,
            string descriptionKo,
            string visibility,
            string scope,
            int prestigeReward,
            IReadOnlyList<string> requiredFeatureIds,
            IReadOnlyList<CivicAchievementConditionDefinition> conditions)
        {
            Id = id;
            Category = category;
            TitleKo = titleKo;
            DescriptionKo = descriptionKo;
            Visibility = visibility;
            Scope = scope;
            PrestigeReward = prestigeReward;
            RequiredFeatureIds = requiredFeatureIds;
            Conditions = conditions;
        }

        public string Id { get; }
        public string Category { get; }
        public string TitleKo { get; }
        public string DescriptionKo { get; }
        public string Visibility { get; }
        public string Scope { get; }
        public int PrestigeReward { get; }
        public IReadOnlyList<string> RequiredFeatureIds { get; }
        public IReadOnlyList<CivicAchievementConditionDefinition> Conditions { get; }
    }

    public sealed class CivicAchievementConditionDefinition
    {
        public CivicAchievementConditionDefinition(string achievementId, string metricId, string comparator, double value, double duration, bool forbidden)
        {
            AchievementId = achievementId;
            MetricId = metricId;
            Comparator = comparator;
            Value = value;
            Duration = duration;
            Forbidden = forbidden;
        }

        public string AchievementId { get; }
        public string MetricId { get; }
        public string Comparator { get; }
        public double Value { get; }
        public double Duration { get; }
        public bool Forbidden { get; }
    }

    public sealed class CivicModuleContent
    {
        public CivicModuleContent(IReadOnlyList<CivicLegacyPerkDefinition> legacyPerks, IReadOnlyList<CivicAchievementDefinition> achievements)
        {
            LegacyPerks = legacyPerks;
            Achievements = achievements;
        }

        public IReadOnlyList<CivicLegacyPerkDefinition> LegacyPerks { get; }
        public IReadOnlyList<CivicAchievementDefinition> Achievements { get; }
    }

    public static class CivicModuleContentLoader
    {
        private const string ResourceRoot = "CivicModules/";

        public static CivicModuleContent LoadFromResources()
        {
            return new CivicModuleContent(LoadLegacyPerksFromResources(), LoadAchievementsFromResources());
        }

        public static IReadOnlyList<CivicLegacyPerkDefinition> LoadLegacyPerksFromResources()
        {
            var legacy = Resources.Load<TextAsset>(ResourceRoot + "legacy_perks");
            if (legacy == null)
            {
                throw new CivicDataException(new[] { "legacy_perks.csv resource is missing." });
            }

            var errors = new List<string>();
            var perks = ParseLegacyPerks(legacy.text, errors);
            Validate(perks, Array.Empty<CivicAchievementDefinition>(), errors);
            if (errors.Count > 0)
            {
                throw new CivicDataException(errors.ToArray());
            }

            return perks;
        }

        public static IReadOnlyList<CivicAchievementDefinition> LoadAchievementsFromResources()
        {
            var achievements = Resources.Load<TextAsset>(ResourceRoot + "achievements");
            var conditions = Resources.Load<TextAsset>(ResourceRoot + "achievement_conditions");
            if (achievements == null || conditions == null)
            {
                throw new CivicDataException(new[] { "Achievement CSV resources are missing." });
            }

            var errors = new List<string>();
            var conditionDefinitions = ParseAchievementConditions(conditions.text, errors);
            var definitions = ParseAchievements(achievements.text, conditionDefinitions, errors);
            Validate(Array.Empty<CivicLegacyPerkDefinition>(), definitions, errors);
            if (errors.Count > 0)
            {
                throw new CivicDataException(errors.ToArray());
            }

            return definitions;
        }

        public static CivicModuleContent Parse(string legacyPerksCsv, string achievementsCsv, string conditionsCsv)
        {
            var errors = new List<string>();
            var perks = ParseLegacyPerks(legacyPerksCsv, errors);
            var conditionDefinitions = ParseAchievementConditions(conditionsCsv, errors);
            var achievements = ParseAchievements(achievementsCsv, conditionDefinitions, errors);
            Validate(perks, achievements, errors);
            if (errors.Count > 0)
            {
                throw new CivicDataException(errors.ToArray());
            }

            return new CivicModuleContent(perks, achievements);
        }

        private static IReadOnlyList<CivicLegacyPerkDefinition> ParseLegacyPerks(string csv, ICollection<string> errors)
        {
            return CivicCsvParser.Parse(csv, errors, "legacy_perks.csv")
                .Select(row => new CivicLegacyPerkDefinition(
                    Value(row, "id"),
                    Value(row, "category"),
                    Value(row, "displayNameKo"),
                    Integer(row, "maxRank", errors, "legacy_perks.csv"),
                    Value(row, "effectType"),
                    Value(row, "targetId"),
                    Number(row, "amount", errors, "legacy_perks.csv")))
                .ToArray();
        }

        private static IReadOnlyList<CivicAchievementConditionDefinition> ParseAchievementConditions(string csv, ICollection<string> errors)
        {
            return CivicCsvParser.Parse(csv, errors, "achievement_conditions.csv")
                .Select(row => new CivicAchievementConditionDefinition(
                    Value(row, "achievementId"),
                    Value(row, "metricId"),
                    Value(row, "comparator"),
                    Number(row, "value", errors, "achievement_conditions.csv"),
                    Number(row, "duration", errors, "achievement_conditions.csv"),
                    Boolean(row, "forbidden", errors, "achievement_conditions.csv")))
                .ToArray();
        }

        private static IReadOnlyList<CivicAchievementDefinition> ParseAchievements(
            string csv,
            IReadOnlyList<CivicAchievementConditionDefinition> conditions,
            ICollection<string> errors)
        {
            return CivicCsvParser.Parse(csv, errors, "achievements.csv")
                .Select(row =>
                {
                    var id = Value(row, "id");
                    return new CivicAchievementDefinition(
                        id,
                        Value(row, "category"),
                        Value(row, "titleKo"),
                        Value(row, "descriptionKo"),
                        Value(row, "visibility"),
                        Value(row, "scope"),
                        Integer(row, "prestigeReward", errors, "achievements.csv"),
                        SplitIds(Value(row, "requiredFeatureIds")),
                        conditions.Where(condition => condition.AchievementId == id).ToArray());
                })
                .ToArray();
        }

        private static void Validate(
            IReadOnlyList<CivicLegacyPerkDefinition> perks,
            IReadOnlyList<CivicAchievementDefinition> achievements,
            ICollection<string> errors)
        {
            AddDuplicateErrors(perks.Select(item => item.Id), "legacy perk", errors);
            AddDuplicateErrors(achievements.Select(item => item.Id), "achievement", errors);
            foreach (var perk in perks)
            {
                if (string.IsNullOrWhiteSpace(perk.Id) || string.IsNullOrWhiteSpace(perk.EffectType) || perk.MaxRank <= 0)
                {
                    errors.Add($"Invalid legacy perk definition: {perk.Id}");
                }
            }

            foreach (var achievement in achievements)
            {
                if (string.IsNullOrWhiteSpace(achievement.Id) || achievement.Conditions.Count == 0 || achievement.PrestigeReward < 0)
                {
                    errors.Add($"Invalid achievement definition: {achievement.Id}");
                }

                foreach (var condition in achievement.Conditions)
                {
                    if (condition.Comparator != ">=" && condition.Comparator != "<=" && condition.Comparator != "==")
                    {
                        errors.Add($"Unsupported achievement comparator: {achievement.Id}/{condition.Comparator}");
                    }

                    if (condition.Duration < 0d)
                    {
                        errors.Add($"Achievement duration cannot be negative: {achievement.Id}");
                    }
                }
            }
        }

        private static void AddDuplicateErrors(IEnumerable<string> ids, string label, ICollection<string> errors)
        {
            foreach (var duplicate in ids.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                errors.Add($"Duplicate {label} ID: {duplicate.Key}");
            }
        }

        private static string Value(IReadOnlyDictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        }

        private static int Integer(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            errors.Add($"{source} has invalid integer {key}: {raw}");
            return 0;
        }

        private static double Number(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            errors.Add($"{source} has invalid number {key}: {raw}");
            return 0d;
        }

        private static bool Boolean(IReadOnlyDictionary<string, string> row, string key, ICollection<string> errors, string source)
        {
            var raw = Value(row, key);
            if (bool.TryParse(raw, out var value))
            {
                return value;
            }

            errors.Add($"{source} has invalid boolean {key}: {raw}");
            return false;
        }

        private static IReadOnlyList<string> SplitIds(string value)
        {
            return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
