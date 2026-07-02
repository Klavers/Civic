using System;
using System.Linq;
using Civic.Features;
using NUnit.Framework;

namespace Civic.UI.Tests
{
    public sealed class CivicFeatureFrameworkTests
    {
        [SetUp]
        public void SetUp()
        {
            CivicFeatureRuntime.ResetForMainMenu();
        }

        [Test]
        public void Registry_ContainsEightIndependentSugModules()
        {
            Assert.That(CivicFeatureRegistry.Features.Count, Is.EqualTo(8));
            Assert.That(CivicFeatureRegistry.Features.Select(feature => feature.Id), Is.Unique);
            Assert.That(CivicFeatureRegistry.Integrations.Select(integration => integration.Id), Is.Unique);
        }

        [Test]
        public void Resolver_ActivatesIntegrationOnlyWhenAllInputsAreEnabled()
        {
            var prestigeOnly = CivicFeatureResolver.Resolve(new[] { CivicFeatureRegistry.Prestige });
            Assert.That(prestigeOnly.IsValid, Is.True);
            Assert.That(prestigeOnly.IsIntegrationEnabled("integration.achievementPrestige"), Is.False);

            var combined = CivicFeatureResolver.Resolve(new[]
            {
                CivicFeatureRegistry.Prestige,
                CivicFeatureRegistry.Achievements
            });
            Assert.That(combined.IsValid, Is.True);
            Assert.That(combined.IsIntegrationEnabled("integration.achievementPrestige"), Is.True);
        }

        [Test]
        public void Resolver_ReportsMissingRequirementAndConflict()
        {
            var definitions = new[]
            {
                new CivicFeatureDefinition("a", "A", string.Empty, requiresAll: new[] { "b" }),
                new CivicFeatureDefinition("b", "B", string.Empty, conflictsWith: new[] { "c" }),
                new CivicFeatureDefinition("c", "C", string.Empty)
            };

            var missing = CivicFeatureResolver.Resolve(definitions, Array.Empty<CivicFeatureIntegrationDefinition>(), new[] { "a" });
            Assert.That(missing.IsValid, Is.False);
            Assert.That(missing.Errors.Any(error => error.Contains("b")), Is.True);

            var conflict = CivicFeatureResolver.Resolve(definitions, Array.Empty<CivicFeatureIntegrationDefinition>(), new[] { "b", "c" });
            Assert.That(conflict.IsValid, Is.False);
            Assert.That(conflict.Errors.Any(error => error.Contains("동시에")), Is.True);
        }

        [Test]
        public void Runtime_LocksResolvedSetAfterRunStarts()
        {
            CivicFeatureRuntime.SetPending(new[] { CivicFeatureRegistry.Wonders });
            var resolution = CivicFeatureRuntime.BeginRun();

            Assert.That(resolution.IsEnabled(CivicFeatureRegistry.Wonders), Is.True);
            Assert.That(CivicFeatureRuntime.IsRunLocked, Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                CivicFeatureRuntime.SetPending(new[] { CivicFeatureRegistry.Events }));
        }

        [Test]
        public void CommandLine_ParsesEqualsAndSeparateValueForms()
        {
            var equalsForm = CivicFeatureRuntime.ParseCommandLine(new[]
            {
                "Civic.exe",
                "-civicFeatures=meta.prestige,narrative.events"
            });
            var separateForm = CivicFeatureRuntime.ParseCommandLine(new[]
            {
                "Civic.exe",
                "-civicFeatures",
                "projects.wonders;people.greatPeople"
            });

            Assert.That(equalsForm, Is.EquivalentTo(new[] { "meta.prestige", "narrative.events" }));
            Assert.That(separateForm, Is.EquivalentTo(new[] { "projects.wonders", "people.greatPeople" }));
        }

        [Test]
        public void FeatureMatrix_CoversBaselineEachPairwiseAndAllOn()
        {
            var cases = CivicFeatureMatrix.CreateDefaultCases();

            Assert.That(cases.Count, Is.EqualTo(38));
            Assert.That(cases.Count(item => item.Name == "Baseline"), Is.EqualTo(1));
            Assert.That(cases.Count(item => item.Name.StartsWith("EachModule:")), Is.EqualTo(8));
            Assert.That(cases.Count(item => item.Name.StartsWith("Pairwise:")), Is.EqualTo(28));
            Assert.That(cases.Count(item => item.Name == "AllOn"), Is.EqualTo(1));
            Assert.That(cases.All(item => CivicFeatureResolver.Resolve(item.RequestedIds).IsValid), Is.True);
        }

        [Test]
        public void RunFeatureSaveHeader_RoundTripsAndRejectsChangedResolvedSet()
        {
            var resolution = CivicFeatureResolver.Resolve(new[]
            {
                CivicFeatureRegistry.Prestige,
                CivicFeatureRegistry.Achievements
            });
            var json = CivicRunFeatureSaveHeader.Capture(resolution, 8128).ToJson();
            var loaded = CivicRunFeatureSaveHeader.FromJson(json);

            Assert.That(loaded.ValidateAndResolve().EnabledFeatureIds, Is.EquivalentTo(resolution.EnabledFeatureIds));
            Assert.That(loaded.RunSeed, Is.EqualTo(8128));
            loaded.ResolvedFeatureIds.Remove(CivicFeatureRegistry.Prestige);
            Assert.Throws<InvalidOperationException>(() => loaded.ValidateAndResolve());
        }
    }
}
