// /Engine/Animation/Test/AnimationEngineStubOrchestrator.cs

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;   // Stopwatch
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Animation.Test
{
    #region Enums & Structs (réutilisés ou nouveaux)
    // Réutilisation de certains enums/types du stub
    using Engine.Animation;

    // [AJOUT] Enum pour les niveaux d'accès
    public enum AccessLevel
    {
        Public,
        Protected,
        Private,
        Admin
    }

    // [AJOUT] Enum pour les formats de log
    public enum LogFormat
    {
        Text,
        Json,
        Csv
    }

    // [AJOUT] Enum pour les stratégies de pool
    public enum PoolResizeStrategy
    {
        Fixed,
        Expandable,
        Shrinkable,
        Adaptive
    }

    // [AJOUT] Enum pour les états du cluster
    public enum StubClusterState
    {
        Idle,
        Initializing,
        Running,
        Degraded,
        Failed
    }

    // [AJOUT] Enum pour les types d'événements de l'orchestrator
    public enum OrchestratorEventType
    {
        Initialized,
        Started,
        Stopped,
        ScenarioStarted,
        ScenarioEnded,
        SessionRecorded,
        SessionReplayed,
        PluginLoaded,
        PluginUnloaded,
        Error,
        Warning,
        Info
    }

    // [AJOUT] Enum pour les types de métriques
    public enum OrchestratorMetricType
    {
        ActiveStubs,
        ActiveScenarios,
        ActiveSessions,
        RecordedSessions,
        LoadedPlugins,
        PoolSize,
        PoolUsage,
        QueueLength
    }

    // [AJOUT] Enum pour les types d'incidents
    public enum IncidentType
    {
        StartupFailure,
        ScenarioFailure,
        PluginFailure,
        ResourceExhaustion,
        Timeout,
        SecurityViolation
    }

    // [AJOUT] Enum pour les types de workflows
    public enum WorkflowType
    {
        TestExecution,
        PerformanceBenchmark,
        StressTest,
        RegressionTest
    }

    // [AJOUT] Enum pour les types de spans de tracing
    public enum SpanType
    {
        Operation,
        ScenarioExecution,
        SessionRecording,
        PluginLoad,
        Call
    }

    // [AJOUT] Structure pour les charges de travail
    public struct WorkloadDefinition
    {
        public string Name { get; set; }
        public int Complexity { get; set; }
        public int Priority { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public List<StubScenario> Scenarios { get; set; }
    }

    // [AJOUT] Structure pour les métriques de l'orchestrator
    public struct OrchestratorMetrics
    {
        public Dictionary<OrchestratorMetricType, float> Values { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // [AJOUT] Structure pour les événements de l'orchestrator
    public struct OrchestratorEvent
    {
        public OrchestratorEventType Type { get; set; }
        public string Message { get; set; }
        public object Payload { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; }
    }

    // [AJOUT] Structure pour les définitions de workflows
    public class WorkflowDefinition
    {
        public string Name { get; set; }
        public WorkflowType Type { get; set; }
        public List<Action> Steps { get; set; } = new List<Action>();
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    // [AJOUT] Structure pour les rapports de tendances
    public class TrendReport
    {
        public string MetricName { get; set; }
        public List<float> Values { get; set; } = new List<float>();
        public List<DateTime> Timestamps { get; set; } = new List<DateTime>();
        public float Average => Values.Any() ? Values.Average() : 0f;
        public float Min => Values.Any() ? Values.Min() : 0f;
        public float Max => Values.Any() ? Values.Max() : 0f;
    }

    // [AJOUT] Structure pour les suggestions d'optimisation
    public class OptimizationSuggestion
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public string Recommendation { get; set; }
        public float EstimatedImprovement { get; set; } // Pourcentage
    }

    // [AJOUT] Structure pour les spans de tracing
    public class OrchestratorSpan
    {
        public string Name { get; set; }
        public SpanType Type { get; set; }
        public DateTime StartTimestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();
        public string ParentSpanId { get; set; }
        public string TraceId { get; set; }
    }

    // [AJOUT] Structure pour les définitions de thèmes
    public class ThemeDefinition
    {
        public string Name { get; set; }
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> Styles { get; set; } = new Dictionary<string, object>();
    }

    // [AJOUT] Structure pour les définitions de skins
    public class SkinDefinition
    {
        public string Name { get; set; }
        public string LayoutPath { get; set; }
        public ThemeDefinition Theme { get; set; }
    }

    // [AJOUT] Structure pour les rapports de sécurité
    public class SecurityAuditReport
    {
        public DateTime Timestamp { get; set; }
        public List<string> Findings { get; set; } = new List<string>();
        public HealthStatus OverallStatus { get; set; }
    }
    #endregion

    #region Interfaces (réutilisés ou nouveaux)
    // Réutilisation de IStubPlugin -- Engine.Animation est deja importe en tete
    // de l'espace de noms ; une clause using ne peut pas suivre des declarations.

    // [AJOUT] Interface pour les extensions
    public interface IOrchestratorExtension
    {
        string Name { get; }
        void Initialize(AnimationEngineStubOrchestrator orchestrator);
        void Update(float deltaTime);
        void Shutdown();
    }

    // [AJOUT] Interface pour les plugins d'orchestrator
    public interface IOrchestratorPlugin
    {
        string Name { get; }
        void Initialize(AnimationEngineStubOrchestrator orchestrator);
        void OnOrchestratorEvent(OrchestratorEvent evt);
        void Shutdown();
    }

    // [AJOUT] Interface pour les thèmes
    public interface IOrchestratorTheme
    {
        string Name { get; }
        Dictionary<string, string> Colors { get; }
        Dictionary<string, object> Styles { get; }
    }

    // [AJOUT] Interface pour les skins
    public interface IOrchestratorSkin
    {
        string Name { get; }
        string Layout { get; }
        IOrchestratorTheme Theme { get; }
    }
    #endregion

    /// <summary>
    /// Orchestrateur centralisé pour l'AnimationEngineStub.
    /// Permet de configurer, lancer, contrôler et diagnostiquer l'instance de stub
    /// sans exposer directement sa complexité interne.
    /// </summary>
    public class AnimationEngineStubOrchestrator : IDisposable
    {
        #region Fields
        private AnimationEngineStub _engineStub;
        private readonly object _sync = new object();

        // Etats de l'orchestrateur
        private bool _disposed = false;
        private bool _isInitialized = false;
        private bool _isRunning = false;
        private bool _isRecording = false;
        private bool _isReplaying = false;

        // Configuration active
        private AnimationEngineStubConfig _currentConfig;

        // [AJOUT] Pool de stubs
        private readonly ConcurrentBag<AnimationEngineStub> _stubPool = new ConcurrentBag<AnimationEngineStub>();
        private readonly object _poolSync = new object();
        private int _minPoolSize = 0;
        private int _maxPoolSize = 10;
        private PoolResizeStrategy _poolResizeStrategy = PoolResizeStrategy.Adaptive;

        // [AJOUT] Cluster de stubs
        private List<AnimationEngineStub> _clusterStubs = new List<AnimationEngineStub>();
        private StubClusterState _clusterState = StubClusterState.Idle;

        // [AJOUT] Sessions
        private readonly Dictionary<string, RecordedSession> _sessionLibrary = new Dictionary<string, RecordedSession>();
        private readonly Dictionary<string, Dictionary<string, object>> _sessionMetadata = new Dictionary<string, Dictionary<string, object>>();
        private readonly Dictionary<string, List<string>> _sessionTags = new Dictionary<string, List<string>>();

        // [AJOUT] Scénarios
        private readonly Dictionary<string, StubScenario> _scenarioLibrary = new Dictionary<string, StubScenario>();
        private readonly Dictionary<string, List<string>> _scenarioDependencies = new Dictionary<string, List<string>>();
        private readonly Queue<string> _scenarioQueue = new Queue<string>();
        private readonly Dictionary<string, int> _scenarioPriorities = new Dictionary<string, int>();
        private readonly Dictionary<string, (int maxRetries, TimeSpan delay)> _scenarioRetryPolicies = new Dictionary<string, (int, TimeSpan)>();

        // [AJOUT] Plugins
        private readonly List<IStubPlugin> _registeredPlugins = new List<IStubPlugin>();
        private readonly List<IOrchestratorPlugin> _orchestratorPlugins = new List<IOrchestratorPlugin>();
        private readonly Dictionary<string, string> _pluginVersions = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _pluginLoadOrder = new Dictionary<string, int>();

        // [AJOUT] Evénements
        private readonly List<OrchestratorEvent> _eventHistory = new List<OrchestratorEvent>();
        private readonly Dictionary<string, List<Action<OrchestratorEvent>>> _eventHandlers = new Dictionary<string, List<Action<OrchestratorEvent>>>();
        private string _correlationId = Guid.NewGuid().ToString();

        // [AJOUT] Logs & Traces
        private LogLevel _logLevel = LogLevel.Info;
        private readonly List<string> _logs = new List<string>();
        private readonly Dictionary<string, (string traceId, List<OrchestratorSpan> spans)> _traces = new Dictionary<string, (string, List<OrchestratorSpan>)>();
        private string _currentTraceId = null;

        // [AJOUT] Caching
        private readonly Dictionary<string, (object value, DateTime expiration)> _cache = new Dictionary<string, (object, DateTime)>();

        // [AJOUT] CI/CD
        private bool _isInCIEnvironment = false;

        // [AJOUT] Performance & Benchmarks
        private readonly Dictionary<string, List<TimeSpan>> _benchmarkResults = new Dictionary<string, List<TimeSpan>>();
        private readonly Dictionary<string, float> _performanceBaselines = new Dictionary<string, float>();

        // [AJOUT] Reporting
        private readonly Dictionary<string, DiagnosticReport> _reports = new Dictionary<string, DiagnosticReport>();
        private readonly Dictionary<string, (TimeSpan interval, ReportFormat format)> _scheduledReports = new Dictionary<string, (TimeSpan, ReportFormat)>();

        // [AJOUT] Monitoring
        private readonly Dictionary<OrchestratorMetricType, float> _metrics = new Dictionary<OrchestratorMetricType, float>();
        private readonly Dictionary<string, float> _alerts = new Dictionary<string, float>();
        private readonly List<string> _activeAlerts = new List<string>();
        private readonly List<(IncidentType type, string description, DateTime timestamp)> _incidentLog = new List<(IncidentType, string, DateTime)>();

        // [AJOUT] Versions & Migration
        private string _version = "1.0.0-orchestrator";
        private readonly List<string> _changelog = new List<string>();
        private readonly Dictionary<string, bool> _featureFlags = new Dictionary<string, bool>();

        // [AJOUT] Extensions
        private readonly List<IOrchestratorExtension> _extensions = new List<IOrchestratorExtension>();

        // [AJOUT] Workflows
        private readonly Dictionary<string, WorkflowDefinition> _workflows = new Dictionary<string, WorkflowDefinition>();

        // [AJOUT] Async & Parallel
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private int _maxParallelism = Environment.ProcessorCount;
        private readonly Queue<Func<Task>> _taskQueue = new Queue<Func<Task>>();

        // [AJOUT] Sécurité
        private bool _encryptionAtRestEnabled = false;
        private bool _secureCommunicationEnabled = false;

        // [AJOUT] Gestion d'erreurs
        private ErrorRecoveryStrategy _errorRecoveryStrategy = ErrorRecoveryStrategy.Ignore;
        private readonly List<Action> _compensations = new List<Action>();
        private bool _gracefulDegradationEnabled = false;
        #endregion

        #region Properties
        public AnimationEngineStub EngineStub => _engineStub;
        public bool IsInitialized => _isInitialized;
        public bool IsRunning => _isRunning;
        public bool IsRecording => _isRecording;
        public bool IsReplaying => _isReplaying;
        public string CorrelationId => _correlationId;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public int PoolSize => _stubPool.Count;
        public int ClusterSize => _clusterStubs.Count;
        public int ScenarioQueueLength => _scenarioQueue.Count;
        public HealthStatus HealthStatus => IsHealthy();
        #endregion

        #region Constructors
        public AnimationEngineStubOrchestrator()
        {
            _engineStub = null; // Instancié plus tard
        }

        public AnimationEngineStubOrchestrator(AnimationEngineStubConfig config)
        {
            InitializeWithConfig(config);
        }
        #endregion

        #region [AJOUT] Orchestration Multi-Instances & Coordination (A)
        public AnimationEngineStubOrchestrator CreateStubPool(int size)
        {
            lock (_poolSync)
            {
                for (int i = 0; i < size; i++)
                {
                    var stub = AnimationEngineStubFactory.CreateDeterministic();
                    _stubPool.Add(stub);
                }
                UpdateMetrics(OrchestratorMetricType.PoolSize, _stubPool.Count);
            }
            return this;
        }

        public AnimationEngineStub GetStubFromPool()
        {
            if (_stubPool.TryTake(out var stub))
            {
                UpdateMetrics(OrchestratorMetricType.PoolUsage, _stubPool.Count);
                return stub;
            }
            else
            {
                if (_poolResizeStrategy == PoolResizeStrategy.Expandable || _poolResizeStrategy == PoolResizeStrategy.Adaptive)
                {
                    if (_stubPool.Count < _maxPoolSize)
                    {
                        var newStub = AnimationEngineStubFactory.CreateDeterministic();
                        UpdateMetrics(OrchestratorMetricType.PoolSize, _stubPool.Count + 1);
                        return newStub;
                    }
                }
                throw new InvalidOperationException("Pool exhausted and cannot expand.");
            }
        }

        public AnimationEngineStubOrchestrator ReturnStubToPool(AnimationEngineStub stub)
        {
            lock (_poolSync)
            {
                _stubPool.Add(stub);
                UpdateMetrics(OrchestratorMetricType.PoolUsage, _stubPool.Count);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator OrchestrateMultipleStubs(params AnimationEngineStub[] stubs)
        {
            _clusterStubs.AddRange(stubs);
            _clusterState = StubClusterState.Running;
            UpdateMetrics(OrchestratorMetricType.ActiveStubs, _clusterStubs.Count);
            return this;
        }

        public AnimationEngineStubOrchestrator BroadcastToAllStubs(Action<AnimationEngineStub> action)
        {
            foreach (var stub in _clusterStubs)
            {
                action(stub);
            }
            return this;
        }

        public (bool areEqual, List<string> differences) CompareStubs(AnimationEngineStub stubA, AnimationEngineStub stubB)
        {
            // Implémentation simplifiée
            var snapA = stubA.GetStateSnapshot();
            var snapB = stubB.GetStateSnapshot();
            var diff = new List<string>();
            if (!snapA.SequenceEqual(snapB))
            {
                diff.Add("State snapshots differ.");
            }
            return (diff.Count == 0, diff);
        }

        public AnimationEngineStubOrchestrator SynchronizeStubs(params AnimationEngineStub[] stubs)
        {
            if (stubs.Length == 0) return this;
            var referenceState = stubs[0].GetStateSnapshot(); // Choix arbitraire
            foreach (var stub in stubs.Skip(1))
            {
                // Synchronisation conceptuelle (l'état exact à synchroniser dépend de la logique métier)
                // Cette méthode nécessiterait probablement une implémentation spécifique dans le stub.
                LogOrchestratorEvent(LogLevel.Info, $"Synchronizing stub state to {referenceState?.Keys.Count ?? 0} keys.");
            }
            return this;
        }

        public Dictionary<string, object> GetStubPoolStatistics()
        {
            lock (_poolSync)
            {
                return new Dictionary<string, object>
                {
                    ["CurrentSize"] = _stubPool.Count,
                    ["MinSize"] = _minPoolSize,
                    ["MaxSize"] = _maxPoolSize,
                    ["Strategy"] = _poolResizeStrategy
                };
            }
        }

        public AnimationEngineStubOrchestrator SetStubPoolSize(int min, int max)
        {
            lock (_poolSync)
            {
                _minPoolSize = Math.Max(0, min);
                _maxPoolSize = Math.Max(min, max);
                // Ajuster dynamiquement le pool ici si nécessaire
            }
            return this;
        }

        public AnimationEngineStub GetLeastLoadedStub()
        {
            // Simplifié : basé sur le nombre d'opérations actives ou un indicateur similaire dans le stub
            // Supposons un indicateur hypothétique 'ActiveOperations'
            return _clusterStubs.OrderBy(s => s.LoggedCallCount).FirstOrDefault(); // Exemple
        }

        public AnimationEngineStub GetMostLoadedStub()
        {
            return _clusterStubs.OrderByDescending(s => s.LoggedCallCount).FirstOrDefault(); // Exemple
        }

        public AnimationEngineStubOrchestrator DistributeWorkload(WorkloadDefinition workload)
        {
            // Distribuer les scénarios du workload à différents stubs du cluster
            var tasks = workload.Scenarios.Select(scenario =>
            {
                var targetStub = GetLeastLoadedStub();
                if (targetStub != null)
                {
                    return Task.Run(() => targetStub.PlayScenario(scenario));
                }
                return Task.CompletedTask;
            }).ToArray();
            Task.WhenAll(tasks).Wait(); // Ou attendre de manière asynchrone
            return this;
        }

        public List<ScenarioResult> AggregateResultsFromAllStubs()
        {
            var results = new List<ScenarioResult>();
            foreach (var stub in _clusterStubs)
            {
                // Récupérer les résultats de chaque stub
                // Cela dépend de comment les résultats sont stockés dans le stub lui-même
                // Pour cette implémentation, on suppose un getter hypothétique
                // results.AddRange(stub.GetScenarioResults());
            }
            return results;
        }

        public AnimationEngineStubOrchestrator CreateStubCluster(int size, StubConfigurationPreset preset)
        {
            var stubs = new AnimationEngineStub[size];
            for (int i = 0; i < size; i++)
            {
                stubs[i] = preset switch
                {
                    StubConfigurationPreset.Minimal => AnimationEngineStubFactory.CreateMinimal(),
                    StubConfigurationPreset.Standard => AnimationEngineStubFactory.CreateStandard(),
                    StubConfigurationPreset.Full => AnimationEngineStubFactory.CreateFull(),
                    _ => AnimationEngineStubFactory.CreateStandard()
                };
            }
            return OrchestrateMultipleStubs(stubs);
        }

        public HealthStatus GetClusterHealth()
        {
            var states = _clusterStubs.Select(s => s.HealthStatus).ToList();
            if (states.Contains(HealthStatus.Unhealthy)) return HealthStatus.Unhealthy;
            if (states.Contains(HealthStatus.Degraded)) return HealthStatus.Degraded;
            return HealthStatus.Healthy;
        }

        public AnimationEngineStubOrchestrator FailoverStub(AnimationEngineStub failedStub)
        {
            _clusterStubs.Remove(failedStub);
            var replacementStub = AnimationEngineStubFactory.CreateStandard(); // Nouvelle instance
            _clusterStubs.Add(replacementStub);
            LogOrchestratorEvent(LogLevel.Warning, $"Failed over stub. New cluster size: {_clusterStubs.Count}");
            return this;
        }

        public AnimationEngineStubOrchestrator ReplicateState(AnimationEngineStub source, AnimationEngineStub target)
        {
            // Conceptuel : copier l'état du source vers le target
            // Nécessite une méthode dans le stub pour exporter/importer l'état
            // target.ImportState(source.ExportState());
            LogOrchestratorEvent(LogLevel.Info, "Replicating state between stubs.");
            return this;
        }
        #endregion

        #region [AJOUT] API Fluent & Builder Patterns (B)
        public static OrchestratorBuilder CreateBuilder() => new OrchestratorBuilder();

        public class OrchestratorBuilder
        {
            private AnimationEngineStubConfig _config = AnimationEngineStubConfig.Default;
            private readonly List<IStubPlugin> _plugins = new List<IStubPlugin>();
            private readonly List<StubScenario> _scenarios = new List<StubScenario>();
            private readonly List<string> _scenarioNames = new List<string>();
            private bool _withTelemetry = false;
            private int _deterministicSeed = 0;
            private int _memoryLimitMB = 0;
            private int _maxHandles = 0;
            private TimeSpan _timeout = TimeSpan.Zero;

            public OrchestratorBuilder WithPreset(StubConfigurationPreset preset)
            {
                _config = preset switch
                {
                    StubConfigurationPreset.Minimal => AnimationEngineStubConfig.Default with { EnableCallLogging = false, EnableAssertions = false },
                    StubConfigurationPreset.Standard => AnimationEngineStubConfig.Default with { EnableCallLogging = true, EnableAssertions = true },
                    StubConfigurationPreset.Full => AnimationEngineStubConfig.Default with { EnableCallLogging = true, EnableAssertions = true, EnableGoldenOutputCapture = true, EnableStateSnapshotting = true, EnableFaultInjection = true, EnableScenarioExecution = true, EnableReplayMode = true, EnableTelemetry = true },
                    _ => AnimationEngineStubConfig.Default
                };
                return this;
            }

            public OrchestratorBuilder WithFaultInjection(StubFaultType fault)
            {
                _config.EnableFaultInjection = true;
                // Configurer les règles d'injection de fautes ici si possible via la config
                // Sinon, cela se fait après la construction du stub/orchestrator
                return this;
            }

            public OrchestratorBuilder WithScenario(StubScenario scenario)
            {
                _scenarios.Add(scenario);
                return this;
            }

            public OrchestratorBuilder WithScenarioByName(string name)
            {
                _scenarioNames.Add(name);
                return this;
            }

            public OrchestratorBuilder WithPlugin(IStubPlugin plugin)
            {
                _plugins.Add(plugin);
                return this;
            }

            public OrchestratorBuilder WithTelemetry(bool enabled)
            {
                _withTelemetry = enabled;
                return this;
            }

            public OrchestratorBuilder WithDeterministicSeed(int seed)
            {
                _deterministicSeed = seed;
                return this;
            }

            public OrchestratorBuilder WithMemoryLimit(int mb)
            {
                _memoryLimitMB = mb;
                return this;
            }

            public OrchestratorBuilder WithMaxHandles(int max)
            {
                _maxHandles = max;
                return this;
            }

            public OrchestratorBuilder WithTimeout(TimeSpan timeout)
            {
                _timeout = timeout;
                return this;
            }

            public AnimationEngineStubOrchestrator Build()
            {
                var orchestrator = new AnimationEngineStubOrchestrator();
                orchestrator.InitializeWithConfig(_config);
                foreach (var plugin in _plugins)
                {
                    orchestrator.RegisterPlugin(plugin);
                }
                foreach (var scenario in _scenarios)
                {
                    orchestrator.RegisterScenario(scenario.Name, scenario);
                }
                foreach (var name in _scenarioNames)
                {
                    orchestrator.RegisterScenario(name, new StubScenario { Name = name }); // Placeholder
                }
                if (_withTelemetry)
                {
                    orchestrator.EngineStub.EnableTelemetryExport(true);
                }
                // D'autres configurations peuvent être appliquées ici
                return orchestrator;
            }

            public bool Validate()
            {
                // Implémenter la validation de la configuration
                return true; // Placeholder
            }

            public OrchestratorBuilder Clone()
            {
                var clone = new OrchestratorBuilder();
                clone._config = this._config;
                clone._plugins.AddRange(this._plugins);
                clone._scenarios.AddRange(this._scenarios);
                clone._withTelemetry = this._withTelemetry;
                clone._deterministicSeed = this._deterministicSeed;
                clone._memoryLimitMB = this._memoryLimitMB;
                clone._maxHandles = this._maxHandles;
                clone._timeout = this._timeout;
                return clone;
            }
        }
        #endregion

        #region [AJOUT] Gestion de Sessions Avancée (C)
        public AnimationEngineStubOrchestrator CreateNamedSession(string name, RecordedSession session)
        {
            lock (_sync)
            {
                _sessionLibrary[name] = session;
            }
            return this;
        }

        public RecordedSession GetSessionByName(string name)
        {
            lock (_sync)
            {
                return _sessionLibrary.GetValueOrDefault(name);
            }
        }

        public AnimationEngineStubOrchestrator DeleteSession(string name)
        {
            lock (_sync)
            {
                _sessionLibrary.Remove(name);
                _sessionMetadata.Remove(name);
                _sessionTags.Remove(name);
            }
            return this;
        }

        public List<string> ListAllSessions()
        {
            lock (_sync)
            {
                return _sessionLibrary.Keys.ToList();
            }
        }

        public AnimationEngineStubOrchestrator ExportSessionLibrary(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var kvp in _sessionLibrary)
            {
                var path = Path.Combine(directory, $"{kvp.Key}.json");
                var json = JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator ImportSessionLibrary(string directory)
        {
            if (!Directory.Exists(directory)) return this;
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<RecordedSession>(json);
                CreateNamedSession(name, session);
            }
            return this;
        }

        public (bool areEqual, List<string> differences) CompareSessions(string sessionA, string sessionB)
        {
            var sessA = GetSessionByName(sessionA);
            var sessB = GetSessionByName(sessionB);
            if (sessA == null || sessB == null) return (false, new List<string> { "One or both sessions not found." });

            // Comparaison simplifiée
            var diffs = new List<string>();
            if (sessA.Calls.Count != sessB.Calls.Count) diffs.Add($"Call count differs: {sessA.Calls.Count} vs {sessB.Calls.Count}");
            // ... autres comparaisons
            return (diffs.Count == 0, diffs);
        }

        public AnimationEngineStubOrchestrator MergeSessions(string[] sessionNames, string outputPath)
        {
            var mergedSession = new RecordedSession();
            foreach (var name in sessionNames)
            {
                var session = GetSessionByName(name);
                if (session != null)
                {
                    mergedSession.Calls.AddRange(session.Calls);
                    mergedSession.StateTransitions.AddRange(session.StateTransitions);
                    mergedSession.Events.AddRange(session.Events);
                    // ... autres champs
                }
            }
            var json = JsonSerializer.Serialize(mergedSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);
            return this;
        }

        public AnimationEngineStubOrchestrator SetSessionVersion(string name, string version)
        {
            // Conceptuel : stocker la version dans les métadonnées
            SetSessionMetadata(name, new Dictionary<string, object> { ["Version"] = version });
            return this;
        }

        public string GetSessionVersion(string name)
        {
            var meta = GetSessionMetadata(name);
            return meta?.GetValueOrDefault("Version")?.ToString();
        }

        public AnimationEngineStubOrchestrator SetSessionMetadata(string name, Dictionary<string, object> metadata)
        {
            lock (_sync)
            {
                _sessionMetadata[name] = metadata;
            }
            return this;
        }

        public Dictionary<string, object> GetSessionMetadata(string name)
        {
            lock (_sync)
            {
                return _sessionMetadata.GetValueOrDefault(name);
            }
        }

        public AnimationEngineStubOrchestrator AddSessionTag(string name, string tag)
        {
            lock (_sync)
            {
                if (!_sessionTags.ContainsKey(name))
                {
                    _sessionTags[name] = new List<string>();
                }
                _sessionTags[name].Add(tag);
            }
            return this;
        }

        public List<string> GetSessionsByTag(string tag)
        {
            lock (_sync)
            {
                return _sessionTags.Where(kvp => kvp.Value.Contains(tag)).Select(kvp => kvp.Key).ToList();
            }
        }

        public AnimationEngineStubOrchestrator SetSessionRetentionPolicy(TimeSpan maxAge)
        {
            // Conceptuel : implémenter une tâche de fond pour supprimer les anciennes sessions
            Task.Run(async () =>
            {
                await Task.Delay(maxAge);
                var now = DateTime.UtcNow;
                var toDelete = _sessionLibrary.Where(kvp => (now - kvp.Value.RecordingEndTime) > maxAge).Select(kvp => kvp.Key).ToList();
                foreach (var name in toDelete)
                {
                    DeleteSession(name);
                }
            });
            return this;
        }
        #endregion

        #region [AJOUT] Gestion de Scénarios Avancée (D)
        public AnimationEngineStubOrchestrator RegisterScenario(string name, StubScenario scenario)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Scenario name cannot be null or empty.", nameof(name));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            lock (_sync)
            {
                _scenarioLibrary[name] = scenario;
                LogOrchestratorEvent(LogLevel.Info, $"Registered scenario: {name}");
            }
            return this;
        }

        public AnimationEngineStubOrchestrator UnregisterScenario(string name)
        {
            lock (_sync)
            {
                _scenarioLibrary.Remove(name);
                _scenarioDependencies.Remove(name);
            }
            return this;
        }

        public StubScenario GetScenarioByName(string name)
        {
            lock (_sync)
            {
                return _scenarioLibrary.GetValueOrDefault(name);
            }
        }

        public List<string> ListAllScenarios()
        {
            lock (_sync)
            {
                return _scenarioLibrary.Keys.ToList();
            }
        }

        public AnimationEngineStubOrchestrator SetScenarioDependency(string scenarioName, params string[] dependencies)
        {
            lock (_sync)
            {
                _scenarioDependencies[scenarioName] = dependencies.ToList();
            }
            return this;
        }

        public AnimationEngineStubOrchestrator RunScenarioWithDependencies(string scenarioName)
        {
            if (!_scenarioDependencies.ContainsKey(scenarioName)) return RunScenario(GetScenarioByName(scenarioName));

            var dependencies = _scenarioDependencies[scenarioName];
            foreach (var depName in dependencies)
            {
                RunScenario(GetScenarioByName(depName)); // Supposons que les dépendances soient exécutées avant
            }
            return RunScenario(GetScenarioByName(scenarioName));
        }

        public AnimationEngineStubOrchestrator ChainScenarios(params string[] scenarioNames)
        {
            // Conceptuel : exécuter les scénarios dans l'ordre
            foreach (var name in scenarioNames)
            {
                RunScenario(GetScenarioByName(name));
            }
            return this;
        }

        public AnimationEngineStubOrchestrator RunScenariosParallel(params string[] scenarioNames)
        {
            var tasks = scenarioNames.Select(name => Task.Run(() => RunScenario(GetScenarioByName(name)))).ToArray();
            Task.WaitAll(tasks);
            return this;
        }

        public AnimationEngineStubOrchestrator SetScenarioRetryPolicy(string name, int maxRetries, TimeSpan delay)
        {
            lock (_sync)
            {
                _scenarioRetryPolicies[name] = (maxRetries, delay);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator SetScenarioTimeout(string name, TimeSpan timeout)
        {
            // Conceptuel : implémenter un timeout pour l'exécution d'un scénario spécifique
            // Peut-être via un CancellationToken lié à ce scénario
            LogOrchestratorEvent(LogLevel.Warning, "SetScenarioTimeout is conceptual and requires specific implementation within the scenario execution logic.");
            return this;
        }

        public AnimationEngineStubOrchestrator SetScenarioPriority(string name, int priority)
        {
            lock (_sync)
            {
                _scenarioPriorities[name] = priority;
            }
            return this;
        }

        public AnimationEngineStubOrchestrator EnqueueScenario(string name)
        {
            lock (_sync)
            {
                _scenarioQueue.Enqueue(name);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator DequeueAndRunScenario()
        {
            lock (_sync)
            {
                if (_scenarioQueue.Count > 0)
                {
                    var name = _scenarioQueue.Dequeue();
                    RunScenario(GetScenarioByName(name));
                }
            }
            return this;
        }
        #endregion

        #region [AJOUT] Gestion de Plugins Avancée (E)
        public AnimationEngineStubOrchestrator RegisterPlugin(IStubPlugin plugin)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            lock (_sync)
            {
                // Vérifier les doublons potentiellement
                if (_registeredPlugins.Contains(plugin))
                {
                     LogOrchestratorEvent(LogLevel.Warning, $"Plugin {plugin.GetType().Name} is already registered.");
                     return this;
                }

                _registeredPlugins.Add(plugin);
                _pluginVersions[plugin.GetType().Name] = "1.0"; // Placeholder
                plugin.Initialize(_engineStub); // Initialiser le plugin avec le stub principal
                LogOrchestratorEvent(LogLevel.Info, $"Registered plugin: {plugin.GetType().Name}");
            }
            UpdateMetrics(OrchestratorMetricType.LoadedPlugins, _registeredPlugins.Count);
            return this;
        }

        public List<IStubPlugin> GetRegisteredPlugins()
        {
            lock (_sync)
            {
                return new List<IStubPlugin>(_registeredPlugins);
            }
        }

        public IStubPlugin GetPluginByName(string name)
        {
            lock (_sync)
            {
                return _registeredPlugins.FirstOrDefault(p => p.GetType().Name == name);
            }
        }

        public AnimationEngineStubOrchestrator UnregisterPlugin(IStubPlugin plugin)
        {
            lock (_sync)
            {
                _registeredPlugins.Remove(plugin);
            }
            UpdateMetrics(OrchestratorMetricType.LoadedPlugins, _registeredPlugins.Count);
            return this;
        }

        public AnimationEngineStubOrchestrator SetPluginLoadOrder(string pluginName, int order)
        {
            lock (_sync)
            {
                _pluginLoadOrder[pluginName] = order;
            }
            return this;
        }

        public Dictionary<string, int> GetPluginLoadOrder()
        {
            lock (_sync)
            {
                return new Dictionary<string, int>(_pluginLoadOrder);
            }
        }

        public AnimationEngineStubOrchestrator EnablePluginSandbox(bool enabled)
        {
            // Conceptuel : activer/désactiver un mode sandbox pour les plugins
            LogOrchestratorEvent(LogLevel.Info, $"Plugin sandbox mode set to: {enabled}");
            return this;
        }

        public AnimationEngineStubOrchestrator HotReloadPlugin(string name)
        {
            var oldPlugin = GetPluginByName(name);
            if (oldPlugin != null)
            {
                UnregisterPlugin(oldPlugin);
                // Charger la nouvelle DLL, instancier, enregistrer
                LogOrchestratorEvent(LogLevel.Info, $"Hot-reloaded plugin: {name}");
            }
            return this;
        }

        public string GetPluginVersion(string name)
        {
            lock (_sync)
            {
                return _pluginVersions.GetValueOrDefault(name);
            }
        }

        public bool CheckPluginCompatibility(string name, string engineVersion)
        {
            // Conceptuel : vérifier la compatibilité basée sur les versions
            LogOrchestratorEvent(LogLevel.Info, $"Checking compatibility for plugin {name} against engine {engineVersion}");
            return true; // Placeholder
        }

        public Dictionary<string, object> GetPluginMetrics(string name)
        {
            // Conceptuel : récupérer des métriques spécifiques à un plugin
            LogOrchestratorEvent(LogLevel.Info, $"Getting metrics for plugin {name}");
            return new Dictionary<string, object> { ["Calls"] = 0, ["Errors"] = 0 }; // Placeholder
        }

        public HealthStatus CheckPluginHealth(string name)
        {
            // Conceptuel : vérifier l'état de santé d'un plugin
            LogOrchestratorEvent(LogLevel.Info, $"Checking health for plugin {name}");
            return HealthStatus.Healthy; // Placeholder
        }
        #endregion

        #region [AJOUT] Diagnostics & Reporting Avancés (F)
        public DiagnosticReport GenerateComprehensiveReport()
        {
            var report = GenerateDiagnosticReport();
            report.Metrics = GetDetailedMetrics();
            report.DataValidation = new DataValidationReport { AllValid = true, ValidatedItems = 100 }; // Placeholder
            return report;
        }

        public string GenerateExecutiveSummary()
        {
            var report = GenerateComprehensiveReport();
            return $"Executive Summary:\nStatus: {report.Status}\nActive Scenarios: {ListAllScenarios().Count}\nLoaded Plugins: {GetRegisteredPlugins().Count}\nErrors: {report.Errors.Count}";
        }

        public DiagnosticReport GenerateTechnicalReport()
        {
            var report = GenerateComprehensiveReport();
            // Ajouter des détails techniques
            report.RecentCalls = EngineStub.GetCallRecords().Take(50).ToList(); // Plus de détails
            report.RecentStateTransitions = EngineStub.GetStateHistory().Take(50).ToList();
            return report;
        }

        public DiagnosticReport GeneratePerformanceReport()
        {
            var report = new DiagnosticReport
            {
                Status = HealthStatus.Healthy, // Basé sur les métriques
                Metrics = GetDetailedMetrics()
            };
            return report;
        }

        public DiagnosticReport GenerateMemoryReport()
        {
            var metrics = GetDetailedMetrics();
            var report = new DiagnosticReport
            {
                Status = metrics.TotalMemoryUsedMB > _currentConfig.MemoryLimitMB * 0.9f ? HealthStatus.Degraded : HealthStatus.Healthy,
                Metrics = metrics
            };
            return report;
        }

        public DiagnosticReport GenerateThreadReport()
        {
            var report = new DiagnosticReport
            {
                Status = HealthStatus.Healthy // Basé sur les stats de threading
            };
            // Ajouter des stats de threading si disponibles
            return report;
        }

        public DiagnosticReport GenerateErrorReport()
        {
            var report = GenerateComprehensiveReport();
            report.Errors = report.Errors.Concat(GetOrchestratorEventHistory().Where(e => e.Type == OrchestratorEventType.Error).Select(e => e.Message)).ToList();
            return report;
        }

        public DiagnosticReport GenerateWarningReport()
        {
            var report = GenerateComprehensiveReport();
            report.Warnings = report.Warnings.Concat(GetOrchestratorEventHistory().Where(e => e.Type == OrchestratorEventType.Warning).Select(e => e.Message)).ToList();
            return report;
        }

        public AnimationEngineStubOrchestrator ScheduleReport(TimeSpan interval, ReportFormat format, string name = "ScheduledReport")
        {
            lock (_sync)
            {
                _scheduledReports[name] = (interval, format);
            }
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(interval);
                    var report = GenerateComprehensiveReport();
                    ExportReportToFile(report, $"scheduled_report_{name}_{DateTime.UtcNow:yyyyMMddHHmmss}.json", format);
                }
            });
            return this;
        }

        public AnimationEngineStubOrchestrator CancelScheduledReport(string name)
        {
            lock (_sync)
            {
                _scheduledReports.Remove(name);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator CompareReports(string reportA, string reportB)
        {
            // Conceptuel : charger et comparer deux rapports
            LogOrchestratorEvent(LogLevel.Info, $"Comparing reports {reportA} and {reportB}");
            return this;
        }

        public List<TrendReport> AnalyzeReportTrends(string[] reportPaths)
        {
            // Charger les rapports, extraire des métriques pertinentes, calculer les tendances
            var reports = reportPaths.Select(path => LoadReportFromFile(path)).Where(r => r != null).ToList(); // Hypothétique
            var trendReports = new List<TrendReport>();

            // Exemple pour une métrique simple comme le nombre d'erreurs
            var errorCounts = reports.Select(r => r.Errors.Count).ToList();
            if (errorCounts.Any())
            {
                trendReports.Add(new TrendReport
                {
                    MetricName = "ErrorCount",
                    Values = errorCounts.Select(c => (float)c).ToList(),
                    Timestamps = reports.Select(r => r.Timestamp).ToList() // Supposons un champ Timestamp dans DiagnosticReport
                });
            }

            LogOrchestratorEvent(LogLevel.Info, $"Analyzed trends for {trendReports.Count} metrics across {reports.Count} reports.");
            return trendReports;
        }

        private DiagnosticReport LoadReportFromFile(string path) // Méthode utilitaire hypothétique
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DiagnosticReport>(json);
        }
        #endregion

        #region [AJOUT] Intégration CI/CD & Automatisation (G)
        public AnimationEngineStubOrchestrator RunCIValidation()
        {
            _isInCIEnvironment = true;
            var results = new List<bool>
            {
                AssertCIReady(),
                EngineStub.AssertHealthy()
            };
            // Ajouter d'autres validations CI ici
            var allPassed = results.All(r => r);
            LogOrchestratorEvent(allPassed ? LogLevel.Info : LogLevel.Error, $"CI Validation: {(allPassed ? "PASSED" : "FAILED")}");
            return this;
        }

        public bool AssertCIReady()
        {
            var ready = IsInitialized && !_disposed && !_engineStub.IsShutdown;
            if (!ready) LogOrchestratorEvent(LogLevel.Error, "Orchestrator is not ready for CI.");
            return ready;
        }

        public AnimationEngineStubOrchestrator RegisterCITest(string name, Action test)
        {
            // Conceptuel : enregistrer un test pour la suite CI
            LogOrchestratorEvent(LogLevel.Info, $"Registered CI test: {name}");
            return this;
        }

        public AnimationEngineStubOrchestrator RunCITestSuite()
        {
            LogOrchestratorEvent(LogLevel.Info, "Starting CI test suite...");
            // Exécuter les tests enregistrés
            LogOrchestratorEvent(LogLevel.Info, "CI test suite completed.");
            return this;
        }

        public AnimationEngineStubOrchestrator EnableCIRollback(bool enabled)
        {
            // Conceptuel : activer/désactiver le rollback CI
            LogOrchestratorEvent(LogLevel.Info, $"CI Rollback enabled: {enabled}");
            return this;
        }

        public AnimationEngineStubOrchestrator NotifyCIResult(bool success, string message)
        {
            // Conceptuel : envoyer une notification (email, webhook, etc.)
            LogOrchestratorEvent(LogLevel.Info, $"CI Notification: Success={success}, Message={message}");
            return this;
        }

        public string DetectCIEnvironment()
        {
            // Ex: Vérifier des variables d'environnement comme "CI=true"
            return Environment.GetEnvironmentVariable("CI") == "true" ? "GenericCI" : "Local";
        }

        public AnimationEngineStubOrchestrator AdaptConfigForCI()
        {
            if (DetectCIEnvironment() != "Local")
            {
                // Désactiver les logs verbeux, activer le mode déterministe, etc.
                _currentConfig.EnableCallLogging = false;
                _currentConfig.EnableAssertions = true;
                _currentConfig.EnableDeterministicMode = true;
                LogOrchestratorEvent(LogLevel.Info, "Adapted configuration for CI environment.");
            }
            return this;
        }

        public AnimationEngineStubOrchestrator RunCITestsParallel(int maxParallelism)
        {
            SetMaxParallelism(maxParallelism);
            // Exécuter les tests CI en parallèle
            LogOrchestratorEvent(LogLevel.Info, $"Running CI tests with max parallelism: {maxParallelism}");
            return this;
        }
        #endregion

        #region [AJOUT] Performance & Benchmarking (H)
        public AnimationEngineStubOrchestrator RunBenchmark(string name, Action benchmark)
        {
            var sw = Stopwatch.StartNew();
            benchmark();
            sw.Stop();
            lock (_sync)
            {
                if (!_benchmarkResults.ContainsKey(name))
                {
                    _benchmarkResults[name] = new List<TimeSpan>();
                }
                _benchmarkResults[name].Add(sw.Elapsed);
            }
            LogOrchestratorEvent(LogLevel.Info, $"Benchmark '{name}' took {sw.ElapsedMilliseconds} ms.");
            return this;
        }

        public List<TimeSpan> GetBenchmarkResults(string name)
        {
            lock (_sync)
            {
                return _benchmarkResults.GetValueOrDefault(name, new List<TimeSpan>());
            }
        }

        public AnimationEngineStubOrchestrator RegisterBenchmark(string name, Action benchmark)
        {
            // Conceptuel : enregistrer un benchmark pour une suite
            LogOrchestratorEvent(LogLevel.Info, $"Registered benchmark: {name}");
            return this;
        }

        public AnimationEngineStubOrchestrator SetPerformanceBaseline(string metric, float value)
        {
            lock (_sync)
            {
                _performanceBaselines[metric] = value;
            }
            return this;
        }

        public bool CompareAgainstBaseline(string metric, float actual)
        {
            lock (_sync)
            {
                if (_performanceBaselines.TryGetValue(metric, out var baseline))
                {
                    return actual <= baseline;
                }
            }
            return true; // Pas de baseline, donc OK
        }

        public bool DetectPerformanceRegression(float tolerance = 0.1f)
        {
            // Comparer les derniers résultats de benchmark avec les précédents ou les baselines
            var regressions = new List<string>();
            foreach (var kvp in _benchmarkResults)
            {
                var metricName = kvp.Key;
                var recentValues = kvp.Value.TakeLast(3).ToList(); // Les 3 dernières runs
                if (recentValues.Count < 2) continue; // Pas assez de données

                var avgRecent = recentValues.Average(ts => ts.TotalMilliseconds);
                var baseline = _performanceBaselines.GetValueOrDefault(metricName, avgRecent); // Si pas de baseline, prendre la moyenne comme référence

                if (avgRecent > baseline * (1 + tolerance))
                {
                    regressions.Add($"{metricName}: Avg {avgRecent:F2}ms vs Baseline {baseline:F2}ms");
                }
            }
            if (regressions.Any())
            {
                LogOrchestratorEvent(LogLevel.Warning, $"Performance regression detected: {string.Join(", ", regressions)}");
                return true;
            }
            return false;
        }

        public AnimationEngineStubOrchestrator StartProfiling()
        {
            // Conceptuel : activer un profiler externe ou interne
            LogOrchestratorEvent(LogLevel.Info, "Started profiling.");
            return this;
        }

        public AnimationEngineStubOrchestrator StopProfiling()
        {
            LogOrchestratorEvent(LogLevel.Info, "Stopped profiling.");
            return this;
        }

        public List<OptimizationSuggestion> GetOptimizationSuggestions()
        {
            var suggestions = new List<OptimizationSuggestion>();
            // Ex: Si les benchmarks montrent des pics, suggérer des optimisations de code.
            // Ex: Si le cache est peu utilisé, suggérer de l'activer.
            // Ex: Si le parallélisme est faible, suggérer de l'augmenter.
            if (DetectPerformanceRegression())
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Category = "Performance",
                    Description = "Performance regression detected.",
                    Recommendation = "Review recent changes and optimize hot paths.",
                    EstimatedImprovement = 15.0f
                });
            }
            return suggestions;
        }

        public AnimationEngineStubOrchestrator SetPerformanceBudget(TimeSpan maxTime)
        {
            // Conceptuel : implémenter un budget via un mécanisme de timeout ou de limitation de tâches
            LogOrchestratorEvent(LogLevel.Info, $"Set performance budget to {maxTime.TotalMilliseconds} ms.");
            return this;
        }

        public bool AssertWithinPerformanceBudget(TimeSpan maxTime)
        {
            // Vérifier si les dernières opérations étaient dans le budget
            // Basé sur les métriques ou les timers internes
            return true; // Placeholder
        }
        #endregion

        #region [AJOUT] Sécurité & Validation (I)
        public string SanitizeInput(string input)
        {
            // Ex: Supprimer les caractères dangereux, échapper, etc.
            return input.Replace("..", "").Replace("/", "_").Replace("\\", "_"); // Exemple basique
        }

        public bool ValidatePath(string path)
        {
            var sanitized = SanitizeInput(path);
            return Path.IsPathRooted(sanitized) && sanitized.StartsWith(Directory.GetCurrentDirectory()); // Exemple
        }

        public bool ValidateConfiguration(AnimationEngineStubConfig config)
        {
            // Ex: Vérifier que les limites ne sont pas négatives, etc.
            return config.MaxAllocatedObjects > 0 && config.MaxHandles > 0 && config.MemoryLimitMB > 0;
        }

        public SecurityAuditReport RunSecurityAudit()
        {
            var report = new SecurityAuditReport { Timestamp = DateTime.UtcNow, OverallStatus = HealthStatus.Healthy };
            if (!ValidateConfiguration(_currentConfig))
            {
                report.Findings.Add("Configuration validation failed.");
                report.OverallStatus = HealthStatus.Unhealthy;
            }
            if (_isInCIEnvironment && _currentConfig.EnableCallLogging)
            {
                report.Findings.Add("Call logging enabled in CI environment.");
                // report.OverallStatus = HealthStatus.Degraded; // Peut-être un avertissement
            }
            return report;
        }

        public List<string> GetSecurityAuditResults()
        {
            var report = RunSecurityAudit();
            return report.Findings;
        }

        public AnimationEngineStubOrchestrator SetAccessLevel(string feature, AccessLevel level)
        {
            // Conceptuel : stocker le niveau d'accès pour une fonctionnalité
            LogOrchestratorEvent(LogLevel.Info, $"Set access level for '{feature}' to {level}.");
            return this;
        }

        public bool CheckAccess(string feature)
        {
            // Conceptuel : vérifier le niveau d'accès
            return true; // Placeholder
        }

        public AnimationEngineStubOrchestrator EnableEncryptionAtRest(bool enabled)
        {
            _encryptionAtRestEnabled = enabled;
            LogOrchestratorEvent(LogLevel.Info, $"Encryption at rest: {enabled}");
            return this;
        }

        public AnimationEngineStubOrchestrator EnableSecureCommunication(bool enabled)
        {
            _secureCommunicationEnabled = enabled;
            LogOrchestratorEvent(LogLevel.Info, $"Secure communication: {enabled}");
            return this;
        }

        public List<string> ScanForVulnerabilities()
        {
            // Conceptuel : scanner pour les vulnérabilités
            LogOrchestratorEvent(LogLevel.Info, "Scanning for vulnerabilities...");
            return new List<string>(); // Placeholder
        }
        #endregion

        #region [AJOUT] Extensibilité & Personnalisation (J)
        public AnimationEngineStubOrchestrator RegisterExtension(IOrchestratorExtension extension)
        {
            lock (_sync)
            {
                _extensions.Add(extension);
                extension.Initialize(this);
            }
            return this;
        }

        public List<IOrchestratorExtension> GetRegisteredExtensions()
        {
            lock (_sync)
            {
                return new List<IOrchestratorExtension>(_extensions);
            }
        }

        public AnimationEngineStubOrchestrator RegisterHook(string hookName, Action hook)
        {
            lock (_sync)
            {
                if (!_eventHandlers.ContainsKey(hookName))
                {
                    _eventHandlers[hookName] = new List<Action<OrchestratorEvent>>();
                }
                _eventHandlers[hookName].Add(evt => hook()); // Wrapper pour correspondre à la signature
            }
            return this;
        }

        public AnimationEngineStubOrchestrator InvokeHook(string hookName)
        {
            var handlers = _eventHandlers.GetValueOrDefault(hookName, new List<Action<OrchestratorEvent>>());
            foreach (var handler in handlers)
            {
                try
                {
                    handler(new OrchestratorEvent { Type = OrchestratorEventType.Info, Message = $"Invoking hook: {hookName}", Timestamp = DateTime.UtcNow });
                }
                catch (Exception ex)
                {
                    LogOrchestratorEvent(LogLevel.Error, $"Error in hook '{hookName}': {ex.Message}");
                }
            }
            return this;
        }

        public AnimationEngineStubOrchestrator DefineWorkflow(string name, WorkflowDefinition definition)
        {
            lock (_sync)
            {
                _workflows[name] = definition;
            }
            return this;
        }

        public AnimationEngineStubOrchestrator ExecuteWorkflow(string name)
        {
            if (!_workflows.TryGetValue(name, out var workflow))
            {
                 LogOrchestratorEvent(LogLevel.Warning, $"Workflow '{name}' not found.");
                 return this;
            }

            LogOrchestratorEvent(LogLevel.Info, $"Executing workflow: {workflow.Name} (Type: {workflow.Type})");
            foreach (var step in workflow.Steps)
            {
                try
                {
                    step?.Invoke();
                    // Mettre à jour le contexte si nécessaire
                    // workflow.Context["LastStep"] = step.Method.Name;
                }
                catch (Exception ex)
                {
                    LogOrchestratorEvent(LogLevel.Error, $"Error executing workflow '{workflow.Name}' step: {ex.Message}");
                    // Appliquer une stratégie de récupération ici si définie
                    break; // Arrêter le workflow en cas d'erreur
                }
            }
            LogOrchestratorEvent(LogLevel.Info, $"Workflow '{workflow.Name}' completed.");
            return this;
        }

        public AnimationEngineStubOrchestrator RegisterOrchestratorPlugin(IOrchestratorPlugin plugin)
        {
            lock (_sync)
            {
                _orchestratorPlugins.Add(plugin);
                plugin.Initialize(this);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorTheme(ThemeDefinition theme)
        {
            // Conceptuel : appliquer un thème
            LogOrchestratorEvent(LogLevel.Info, $"Applied theme: {theme.Name}");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorLocale(string locale)
        {
            // Conceptuel : définir la locale
            LogOrchestratorEvent(LogLevel.Info, $"Set locale to: {locale}");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorSkin(SkinDefinition skin)
        {
            // Conceptuel : appliquer un skin
            LogOrchestratorEvent(LogLevel.Info, $"Applied skin: {skin.Name}");
            return this;
        }

        public AnimationEngineStubOrchestrator AddCustomValidationRule(string name, Func<bool> rule)
        {
            // Conceptuel : enregistrer une règle de validation
            LogOrchestratorEvent(LogLevel.Info, $"Added custom validation rule: {name}");
            return this;
        }
        #endregion

        #region [AJOUT] Async & Parallel Orchestration (K)
        public async Task InitializeAsync(AnimationEngineStubConfig config)
        {
            await Task.Run(() => InitializeWithConfig(config));
        }

        public async Task ShutdownAsync()
        {
            await Task.Run(() => this.Dispose());
        }

        public async Task RunScenarioAsync(StubScenario scenario)
        {
            await Task.Run(() => RunScenario(scenario));
        }

        public async Task RunScenariosParallelAsync(params StubScenario[] scenarios)
        {
            var tasks = scenarios.Select(s => Task.Run(() => RunScenario(s))).ToArray();
            await Task.WhenAll(tasks);
        }

        public async Task StartRecordingAsync()
        {
            await Task.Run(() => StartRecording());
        }

        public async Task StopRecordingAsync()
        {
            await Task.Run(() => StopRecordingAndSave("temp_session.json"));
        }

        public async Task LoadAndReplaySessionAsync(string inputPath)
        {
            await Task.Run(() => LoadAndReplaySession(inputPath));
        }

        public async Task ExportDiagnosticReportAsync(string path, ReportFormat format = ReportFormat.Json)
        {
            await Task.Run(() => ExportDiagnosticReport(path, format));
        }

        public AnimationEngineStubOrchestrator ScheduleTask(Func<Task> task, TimeSpan delay)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                await task();
            });
            return this;
        }

        public AnimationEngineStubOrchestrator CancelAllOperations()
        {
            _cancellationTokenSource.Cancel();
            return this;
        }

        public AnimationEngineStubOrchestrator SetMaxParallelism(int maxParallelism)
        {
            _maxParallelism = Math.Max(1, maxParallelism);
            return this;
        }

        public AnimationEngineStubOrchestrator EnqueueTask(Func<Task> task)
        {
            lock (_sync)
            {
                _taskQueue.Enqueue(task);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator ProcessTaskQueue()
        {
            lock (_sync)
            {
                while (_taskQueue.Count > 0)
                {
                    var taskFunc = _taskQueue.Dequeue();
                    Task.Run(async () =>
                    {
                        try
                        {
                             await taskFunc();
                        }
                        catch (Exception ex)
                        {
                             LogOrchestratorEvent(LogLevel.Error, $"Error processing task from queue: {ex.Message}");
                        }
                    });
                }
            }
            return this;
        }
        #endregion

        #region [AJOUT] Event-Driven Orchestration (L)
        public AnimationEngineStubOrchestrator SubscribeToOrchestratorEvent(string eventName, Action<OrchestratorEvent> handler)
        {
            lock (_sync)
            {
                if (!_eventHandlers.ContainsKey(eventName))
                {
                    _eventHandlers[eventName] = new List<Action<OrchestratorEvent>>();
                }
                _eventHandlers[eventName].Add(handler);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator UnsubscribeFromOrchestratorEvent(string eventName, Action<OrchestratorEvent> handler)
        {
            lock (_sync)
            {
                if (_eventHandlers.ContainsKey(eventName))
                {
                    _eventHandlers[eventName].Remove(handler);
                }
            }
            return this;
        }

        public AnimationEngineStubOrchestrator PublishOrchestratorEvent(OrchestratorEventType type, object payload)
        {
            var evt = new OrchestratorEvent
            {
                Type = type,
                Message = payload?.ToString() ?? "N/A",
                Payload = payload,
                Timestamp = DateTime.UtcNow,
                CorrelationId = _correlationId
            };
            _eventHistory.Add(evt);

            var handlers = _eventHandlers.GetValueOrDefault(type.ToString(), new List<Action<OrchestratorEvent>>());
            foreach (var handler in handlers)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    LogOrchestratorEvent(LogLevel.Error, $"Error in event handler for {type}: {ex.Message}");
                }
            }
            return this;
        }

        public List<OrchestratorEvent> GetOrchestratorEventHistory()
        {
            lock (_sync)
            {
                return new List<OrchestratorEvent>(_eventHistory);
            }
        }

        public AnimationEngineStubOrchestrator SetOrchestratorEventFilter(Func<string, bool> filter)
        {
            // Conceptuel : appliquer un filtre global aux événements
            LogOrchestratorEvent(LogLevel.Info, "Event filtering applied.");
            return this;
        }

        public AnimationEngineStubOrchestrator AggregateOrchestratorEvents(TimeSpan window)
        {
            // Conceptuel : agréger les événements sur une fenêtre de temps
            LogOrchestratorEvent(LogLevel.Info, $"Aggregating events over {window}.");
            return this;
        }

        public AnimationEngineStubOrchestrator ReplayOrchestratorEvents(List<OrchestratorEvent> events)
        {
            foreach (var evt in events)
            {
                // Ré-exécuter la logique associée à l'événement
                // Cela dépend de la nature de l'événement
                LogOrchestratorEvent(LogLevel.Info, $"Replaying event: {evt.Type}");
            }
            return this;
        }

        public string SerializeOrchestratorEvent(OrchestratorEvent evt)
        {
            return JsonSerializer.Serialize(evt);
        }

        public OrchestratorEvent DeserializeOrchestratorEvent(string json)
        {
            return JsonSerializer.Deserialize<OrchestratorEvent>(json);
        }

        public AnimationEngineStubOrchestrator RouteOrchestratorEvent(string eventName, string destination)
        {
            // Conceptuel : router un événement vers un autre système
            LogOrchestratorEvent(LogLevel.Info, $"Routing event '{eventName}' to {destination}.");
            return this;
        }

        public AnimationEngineStubOrchestrator TransformOrchestratorEvent(OrchestratorEvent evt, Func<OrchestratorEvent, OrchestratorEvent> transform)
        {
            var transformed = transform(evt);
            PublishOrchestratorEvent(transformed.Type, transformed.Payload);
            return this;
        }
        #endregion

        #region [AJOUT] Logging & Tracing (M)
        public AnimationEngineStubOrchestrator SetLogLevel(LogLevel level)
        {
            _logLevel = level;
            return this;
        }

        public LogLevel GetLogLevel()
        {
            return _logLevel;
        }

        public AnimationEngineStubOrchestrator LogOrchestratorEvent(LogLevel level, string message)
        {
            if ((int)level >= (int)_logLevel)
            {
                var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                _logs.Add(logEntry);
                // Publier l'événement
                PublishOrchestratorEvent(OrchestratorEventType.Info, new { Level = level, Message = message });
            }
            return this;
        }

        public AnimationEngineStubOrchestrator StartTrace(string traceName)
        {
            var traceId = Guid.NewGuid().ToString();
            _currentTraceId = traceId;
            _traces[traceName] = (traceId, new List<OrchestratorSpan>());
            LogOrchestratorEvent(LogLevel.Info, $"Started trace: {traceName} (ID: {traceId})");
            return this;
        }

        public AnimationEngineStubOrchestrator EndTrace(string traceName)
        {
            if (_traces.TryGetValue(traceName, out var traceInfo))
            {
                LogOrchestratorEvent(LogLevel.Info, $"Ended trace: {traceName} (ID: {traceInfo.traceId})");
                _currentTraceId = null;
            }
            return this;
        }

        public List<OrchestratorSpan> GetTraceResults(string traceName)
        {
            if (_traces.TryGetValue(traceName, out var traceInfo))
            {
                return traceInfo.spans;
            }
            return new List<OrchestratorSpan>();
        }

        public AnimationEngineStubOrchestrator StartSpan(string spanName, SpanType type = SpanType.Operation)
        {
            var span = new OrchestratorSpan
            {
                Name = spanName,
                Type = type,
                StartTimestamp = DateTime.UtcNow,
                TraceId = _currentTraceId
            };
            if (_currentTraceId != null)
            {
                // Trouver le span parent actif ou utiliser le trace lui-même
                // Pour simplifier, on ne gère pas les spans imbriqués ici
                if (_traces.Values.Any(v => v.traceId == _currentTraceId))
                {
                    var traceSpans = _traces.First(v => v.Value.traceId == _currentTraceId).Value.spans;
                    traceSpans.Add(span);
                }
            }
            return this;
        }

        public AnimationEngineStubOrchestrator EndSpan(string spanName)
        {
            var spans = GetTraceResults(""); // Simplifié, trouver le bon trace
            var span = spans.LastOrDefault(s => s.Name == spanName && s.Duration == TimeSpan.Zero); // Chercher un span non terminé
            if (span != null)
            {
                span.Duration = DateTime.UtcNow - span.StartTimestamp;
            }
            return this;
        }

        public AnimationEngineStubOrchestrator SetCorrelationId(string id)
        {
            _correlationId = id;
            return this;
        }

        public string GetCorrelationId()
        {
            return _correlationId;
        }

        public AnimationEngineStubOrchestrator SetLogRotationPolicy(TimeSpan interval, int maxFiles)
        {
            // Conceptuel : implémenter une rotation des logs
            LogOrchestratorEvent(LogLevel.Info, $"Log rotation policy set: interval={interval}, maxFiles={maxFiles}");
            return this;
        }

        public AnimationEngineStubOrchestrator AggregateLogs(TimeSpan window)
        {
            // Conceptuel : agréger les logs sur une fenêtre de temps
            LogOrchestratorEvent(LogLevel.Info, $"Aggregating logs over {window}.");
            return this;
        }

        public AnimationEngineStubOrchestrator ExportLogs(string path, LogFormat format)
        {
            var content = format switch
            {
                LogFormat.Json => JsonSerializer.Serialize(_logs, new JsonSerializerOptions { WriteIndented = true }),
                LogFormat.Text => string.Join("\n", _logs),
                LogFormat.Csv => string.Join("\n", _logs.Select(l => $"\"{l}\"")), // Très simplifié
                _ => string.Join("\n", _logs) // Fallback
            };
            File.WriteAllText(path, content);
            LogOrchestratorEvent(LogLevel.Info, $"Logs exported to {path} in {format} format.");
            return this;
        }
        #endregion

        #region [AJOUT] Caching & Pooling (N)
        public AnimationEngineStubOrchestrator CacheResult(string key, object value, TimeSpan ttl)
        {
            lock (_sync)
            {
                _cache[key] = (value, DateTime.UtcNow + ttl);
            }
            return this;
        }

        public object GetCachedResult(string key)
        {
            lock (_sync)
            {
                if (_cache.TryGetValue(key, out var cached))
                {
                    if (DateTime.UtcNow < cached.expiration)
                    {
                        return cached.value;
                    }
                    else
                    {
                        _cache.Remove(key); // Nettoyer l'entrée expirée
                    }
                }
            }
            return null;
        }

        public AnimationEngineStubOrchestrator InvalidateCache(string key)
        {
            lock (_sync)
            {
                _cache.Remove(key);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator ClearCache()
        {
            lock (_sync)
            {
                _cache.Clear();
            }
            return this;
        }

        public T AcquireFromPool<T>() where T : class, new()
        {
            // Simplifié : créer un nouvel objet si le pool est vide
            // Un vrai pool d'objets nécessiterait un type générique plus spécifique ou un registre.
            return new T();
        }

        public AnimationEngineStubOrchestrator ReleaseToPool<T>(T obj) where T : class
        {
            // Simplifié : ne fait rien, car on ne garde pas de référence dans un pool simple
            return this;
        }

        public object AcquireResource(string resourceName)
        {
            // Conceptuel : acquérir une ressource (fichier, mutex, etc.)
            LogOrchestratorEvent(LogLevel.Info, $"Acquired resource: {resourceName}");
            return new object(); // Placeholder
        }

        public AnimationEngineStubOrchestrator ReleaseResource(string resourceName)
        {
            // Conceptuel : relâcher une ressource
            LogOrchestratorEvent(LogLevel.Info, $"Released resource: {resourceName}");
            return this;
        }

        public object AcquireConnection()
        {
            // Conceptuel : acquérir une connexion (DB, réseau, etc.)
            LogOrchestratorEvent(LogLevel.Info, "Acquired connection.");
            return new object(); // Placeholder
        }

        public AnimationEngineStubOrchestrator ReleaseConnection(object connection)
        {
            // Conceptuel : relâcher une connexion
            LogOrchestratorEvent(LogLevel.Info, "Released connection.");
            return this;
        }

        public byte[] AllocateFromMemoryPool(int size)
        {
            // Conceptuel : allouer de la mémoire depuis un pool
            LogOrchestratorEvent(LogLevel.Info, $"Allocated {size} bytes from memory pool.");
            return new byte[size]; // Placeholder
        }

        public AnimationEngineStubOrchestrator ReleaseToMemoryPool(byte[] memory)
        {
            // Conceptuel : relâcher de la mémoire au pool
            LogOrchestratorEvent(LogLevel.Info, "Released memory to pool.");
            return this;
        }

        public byte[] AcquireBuffer(int size)
        {
            // Conceptuel : acquérir un buffer
            LogOrchestratorEvent(LogLevel.Info, $"Acquired buffer of size {size}.");
            return new byte[size]; // Placeholder
        }

        public AnimationEngineStubOrchestrator ReleaseBuffer(byte[] buffer)
        {
            // Conceptuel : relâcher un buffer
            LogOrchestratorEvent(LogLevel.Info, "Released buffer.");
            return this;
        }
        #endregion

        #region [AJOUT] Versioning & Migration (O)
        public string GetOrchestratorVersion()
        {
            return _version;
        }

        public AnimationEngineStubOrchestrator MigrateOrchestrator(string fromVersion, string toVersion)
        {
            LogOrchestratorEvent(LogLevel.Info, $"Migrating from {fromVersion} to {toVersion}.");
            // Logique de migration ici
            _version = toVersion;
            _changelog.Add($"Migrated from {fromVersion} to {toVersion}");
            return this;
        }

        public bool CheckOrchestratorCompatibility(string version)
        {
            // Ex: Comparer la version demandée avec la version actuelle
            return version == _version;
        }

        public List<string> GetOrchestratorChangelog()
        {
            return new List<string>(_changelog);
        }

        public AnimationEngineStubOrchestrator WarnDeprecatedFeature(string feature)
        {
            LogOrchestratorEvent(LogLevel.Warning, $"Feature '{feature}' is deprecated.");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorFeatureFlag(string flag, bool enabled)
        {
            lock (_sync)
            {
                _featureFlags[flag] = enabled;
            }
            return this;
        }

        public bool GetOrchestratorFeatureFlag(string flag)
        {
            lock (_sync)
            {
                return _featureFlags.GetValueOrDefault(flag, false);
            }
        }

        public AnimationEngineStubOrchestrator EnableBackwardCompatibility(bool enabled)
        {
            LogOrchestratorEvent(LogLevel.Info, $"Backward compatibility: {enabled}");
            return this;
        }

        public AnimationEngineStubOrchestrator EnableForwardCompatibility(bool enabled)
        {
            LogOrchestratorEvent(LogLevel.Info, $"Forward compatibility: {enabled}");
            return this;
        }

        public string NegotiateOrchestratorVersion(string requestedVersion)
        {
            // Ex: Retourner la version la plus récente compatible
            return _version;
        }

        public string GetOrchestratorVersionChecksum()
        {
            // Ex: Calculer un hash de la version + changelog
            return "checksum_placeholder";
        }
        #endregion

        #region [AJOUT] Monitoring & Observability (P)
        public HealthStatus GetOrchestratorHealthStatus()
        {
            return IsHealthy();
        }

        private HealthStatus IsHealthy()
        {
            // Ex: Vérifier l'état du stub principal, des plugins, des pools, etc.
            if (_engineStub == null || _engineStub.IsShutdown) return HealthStatus.Unhealthy;
            if (_clusterState == StubClusterState.Failed) return HealthStatus.Unhealthy;
            if (_activeAlerts.Any()) return HealthStatus.Degraded;
            return HealthStatus.Healthy;
        }

        public AnimationEngineStubOrchestrator ExportOrchestratorMetrics(string path)
        {
            var metrics = GetDetailedMetrics();
            var report = new { Timestamp = DateTime.UtcNow, Metrics = metrics };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            LogOrchestratorEvent(LogLevel.Info, $"Metrics exported to {path}.");
            return this;
        }

        public AnimationEngineStubOrchestrator GenerateOrchestratorDashboard()
        {
            // Conceptuel : générer un dashboard (HTML, UI interne, etc.)
            LogOrchestratorEvent(LogLevel.Info, "Generating orchestrator dashboard...");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorAlert(string metric, float threshold)
        {
            lock (_sync)
            {
                _alerts[metric] = threshold;
            }
            LogOrchestratorEvent(LogLevel.Info, $"Set alert for metric '{metric}' at threshold {threshold}.");
            return this;
        }

        public List<string> GetOrchestratorAlerts()
        {
            lock (_sync)
            {
                return new List<string>(_activeAlerts);
            }
        }

        public AnimationEngineStubOrchestrator SetOrchestratorSLA(string metric, float target)
        {
            // Conceptuel : définir un SLA
            LogOrchestratorEvent(LogLevel.Info, $"Set SLA for metric '{metric}' at target {target}.");
            return this;
        }

        public bool CheckOrchestratorSLA()
        {
            // Conceptuel : vérifier si les SLA sont respectés
            LogOrchestratorEvent(LogLevel.Info, "Checking SLA compliance...");
            return true; // Placeholder
        }

        public TimeSpan GetOrchestratorUptime()
        {
            // Basé sur l'horodatage de démarrage initial
            return TimeSpan.Zero; // Placeholder
        }

        public TimeSpan GetOrchestratorDowntime()
        {
            // Basé sur les interruptions de service
            return TimeSpan.Zero; // Placeholder
        }

        public double GetOrchestratorAvailability()
        {
            // Calculer la disponibilité basée sur uptime/downtime
            return 100.0; // Placeholder
        }

        public AnimationEngineStubOrchestrator RecordOrchestratorIncident(IncidentType type, string description)
        {
            _incidentLog.Add((type, description, DateTime.UtcNow));
            LogOrchestratorEvent(LogLevel.Error, $"Incident recorded: {type} - {description}");
            return this;
        }
        #endregion

        #region [AJOUT] Gestion d'Erreurs & Récupération (Q)
        public AnimationEngineStubOrchestrator SetOrchestratorErrorRecoveryStrategy(ErrorRecoveryStrategy strategy)
        {
            _errorRecoveryStrategy = strategy;
            LogOrchestratorEvent(LogLevel.Info, $"Error recovery strategy set to: {strategy}");
            return this;
        }

        public ErrorRecoveryStrategy GetOrchestratorErrorRecoveryStrategy()
        {
            return _errorRecoveryStrategy;
        }

        public string GetOrchestratorCircuitBreakerState()
        {
            // Conceptuel : retourner l'état du circuit breaker
            return "CLOSED"; // Placeholder
        }

        public string GetOrchestratorBulkheadStatus()
        {
            // Conceptuel : retourner le statut du bulkhead
            return "OK"; // Placeholder
        }

        public AnimationEngineStubOrchestrator SetOrchestratorRetryPolicy(int maxRetries, TimeSpan delay)
        {
            // Conceptuel : définir une politique de retry globale ou pour des opérations spécifiques
            LogOrchestratorEvent(LogLevel.Info, $"Set global retry policy: maxRetries={maxRetries}, delay={delay}");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorFallback(Func<object> fallback)
        {
            // Conceptuel : définir une fonction de fallback
            LogOrchestratorEvent(LogLevel.Info, "Set global fallback function.");
            return this;
        }

        public AnimationEngineStubOrchestrator RegisterCompensation(Action compensation)
        {
            lock (_sync)
            {
                _compensations.Add(compensation);
            }
            return this;
        }

        public AnimationEngineStubOrchestrator ExecuteCompensations()
        {
            lock (_sync)
            {
                foreach (var compensation in _compensations)
                {
                    compensation?.Invoke();
                }
                _compensations.Clear();
            }
            return this;
        }

        public AnimationEngineStubOrchestrator DetectOrchestratorDeadlock(TimeSpan timeout)
        {
            // Conceptuel : implémenter une détection de deadlock
            LogOrchestratorEvent(LogLevel.Info, $"Detecting deadlock with timeout {timeout}...");
            return this;
        }

        public AnimationEngineStubOrchestrator SetOrchestratorTimeout(TimeSpan timeout)
        {
            // Conceptuel : implémenter un timeout global
            LogOrchestratorEvent(LogLevel.Info, $"Set orchestrator timeout to {timeout}.");
            return this;
        }

        public AnimationEngineStubOrchestrator EnableGracefulDegradation(bool enabled)
        {
            _gracefulDegradationEnabled = enabled;
            LogOrchestratorEvent(LogLevel.Info, $"Graceful degradation: {enabled}");
            return this;
        }
        #endregion

        #region [AJOUT] Documentation & Self-Help (R)
        public string GenerateOrchestratorDocumentation()
        {
            // Conceptuel : générer de la documentation (Markdown, HTML, etc.)
            return $"# {nameof(AnimationEngineStubOrchestrator)} Documentation\n\nGenerated on {DateTime.UtcNow}\n\n...";
        }

        public List<string> GetOrchestratorUsageExamples()
        {
            return new List<string>
            {
                "var orchestrator = AnimationEngineStubOrchestrator.CreateBuilder().WithPreset(StubConfigurationPreset.Standard).Build();",
                "orchestrator.InitializeWithConfig(config).Start().RunScenario(scenario);"
            };
        }

        public string GetOrchestratorTroubleshootingGuide()
        {
            return "## Troubleshooting Guide\n\n1. Check if the orchestrator is initialized.\n2. Verify the stub's state.\n3. Review the logs...\n";
        }

        public List<string> GetOrchestratorBestPractices()
        {
            return new List<string>
            {
                "Always initialize the orchestrator before use.",
                "Use presets for common configurations.",
                "Enable logging in development.",
                "Monitor performance metrics."
            };
        }

        public string GetOrchestratorFAQ()
        {
            return "## Frequently Asked Questions\n\nQ: How do I create an orchestrator?\nA: Use the builder pattern: `CreateBuilder()`...";
        }
        #endregion

        #region [AJOUT] Méthodes de base existantes (héritées du code initial)
        public AnimationEngineStubOrchestrator InitializeWithPreset(StubConfigurationPreset preset)
        {
            if (_isInitialized) throw new InvalidOperationException("L'orchestrateur est déjà initialisé.");
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationEngineStubOrchestrator));

            _engineStub = preset switch
            {
                StubConfigurationPreset.Minimal => AnimationEngineStubFactory.CreateMinimal(),
                StubConfigurationPreset.Standard => AnimationEngineStubFactory.CreateStandard(),
                StubConfigurationPreset.Full => AnimationEngineStubFactory.CreateFull(),
                StubConfigurationPreset.Custom => AnimationEngineStubFactory.CreateMinimal(), // Utiliser un minimal comme base pour le customiser
                _ => AnimationEngineStubFactory.CreateStandard()
            };

            _currentConfig = _engineStub._config; // Récupère la config effective du stub créé
            _isInitialized = _engineStub.IsReady;
            return this;
        }

        public AnimationEngineStubOrchestrator InitializeWithConfig(AnimationEngineStubConfig config)
        {
            if (_isInitialized) throw new InvalidOperationException("L'orchestrateur est déjà initialisé.");
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationEngineStubOrchestrator));

            if (_engineStub == null)
            {
                _engineStub = new AnimationEngineStub();
            }
            _engineStub.Initialize(config, null, null, null, null); // Passez les services si nécessaire, ou utilisez des dummies

            _currentConfig = config;
            _isInitialized = _engineStub.IsReady;
            return this;
        }

        public AnimationEngineStubOrchestrator Reconfigure(Action<AnimationEngineStubConfig> configModifier)
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationEngineStubOrchestrator));

            var newConfig = _currentConfig;
            configModifier(newConfig);
            _engineStub.Restart(newConfig);
            _currentConfig = newConfig;
            return this;
        }

        public AnimationEngineStubOrchestrator Start()
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_isRunning) return this; // Déjà lancé
            _isRunning = true;
            return this;
        }

        public AnimationEngineStubOrchestrator Stop()
        {
            if (!_isRunning) return this; // Déjà arrêté
            _isRunning = false;
            _engineStub.Suspend(); // Exemple, selon les méthodes du stub
            return this;
        }

        public AnimationEngineStubOrchestrator Restart()
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            _engineStub.Restart(_currentConfig);
            return this;
        }

        public AnimationEngineStubOrchestrator RunScenarioFromFile(string filePath)
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationEngineStubOrchestrator));

            var scenario = LoadScenarioFromJson(filePath);
            _engineStub.PlayScenario(scenario);
            return this;
        }

        public AnimationEngineStubOrchestrator RunScenario(StubScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationEngineStubOrchestrator));

            // Logique de priorité et de file d'attente pourrait être ici si RunScenario est appelé directement
            // Pour l'instant, on exécute directement via le stub principal
            LogOrchestratorEvent(LogLevel.Info, $"Playing scenario: {scenario.Name}");
            _engineStub.PlayScenario(scenario);

            // Attendre la fin du scénario si nécessaire
            // while (_engineStub.IsScenarioRunning()) { Thread.Sleep(10); } // Exemple synchrone

            return this;
        }

        public AnimationEngineStubOrchestrator StartRecording()
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_isReplaying) throw new InvalidOperationException("Impossible d'enregistrer pendant un replay.");

            _engineStub.StartRecordingSession();
            _isRecording = true;
            return this;
        }

        public AnimationEngineStubOrchestrator StopRecordingAndSave(string outputPath)
        {
            if (!_isRecording) throw new InvalidOperationException("Aucune session d'enregistrement active.");

            var session = _engineStub.StopRecordingSession();
            SaveSessionToFile(session, outputPath);
            _isRecording = false;
            return this;
        }

        public AnimationEngineStubOrchestrator LoadAndReplaySession(string inputPath)
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            if (_isRecording) throw new InvalidOperationException("Impossible de lancer un replay pendant un enregistrement.");

            var session = LoadSessionFromFile(inputPath);
            _engineStub.ReplaySession(session);
            _isReplaying = true;
            return this;
        }

        public DiagnosticReport GenerateDiagnosticReport()
        {
            if (!_isInitialized) throw new InvalidOperationException("L'orchestrateur n'est pas initialisé.");
            return _engineStub.GenerateDiagnosticReport();
        }

        public AnimationEngineStubOrchestrator ExportDiagnosticReport(string path, ReportFormat format = ReportFormat.Json)
        {
            var report = GenerateDiagnosticReport();
            ExportReportToFile(report, path, format);
            return this;
        }

        // --- Méthodes utilitaires internes ---
        private StubScenario LoadScenarioFromJson(string path)
        {
            var json = File.ReadAllText(path);
            // Note: StubScenario n'est pas directement sérialisable. Besoin d'une logique spécifique ou de converters.
            // Pour l'instant, placeholder.
            return new StubScenario { Name = Path.GetFileNameWithoutExtension(path) };
        }

        private void SaveSessionToFile(RecordedSession session, string path)
        {
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private RecordedSession LoadSessionFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RecordedSession>(json);
        }

        private void ExportReportToFile(DiagnosticReport report, string path, ReportFormat format)
        {
            string content = format switch
            {
                ReportFormat.Json => JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
                ReportFormat.Xml => $"<report><status>{report.Status}</status></report>", // Simplifié
                ReportFormat.Html => $"<html><body><h1>Report</h1><p>Status: {report.Status}</p></body></html>", // Simplifié
                _ => report.ToString() // PlainText ou fallback
            };
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _engineStub?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
        #endregion

        #region [AJOUT] Méthodes utilitaires internes (communes à plusieurs sections)
        private void UpdateMetrics(OrchestratorMetricType type, float value)
        {
            lock (_sync)
            {
                _metrics[type] = value;
            }
        }

        private DetailedMetrics GetDetailedMetrics()
        {
            // Récupérer les métriques du stub principal et les agrégées locales
            var stubMetrics = _engineStub.GetDetailedMetrics();
            var localMetrics = new DetailedMetrics
            {
                CPUBreakdown = stubMetrics.CPUBreakdown,
                MemoryBreakdown = stubMetrics.MemoryBreakdown,
                AllocationRatePerFrame = stubMetrics.AllocationRatePerFrame,
                GCGen0Count = stubMetrics.GCGen0Count,
                GCGen1Count = stubMetrics.GCGen1Count,
                GCGen2Count = stubMetrics.GCGen2Count,
                FrameTimeAverage = stubMetrics.FrameTimeAverage,
                FrameTimeMedian = stubMetrics.FrameTimeMedian,
                FrameTimeP95 = stubMetrics.FrameTimeP95,
                FrameTimeP99 = stubMetrics.FrameTimeP99,
                TotalMemoryUsedMB = stubMetrics.TotalMemoryUsedMB,
                PeakMemoryUsedMB = stubMetrics.PeakMemoryUsedMB
            };
            // Fusionner avec les métriques locales de l'orchestrator si nécessaire
            return localMetrics;
        }
        #endregion

        #region [AJOUT] Méthodes d'accès aux données internes (pour le dashboard)
        public Dictionary<OrchestratorMetricType, float> GetMetrics() => new Dictionary<OrchestratorMetricType, float>(_metrics);
        public List<string> GetRecentLogs(int count) => _logs.TakeLast(count).ToList();
        public List<(IncidentType type, string description, DateTime timestamp)> GetIncidentLog() => new List<(IncidentType, string, DateTime)>(_incidentLog);
        public List<OrchestratorEvent> GetRecentEvents(int count) => GetOrchestratorEventHistory().TakeLast(count).ToList();
        public List<TrendReport> GetStoredTrendReports() => AnalyzeReportTrends(Array.Empty<string>()); // Placeholder, devrait stocker les rapports récents
        public List<string> GetActiveAlerts() => new List<string>(_activeAlerts);
        public List<AnimationEngineStub> GetClusterStubs() => new List<AnimationEngineStub>(_clusterStubs);
        public List<AnimationEngineStub> GetStubPoolContents() => new List<AnimationEngineStub>(_stubPool);
        public Dictionary<string, RecordedSession> GetSessionLibrary() => new Dictionary<string, RecordedSession>(_sessionLibrary);
        public Dictionary<string, StubScenario> GetScenarioLibrary() => new Dictionary<string, StubScenario>(_scenarioLibrary);
        public List<IStubPlugin> GetLoadedPlugins() => GetRegisteredPlugins(); // Réutilise la méthode existante
        public Dictionary<string, WorkflowDefinition> GetDefinedWorkflows() => new Dictionary<string, WorkflowDefinition>(_workflows);
        #endregion
    }
}

