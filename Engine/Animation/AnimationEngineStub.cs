// /Engine/Animation/AnimationEngineStub.cs
//
// Point d’entrée principal du moteur d’animation simulé.
// Coordonne les sous‑systèmes Core, Assets, Simulation, Security, Diagnostics, Metrics.
// Responsabilités : Initialisation, orchestration, API publique, événements internes, compatibilité RenderGraph.
// Dépendances : Tous les autres fichiers AnimationEngineStub.*.cs (partials), EventBus, IRenderPipeline.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Net.Http; // Pour AnimationAssetCDNStub
using Engine.Core;
using Snake2000.Engine.Core;   // EventBus, Profiler, ResourceManager
// Engine.Events, Engine.Services, Engine.Jobs et Engine.Resources sont partis :
// aucun de ces espaces de noms n'existe dans le depot. Ils designaient une
// organisation prevue et jamais creee, et coutaient un CS0234 chacun.
using Engine.Profiling;
using Engine.Rendering; // Pour IRenderPipeline

namespace Engine.Animation
{
    #region Enums & Structs (réutilisés ou nouveaux)
    // Réutilisation de certains enums/types du stub
    using Engine.Animation;

    // AnimationEngineState est declare dans un fichier partiel d'AnimationEngineStub.

    // StubSimulationMode est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationQualityLevel est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationCompressionMethod est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationUpdateMode est declare dans un fichier partiel d'AnimationEngineStub.

    // OrchestratorMetricType est declare dans un fichier partiel d'AnimationEngineStub.

    // SubsystemType est declare dans un fichier partiel d'AnimationEngineStub.

    // SubsystemHealthStatus est declare dans un fichier partiel d'AnimationEngineStub.

    // DiagnosticsLevel est declare dans un fichier partiel d'AnimationEngineStub.

    public enum ThreadAffinity
    {
        Any,
        Main,
        Worker,
        Dedicated
    }

    // StubFeatureFlags est declare dans un fichier partiel d'AnimationEngineStub.

    // TraceEvent est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationPose est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationClip est declare dans un fichier partiel d'AnimationEngineStub.

    // Keyframe est declare dans un fichier partiel d'AnimationEngineStub.

    // Transform est declare dans un fichier partiel d'AnimationEngineStub.

    // Vector3 est declare dans un fichier partiel d'AnimationEngineStub.

    // Quaternion est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationEngineMetrics est declare dans un fichier partiel d'AnimationEngineStub.

    // PerformanceSnapshot est declare dans un fichier partiel d'AnimationEngineStub.

    // StressProfile est declare dans un fichier partiel d'AnimationEngineStub.

    // VersionInfo est declare dans un fichier partiel d'AnimationEngineStub.

    // [AJOUT] Structures pour les idées 398-597
    public readonly struct PluginManifest
    {
        public readonly string Name;
        public readonly string Version;
        public readonly string Author;
        public readonly string Description;
        public readonly List<string> Dependencies;
        public readonly Dictionary<string, object> Capabilities;
        public PluginManifest(string name, string version, string author, string desc, List<string> deps, Dictionary<string, object> caps) => (Name, Version, Author, Description, Dependencies, Capabilities) = (name, version, author, desc, deps, caps);
    }

    // TelemetryEvent est declare dans un fichier partiel d'AnimationEngineStub.

    public readonly struct FeatureToggle
    {
        public readonly string Name;
        public readonly bool IsEnabled;
        public readonly string Description;
        public readonly string Group; // Pour A/B Testing
        public FeatureToggle(string name, bool enabled, string desc, string group) => (Name, IsEnabled, Description, Group) = (name, enabled, desc, group);
    }

    public readonly struct ConfigProfile
    {
        public readonly string Name;
        public readonly Dictionary<string, object> Settings;
        public ConfigProfile(string name, Dictionary<string, object> settings) => (Name, Settings) = (name, settings);
    }

    public readonly struct RuntimeSnapshot
    {
        public readonly AnimationEngineState State;
        public readonly AnimationEngineMetrics Metrics;
        public readonly Dictionary<string, object> Flags;
        public readonly DateTime Timestamp;
        public RuntimeSnapshot(AnimationEngineState state, AnimationEngineMetrics metrics, Dictionary<string, object> flags, DateTime time) => (State, Metrics, Flags, Timestamp) = (state, metrics, flags, time);
    }

    public readonly struct HealthStatus
    {
        public readonly bool IsHealthy;
        public readonly List<string> Warnings;
        public readonly List<string> Errors;
        public HealthStatus(bool healthy, List<string> warns, List<string> errs) => (IsHealthy, Warnings, Errors) = (healthy, warns, errs);
    }

    public readonly struct AssetInfo
    {
        public readonly string Name;
        public readonly string Path;
        public readonly string Checksum;
        public readonly long SizeBytes;
        public readonly DateTime LastModified;
        public readonly Dictionary<string, object> Metadata;
        public AssetInfo(string name, string path, string checksum, long size, DateTime modified, Dictionary<string, object> metadata) => (Name, Path, Checksum, SizeBytes, LastModified, Metadata) = (name, path, checksum, size, modified, metadata);
    }

    public readonly struct ChaosEvent
    {
        public readonly string Type; // "CPU_SPIN", "MEMORY_PRESSURE", "NETWORK_LATENCY", etc.
        public readonly string Description;
        public readonly DateTime Timestamp;
        public readonly Dictionary<string, object> Parameters;
        public ChaosEvent(string type, string desc, DateTime time, Dictionary<string, object> parameters) => (Type, Description, Timestamp, Parameters) = (type, desc, time, parameters);
    }

    #endregion

    #region Interfaces

    // IAnimationSubsystem est declare dans un fichier partiel d'AnimationEngineStub.

    // IAnimationPlugin est declare dans un fichier partiel d'AnimationEngineStub.

    // IAssetCatalog est declare dans un fichier partiel d'AnimationEngineStub.

    public interface IAssetCache
    {
        bool TryGetAsset(string name, out object asset);
        void PutAsset(string name, object asset);
        void EvictAsset(string name);
        void Clear();
        int GetCacheSize();
    }

    public interface IIntegrityChecker
    {
        bool VerifyAssetIntegrity(AssetInfo info);
        void RepairAsset(AssetInfo info);
    }

    public interface IAnimationPluginHost
    {
        void LoadPlugin(string path);
        void UnloadPlugin(string name);
        void ReloadPlugin(string name);
        List<IAnimationPlugin> GetLoadedPlugins();
    }

    public interface ITelemetryCollector
    {
        void RecordEvent(TelemetryEvent evt);
        void Flush();
    }

    public interface IRuntimeInspector
    {
        RuntimeSnapshot TakeSnapshot();
        void CompareSnapshots(RuntimeSnapshot a, RuntimeSnapshot b);
        void RollbackToSnapshot(RuntimeSnapshot snapshot);
    }

    public interface IChaosMonkey
    {
        void InjectChaos(ChaosEvent chaosEvent);
        void ScheduleChaos(ChaosEvent chaosEvent, DateTime scheduledTime);
    }

    #endregion

    public partial class AnimationEngineStub : IAnimationEngine
    {
        #region Fields (Core)

