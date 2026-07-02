using System;
using System.Collections.Generic;
using System.Linq;
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
            var balances = CivicModuleBalanceContentLoader.LoadFromResources();
            balances.ValidateAgainst(data);
            var moduleContent = CivicModuleContentLoader.LoadFromResources();
            var achievementRewards = CivicAchievementRewardContentLoader.LoadFromResources(moduleContent.Achievements);
            var civilizations = CivicCivilizationContentLoader.LoadFromResources();
            var politics = CivicPoliticsContentLoader.LoadFromResources();
            var nations = CivicNationContentLoader.LoadFromResources();
            var events = CivicEventContentLoader.LoadFromResources();
            var wonders = CivicWonderContentLoader.LoadFromResources();
            var people = CivicPeopleContentLoader.LoadFromResources();
            ValidateProvisionalEffects(balances, civilizations, politics, nations, events, wonders, people, achievementRewards);
            Debug.Log("CIVIC_DATA_VALIDATION_OK");
            return data;
        }

        private static void ValidateProvisionalEffects(
            CivicModuleBalanceContent balances,
            CivicCivilizationContent civilizations,
            CivicPoliticsContent politics,
            CivicNationContent nations,
            CivicEventContent events,
            CivicWonderContent wonders,
            CivicPeopleContent people,
            IReadOnlyList<CivicAchievementRewardDefinition> achievementRewards)
        {
            var errors = new List<string>();
            var caps = balances.ModifierCaps.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var plannedCount = 0;
            foreach (var effect in civilizations.Effects) { Validate("civilization_effects.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in politics.Effects) { Validate("institution_effects.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in nations.Effects) { Validate("nation_effects.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in events.Effects) { Validate("event_effects.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in wonders.Effects) { Validate("wonder_effects.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in people.Traits) { Validate("person_traits.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var effect in people.Abilities) { Validate("person_abilities.csv", effect.EffectType, effect.TargetId, effect.RuntimeEffectType, effect.RuntimeTargetId, effect.Amount, effect.Duration, effect.CapGroup); }
            foreach (var reward in achievementRewards)
            {
                if (!caps.ContainsKey(reward.CapGroup)) errors.Add("achievement_rewards.csv references unknown cap group: " + reward.CapGroup);
            }
            if (plannedCount != 149) errors.Add("P04 provisional effect baseline must contain 149 planned rows, actual: " + plannedCount);
            if (errors.Count > 0) throw new CivicDataException(errors.ToArray());

            void Validate(string source, string effectType, string targetId, string runtimeEffectType, string runtimeTargetId, double amount, double duration, string capGroup)
            {
                if (effectType == CivicProvisionalEffect.Planned) plannedCount++;
                CivicProvisionalEffect.Validate(source, effectType, targetId, runtimeEffectType, runtimeTargetId, amount, duration, capGroup, caps, errors);
            }
        }
    }
}
