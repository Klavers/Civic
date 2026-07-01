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

    public sealed class CivicPersonPositionSnapshot
    {
        public CivicPersonPositionSnapshot(CivicPersonPositionDefinition definition, CivicActivePersonSnapshot occupant)
        {
            Definition = definition;
            Occupant = occupant;
        }

        public CivicPersonPositionDefinition Definition { get; }
        public CivicActivePersonSnapshot Occupant { get; }
    }

    public sealed class CivicPeopleModule : CivicGameplayModuleBase
    {
        private const int CandidateLimit = 3;
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
        public IReadOnlyList<CivicPersonPositionDefinition> Positions => content.Positions
            .Where(position => position.RequiredFeatureIds.All(Context.IsFeatureEnabled))
            .OrderBy(position => position.Order)
            .ToArray();
        public IReadOnlyList<CivicPersonDefinition> Definitions => content.People;
        public IReadOnlyList<CivicPersonCandidateSnapshot> Candidates => candidateRemaining
            .Select(pair => new CivicPersonCandidateSnapshot(Person(pair.Key), pair.Value)).ToArray();
        public IReadOnlyList<CivicActivePersonSnapshot> ActivePeople => tenureRemaining
            .Select(pair => new CivicActivePersonSnapshot(
                Person(pair.Key),
                assignments.TryGetValue(pair.Key, out var assignment) ? assignment : string.Empty,
                pair.Value,
                RemainingAbilityUses(pair.Key)))
            .ToArray();
        public IReadOnlyList<CivicPersonPositionSnapshot> PositionSnapshots => Positions
            .Select(position => new CivicPersonPositionSnapshot(
                position,
                ActivePeople.FirstOrDefault(person => person.AssignmentId == position.Id)))
            .ToArray();
        public int ActiveSlotLimit => Positions.Count;
        public int AssignedPeopleCount => assignments.Count(pair => !string.IsNullOrEmpty(pair.Value));
        public IReadOnlyCollection<string> RetiredIds => retiredIds;
        public IReadOnlyList<object> InactiveEffects => inactiveEffects;
        public int ProvisionalEffectCount => content.Traits.Count(item => item.EffectType == CivicProvisionalEffect.Planned) + content.Abilities.Count(item => item.EffectType == CivicProvisionalEffect.Planned);

        public IReadOnlyList<CivicPersonConditionDefinition> ConditionsFor(string personId) => content.Conditions.Where(item => item.PersonId == personId).ToArray();
        public IReadOnlyList<CivicPersonEffectDefinition> TraitsFor(string personId) => content.Traits.Where(item => item.PersonId == personId).ToArray();
        public IReadOnlyList<CivicPersonEffectDefinition> TraitsFor(string personId, string positionId) => content.Traits
            .Where(item => item.PersonId == personId && item.PositionId == positionId)
            .ToArray();
        public IReadOnlyList<CivicPersonAbilityDefinition> AbilitiesFor(string personId) => content.Abilities.Where(item => item.PersonId == personId).ToArray();
        public IReadOnlyList<CivicMetricConditionSnapshot> ConditionStatusFor(string personId) => ConditionsFor(personId)
            .Select(condition =>
            {
                var current = Context.Telemetry.GetMetric(condition.MetricId, Context.MetaProgress);
                return new CivicMetricConditionSnapshot(
                    condition.MetricId,
                    condition.Comparator,
                    condition.Value,
                    current,
                    CivicConditionEvaluator.Compare(current, condition.Comparator, condition.Value));
            }).ToArray();

        public double AssignmentScaleFor(string personId)
        {
            return assignments.TryGetValue(personId, out var positionId) && !string.IsNullOrEmpty(positionId) ? 1d : 0d;
        }

        public bool DebugOfferCandidate(string personId)
        {
            if (content.People.All(item => item.Id != personId) || tenureRemaining.ContainsKey(personId)) return false;
            if (candidateRemaining.Count >= CandidateLimit && !candidateRemaining.ContainsKey(personId))
            {
                var oldest = candidateRemaining.Keys.OrderBy(id => id, StringComparer.Ordinal).First();
                candidateRemaining.Remove(oldest);
            }
            appearedIds.Add(personId);
            candidateRemaining[personId] = CandidateLifetime;
            return true;
        }

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
            assignments[personId] = string.Empty;
            var ability = content.Abilities.FirstOrDefault(item => item.PersonId == personId);
            abilityUses[personId] = ability?.UsesPerRun ?? 0;
            if (!Context.MetaProgress.DiscoveredPersonIds.Contains(personId)) Context.MetaProgress.DiscoveredPersonIds.Add(personId);
            CivicMetaSession.Store.Save(Context.MetaProgress);
            RebuildTraitModifiers();
            return true;
        }

        public bool TryAssign(string personId, string positionId)
        {
            if (!tenureRemaining.ContainsKey(personId)) return false;
            var person = Person(personId);
            if (!person.AllowedPositionIds.Contains(positionId) || Positions.All(position => position.Id != positionId)) return false;
            var previousOccupant = assignments.FirstOrDefault(pair => pair.Key != personId && pair.Value == positionId).Key;
            if (!string.IsNullOrEmpty(previousOccupant)) assignments[previousOccupant] = string.Empty;
            assignments[personId] = positionId;
            RebuildTraitModifiers();
            PublishMetrics();
            return true;
        }

        public bool TryUnassign(string personId)
        {
            if (!assignments.TryGetValue(personId, out var positionId) || string.IsNullOrEmpty(positionId)) return false;
            assignments[personId] = string.Empty;
            RebuildTraitModifiers();
            PublishMetrics();
            return true;
        }

        public bool TryUseAbility(string personId)
        {
            if (!tenureRemaining.ContainsKey(personId) ||
                !assignments.TryGetValue(personId, out var positionId) || string.IsNullOrEmpty(positionId) ||
                RemainingAbilityUses(personId) <= 0) return false;
            var ability = content.Abilities.FirstOrDefault(item => item.PersonId == personId);
            if (ability == null) return false;
            abilityUses[personId]--;
            var resolved = ability.Resolve();
            if (resolved.EffectType == "resourceGrant")
            {
                Context.Simulation.GrantResource(resolved.TargetId, CivicNumber.FromDouble(resolved.Amount));
            }
            else
            {
                var sourceId = personId + ":" + ability.Id;
                Context.Modifiers.ReplaceSource("personAbility", sourceId, new[]
                {
                    new CivicModifierEntry("personAbility", sourceId, resolved.EffectType, resolved.TargetId, resolved.Amount, resolved.CapGroup)
                });
                if (resolved.Duration > 0d) timedAbilityRemaining[sourceId] = resolved.Duration;
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
                candidateRemaining[person.Id] = CandidateLifetime * Context.Modifiers.Multiplier(CivicModifierEffectTypes.PersonCandidateWeightMultiplier, person.Id, "*");
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

            foreach (var personId in tenureRemaining.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var positionId = assignments[personId];
                if (string.IsNullOrEmpty(positionId)) continue;
                foreach (var trait in TraitsFor(personId, positionId))
                {
                    var resolved = trait.Resolve();
                    Context.Modifiers.Add(new CivicModifierEntry("person", personId, resolved.EffectType, resolved.TargetId, resolved.Amount, resolved.CapGroup));
                }
            }

            Context.Simulation.RefreshSnapshot();
        }

        private void Retire(string personId)
        {
            var lastPositionId = assignments.TryGetValue(personId, out var positionId) ? positionId : string.Empty;
            tenureRemaining.Remove(personId);
            assignments.Remove(personId);
            Context.Modifiers.RemoveSource("person", personId);
            retiredIds.Add(personId);
            foreach (var trait in TraitsFor(personId, lastPositionId))
            {
                var resolved = trait.Resolve();
                var legacyRatio = RetiredLegacyRatio * Context.Modifiers.Multiplier(CivicModifierEffectTypes.PersonLegacyMultiplier, personId, "*");
                Context.Modifiers.Add(new CivicModifierEntry("personLegacy", personId, resolved.EffectType, resolved.TargetId, resolved.Amount * legacyRatio, resolved.CapGroup));
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
            Context.Telemetry.SetExternalMetric("person.assignedCount", AssignedPeopleCount);
            foreach (var position in Positions) Context.Telemetry.SetExternalMetric("person.positionOccupied." + position.Id, assignments.Values.Contains(position.Id) ? 1d : 0d);
            Context.Telemetry.SetExternalMetric("person.sameAssignmentPair", 0d);
            Context.Telemetry.SetExternalMetric("person.engineerRetirementRemaining", tenureRemaining.Where(pair => Person(pair.Key).ArchetypeId == "engineer").Select(pair => pair.Value).DefaultIfEmpty(double.MaxValue).Min());
        }
    }
}
