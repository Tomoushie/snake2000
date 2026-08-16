// /Engine/Animation/AnimationEngineStub.Core.cs
//
// Responsabilités : Cycle de vie, configuration, hooks, intégration des sous-systèmes, points d'entrée principaux.
// Dépendances : EventBus, Profiler, IJobSystem, ResourceManager, IRenderEngine, IPhysicsEngine.
// Intègre : SubsystemRegistry, MetricCollector (via AnimationEngineStub.Metrics.cs).

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core;
using Engine.Services;
using Engine.Events;
using Engine.Profiling;
using Engine.Jobs;
using Engine.Resources;
using Engine.Rendering;

namespace Engine.Animation
{
    #region Enums & Structs (Core)

    public enum AnimationEngineState
    {
        Uninitialized,
        Initializing,
        Ready,
        Updating,
        Suspending,
        Suspended,
        ShuttingDown,
        Disposed
    }

    public enum StubSimulationMode
    {
        Normal,          // Comportement standard
        Degraded,        // Simule des performances réduites
        ErrorProne,      // Injecte des erreurs aléatoires
        Deterministic,   // Force le déterminisme pour les tests
        Chaos,           // Injecte des fautes aléatoires et variées
        StressTest       // Simule des charges extrêmes
    }

    public enum AnimationQualityLevel
    {
        Low,
        Medium,
        High,
        Ultra
    }

    public enum AnimationCompressionMethod
    {
        None,
        VectorQuantization,
        DeltaCompression,
        KeyframeReduction
    }

    public enum AnimationUpdateMode
    {
        Sequential,
        Parallel,
        Predictive
    }

    // A. Architecture et structure
    public enum SubsystemType
    {
        BlendTree,
        StateMachine,
        InverseKinematics,
        Procedural,
        Compression
    }

    public enum SubsystemHealthStatus
    {
        Healthy,
        Warning,
        Error,
        Recovering
    }

    // G. Sécurité et stabilité
    [Flags]
    public enum StubFeatureFlags
    {
        None = 0,
        CallLogging = 1 << 0,
        Assertions = 1 << 1,
        Snapshots = 1 << 2,
        Replay = 1 << 3,
        FaultInjection = 1 << 4,
        Validation = 1 << 5,
        PerformanceSim = 1 << 6,
        MemoryPressureSim = 1 << 7,
        ThreadingSim = 1 << 8,
        Hooks = 1 << 9,
        SafeMode = 1 << 10, // H. Extensibilité
        // [AJOUT] Nouveaux flags pour les idées 398-597
        PluginLoading = 1 << 11,
        Telemetry = 1 << 12,
        HotReload = 1 << 13,
        ABTesting = 1 << 14,
        CanaryMode = 1 << 15,
        ChaosMonkey = 1 << 16,
        AssetStreaming = 1 << 17,
        AssetCaching = 1 << 18,
        IntegrityChecking = 1 << 19,
        AuditLogging = 1 << 20,
        RateLimiting = 1 << 21,
        CircuitBreaking = 1 << 22,
        GDPRMode = 1 << 23,
        PIIProtection = 1 << 24
    }

    // H. Extensibilité
    public readonly struct VersionInfo
    {
        public readonly string Version;
        public readonly DateTime BuildDate;
        public VersionInfo(string v, DateTime bd) => (Version, BuildDate) = (v, bd);
    }

    // D. Simulation et test
    public readonly struct StressProfile
    {
        public readonly int CpuLoadPercent;
        public readonly int MemoryPressureMB;
        public readonly int ThreadingLoadTasks;
        public StressProfile(int cpu, int mem, int thread) => (CpuLoadPercent, MemoryPressureMB, ThreadingLoadTasks) = (cpu, mem, thread);
    }