        // --- État interne ---
        // _state : declare dans un fichier partiel d'AnimationEngineStub.
        // _stateLock : declare dans un fichier partiel d'AnimationEngineStub.
        // _simulationMode : declare dans un fichier partiel d'AnimationEngineStub.
        // _qualityLevel : declare dans un fichier partiel d'AnimationEngineStub.
        // _updateMode : declare dans un fichier partiel d'AnimationEngineStub.
        // A. Optimisation et structure : Utilisation de ConcurrentBag pour les clips chargés (exemple)
        // _loadedClipsBag : declare dans un fichier partiel d'AnimationEngineStub.
        // _loadedClipsDict : declare dans un fichier partiel d'AnimationEngineStub.
        // _currentPoses : declare dans un fichier partiel d'AnimationEngineStub.
        // _activeAnimations : declare dans un fichier partiel d'AnimationEngineStub.
        // _random : declare dans un fichier partiel d'AnimationEngineStub.
        // _stateWatchdogTimer : declare dans un fichier partiel d'AnimationEngineStub.
        // A. Optimisation et structure : Utilisation de ConcurrentBag pour les logs (thread-safe)
        // _callLog : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableCallLogging : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableAssertions : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableSnapshots : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableReplay : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableFaultInjection : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableValidation : declare dans un fichier partiel d'AnimationEngineStub.
        // _enablePerformanceSim : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableMemoryPressureSim : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableThreadingSim : declare dans un fichier partiel d'AnimationEngineStub.
        // _enableHooks : declare dans un fichier partiel d'AnimationEngineStub.

        // --- Dépendances Injectées ---
        // _eventBus : declare dans un fichier partiel d'AnimationEngineStub.
        // _profiler : declare dans un fichier partiel d'AnimationEngineStub.
        // _jobSystem : declare dans un fichier partiel d'AnimationEngineStub.
        // _resourceManager : declare dans un fichier partiel d'AnimationEngineStub.
        // _renderEngine : declare dans un fichier partiel d'AnimationEngineStub.
        // _physicsEngine : declare dans un fichier partiel d'AnimationEngineStub.

        // --- Configurations ---
        // _config : declare dans un fichier partiel d'AnimationEngineStub.
        // _stubConfig : declare dans un fichier partiel d'AnimationEngineStub.

        // --- Variables de simulation ---
        // _cpuLoadSim : declare dans un fichier partiel d'AnimationEngineStub.
        // _memoryPressureSim : declare dans un fichier partiel d'AnimationEngineStub.
        // _threadingLoadSim : declare dans un fichier partiel d'AnimationEngineStub.

        // --- Métriques internes ---
        private readonly Dictionary<OrchestratorMetricType, float> _metrics = new Dictionary<OrchestratorMetricType, float>();
        private readonly object _metricsLock = new object();

        // --- Gestionnaires (implémentés dans les fichiers partials) ---
        // Exemples de déclarations pour les champs qui seront définis dans les fichiers séparés
        // private readonly DiagnosticsManager _diagnosticsManager = new DiagnosticsManager();
        // private readonly FrameScheduler _frameScheduler = new FrameScheduler();
        // private readonly ScenarioPlayer _scenarioPlayer = new ScenarioPlayer();
        // private readonly RigDefinitionCache _rigCache = new RigDefinitionCache();
        // private readonly BlendTreeOptimizer _blendTreeOptimizer = new BlendTreeOptimizer();
        // private readonly IKSolverCache _ikSolverCache = new IKSolverCache();
        // private readonly AnimationMemoryPool _memoryPool = new AnimationMemoryPool(...);
        // private readonly AnimationMemoryArena _memoryArena = new AnimationMemoryArena(...);
        // private readonly AnimationMemoryUsageTracker _memoryUsageTracker = new AnimationMemoryUsageTracker(...);

        // --- Sous-systèmes (implémentés comme classes internes pour modularité) ---
        // _subsystemRegistry : declare dans un fichier partiel d'AnimationEngineStub.
        // _lifecycleManager : declare dans un fichier partiel d'AnimationEngineStub.
        private readonly SubsystemHealthMonitor _healthMonitor = new SubsystemHealthMonitor();
        private readonly SubsystemProfiler _subsystemProfiler = new SubsystemProfiler();
        // _blendTreeSystem : declare dans un fichier partiel d'AnimationEngineStub.
        // _stateMachineSystem : declare dans un fichier partiel d'AnimationEngineStub.
        // _ikSystem : declare dans un fichier partiel d'AnimationEngineStub.
        // _proceduralSystem : declare dans un fichier partiel d'AnimationEngineStub.
        // _compressionSystem : declare dans un fichier partiel d'AnimationEngineStub.

        // --- Plugins (H. Extensibilité) ---
        // _plugins : declare dans un fichier partiel d'AnimationEngineStub.
        // _version : declare dans un fichier partiel d'AnimationEngineStub.

        // --- K. Architecture avancée ---
        // _hotSwapManager : declare dans un fichier partiel d'AnimationEngineStub.
        // _sandbox : declare dans un fichier partiel d'AnimationEngineStub.
        // _auditTrail : declare dans un fichier partiel d'AnimationEngineStub.
        // _rollbackManager : declare dans un fichier partiel d'AnimationEngineStub.
        // _stateSerializer : declare dans un fichier partiel d'AnimationEngineStub.
        // _stateDeserializer : declare dans un fichier partiel d'AnimationEngineStub.
        // _eventRecorder : declare dans un fichier partiel d'AnimationEngineStub.
        // _eventPlayer : declare dans un fichier partiel d'AnimationEngineStub.

        // --- G. Sécurité et stabilité ---
        // _safeMode : declare dans un fichier partiel d'AnimationEngineStub.
        // _watchdogLock : declare dans un fichier partiel d'AnimationEngineStub.
        // _isStable : declare dans un fichier partiel d'AnimationEngineStub.
        // _animationWatchdog : declare dans un fichier partiel d'AnimationEngineStub.

        // --- [AJOUT] Managers pour idées 398-597 ---
        private readonly AnimationPluginHost _pluginHost = new AnimationPluginHost();
        private readonly FeatureToggleService _featureToggleService = new FeatureToggleService();
        private readonly AnimationConfigManager _configManager = new AnimationConfigManager();
        private readonly RuntimeInspector _runtimeInspector = new RuntimeInspector();
        private readonly AssetCatalog _assetCatalog = new AssetCatalog();
        private readonly AssetCache _assetCache = new AssetCache();
        private readonly IntegrityChecker _integrityChecker = new IntegrityChecker();
        private readonly TelemetryCollector _telemetryCollector = new TelemetryCollector();
        private readonly ChaosMonkey _chaosMonkey = new ChaosMonkey();

        #endregion

        #region Constructors (Core)

        public AnimationEngineStub()
        {
            _stateWatchdogTimer = new Timer(WatchdogCallback, null, Timeout.Infinite, Timeout.Infinite);
            // Initialiser les métriques avec des valeurs par défaut
            foreach (OrchestratorMetricType type in Enum.GetValues(typeof(OrchestratorMetricType)))
            {
                _metrics[type] = 0.0f;
            }
            _version = new VersionInfo("1.0.0-stub", DateTime.UtcNow);
        }

        #endregion

        #region Lifecycle Methods (Core)