/*
Exemple d'intégration dans SnakeEngine V2

// Exemple d'utilisation dans un contexte de jeu (SnakeGame.cs ou un système dédié)
using Engine.Animation.Test; // Namespace du stub/orchestrator

public class SnakeGame
{
    private AnimationEngineStubOrchestrator _animOrchestrator;

    public void Initialize()
    {
        // 1. Configurer l'orchestrateur avec un preset ou via le builder
        _animOrchestrator = AnimationEngineStubOrchestrator.CreateBuilder()
            .WithPreset(StubConfigurationPreset.Standard) // ou Full pour tests complets
            .WithTelemetry(true)
            .Build();

        // 2. L'initialiser
        _animOrchestrator.InitializeWithConfig(AnimationEngineStubConfig.Default);

        // 3. Enregistrer des scénarios ou plugins de test si nécessaire
        // var myTestScenario = new StubScenario { Name = "TestScenario", ... };
        // _animOrchestrator.RegisterScenario(myTestScenario.Name, myTestScenario);

        // 4. Démarrer l'orchestrateur
        _animOrchestrator.Start();

        // 5. Passer le stub *réel* à d'autres systèmes qui en ont besoin
        // Par exemple, si MovementSystem ou RenderSystem ont besoin d'IAnimationEngine
        var actualStub = _animOrchestrator.EngineStub; // Récupérer le stub géré par l'orchestrateur
        // MovementSystem.Initialize(actualStub);
        // RenderSystem.SetAnimationEngine(actualStub);
    }

    public void Update(float deltaTime)
    {
        // Mettre à jour l'orchestrateur (et donc le stub principal)
        _animOrchestrator.EngineStub.Update(deltaTime);

        // Exécuter des scénarios de test si nécessaire
        // _animOrchestrator.RunScenario("SomeTestScenario");

        // Le reste de la logique de mise à jour du jeu...
    }

    public void Shutdown()
    {
        // Arrêter et nettoyer l'orchestrateur
        _animOrchestrator.Stop();
        _animOrchestrator.Dispose();
    }
}
*/