    public readonly struct AnimationPose
    {
        public readonly Dictionary<string, Transform> Bones; // Nom du bone -> Transform
        public readonly float Time;
        public readonly AnimationClip Clip;
        public AnimationPose(Dictionary<string, Transform> bones, float time, AnimationClip clip) => (Bones, Time, Clip) = (bones, time, clip);
    }

    public readonly struct AnimationClip
    {
        public readonly string Name;
        public readonly float Duration;
        public readonly List<Keyframe> Keyframes;
        public readonly AnimationCompressionMethod Compression;
        public readonly string AssetPath; // [AJOUT] Chemin d'origine
        public readonly string Checksum; // [AJOUT] Pour intégrité
        public readonly Dictionary<string, object> Metadata; // [AJOUT] Pour tags, licence, etc.
        public AnimationClip(string name, float duration, List<Keyframe> keyframes, AnimationCompressionMethod comp, string path, string checksum, Dictionary<string, object> metadata) => (Name, Duration, Keyframes, Compression, AssetPath, Checksum, Metadata) = (name, duration, keyframes, comp, path, checksum, metadata);
    }

    public readonly struct Keyframe
    {
        public readonly float Time;
        public readonly Transform Transform;
        public Keyframe(float time, Transform transform) => (Time, Transform) = (time, transform);
    }