        public AnimationEngineStub Initialize(AnimationEngineConfig config, EventBus eventBus, Profiler profiler, IJobSystem jobSystem, ResourceManager resourceManager)
        {
            lock (_stateLock)
            {
                if (_state != AnimationEngineState.Uninitialized)
                    throw new InvalidOperationException("L'AnimationEngineStub est déjà initialisé.");

                _config = config ?? throw new ArgumentNullException(nameof(config));
                _eventBus = eventBus;
                _profiler = profiler;
                _jobSystem = jobSystem;
                _resourceManager = resourceManager;

                // Initialiser la configuration du stub
                _stubConfig = new AnimationEngineStubConfig
                {
                    SimulationMode = StubSimulationMode.Normal,
                    EnableCallLogging = false,
                    EnableAssertions = true,
                    EnableSnapshots = false,
                    EnableReplay = false,
                    EnableFaultInjection = false,
                    EnableValidation = false,
                    EnablePerformanceSim = false,
                    EnableMemoryPressureSim = false,
                    EnableThreadingSim = false,
                    EnableHooks = false,
                    QualityLevel = AnimationQualityLevel.High,
                    UpdateMode = AnimationUpdateMode.Sequential
                };

                // A. Optimisation et structure : Appliquer les flags
                var flags = _stubConfig.FeatureFlags;
                _enableCallLogging = flags.HasFlag(StubFeatureFlags.CallLogging);
                _enableAssertions = flags.HasFlag(StubFeatureFlags.Assertions);
                _enableSnapshots = flags.HasFlag(StubFeatureFlags.Snapshots);
                _enableReplay = flags.HasFlag(StubFeatureFlags.Replay);
                _enableFaultInjection = flags.HasFlag(StubFeatureFlags.FaultInjection);
                _enableValidation = flags.HasFlag(StubFeatureFlags.Validation);
                _enablePerformanceSim = flags.HasFlag(StubFeatureFlags.PerformanceSim);
                _enableMemoryPressureSim = flags.HasFlag(StubFeatureFlags.MemoryPressureSim);
                _enableThreadingSim = flags.HasFlag(StubFeatureFlags.ThreadingSim);
                _enableHooks = flags.HasFlag(StubFeatureFlags.Hooks);
                _safeMode = flags.HasFlag(StubFeatureFlags.SafeMode);

                // [AJOUT] Appliquer les nouveaux flags
                bool enablePluginLoading = flags.HasFlag(StubFeatureFlags.PluginLoading);
                bool enableTelemetry = flags.HasFlag(StubFeatureFlags.Telemetry);
                bool enableHotReload = flags.HasFlag(StubFeatureFlags.HotReload);
                bool enableABTesting = flags.HasFlag(StubFeatureFlags.ABTesting);
                bool enableCanaryMode = flags.HasFlag(StubFeatureFlags.CanaryMode);
                bool enableChaosMonkey = flags.HasFlag(StubFeatureFlags.ChaosMonkey);
                bool enableAssetStreaming = flags.HasFlag(StubFeatureFlags.AssetStreaming);
                bool enableAssetCaching = flags.HasFlag(StubFeatureFlags.AssetCaching);
                bool enableIntegrityChecking = flags.HasFlag(StubFeatureFlags.IntegrityChecking);
                bool enableAuditLogging = flags.HasFlag(StubFeatureFlags.AuditLogging);
                bool enableRateLimiting = flags.HasFlag(StubFeatureFlags.RateLimiting);
                bool enableCircuitBreaking = flags.HasFlag(StubFeatureFlags.CircuitBreaking);
                bool enableGDPRMode = flags.HasFlag(StubFeatureFlags.GDPRMode);
                bool enablePIIProtection = flags.HasFlag(StubFeatureFlags.PIIProtection);

                // [AJOUT] Initialiser les managers en fonction des flags
                if (enablePluginLoading)
                {
                    // Les plugins peuvent être chargés dynamiquement via _pluginHost.LoadPlugin(...)
                    // On peut aussi charger des plugins par défaut ici
                    // _pluginHost.LoadPlugin("DefaultPlugin.dll");
                }
                if (enableTelemetry)
                {
                    // Le collector est déjà initialisé, on peut commencer à enregistrer des événements
                    _telemetryCollector.RecordEvent(new TelemetryEvent("EngineInitialized", new Dictionary<string, object>{{"Version", _version.Version}}, DateTime.UtcNow));
                }
                if (enableChaosMonkey)
                {
                    // Activer le chaos monkey
                    _chaosMonkey.InjectChaos(new ChaosEvent("StartupChaos", "Simulated startup chaos for testing resilience.", DateTime.UtcNow, new Dictionary<string, object>()));
                }
                if (enableIntegrityChecking)
                {
                    // Activer la vérification d'intégrité
                    // Exemple : vérifier les assets au chargement
                }
                if (enableAuditLogging)
                {
                    // Activer l'audit trail
                    _auditTrail.Log("Engine initialized with audit logging enabled.");
                }
                if (enableGDPRMode)
                {
                    // Activer le mode GDPR (masquer PII, etc.)
                }
                if (enablePIIProtection)
                {
                    // Activer la protection des données personnelles
                }

                // Initialiser les sous-systèmes et les enregistrer via le registre
                _blendTreeSystem = new AnimationBlendTreeSystem(this);
                _stateMachineSystem = new AnimationStateMachineSystem(this);
                _ikSystem = new AnimationInverseKinematicsSystem(this);
                _proceduralSystem = new AnimationProceduralSystem(this);
                _compressionSystem = new AnimationCompressionSystem(this);

                _subsystemRegistry.Register(_blendTreeSystem);
                _subsystemRegistry.Register(_stateMachineSystem);
                _subsystemRegistry.Register(_ikSystem);
                _subsystemRegistry.Register(_proceduralSystem);
                _subsystemRegistry.Register(_compressionSystem);

                // Planifier l'initialisation via le LifecycleManager
                _lifecycleManager.AddForInitialization(_blendTreeSystem);
                _lifecycleManager.AddForInitialization(_stateMachineSystem);
                _lifecycleManager.AddForInitialization(_ikSystem);
                _lifecycleManager.AddForInitialization(_proceduralSystem);
                _lifecycleManager.AddForInitialization(_compressionSystem);

                _lifecycleManager.InitializeAll();

                _state = AnimationEngineState.Initializing;
                // Simuler un peu de travail d'initialisation
                Thread.Sleep(50);

                _state = AnimationEngineState.Ready;
                LogCall("Initialize");
                return this;
            }
        }

        public AnimationEngineStub Suspend()
        {
            lock (_stateLock)
            {
                if (_state == AnimationEngineState.Ready || _state == AnimationEngineState.Updating)
                {
                    _state = AnimationEngineState.Suspending;
                    // Simuler la suspension
                    Thread.Sleep(10);
                    _state = AnimationEngineState.Suspended;
                    LogCall("Suspend");
                }
                return this;
            }
        }

        public AnimationEngineStub Resume()
        {
            lock (_stateLock)
            {
                if (_state == AnimationEngineState.Suspended)
                {
                    _state = AnimationEngineState.Ready;
                    LogCall("Resume");
                }
                return this;
            }
        }

        public AnimationEngineStub Restart(AnimationEngineStubConfig newConfig)
        {
            lock (_stateLock)
            {
                // Nettoyer l'état actuel
                _activeAnimations.Clear();
                _currentPoses.Clear();
                _loadedClipsBag.Clear();
                _loadedClipsDict.Clear();

                // Désinitialiser les anciens sous-systèmes via le LifecycleManager
                _lifecycleManager.ShutdownAll();
                _subsystemRegistry = new SubsystemRegistry(); // Réinitialiser le registre
                _lifecycleManager = new SubsystemLifecycleManager(); // Réinitialiser le manager

                // Appliquer la nouvelle config
                _stubConfig = newConfig;

                // Réinitialiser l'état
                _state = AnimationEngineState.Initializing;
                Thread.Sleep(50); // Simuler ré-initialisation
                _state = AnimationEngineState.Ready;
                LogCall("Restart");
                return this;
            }
        }

        // A. Optimisation et structure : Méthode Reset
        public AnimationEngineStub Reset()
        {
            lock (_stateLock)
            {
                // Ne redémarre pas complètement, réinitialise juste l'état dynamique
                _activeAnimations.Clear();
                _currentPoses.Clear();
                // Ne vide pas _loadedClipsBag/_loadedClipsDict pour conserver les ressources chargées
                // Ne réinitialise pas les sous-systèmes
                LogCall("Reset");
                return this;
            }
        }

