using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public sealed class CivicPersonCandidateSnapshot
    {
        public CivicPersonCandidateSnapshot(CivicPersonDefinition definition, double remainingSeconds)
        {
            Definition = definition;
            RemainingSeconds = remainingSeconds;
        }

        public CivicPersonDefinition Definition { get; }
        public double RemainingSeconds { get; }
    }

    public sealed class CivicActivePersonSnapshot
    {
        public CivicActivePersonSnapshot(CivicPersonDefinition definition, string assignmentId, double tenureRemaining, int abilityUsesRemaining)
        {
            Definition = definition;
            AssignmentId = assignmentId;
            TenureRemaining = tenureRemaining;
            AbilityUsesRemaining = abilityUsesRemaining;
        }

        public CivicPersonDefinition Definition { get; }
        public string AssignmentId { get; }
        public double TenureRemaining { get; }
        public int AbilityUsesRemaining { get; }
    }

    public sealed class CivicPeopleModule : CivicGameplayModuleBase
    {
        private const int CandidateLimit = 3;
        private const int ActiveSlotLimit = 3;
        private const double CandidateLifetime = 120d;
        private const double RetiredLegacyRatio = 0.10d;

        private readonly CivicPeopleContent content;
        private readonly Dictionary<string, double> candidateRemaining = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> appearedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> tenureRemaining = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> abilityUses = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> timedAbilityRemaining = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> retiredIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<object> inactiveEffects = new List<object>();

        public CivicPeopleModule(CivicPeopleContent content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public override string FeatureId => CivicFeatureRegistry.GreatPeople;
        public IReadOnlyList<CivicPersonCandidateSnapshot> Candidates => candidateRemaining
            .Select(pair => new CivicPersonCandidateSnapshot(Person(pair.Key), pair.Value)).ToArray();
        public IReadOnlyList<CivicActivePersonSnapshot> ActivePeople => tenureRemaining
            .Select(pair => new CivicActivePersonSnapshot(
                Person(pair.Key),
                assignments.TryGetValue(pair.Key, out var assignment) ? assignment : string.Empty,
                pair.Value,
                RemainingAbilityUses(pair.Key)))
            .ToArray();
        public IReadOnlyCollection<string> RetiredIds => retiredIds;
        public IReadOnlyList<object> InactiveEffects => inactiveEffects;

        public override void Initialize(CivicModuleContext context)
        {
            base.Initialize(context);
            EvaluateCandidates();
            PublishMetrics();
        }

        public override void AfterAdvance(double seconds)
        {
            var elapsed = Math.Max(0d, seconds);
            foreach (var id in candidateRemaining.Keys.ToArray())
            {
                candidateRemaining[id] -= elapsed;
                if (candidateRemaining[id] <= 0d) candidateRemaining.Remove(id);
            }

            foreach (var id in tenureRemaining.Keys.ToArray())
            {
                tenureRemaining[id] -= elapsed;
                if (tenureRemaining[id] <= 0d) Retire(id);
            }

            foreach (var sourceId in timedAbilityRemaining.Keys.ToArray())
            {
                timedAbilityRemaining[sourceId] -= elapsed;
                if (timedAbilityRemaining[sourceId] > 0d) continue;
                Context.Modifiers.RemoveSource("personAbility", sourceId);
                timedAbilityRemaining.Remove(sourceId);
            }

            EvaluateCandidates();
            PublishMetrics();
        }

        public bool TryRecruit(string personId)
        {
            if (!candidateRemaining.ContainsKey(personId) || tenureRemaining.Count >= ActiveSlotLimit) return false;
            var person = Person(personId);
            candidateRemaining.Remove(personId);
            tenureRemaining[personId] = person.BaseTenure;
            assignments[personId] = person.AllowedAssignments[0];
            var ability = content.Abilities.FirstOrDefault(item => item.PersonId == personId);
            abilityUses[personId] = ability?.UsesPerRun ?? 0;
            if (!Context.MetaProgress.DiscoveredPersonIds.Contains(personId)) Context.MetaProgress.DiscoveredPersonIds.Add(personId);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            RebuildTraitModifiers();
            return true;
        }

        public bool TryAssign(string personId, string assignmentId)
        {
            if (!tenureRemaining.ContainsKey(personId)) return false;
            var person = Person(personId);
            if (!person.AllowedAssignments.Contains(assignmentId)) return false;
            assignments[personId] = assignmentId;
            RebuildTraitModifiers();
            return true;
        }

        public bool TryUseAbility(string personId)
        {
            if (!tenureRemaining.ContainsKey(personId) || RemainingAbilityUses(personId) <= 0) return false;
            var ability = content.Abilities.FirstOrDefault(item => item.PersonId == personId);
            if (ability == null) return false;
            abilityUses[personId]--;
            if (ability.EffectType == "resourceGrant")
            {
                Context.Simulation.GrantResource(ability.TargetId, CivicNumber.FromDouble(ability.Amount));
            }
            else if (ability.EffectType == "planned")
            {
                if (!inactiveEffects.Contains(ability)) inactiveEffects.Add(ability);
            }
            else
            {
                var sourceId = personId + ":" + ability.Id;
                Context.Modifiers.ReplaceSource("personAbility", sourceId, new[]
                {
                    new CivicModifierEntry("personAbility", sourceId, ability.EffectType, ability.TargetId, ability.Amount)
                });
                if (ability.Duration > 0d) timedAbilityRemaining[sourceId] = ability.Duration;
            }

            Context.Simulation.RefreshSnapshot();
            return true;
        }

        public bool DismissCandidate(string personId)
        {
            return candidateRemaining.Remove(personId);
        }

        private void EvaluateCandidates()
        {
            if (candidateRemaining.Count >= CandidateLimit) return;
            var eligible = content.People
                .Where(person => !appearedIds.Contains(person.Id) && IsAvailable(person) && ConditionsSatisfied(person))
                .OrderBy(person => StableOrder(person.Id))
                .Take(CandidateLimit - candidateRemaining.Count);
            foreach (var person in eligible)
            {
                appearedIds.Add(person.Id);
                candidateRemaining[person.Id] = CandidateLifetime;
            }
        }

        private bool IsAvailable(CivicPersonDefinition person)
        {
            return person.RequiredFeatureIds.All(Context.IsFeatureEnabled);
        }

        private bool ConditionsSatisfied(CivicPersonDefinition person)
        {
            return content.Conditions.Where(item => item.PersonId == person.Id).All(condition =>
                CivicConditionEvaluator.Compare(
                    Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress),
                    condition.Comparator,
                    condition.Value));
        }

        private void RebuildTraitModifiers()
        {
            foreach (var personId in tenureRemaining.Keys)
            {
                Context.Modifiers.RemoveSource("person", personId);
            }

            var assignmentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var personId in tenureRemaining.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var assignment = assignments[personId];
                assignmentCounts[assignment] = assignmentCounts.TryGetValue(assignment, out var count) ? count + 1 : 1;
                var scale = assignmentCounts[assignment] == 1 ? 1d : assignmentCounts[assignment] == 2 ? 0.70d : 0.50d;
                foreach (var trait in content.Traits.Where(item => item.PersonId == personId))
                {
                    if (trait.EffectType == "planned")
                    {
                        if (!inactiveEffects.Contains(trait)) inactiveEffects.Add(trait);
                        continue;
                    }

                    Context.Modifiers.Add(new CivicModifierEntry("person", personId, trait.EffectType, trait.TargetId, trait.Amount * scale, trait.CapGroup));
                }
            }

            Context.Simulation.RefreshSnapshot();
        }

        private void Retire(string personId)
        {
            tenureRemaining.Remove(personId);
            assignments.Remove(personId);
            Context.Modifiers.RemoveSource("person", personId);
            retiredIds.Add(personId);
            foreach (var trait in content.Traits.Where(item => item.PersonId == personId && item.EffectType != "planned"))
            {
                Context.Modifiers.Add(new CivicModifierEntry("personLegacy", personId, trait.EffectType, trait.TargetId, trait.Amount * RetiredLegacyRatio, trait.CapGroup));
            }

            RebuildTraitModifiers();
            PublishMetrics();
        }

        private int RemainingAbilityUses(string personId) => abilityUses.TryGetValue(personId, out var value) ? value : 0;
        private CivicPersonDefinition Person(string id) => content.People.First(item => item.Id == id);

        private uint StableOrder(string personId)
        {
            var text = Context.Telemetry.RunId + ":" + personId;
            var hash = 2166136261u;
            foreach (var character in text)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }

        private void PublishMetrics()
        {
            Context.Telemetry.SetExternalMetric("person.retiredCount", retiredIds.Count);
            Context.Telemetry.SetExternalMetric("person.activeCount", tenureRemaining.Count);
            Context.Telemetry.SetExternalMetric("person.sameAssignmentPair", assignments.Values.GroupBy(item => item, StringComparer.Ordinal).Any(group => group.Count() >= 2) ? 1d : 0d);
            Context.Telemetry.SetExternalMetric("person.engineerRetirementRemaining", tenureRemaining.Where(pair => Person(pair.Key).ArchetypeId == "engineer").Select(pair => pair.Value).DefaultIfEmpty(double.MaxValue).Min());
        }
    }
}
