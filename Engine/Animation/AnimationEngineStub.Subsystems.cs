using System;
using System.Collections.Generic;

namespace Engine.Animation
{
    // Les vingt-huit types que AnimationEngineStub et ses partiels reclamaient.
    // Chaque membre ci-dessous vient d'un site d'appel reel du stub ; les corps
    // sont neutres parce qu'aucun appelant n'attend encore un comportement.
    public class AnimationConfigManager
    {
        public void RegisterProfile(StressProfile profile) { }
        public void ApplyProfile(string name) { }
        public T GetSetting<T>(string key, T defaultValue) => default;
        public void SetSetting(string key, object value) { }
    }

    public class AnimationPluginHost
    {
        public void LoadPlugin(string path) { }
        public void UnloadPlugin(string name) { }
        public void ReloadPlugin(string name) { }
        public List<IAnimationPlugin> GetLoadedPlugins() => new List<IAnimationPlugin>();
    }

    public class AssetCache
    {
        public void PutAsset(string name, object asset) { }
        public bool TryGetAsset(string name, out object asset) { asset = default; return false; }
        public void EvictAsset(string name) { }
        public void Clear() { }
        public long GetCacheSize() => 0;
    }

    public class AssetCatalog
    {
        public void RegisterAsset(AssetInfo info) { }
        public AssetInfo GetAssetInfo(string name) => default;
        public void MarkAssetAsUsed(string name) { }
        public void MarkAssetAsUnused(string name) { }
        public List<AssetInfo> GetAllAssets() => new List<AssetInfo>();
    }

    public class ChaosMonkey
    {
        public void InjectChaos(ChaosEvent chaosEvent) { }
        public void ScheduleChaos(ChaosEvent chaosEvent, DateTime time) { }
    }

    public class FeatureToggleService
    {
        public bool IsEnabled(string name) => false;
        public void SetToggle(string name, bool enabled, string description, string group) { }
        public Dictionary<string, bool> GetAllToggles() => new Dictionary<string, bool>();
    }

    public interface IExternalSubsystem
    {
        string Name { get; }
        SubsystemType Type { get; }
        string Version { get; }
        // Declarations seules : un corps ici serait une implementation par
        // defaut d'interface, qui dispenserait les implementeurs de les ecrire.
        void Initialize();
        void Shutdown();
        List<string> GetDependencies();
        SubsystemHealthStatus GetHealthStatus();
    }

    public class IntegrityChecker
    {
        public bool VerifyAssetIntegrity(AssetInfo info) => false;
        public void RepairAsset(AssetInfo info) { }
    }

    public class RuntimeInspector
    {
        public RuntimeSnapshot TakeSnapshot() => default;
        public bool CompareSnapshots(RuntimeSnapshot a, RuntimeSnapshot b) => false;
        public void RollbackToSnapshot(RuntimeSnapshot snapshot) { }
    }

    public class SubsystemAuditTrail
    {
        public void Log(string message) { }
    }

    public class SubsystemEventRecorder
    {
        public void Record(TraceEvent traceEvent) { }
    }

    public class SubsystemLifecycleManager
    {
        public void AddForInitialization(object subsystem) { }
        public void InitializeAll() { }
        public void ShutdownAll() { }
    }

    public class SubsystemProfiler
    {
        public void RecordTime(SubsystemType type, float milliseconds) { }
    }

    public class SubsystemRegistry
    {
        public void Register(object subsystem) { }
        public T Get<T>() where T : class => default;
        public bool ContainsKey(string name) => false;
        public bool TryGetValue(string name, out object subsystem) { subsystem = default; return false; }
    }

    public class TelemetryCollector
    {
        public void RecordEvent(TelemetryEvent telemetryEvent) { }
        public int GetEventCount() => 0;
    }

    // Aucun membre : le releve d usage montre des types instancies, enregistres et recuperes, jamais interroges.
    public class AnimationBlendTreeSystem { }
    public class AnimationCompressionSystem { }
    public class AnimationInverseKinematicsSystem { }
    public class AnimationProceduralSystem { }
    public class AnimationStateMachineSystem { }
    // Le seul des treize types « sans membre » a qui un site d'appel en reclame :
    // dix proprietes lues, et un constructeur a treize parametres releve sur les
    // deux `new SubsystemDescriptor(...)` d'Index.cs, lignes 189 et 453.
    public class SubsystemDescriptor
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<string> Dependencies { get; set; }
        public SubsystemType Type { get; set; }
        public SubsystemHealthStatus Status { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; }
        public string Author { get; set; }
        public DateTime LastModified { get; set; }
        public StubFeatureFlags FeatureFlags { get; set; }
        public SubsystemSecurityLevel SecurityLevel { get; set; }
        public StressProfile StressProfile { get; set; }
        public float PerformanceScore { get; set; }

        public SubsystemDescriptor(
            string name, string version, List<string> dependencies, SubsystemType type,
            SubsystemHealthStatus status, string description, List<string> tags, string author,
            DateTime lastModified, StubFeatureFlags featureFlags, SubsystemSecurityLevel securityLevel,
            StressProfile stressProfile, float performanceScore)
        {
            Name = name; Version = version; Dependencies = dependencies; Type = type;
            Status = status; Description = description; Tags = tags; Author = author;
            LastModified = lastModified; FeatureFlags = featureFlags; SecurityLevel = securityLevel;
            StressProfile = stressProfile; PerformanceScore = performanceScore;
        }
    }
    public class SubsystemEventPlayer { }
    public class SubsystemHealthMonitor { }
    public class SubsystemHotSwapManager { }
    public class SubsystemRollback { }
    public class SubsystemSandbox { }
    public class SubsystemStateDeserializer { }
    public class SubsystemStateSerializer { }
}