using System;
using System.Collections.Generic;
using Civic.Features;

namespace Civic.Simulation.Modules
{
    public interface ICivicGameplayModule
    {
        string FeatureId { get; }
        void Initialize(CivicModuleContext context);
        void BeforeAdvance(double seconds);
        void AfterAdvance(double seconds);
        void OnBuildingConstructed(string buildingId);
        void OnTechnologyResearched(string technologyId);
    }

    public abstract class CivicGameplayModuleBase : ICivicGameplayModule
    {
        protected CivicModuleContext Context { get; private set; }
        public abstract string FeatureId { get; }

        public virtual void Initialize(CivicModuleContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public virtual void BeforeAdvance(double seconds) { }
        public virtual void AfterAdvance(double seconds) { }
        public virtual void OnBuildingConstructed(string buildingId) { }
        public virtual void OnTechnologyResearched(string technologyId) { }
    }

    public sealed class CivicModuleContext
    {
        private readonly CivicFeatureResolution features;
        private readonly Dictionary<string, ICivicGameplayModule> modules;

        internal CivicModuleContext(
            CivicGameSimulation simulation,
            CivicFeatureResolution features,
            CivicMetaProgress metaProgress,
            CivicRunTelemetry telemetry,
            Dictionary<string, ICivicGameplayModule> modules)
        {
            Simulation = simulation;
            this.features = features;
            MetaProgress = metaProgress;
            Telemetry = telemetry;
            this.modules = modules;
        }

        public CivicGameSimulation Simulation { get; }
        public CivicModifierLedger Modifiers => Simulation.Modifiers;
        public CivicMetaProgress MetaProgress { get; }
        public CivicRunTelemetry Telemetry { get; }
        public bool IsFeatureEnabled(string featureId) => features.IsEnabled(featureId);
        public bool IsIntegrationEnabled(string integrationId) => features.IsIntegrationEnabled(integrationId);

        public T GetModule<T>(string featureId) where T : class, ICivicGameplayModule
        {
            return modules.TryGetValue(featureId, out var module) ? module as T : null;
        }
    }
}