    public struct Transform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Transform(Vector3 pos, Quaternion rot, Vector3 scale) => (Position, Rotation, Scale) = (pos, rot, scale);
    }

    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) => (X, Y, Z) = (x, y, z);
        public static Vector3 Zero => new Vector3(0, 0, 0);
    }

    public struct Quaternion
    {
        public float X, Y, Z, W;
        public Quaternion(float x, float y, float z, float w) => (X, Y, Z, W) = (x, y, z, w);
        public static Quaternion Identity => new Quaternion(0, 0, 0, 1);
    }

    #endregion

    #region Interfaces (Core)

    /// <summary>
    /// Interface uniforme pour tous les sous-systèmes d'animation.
    /// </summary>
    public interface IAnimationSubsystem
    {
        string Name { get; }
        SubsystemType Type { get; }
        void Initialize(AnimationEngineStub engine);
        void Update(float deltaTime);
        void Shutdown();
        AnimationEngineMetrics GetMetrics(); // Pour le dashboard
        SubsystemHealthStatus GetHealthStatus();
    }

    // H. Extensibilité
    public interface IAnimationPlugin
    {
        string Name { get; }
        void Initialize(AnimationEngineStub engine);
        void Update(float deltaTime);
        void Shutdown();
        PluginManifest GetManifest(); // [AJOUT]
    }

    #endregion

    public partial class AnimationEngineStub : IAnimationEngine
    {
        #region Constants & Internal Classes (Core)

        // A. Optimisation et structure
        internal static class AnimationConstants
        {
            public const int MaxConcurrentAnimations = 100;
            public const int MaxLoggedCalls = 1000;
            public const int TraceBufferSize = 100;
            public const int FrameBudgetMs = 16; // 60 FPS
            public const int MemoryPoolBlockSize = 1024; // 1KB
            public const int MemoryArenaSize = 10 * 1024 * 1024; // 10MB
        }

        #endregion

        #region Fields (Core)

        // --- État interne ---
        private AnimationEngineState _state = AnimationEngineState.Uninitialized;
        private readonly object _stateLock = new object();
        private StubSimulationMode _simulationMode = StubSimulationMode.Normal;
        private AnimationQualityLevel _qualityLevel = AnimationQualityLevel.High;
        private AnimationUpdateMode _updateMode = AnimationUpdateMode.Sequential;
        // A. Optimisation et structure : Utilisation de ConcurrentBag pour les clips chargés (exemple)
        private readonly ConcurrentBag<AnimationClip> _loadedClipsBag = new ConcurrentBag<AnimationClip>();
        private readonly Dictionary<string, AnimationClip> _loadedClipsDict = new Dictionary<string, AnimationClip>();
        private readonly Dictionary<string, AnimationPose> _currentPoses = new Dictionary<string, AnimationPose>();
        private readonly List<string> _activeAnimations = new List<string>();
        private readonly Random _random = new Random();
        private readonly Timer _stateWatchdogTimer; // Pour détecter les blocages
        // A. Optimisation et structure : Utilisation de ConcurrentBag pour les logs (thread-safe)
        private readonly ConcurrentBag<string> _callLog = new ConcurrentBag<string>();
        private bool _enableCallLogging = false;
        private bool _enableAssertions = true;
        private bool _enableSnapshots = false;
        private bool _enableReplay = false;
        private bool _enableFaultInjection = false;
        private bool _enableValidation = false;
        private bool _enablePerformanceSim = false;
        private bool _enableMemoryPressureSim = false;
        private bool _enableThreadingSim = false;
        private bool _enableHooks = false;

        // --- Dépendances Injectées ---
        private EventBus _eventBus;
        private Profiler _profiler;
        private IJobSystem _jobSystem;
        private ResourceManager _resourceManager;
        private IRenderEngine _renderEngine; // Pour le hook de rendu
        private IPhysicsEngine _physicsEngine; // Pour le hook de physique

        // --- Configurations ---
        private AnimationEngineConfig _config;
        private AnimationEngineStubConfig _stubConfig;

        // --- Variables de simulation ---
        private int _cpuLoadSim = 0;
        private int _memoryPressureSim = 0;
        private int _threadingLoadSim = 0;

        // --- Gestionnaires (Core, mais dépend de Metrics.cs pour _metricCollector) ---
        private readonly SubsystemRegistry _subsystemRegistry = new SubsystemRegistry();
        private readonly SubsystemLifecycleManager _lifecycleManager = new SubsystemLifecycleManager();
        private AnimationBlendTreeSystem _blendTreeSystem;
        private AnimationStateMachineSystem _stateMachineSystem;
        private AnimationInverseKinematicsSystem _ikSystem;
        private AnimationProceduralSystem _proceduralSystem;
        private AnimationCompressionSystem _compressionSystem;

        // --- Plugins (H. Extensibilité) ---
        private readonly List<IAnimationPlugin> _plugins = new List<IAnimationPlugin>();
        private VersionInfo _version = VersionInfo.Default;

        // --- K. Architecture avancée ---
        private readonly SubsystemHotSwapManager _hotSwapManager = new SubsystemHotSwapManager();
        private readonly SubsystemSandbox _sandbox = new SubsystemSandbox();
        private readonly SubsystemAuditTrail _auditTrail = new SubsystemAuditTrail();
        private readonly SubsystemRollback _rollbackManager = new SubsystemRollback();
        private readonly SubsystemStateSerializer _stateSerializer = new SubsystemStateSerializer();
        private readonly SubsystemStateDeserializer _stateDeserializer = new SubsystemStateDeserializer();
        private readonly SubsystemEventRecorder _eventRecorder = new SubsystemEventRecorder();
        private readonly SubsystemEventPlayer _eventPlayer = new SubsystemEventPlayer();

        // --- G. Sécurité et stabilité ---
        private bool _safeMode = false;
        private readonly object _watchdogLock = new object();
        private bool _isStable = true;
        private readonly AnimationWatchdog _animationWatchdog = new AnimationWatchdog();

        // --- [AJOUT] Managers pour idées 398-597 (Core dépend de Metrics pour _metricCollector) ---
        // Les déclarations sont dans les fichiers respectifs :
        // private readonly AnimationPluginHost _pluginHost = new AnimationPluginHost(); // dans Plugins.cs
        // private readonly FeatureToggleService _featureToggleService = new FeatureToggleService(); // dans Features.cs
        // private readonly AnimationConfigManager _configManager = new AnimationConfigManager(); // dans Config.cs
        // private readonly RuntimeInspector _runtimeInspector = new RuntimeInspector(); // dans Runtime.cs
        // private readonly AssetCatalog _assetCatalog = new AssetCatalog(); // dans Assets.cs
        // private readonly AssetCache _assetCache = new AssetCache(); // dans Assets.cs
        // private readonly IntegrityChecker _integrityChecker = new IntegrityChecker(); // dans Security.cs
        // private readonly TelemetryCollector _telemetryCollector = new TelemetryCollector(); // dans Diagnostics.cs
        // private readonly ChaosMonkey _chaosMonkey = new ChaosMonkey(); // dans Chaos.cs
        // private readonly MetricCollector _metricCollector = new MetricCollector(); // dans Metrics.cs (maintenant centralisé)
        // private readonly MetricsExporter _metricsExporter = new MetricsExporter(); // dans Metrics.cs
        // private readonly MetricsHealthMonitor _metricsHealthMonitor = new MetricsHealthMonitor(); // dans Metrics.cs
        // private readonly MetricsSnapshotHistory _snapshotHistory = new MetricsSnapshotHistory(); // dans Metrics.cs

        #endregion

        #region Constructors (Core)

        public AnimationEngineStub()
        {
            _stateWatchdogTimer = new Timer(WatchdogCallback, null, Timeout.Infinite, Timeout.Infinite);
            // Les métriques sont initialisées dans MetricCollector via Metrics.cs
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

                // [AJOUT] Appliquer les nouveaux flags (doivent être gérés dans les fichiers concernés)
                // bool enablePluginLoading = flags.HasFlag(StubFeatureFlags.PluginLoading);
                // bool enableTelemetry = flags.HasFlag(StubFeatureFlags.Telemetry);
                // ... etc ...

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

        // ... (Autres méthodes de cycle de vie : Suspend, Resume, Restart, Reset, Shutdown, Dispose) ...

        #endregion

        #region Core Animation Methods (Core)

        public AnimationEngineStub LoadAnimationClip(string clipName, string path)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"LoadAnimationClip({clipName}, {path})");

            if (!_loadedClipsDict.ContainsKey(clipName))
            {
                string checksum = GenerateChecksum(path);
                var clip = new AnimationClip(clipName, 2.0f, new List<Keyframe>(), AnimationCompressionMethod.None, path, checksum, new Dictionary<string, object>{{"Author", "System"}, {"License", "Internal"}});
                _loadedClipsBag.Add(clip);
                _loadedClipsDict[clipName] = clip;
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, 1);
                _metricCollector.Increment(OrchestratorMetricType.AssetsLoaded, 1);
                // ... autres logiques (catalogue, intégrité, télémétrie) ...
            }
            return this;
        }

        public AnimationEngineStub UnloadAnimationClip(string clipName)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"UnloadAnimationClip({clipName})");
            if (_loadedClipsDict.Remove(clipName, out var clip))
            {
                 _loadedClipsBag.TryTake(out _); // Retirer du bag aussi
                 // [AJOUT] Mise à jour via MetricCollector
                 _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, -1);
                 _metricCollector.Increment(OrchestratorMetricType.AssetsUnloaded, 1);
                 // ... autres logiques (catalogue, télémétrie) ...
            }
            return this;
        }

        public AnimationEngineStub PlayAnimation(string entityName, string clipName, float blendInTime = 0.1f)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            if (!_loadedClipsDict.ContainsKey(clipName)) throw new ArgumentException("Clip not loaded.", nameof(clipName));
            LogCall($"PlayAnimation({entityName}, {clipName}, {blendInTime})");

            if (!_activeAnimations.Contains($"{entityName}_{clipName}"))
            {
                _activeAnimations.Add($"{entityName}_{clipName}");
                _currentPoses[entityName] = new AnimationPose(new Dictionary<string, Transform>(), 0, _loadedClipsDict[clipName]);
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, 1);
                // ... autres logiques (catalogue, télémétrie) ...
            }
            return this;
        }

        public AnimationEngineStub StopAnimation(string entityName, string clipName, float blendOutTime = 0.1f)
        {
            if (_state != AnimationEngineState.Ready) throw new InvalidOperationException("Engine must be ready.");
            LogCall($"StopAnimation({entityName}, {clipName}, {blendOutTime})");

            if (_activeAnimations.Remove($"{entityName}_{clipName}"))
            {
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, -1);
                // ... autres logiques (télémétrie) ...
            }
            return this;
        }

        public AnimationEngineStub Update(float deltaTime)
        {
            lock (_stateLock)
            {
                if (_state != AnimationEngineState.Ready) return this;

                _state = AnimationEngineState.Updating;

                // [AJOUT] Réinitialiser les métriques de frame via MetricCollector
                _metricCollector.SetValue(OrchestratorMetricType.TotalBonesAnimated, 0);
                _metricCollector.SetValue(OrchestratorMetricType.BlendsCalculated, 0);
                _metricCollector.SetValue(OrchestratorMetricType.IKIterations, 0);
                _metricCollector.SetValue(OrchestratorMetricType.ProceduralUpdates, 0);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // ... (logique de simulation, de stress, de sécurité) ...

                // [AJOUT] Mettre à jour les sous-systèmes via le registre et collecter leurs métriques
                var blendSys = _subsystemRegistry.Get<AnimationBlendTreeSystem>();
                var stateSys = _subsystemRegistry.Get<AnimationStateMachineSystem>();
                var ikSys = _subsystemRegistry.Get<AnimationInverseKinematicsSystem>();
                var procSys = _subsystemRegistry.Get<AnimationProceduralSystem>();
                var compSys = _subsystemRegistry.Get<AnimationCompressionSystem>();

                if (blendSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); blendSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.BlendTree, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.BlendsCalculated, 1); }
                if (stateSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); stateSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.StateMachine, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.ActivePlaybacks, 1); } // Exemple
                if (ikSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); ikSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.InverseKinematics, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.IKIterations, 1); }
                if (procSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); procSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Procedural, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.ProceduralUpdates, 1); }
                if (compSys != null) { var sw = System.Diagnostics.Stopwatch.StartNew(); compSys.Update(deltaTime); sw.Stop(); _subsystemProfiler.RecordTime(SubsystemType.Compression, (float)sw.ElapsedMilliseconds); _metricCollector.Increment(OrchestratorMetricType.CompressedClipsLoaded, 1); } // Exemple

                // ... (logique d'avancement des animations) ...

                stopwatch.Stop();
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.SetValue(OrchestratorMetricType.CpuUpdateMs, (float)stopwatch.ElapsedMilliseconds);

                // ... (logique de frame time, de snapshot, de santé) ...

                // [AJOUT] Enregistrer des événements de télémétrie via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.TelemetryEvents, 1); // Exemple simple

                // [AJOUT] Enregistrer le snapshot dans l'historique via MetricsSnapshotHistory
                var currentMetrics = _metricCollector.GetSnapshot();
                _snapshotHistory.RecordSnapshot(currentMetrics);

                // ... (calcul de la santé, chaos, etc.) ...

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
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.RenderedPoses, 1);
                ValidatePose(pose);
                return pose;
            }
            LogCall($"GetAnimationPose({entityName}) - Not Found");
            return new AnimationPose(new Dictionary<string, Transform>(), 0, new AnimationClip());
        }

        #endregion

        // ... (Autres sections : Properties, IAnimationEngine impl, Sub-Systems, Movement Bridge, RenderGraph, Logging, etc.) ...

        #region Sub-Systems Integration (via IAnimationSubsystem) (Core)

        public AnimationEngineStub ApplyBlendTree(string entityName, string treeName) { LogCall($"ApplyBlendTree({entityName}, {treeName})"); _metricCollector.Increment(OrchestratorMetricType.BlendsCalculated, 1); return this; }
        public AnimationEngineStub SetAnimationState(string entityName, string stateName) { LogCall($"SetAnimationState({entityName}, {stateName})"); return this; }
        public AnimationEngineStub SolveIK(string entityName, string chainName, Vector3 target) { LogCall($"SolveIK({entityName}, {chainName}, {target})"); _metricCollector.Increment(OrchestratorMetricType.IKIterations, 1); return this; }
        public AnimationEngineStub UpdateProcedural(string entityName, string procAnimName) { LogCall($"UpdateProcedural({entityName}, {procAnimName})"); _metricCollector.Increment(OrchestratorMetricType.ProceduralUpdates, 1); return this; }
        public AnimationEngineStub DecompressAnimation(AnimationClip clip) { LogCall($"DecompressAnimation({clip.Name})"); return this; }

        #endregion

        #region RenderGraph Integration (Core)

        /// <summary>
        /// Hook appelé par le RenderGraph (via IRenderPipeline) pour récupérer la pose animée d'une entité avant le rendu.
        /// </summary>
        public AnimationPose OnRenderPose(string entityName)
        {
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
                // ... (autres logiques de Diagnostics.cs, Events, etc.) ...
                // [AJOUT] Enregistrer l'événement de log pour la télémétrie (via Metrics.cs)
                _metricCollector.Increment(OrchestratorMetricType.TelemetryEvents, 1);
            }
        }

        // ... (Autres méthodes : Assert, TakeSnapshot, ReplayFromSnapshot, InjectFault, ValidateState) ...

        #endregion

        #region Simulation Methods (Core)

        private void SimulateCPULoad() { if (_cpuLoadSim > 0) Thread.Sleep(_cpuLoadSim); }
        private void SimulateMemoryPressure()
        {
            if (_memoryPressureSim > 0)
            {
                byte[] dummyArray;
                if (_memoryPool != null) { dummyArray = _memoryPool.Acquire(); _memoryPool.Release(dummyArray); }
                else { dummyArray = new byte[_memoryPressureSim * 1024 * 1024]; }
                Thread.Sleep(1);
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.MemoryUsedBytes, dummyArray.Length);
                _memoryUsageTracker.ReportAllocation("SimulatedPressure", dummyArray.Length);
            }
        }
        private void SimulateThreadingLoad()
        {
            if (_threadingLoadSim > 0)
            {
                for (int i = 0; i < _threadingLoadSim; i++)
                    ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(1); });
                // [AJOUT] Mise à jour via MetricCollector
                _metricCollector.Increment(OrchestratorMetricType.ThreadingTasksQueued, _threadingLoadSim);
            }
        }

        #endregion

        #region Watchdog & Health Check (Security.cs)

        private void WatchdogCallback(object state)
        {
            lock (_watchdogLock)
            {
                if (_state == AnimationEngineState.Updating)
                {
                    LogCall("WATCHDOG: Engine appears stuck in Updating state!");
                    _state = AnimationEngineState.Ready;
                    _isStable = false;
                }
            }
        }

        public AnimationEngineStub EnableWatchdog(int intervalMs = 5000) { _stateWatchdogTimer.Change(intervalMs, intervalMs); LogCall($"EnableWatchdog({intervalMs}ms)"); return this; }
        public AnimationEngineStub DisableWatchdog() { _stateWatchdogTimer.Change(Timeout.Infinite, Timeout.Infinite); LogCall("DisableWatchdog"); return this; }
        public bool IsHealthy() => _state == AnimationEngineState.Ready || _state == AnimationEngineState.Suspended;

        #endregion

        #region Metrics & Dashboard Integration (Core - Relié à Metrics.cs)

        // La méthode GetMetrics() est maintenant dans AnimationEngineStub.Metrics.cs
        // public AnimationEngineMetrics GetMetrics() => _metricCollector.GetSnapshot(); // Déplacée

        #endregion

        #region Diagnostics & Instrumentation (Core - Relié à Diagnostics.cs)

        // La méthode DumpState() est maintenant dans AnimationEngineStub.Diagnostics.cs
        // public string DumpState() { ... } // Déplacée

        #endregion

        #region C. Performance et threading (Core - Relié à Threading.cs)

        // La méthode GetPerformanceSnapshot() est maintenant dans AnimationEngineStub.Threading.cs
        // public PerformanceSnapshot GetPerformanceSnapshot() { ... } // Déplacée

        #endregion

        #region D. Simulation et test (Core - Relié à Simulation.cs)

        // Les méthodes SetDegradedMode, SetStressProfile sont maintenant dans AnimationEngineStub.Simulation.cs
        // public AnimationEngineStub SetDegradedMode(bool enabled) { ... } // Déplacée
        // public AnimationEngineStub SetStressProfile(StressProfile profile) { ... } // Déplacée

        #endregion

        #region Pont d'animation (IAnimationEngine)

        private readonly List<IAnimationBridge> _bridges = new List<IAnimationBridge>();

        public void InitializeBridge(IAnimationBridge bridge)
        {
            LogCall("InitializeBridge");

            if (bridge == null)
                return;

            lock (_bridges)
            {
                if (!_bridges.Contains(bridge))
                    _bridges.Add(bridge);
            }
        }

        public void ShutdownBridge(IAnimationBridge bridge)
        {
            LogCall("ShutdownBridge");

            if (bridge == null)
                return;

            lock (_bridges)
            {
                _bridges.Remove(bridge);
            }
        }

        public System.Numerics.Vector2 ExtractRootMotionDelta(Snake2000.Engine.Core.Entity entity)
        {
            LogCall($"ExtractRootMotionDelta({entity.Id})");

            // Le stub ne simule pas de deplacement racine.
            return System.Numerics.Vector2.Zero;
        }

        #endregion

        #region Helpers (Core)

        private string GenerateChecksum(string path) => "CHK_" + path.GetHashCode().ToString("X8");

        #endregion
    }

    #region Supporting Types (Conceptual - Core)

    // IAnimationEngine etait redeclare ici, en concurrence avec Engine/IAnimationEngine.cs.
    // Le contrat unique vit desormais dans Engine/IAnimationEngine.cs.

    public enum StubFaultType
    {
        MemoryAllocationFailure,
        ThreadStarvation,
        CorruptedData,
        InvalidStateTransition,
        ResourceNotFound,
        Timeout,
        Overflow,
        Underflow
    }

    public class AnimationEngineConfig
    {
        public int MaxConcurrentAnimations { get; set; } = 100;
        public AnimationQualityLevel DefaultQuality { get; set; } = AnimationQualityLevel.High;
        // ... autres paramètres
    }

    public class AnimationEngineStubConfig
    {
        public StubSimulationMode SimulationMode { get; set; }
        public bool EnableCallLogging { get; set; }
        public bool EnableAssertions { get; set; }
        public bool EnableSnapshots { get; set; }
        public bool EnableReplay { get; set; }
        public bool EnableFaultInjection { get; set; }
        public bool EnableValidation { get; set; }
        public bool EnablePerformanceSim { get; set; }
        public bool EnableMemoryPressureSim { get; set; }
        public bool EnableThreadingSim { get; set; }
        public bool EnableHooks { get; set; }
        public AnimationQualityLevel QualityLevel { get; set; }
        public AnimationUpdateMode UpdateMode { get; set; }
        public StubFeatureFlags FeatureFlags { get; set; } = StubFeatureFlags.None;
        public static AnimationEngineStubConfig Default => new AnimationEngineStubConfig();
    }

    public static class StressProfileExtensions
    {
        public static StressProfile Default => new StressProfile(0, 0, 0);
        public static StressProfile Heavy => new StressProfile(80, 500, 100);
    }

    public static class VersionInfoExtensions
    {
        public static VersionInfo Default => new VersionInfo("1.0.0", DateTime.MinValue);
    }

    #endregion
}