        public AnimationEngineStub Shutdown()
        {
            lock (_stateLock)
            {
                if (_state == AnimationEngineState.Disposed) return this;

                _state = AnimationEngineState.ShuttingDown;
                // Simuler l'arrêt
                Thread.Sleep(20);

                // Désinitialiser les sous-systèmes via le LifecycleManager
                _lifecycleManager.ShutdownAll();

                // Désinitialiser les plugins
                foreach (var plugin in _plugins)
                {
                    plugin.Shutdown();
                }

                // Libérer les ressources
                _stateWatchdogTimer?.Dispose();
                _callLog.Clear();
                _diagnosticsManager.Log(new TraceEvent("Shutdown complete", DateTime.UtcNow, _state, null));

                _state = AnimationEngineState.Uninitialized;
                LogCall("Shutdown");
                return this;
            }
        }

        public AnimationEngineStub Dispose()
        {
            Shutdown();
            _state = AnimationEngineState.Disposed;
            LogCall("Dispose");
            GC.SuppressFinalize(this);
            return this;
        }

        #endregion

        #region Core Animation Methods (Core)

        public AnimationEngineStub LoadAnimationClip(string clipName, string path)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"LoadAnimationClip({clipName}, {path})");

            // Simuler le chargement
            if (!_loadedClipsDict.ContainsKey(clipName))
            {
                // [AJOUT] Générer un checksum pour l'intégrité
                string checksum = GenerateChecksum(path);
                var clip = new AnimationClip(clipName, 2.0f, new List<Keyframe>(), AnimationCompressionMethod.None, path, checksum, new Dictionary<string, object>{{"Author", "System"}, {"License", "Internal"}});
                _loadedClipsBag.Add(clip);
                _loadedClipsDict[clipName] = clip;
                _metrics[OrchestratorMetricType.CompressedClipsLoaded]++;
                _metrics[OrchestratorMetricType.AssetsLoaded]++; // [AJOUT]
                // R. Mémoire & allocation
                _memoryUsageTracker.ReportAllocation(clipName, Marshal.SizeOf(clip));
                // [AJOUT] Enregistrer dans le catalogue
                _assetCatalog.RegisterAsset(new AssetInfo(clipName, path, checksum, 0, DateTime.UtcNow, clip.Metadata));
                // [AJOUT] Vérifier l'intégrité si activé
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.IntegrityChecking))
                {
                    var info = _assetCatalog.GetAssetInfo(clipName);
                    if (info != null && !_integrityChecker.VerifyAssetIntegrity(info))
                    {
                        LogCall($"ERROR: Integrity check failed for asset {clipName}!");
                        // Gérer l'erreur : charger une version de repli, signaler une alerte, etc.
                    }
                }
                // [AJOUT] Enregistrer l'événement de chargement pour la télémétrie
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                {
                    _telemetryCollector.RecordEvent(new TelemetryEvent("AssetLoaded", new Dictionary<string, object>{{"Name", clipName}, {"Path", path}}, DateTime.UtcNow));
                }
            }
            _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, 1);
            _metricCollector.Increment(OrchestratorMetricType.AssetsLoaded, 1);
            return this;
        }

        public AnimationEngineStub UnloadAnimationClip(string clipName)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"UnloadAnimationClip({clipName})");
            if (_loadedClipsDict.Remove(clipName, out var clip))
            {
                 _loadedClipsBag.TryTake(out _); // Retirer du bag aussi
                 _metrics[OrchestratorMetricType.CompressedClipsLoaded]--;
                 _metrics[OrchestratorMetricType.AssetsUnloaded]++; // [AJOUT]
                 // R. Mémoire & allocation
                 _memoryUsageTracker.ReportDeallocation(clipName, Marshal.SizeOf(clip));
                 // [AJOUT] Marquer comme inutilisé dans le catalogue
                 _assetCatalog.MarkAssetAsUnused(clipName);
                 // [AJOUT] Enregistrer l'événement de déchargement pour la télémétrie
                 if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                 {
                     _telemetryCollector.RecordEvent(new TelemetryEvent("AssetUnloaded", new Dictionary<string, object>{{"Name", clipName}}, DateTime.UtcNow));
                 }
            }
            _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, -1);
            _metricCollector.Increment(OrchestratorMetricType.AssetsUnloaded, 1);
            return this;
        }

        public AnimationEngineStub PlayAnimation(string entityName, string clipName, float blendInTime = 0.1f)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            if (!_loadedClipsDict.ContainsKey(clipName)) throw new ArgumentException("Clip not loaded.", nameof(clipName));
            LogCall($"PlayAnimation({entityName}, {clipName}, {blendInTime})");

            // Simuler le démarrage d'une animation
            if (!_activeAnimations.Contains($"{entityName}_{clipName}"))
            {
                _activeAnimations.Add($"{entityName}_{clipName}");
                // Initialiser un pose par défaut
                _currentPoses[entityName] = new AnimationPose(new Dictionary<string, Transform>(), 0, _loadedClipsDict[clipName]);
                _metrics[OrchestratorMetricType.ActivePlaybacks]++;
                // [AJOUT] Marquer l'asset comme utilisé
                _assetCatalog.MarkAssetAsUsed(clipName);
                // [AJOUT] Enregistrer l'événement de playback pour la télémétrie
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                {
                    _telemetryCollector.RecordEvent(new TelemetryEvent("AnimationPlayed", new Dictionary<string, object>{{"Entity", entityName}, {"Clip", clipName}}, DateTime.UtcNow));
                }
            }
            _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, 1);
            return this;
        }

        public AnimationEngineStub StopAnimation(string entityName, string clipName, float blendOutTime = 0.1f)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"StopAnimation({entityName}, {clipName}, {blendOutTime})");

            // Simuler l'arrêt
            if (_activeAnimations.Remove($"{entityName}_{clipName}"))
            {
                _metrics[OrchestratorMetricType.ActivePlaybacks]--;
                // [AJOUT] Enregistrer l'événement de stop pour la télémétrie
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                {
                    _telemetryCollector.RecordEvent(new TelemetryEvent("AnimationStopped", new Dictionary<string, object>{{"Entity", entityName}, {"Clip", clipName}}, DateTime.UtcNow));
                }
            }
            _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, -1);
            return this;
        }

        public AnimationEngineStub Update(float deltaTime)
        {
            lock (_stateLock)
            {
                if (_state != AnimationEngineState.Ready) return this; // Ne met à jour que si prêt

                _state = AnimationEngineState.Updating;

                // Réinitialiser les métriques de frame
                _metrics[OrchestratorMetricType.TotalBonesAnimated] = 0;
                _metrics[OrchestratorMetricType.BlendsCalculated] = 0;
                _metrics[OrchestratorMetricType.IKIterations] = 0;
                _metrics[OrchestratorMetricType.ProceduralUpdates] = 0;

                // Meme remise a zero cote MetricCollector, qui alimente les snapshots.
                _metricCollector.SetValue(OrchestratorMetricType.TotalBonesAnimated, 0);
                _metricCollector.SetValue(OrchestratorMetricType.BlendsCalculated, 0);
                _metricCollector.SetValue(OrchestratorMetricType.IKIterations, 0);
                _metricCollector.SetValue(OrchestratorMetricType.ProceduralUpdates, 0);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // C. Performance et threading : Vérifier le budget de frame
                if (_frameBudgetMs > 0 && deltaTime > _frameBudgetMs)
                {
                    LogCall($"WARNING: Frame budget exceeded by {deltaTime - _frameBudgetMs}ms");
                }

                // L. Threading & CPU/GPU : Mettre à jour le monitor de threads
                _threadMonitor.UpdateActivity(Thread.CurrentThread);

                // L. Threading & CPU/GPU : Détecter la surcharge
                if (_overloadDetector.IsOverloaded())
                {
                    LogCall("WARNING: Thread overload detected!");
                }

                // Simuler des charges CPU/Mémoire/Threading si activées
                if (_enablePerformanceSim) SimulateCPULoad();
                if (_enableMemoryPressureSim) SimulateMemoryPressure();
                if (_enableThreadingSim) SimulateThreadingLoad();

                // D. Simulation et test : Appliquer le profil de stress
                if (_simulationMode == StubSimulationMode.StressTest)
                {
                    _cpuLoadSim = _stressProfile.CpuLoadPercent;
                    _memoryPressureSim = _stressProfile.MemoryPressureMB;
                    _threadingLoadSim = _stressProfile.ThreadingLoadTasks;
                }

                // G. Sécurité et stabilité : Mode safe
                if (_safeMode)
                {
                    // Désactiver les threads, allocations dynamiques, etc.
                    // Pour la simulation, on peut juste désactiver les simulations de charge
                    _cpuLoadSim = 0;
                    _memoryPressureSim = 0;
                    _threadingLoadSim = 0;
                }

                // [AJOUT] Mettre à jour les plugins
                foreach (var plugin in _plugins)
                {
                    plugin.Update(deltaTime);
                }

                // [AJOUT] Mettre à jour les sous-systèmes via le registre
                var blendSys = _subsystemRegistry.Get<AnimationBlendTreeSystem>();
                var stateSys = _subsystemRegistry.Get<AnimationStateMachineSystem>();
                var ikSys = _subsystemRegistry.Get<AnimationInverseKinematicsSystem>();
                var procSys = _subsystemRegistry.Get<AnimationProceduralSystem>();
                var compSys = _subsystemRegistry.Get<AnimationCompressionSystem>();

                if (blendSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); blendSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.BlendTree, (float)sw.ElapsedMilliseconds); }
                if (stateSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); stateSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.StateMachine, (float)sw.ElapsedMilliseconds); }
                if (ikSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); ikSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.InverseKinematics, (float)sw.ElapsedMilliseconds); }
                if (procSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); procSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Procedural, (float)sw.ElapsedMilliseconds); }
                if (compSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); compSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Compression, (float)sw.ElapsedMilliseconds); }

                // Simuler l'avancement des animations actives
                foreach (var activeAnim in _activeAnimations)
                {
                    var parts = activeAnim.Split('_');
                    if (parts.Length >= 2)
                    {
                        var entityName = parts[0];
                        var clipName = parts[1];
                        if (_currentPoses.ContainsKey(entityName))
                        {
                            ref var pose = ref _currentPoses[entityName];
                            var newTime = pose.Time + deltaTime;
                            if (newTime > pose.Clip.Duration)
                            {
                                // Boucler ou s'arrêter selon le mode
                                newTime = 0; // Boucler pour la simulation
                            }
                            // Recréer la pose avec le nouveau temps
                            var newPose = new AnimationPose(pose.Bones, newTime, pose.Clip);
                            _currentPoses[entityName] = newPose;

                            // M. Simulation avancée : Mettre à jour le prédicteur de pose
                            _posePredictor.UpdateHistory(entityName, newPose.Bones.FirstOrDefault().Value);
                            // M. Simulation avancée : Enregistrer dans la timeline
                            _poseTimelineRecorder.RecordPose(entityName, newPose);

                            _metrics[OrchestratorMetricType.ActivePoses]++;
                            _metrics[OrchestratorMetricType.TotalBonesAnimated] += pose.Bones.Count;
                        }
                    }
                }

                // Simuler des erreurs si en mode ErrorProne
                if (_simulationMode == StubSimulationMode.ErrorProne && _random.NextDouble() < 0.01) // 1% de chance
                {
                    LogCall("ERROR: Simulated exception during update!");
                    _metrics[OrchestratorMetricType.ErrorCount]++;
                    // G. Sécurité et stabilité : Handler de récupération
                    if (_simulationMode == StubSimulationMode.ErrorProne) // Exemple simple
                    {
                         _state = AnimationEngineState.Ready; // Tentative de récupération
                         _isStable = false; // Marquer comme instable
                    }
                }
                else if (_random.NextDouble() < 0.02) // 2% de chance d'avertissement
                {
                     _metrics[OrchestratorMetricType.WarningCount]++;
                }

                // Mise a jour des sous-systemes par le registre, chacun chronometre.
                var blendSys = _subsystemRegistry.Get<AnimationBlendTreeSystem>();
                var stateSys = _subsystemRegistry.Get<AnimationStateMachineSystem>();
                var ikSys = _subsystemRegistry.Get<AnimationInverseKinematicsSystem>();
                var procSys = _subsystemRegistry.Get<AnimationProceduralSystem>();
                var compSys = _subsystemRegistry.Get<AnimationCompressionSystem>();

                if (blendSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); blendSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.BlendTree, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.BlendsCalculated, 1); }
                if (stateSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); stateSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.StateMachine, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, 1); }
                if (ikSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); ikSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.InverseKinematics, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.IKIterations, 1); }
                if (procSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); procSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Procedural, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.ProceduralUpdates, 1); }
                if (compSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); compSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Compression, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, 1); }

                stopwatch.Stop();
                _metrics[OrchestratorMetricType.CpuUpdateMs] = (float)stopwatch.ElapsedMilliseconds;
                _metricCollector.SetValue(OrchestratorMetricType.CpuUpdateMs, (float)stopwatch.ElapsedMilliseconds);
                _metricCollector.Increment(OrchestratorMetricType.TelemetryEvents, 1);
                _snapshotHistory.RecordSnapshot(_metricCollector.GetSnapshot());

                // C. Performance et threading : Stocker le temps de frame
                lock (_perfLock)
                {
                    _frameTimes.Enqueue((float)stopwatch.ElapsedMilliseconds);
                    if (_frameTimes.Count > 100) _frameTimes.Dequeue(); // Garder les 100 derniers
                }

                // N. Diagnostics avancés : Prendre un snapshot de mémoire
                var memSnapshot = new AnimationMemorySnapshot
                {
                    TotalAllocatedBytes = _memoryUsageTracker.GetUsage().current,
                    AllocationBreakdown = new Dictionary<string, long> { { "Total", _memoryUsageTracker.GetUsage().current } } // Simplifié
                };
                _memoryProfiler.TakeSnapshot(memSnapshot);

                // [AJOUT] Enregistrer des événements de télémétrie
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                {
                    _telemetryCollector.RecordEvent(new TelemetryEvent("FrameUpdate", new Dictionary<string, object>{{"DeltaTime", deltaTime}, {"CpuTimeMs", _metrics[OrchestratorMetricType.CpuUpdateMs]}}, DateTime.UtcNow));
                }

                // [AJOUT] Mettre à jour les métriques de runtime
                _metrics[OrchestratorMetricType.TelemetryEvents] = _telemetryCollector.GetEventCount(); // Placeholder pour la méthode GetEventCount()

                // Calculer la santé
                float errors = _metrics[OrchestratorMetricType.ErrorCount];
                float warnings = _metrics[OrchestratorMetricType.WarningCount];
                float maxIssues = 100; // Seuil arbitraire
                _metrics[OrchestratorMetricType.HealthPercentage] = Math.Max(0, 100 - ((errors * 10) + (warnings * 1))); // Pénalités arbitraires

                // D. Simulation et test : Mode dégradé
                if (_degradedMode)
                {
                    // Désactiver certains sous-systèmes ou réduire la qualité
                    // Exemple simple : réduire le nombre de bones calculés
                    _metrics[OrchestratorMetricType.TotalBonesAnimated] /= 2;
                }

                // Simuler du chaos si en mode Chaos
                if (_simulationMode == StubSimulationMode.Chaos)
                {
                    if (_random.NextDouble() < 0.02) // 2% de chance
                    {
                        LogCall("CHAOS: Random state change or fault injected.");
                        // Exemples de chaos : suspendre, modifier la qualité, injecter des erreurs, etc.
                        if (_random.Next(2) == 0) Suspend();
                        else _qualityLevel = (AnimationQualityLevel)_random.Next(4);
                    }
                }

                _state = AnimationEngineState.Ready;
                LogCall($"Update({deltaTime})");
                return this;
            }
        }

        public AnimationPose GetAnimationPose(string entityName)
        {
            if (_currentPoses.TryGetValue(entityName, out var pose))
            {
                LogCall($"GetAnimationPose({entityName})");
                _metrics[OrchestratorMetricType.RenderedPoses]++;
                // B. Diagnostic et instrumentation : Valider la pose
                ValidatePose(pose);
                return pose;
            }
            LogCall($"GetAnimationPose({entityName}) - Not Found");
            _metricCollector.Increment(OrchestratorMetricType.RenderedPoses, 1);
            return new AnimationPose(new Dictionary<string, Transform>(), 0, new AnimationClip()); // Retourne une pose vide ou une pose par défaut
        }

        #endregion

        #region Properties (Core)

        public AnimationEngineState State => _state;
        public StubSimulationMode SimulationMode => _simulationMode;
        public AnimationQualityLevel QualityLevel => _qualityLevel;
        public bool IsReady => _state == AnimationEngineState.Ready;
        public bool IsUpdating => _state == AnimationEngineState.Updating;
        public bool IsSuspended => _state == AnimationEngineState.Suspended;
        public bool EnableCallLogging => _enableCallLogging;
        public bool EnableAssertions => _enableAssertions;
        public bool EnableSnapshots => _enableSnapshots;
        public bool EnableReplay => _enableReplay;
        public bool EnableFaultInjection => _enableFaultInjection;
        public bool EnableValidation => _enableValidation;
        public bool EnablePerformanceSim => _enablePerformanceSim;
        public bool EnableMemoryPressureSim => _enableMemoryPressureSim;
        public bool EnableThreadingSim => _enableThreadingSim;
        public bool EnableHooks => _enableHooks;
        // A. Optimisation et structure : Retourne une copie via ToList()
        public List<string> CallLog => _callLog.ToList();

        // G. Sécurité et stabilité
        public bool IsStable => _isStable;

        #endregion

        #region IAnimationEngine Explicit Implementation (Placeholder) (Core)

        // Les neuf implementations EXPLICITES qui vivaient ici sont parties, et
        // c'est la consequence directe de la reduction d'IAnimationEngine a trois
        // membres : `X IAnimationEngine.Membre` exige que Membre figure au
        // contrat, d'ou neuf CS0539.
        //
        // Elles ne perdent rien. C'etaient de purs relais — `=> this.Membre` —
        // vers des membres PUBLICS du stub, qui restent tous en place : State,
        // IsReady, Initialize, Update, Shutdown, Dispose, LoadAnimationClip,
        // PlayAnimation, GetAnimationPose. Seule la forme explicite disparait.
        //
        // Le jour ou l'un d'eux rejoindra le contrat parce qu'un appelant reel
        // le demande, il sera deja implemente.

        #endregion

        #region Sub-Systems Integration (via IAnimationSubsystem) (Core)

        // Ces méthodes sont des points d'entrée pour interagir avec les sous-systèmes.
        // Elles peuvent être appelées par d'autres systèmes ou par l'orchestrateur.

        // LogCall : version instrumentee conservee dans le fichier partiel
        public AnimationEngineStub SetAnimationState(string entityName, string stateName) { LogCall($"SetAnimationState({entityName}, {stateName})"); return this; }
        // SolveIK : version instrumentee conservee dans le fichier partiel
        // UpdateProcedural : version instrumentee conservee dans le fichier partiel
        public AnimationEngineStub DecompressAnimation(AnimationClip clip) { LogCall($"DecompressAnimation({clip.Name})"); return this; }

        #endregion

        #region Movement Bridge Integration (Core)

        /// <summary>
        /// Applique un déplacement et une rotation de Root Motion à une entité.
        /// Utilisée par MovementAnimationBridgeSystem pour synchroniser la physique et l'animation.
        /// </summary>
        public AnimationEngineStub ApplyRootMotion(string entityName, Vector3 deltaPosition, Quaternion deltaRotation)
        {
            // Logique de mise à jour de la position/rotation de l'entité basée sur le Root Motion
            // Ceci est un hook conceptuel. L'implémentation réelle dépendrait de la structure Entity-Component du moteur.
            LogCall($"ApplyRootMotion({entityName}, {deltaPosition}, {deltaRotation})");
            _metrics[OrchestratorMetricType.RootMotionApplied]++;
            // Exemple de mise à jour d'une propriété de l'entité si elle existait dans ce stub
            // if (_entities.TryGetValue(entityName, out var entity))
            // {
            //     entity.Transform.Position += deltaPosition;
            //     entity.Transform.Rotation = deltaRotation * entity.Transform.Rotation; // Combinaison des rotations
            // }
            return this;
        }

        #endregion

        #region RenderGraph Integration (Core)

        /// <summary>
        /// Hook appelé par le RenderGraph (via IRenderPipeline) pour récupérer la pose animée d'une entité avant le rendu.
        /// </summary>
        public AnimationPose OnRenderPose(string entityName)
        {
            // Retourne la pose actuelle de l'entité pour le rendu
            LogCall($"OnRenderPose({entityName})");
            return GetAnimationPose(entityName);
        }

        #endregion

        #region Logging, Assertions, Snapshots, Replay, Fault Injection (Core)

        private void LogCall(string callDescription)
        {
            if (_enableCallLogging)
            {
                _callLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {callDescription}");
                _metricCollector.Increment(OrchestratorMetricType.TelemetryEvents, 1);
                // B. Diagnostic et instrumentation : Ajouter au trace buffer
                _diagnosticsManager.Log(new TraceEvent(callDescription, DateTime.UtcNow, _state, null));
                // K. Architecture avancée : Enregistrer l'événement
                _eventRecorder.Record(new TraceEvent(callDescription, DateTime.UtcNow, _state, null));
                // K. Architecture avancée : Ajouter à l'audit trail
                _auditTrail.Log(callDescription);
                // [AJOUT] Enregistrer l'événement de log pour la télémétrie
                if (_stubConfig.FeatureFlags.HasFlag(StubFeatureFlags.Telemetry))
                {
                    _telemetryCollector.RecordEvent(new TelemetryEvent("LogEvent", new Dictionary<string, object>{{"Message", callDescription}}, DateTime.UtcNow));
                }
            }
        }

        public List<string> GetCallLog() => CallLog; // Retourne une copie

        public AnimationEngineStub ClearCallLog()
        {
            _callLog.Clear();
            return this;
        }

        public AnimationEngineStub Assert(bool condition, string message)
        {
            if (_enableAssertions && !condition)
            {
                LogCall($"ASSERTION FAILED: {message}");
                // Dans un vrai moteur, cela pourrait lever une exception ou un événement.
                // throw new InvalidOperationException($"Assertion failed: {message}");
            }
            return this;
        }

        public AnimationEngineStub TakeSnapshot(string snapshotId)
        {
            if (_enableSnapshots)
            {
                LogCall($"TakeSnapshot({snapshotId}) - Conceptual: Store current state of clips, poses, active anims.");
                // Ici, on sauvegarderait l'état interne dans une structure ou un fichier.
                // [AJOUT] Utiliser le RuntimeInspector
                var snapshot = _runtimeInspector.TakeSnapshot();
                LogCall($"Snapshot taken: {snapshotId}");
                _metrics[OrchestratorMetricType.SnapshotsTaken]++;
            }
            return this;
        }

        public AnimationEngineStub ReplayFromSnapshot(string snapshotId)
        {
            if (_enableReplay)
            {
                LogCall($"ReplayFromSnapshot({snapshotId}) - Conceptual: Restore state from snapshot.");
                // Ici, on restaurerait l'état interne depuis la structure ou le fichier.
                // [AJOUT] Utiliser le RuntimeInspector
                // var snapshot = LoadSnapshot(snapshotId); // Méthode à implémenter
                // _runtimeInspector.RollbackToSnapshot(snapshot);
            }
            return this;
        }

        public AnimationEngineStub InjectFault(StubFaultType faultType, float probability = 1.0f)
        {
            if (_enableFaultInjection && _random.NextDouble() < probability)
            {
                LogCall($"InjectFault({faultType}) - Conceptual: Simulate specific failure mode.");
                switch (faultType)
                {
                    case StubFaultType.MemoryAllocationFailure:
                        // Simuler un échec d'allocation mémoire
                        break;
                    case StubFaultType.ThreadStarvation:
                        // Simuler un manque de threads
                        break;
                    case StubFaultType.CorruptedData:
                        // Simuler des données d'animation corrompues
                        break;
                    // ... autres types de fautes
                }
            }
            return this;
        }

        public AnimationEngineStub ValidateState()
        {
            if (_enableValidation)
            {
                LogCall("ValidateState - Checking internal consistency.");
                // Exemples de validations
                Assert(_loadedClipsDict.Count >= 0, "Loaded clips count cannot be negative.");
                Assert(_activeAnimations.Count <= _loadedClipsDict.Count * 10, "Too many active animations compared to loaded clips.");
                // ... d'autres validations
            }
            return this;
        }

        #endregion

        #region Simulation Methods (Core)

        private void SimulateCPULoad()
        {
            // Simuler une charge CPU en bloquant le thread principal
            if (_cpuLoadSim > 0)
            {
                Thread.Sleep(_cpuLoadSim);
            }
        }

        private void SimulateMemoryPressure()
        {
            // Simuler une pression mémoire en allouant temporairement
            if (_memoryPressureSim > 0)
            {
                // R. Mémoire & allocation : Utiliser le pool ou l'arena si disponible
                byte[] dummyArray;
                if (_memoryPool != null)
                {
                    dummyArray = _memoryPool.Acquire();
                    // Utiliser dummyArray...
                    _memoryPool.Release(dummyArray);
                }
                else
                {
                    dummyArray = new byte[_memoryPressureSim * 1024 * 1024]; // Mo en bytes
                }
                Thread.Sleep(1); // Laisser un peu de temps pour que le GC agisse
                _metrics[OrchestratorMetricType.MemoryUsedBytes] += dummyArray.Length;
                _memoryUsageTracker.ReportAllocation("SimulatedPressure", dummyArray.Length);
            _metricCollector.Increment(OrchestratorMetricType.MemoryUsedBytes, dummyArray.Length);
            }
        }

        private void SimulateThreadingLoad()
        {
            // Simuler une charge threading en lançant des tâches légères
            if (_threadingLoadSim > 0)
            {
                for (int i = 0; i < _threadingLoadSim; i++)
                {
                    ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(1); });
                }
                _metrics[OrchestratorMetricType.ThreadingTasksQueued] += _threadingLoadSim;
            _metricCollector.Increment(OrchestratorMetricType.ThreadingTasksQueued, _threadingLoadSim);
            }
        }

        #endregion

        #region Watchdog & Health Check (Security.cs)

        private void WatchdogCallback(object state)
        {
            // Vérifier si le moteur est bloqué dans un état non-progressif
            // Cette logique est simplifiée, dans un vrai moteur, elle serait plus sophistiquée.
            lock (_watchdogLock)
            {
                if (_state == AnimationEngineState.Updating)
                {
                    LogCall("WATCHDOG: Engine appears stuck in Updating state!");
                    // Potentiellement forcer un retour à Ready ou une suspension.
                    _state = AnimationEngineState.Ready;
                    _isStable = false; // Marquer comme instable
                }
            _isStable = false;
            }
        }

        public AnimationEngineStub EnableWatchdog(int intervalMs = 5000)
        {
            _stateWatchdogTimer.Change(intervalMs, intervalMs);
            LogCall($"EnableWatchdog({intervalMs}ms)");
            return this;
        }

        public AnimationEngineStub DisableWatchdog()
        {
            _stateWatchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);
            LogCall("DisableWatchdog");
            return this;
        }

        public bool IsHealthy()
        {
            // Une simple vérification de l'état, pourrait être plus complexe
            return _state == AnimationEngineState.Ready || _state == AnimationEngineState.Suspended;
        }

        #endregion

        #region Metrics & Dashboard Integration (Metrics.cs)

        public AnimationEngineMetrics GetMetrics()
        {
            lock (_metricsLock)
            {
                return new AnimationEngineMetrics(new Dictionary<OrchestratorMetricType, float>(_metrics));
            }
        }

        #endregion

        #region Diagnostics & Instrumentation (Diagnostics.cs)

        // Expose un DumpState() pour exporter l’état complet du stub (utile pour tests unitaires).
        public string DumpState()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- AnimationEngineStub State Dump ---");
            sb.AppendLine($"State: {_state}");
            sb.AppendLine($"SimulationMode: {_simulationMode}");
            sb.AppendLine($"Loaded Clips Count: {_loadedClipsDict.Count}");
            sb.AppendLine($"Active Animations Count: {_activeAnimations.Count}");
            sb.AppendLine($"Current Poses Count: {_currentPoses.Count}");
            sb.AppendLine("--- Metrics ---");
            foreach (var kvp in _metrics)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine("--- Call Log (Last 10) ---");
            var log = CallLog;
            for (int i = Math.Max(0, log.Count - 10); i < log.Count; i++)
            {
                sb.AppendLine(log[i]);
            }
            sb.AppendLine("--- Trace Buffer ---");
            foreach (var traceEvent in _diagnosticsManager.GetRecentEvents())
            {
                sb.AppendLine($"[{traceEvent.Timestamp:HH:mm:ss.fff}] {traceEvent.Message} (State: {traceEvent.State})");
            }
            return sb.ToString();
        }

        // Ajoute un ValidatePose() pour vérifier la cohérence des données avant rendu.
        private void ValidatePose(AnimationPose pose)
        {
            if (_enableValidation)
            {
                foreach (var bone in pose.Bones)
                {
                    // Exemple de validation simple
                    Assert(!float.IsNaN(bone.Value.Position.X), $"Invalid X in bone {bone.Key}");
                    Assert(!float.IsInfinity(bone.Value.Position.X), $"Invalid X in bone {bone.Key}");
                }
            }
        }

        #endregion

        #region C. Performance et threading (Threading.cs)

        // Expose un ParallelUpdate() pour tester la scalabilité multi‑thread.
        public async Task<AnimationEngineStub> ParallelUpdateAsync(float deltaTime)
        {
            await Task.Run(() => Update(deltaTime));
            return this;
        }

        // Inclure un PerformanceSnapshot struct (CPU, GPU, Memory, Threads, FPS).
        public PerformanceSnapshot GetPerformanceSnapshot()
        {
            float avgFrameTime = 0;
            int count = 0;
            lock (_perfLock)
            {
                foreach (var ft in _frameTimes)
                {
                    avgFrameTime += ft;
                    count++;
                }
            }
            if (count > 0) avgFrameTime /= count;
            return new PerformanceSnapshot(_metrics[OrchestratorMetricType.CpuUpdateMs], (long)_metrics[OrchestratorMetricType.MemoryUsedBytes], Environment.ProcessorCount, avgFrameTime);
        }

        #endregion

        #region D. Simulation et test (Simulation.cs)

        // Ajoute un DegradedMode (désactive certains sous‑systèmes pour tester la robustesse).
        public AnimationEngineStub SetDegradedMode(bool enabled)
        {
            _degradedMode = enabled;
            LogCall($"SetDegradedMode({enabled})");
            return this;
        }

        // Ajoute un StressProfile struct pour configurer la charge CPU/GPU.
        public AnimationEngineStub SetStressProfile(StressProfile profile)
        {
            _stressProfile = profile;
            _stressTestManager.SetProfile(profile); // Mettre à jour le gestionnaire de stress
            LogCall($"SetStressProfile(CPU:{profile.CpuLoadPercent}%, Mem:{profile.MemoryPressureMB}MB, Threads:{profile.ThreadingLoadTasks})");
            return this;
        }

        #endregion

        #region E. Intégration moteur (Core.cs)

        // Ajoute un RegisterToEventBus() pour écouter les événements du moteur (ex. : pause, resume).
        public AnimationEngineStub RegisterToEventBus(EventBus bus)
        {
            _eventBus = bus;
            // Exemple d'écoute d'événements
            // bus.Subscribe<PauseEvent>(e => Suspend());
            // bus.Subscribe<ResumeEvent>(e => Resume());
            LogCall("Registered to EventBus");
            return this;
        }

        // Expose un GetSubsystemStatus() pour ton orchestrateur.
        public Dictionary<string, bool> GetSubsystemStatus()
        {
            var status = new Dictionary<string, bool>();
            foreach (var sys in new List<IAnimationSubsystem>{_blendTreeSystem, _stateMachineSystem, _ikSystem, _proceduralSystem, _compressionSystem})
            {
                status[sys.Name] = sys.GetHealthStatus() == SubsystemHealthStatus.Healthy;
            }
            return status;
        }

        #endregion

        #region F. Debug et visualisation (Core.cs)

        // Expose un GetActiveAnimations() pour ton dashboard.
        public List<string> GetActiveAnimations() => new List<string>(_activeAnimations);

        #endregion

        #region G. Sécurité et stabilité (Security.cs)

        // Ajoute un RecoveryHandler() pour restaurer l’état après une erreur critique.
        public AnimationEngineStub SetRecoveryHandler(Action recoveryAction)
        {
            // Stocker l'action de récupération
            // L'appeler dans le WatchdogCallback ou en cas d'erreur critique
            LogCall("Recovery handler set.");
            return this;
        }

        #endregion

        #region H. Extensibilité (Core.cs)

        // Expose un RegisterSubsystem<T>() pour ajouter dynamiquement des modules.
        public AnimationEngineStub RegisterPlugin(IAnimationPlugin plugin)
        {
            _plugins.Add(plugin);
            plugin.Initialize(this);
            LogCall($"Registered plugin: {plugin.Name}");
            return this;
        }

        // Inclure un VersionInfo struct pour identifier la version du stub.
        public VersionInfo GetVersion() => _version;

        // Prépare un CompatibilityCheck() pour valider la cohérence entre stub et moteur.
        public bool CompatibilityCheck(VersionInfo engineVersion)
        {
            // Logique simple de compatibilité
            return _version.Version.StartsWith(engineVersion.Version.Substring(0, engineVersion.Version.IndexOf('.')));
        }

        #endregion

        #region [AJOUT] Méthodes pour idées 398-597 (Core.cs)

        // Plugins
        public AnimationEngineStub LoadPluginFromFile(string path) { _pluginHost.LoadPlugin(path); return this; }
        public AnimationEngineStub UnloadPluginByName(string name) { _pluginHost.UnloadPlugin(name); return this; }
        public AnimationEngineStub ReloadPluginByName(string name) { _pluginHost.ReloadPlugin(name); return this; }
        public List<IAnimationPlugin> GetLoadedPlugins() => _pluginHost.GetLoadedPlugins();

        // Features
        public AnimationEngineStub SetFeatureToggle(string name, bool enabled, string description = "", string group = "") { _featureToggleService.SetToggle(name, enabled, description, group); return this; }
        public bool IsFeatureEnabled(string name) => _featureToggleService.IsEnabled(name);
        public List<FeatureToggle> GetAllFeatureToggles() => _featureToggleService.GetAllToggles();

        // Config
        public AnimationEngineStub SetConfigValue(string key, object value) { _configManager.SetSetting(key, value); return this; }
        public T GetConfigValue<T>(string key, T defaultValue = default) => _configManager.GetSetting(key, defaultValue);
        public AnimationEngineStub RegisterConfigProfile(ConfigProfile profile) { _configManager.RegisterProfile(profile); return this; }
        public AnimationEngineStub ApplyConfigProfile(string name) { _configManager.ApplyProfile(name); return this; }

        // Runtime
        public RuntimeSnapshot TakeRuntimeSnapshot() => _runtimeInspector.TakeSnapshot();
        public AnimationEngineStub CompareRuntimeSnapshots(RuntimeSnapshot a, RuntimeSnapshot b) { _runtimeInspector.CompareSnapshots(a, b); return this; }
        public AnimationEngineStub RollbackToRuntimeSnapshot(RuntimeSnapshot snapshot) { _runtimeInspector.RollbackToSnapshot(snapshot); return this; }

        // Assets
        public AnimationEngineStub RegisterAssetToCatalog(AssetInfo info) { _assetCatalog.RegisterAsset(info); return this; }
        public AssetInfo GetAssetInfoFromCatalog(string name) => _assetCatalog.GetAssetInfo(name);
        public List<AssetInfo> GetAllAssetsFromCatalog() => _assetCatalog.GetAllAssets();
        public AnimationEngineStub MarkAssetAsUsedInCatalog(string name) { _assetCatalog.MarkAssetAsUsed(name); return this; }
        public AnimationEngineStub MarkAssetAsUnusedInCatalog(string name) { _assetCatalog.MarkAssetAsUnused(name); return this; }

        // Caching
        public bool TryGetCachedAsset(string name, out object asset) => _assetCache.TryGetAsset(name, out asset);
        public AnimationEngineStub PutAssetInCache(string name, object asset) { _assetCache.PutAsset(name, asset); return this; }
        public AnimationEngineStub EvictAssetFromCache(string name) { _assetCache.EvictAsset(name); return this; }
        public AnimationEngineStub ClearAssetCache() { _assetCache.Clear(); return this; }
        public int GetAssetCacheSize() => _assetCache.GetCacheSize();

        // Integrity
        public bool VerifyAssetIntegrity(AssetInfo info) => _integrityChecker.VerifyAssetIntegrity(info);
        public AnimationEngineStub RepairAsset(AssetInfo info) { _integrityChecker.RepairAsset(info); return this; }

        // Telemetry
        // RecordTelemetryEvent : version instrumentee conservee dans le fichier partiel
        // FlushTelemetry : version instrumentee conservee dans le fichier partiel

        // Chaos
        public AnimationEngineStub InjectChaos(ChaosEvent chaosEvent) { _chaosMonkey.InjectChaos(chaosEvent); return this; }
        public AnimationEngineStub ScheduleChaos(ChaosEvent chaosEvent, DateTime time) { _chaosMonkey.ScheduleChaos(chaosEvent, time); return this; }

        // Helpers
        private string GenerateChecksum(string path) => "CHK_" + path.GetHashCode().ToString("X8"); // Stub simple

        #endregion
    }

    #region Supporting Types (Conceptual)

    // IAnimationEngine etait redeclare ici, en concurrence avec Engine/IAnimationEngine.cs.
    // Le contrat unique vit desormais dans Engine/IAnimationEngine.cs.

    // StubFaultType est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationEngineConfig est declare dans un fichier partiel d'AnimationEngineStub.

    // AnimationEngineStubConfig est declare dans un fichier partiel d'AnimationEngineStub.

    // StressProfileExtensions est declare dans un fichier partiel d'AnimationEngineStub.

    // VersionInfoExtensions est declare dans un fichier partiel d'AnimationEngineStub.

    #endregion
}