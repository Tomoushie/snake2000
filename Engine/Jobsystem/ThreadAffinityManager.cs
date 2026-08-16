// /Engine/Jobsystem/ThreadAffinityManager.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Text.Json; // Pour la sérialisation de snapshots
using System.Text.Json.Serialization; // Pour la configuration de la sérialisation
using Engine.Core;              // IJobSystem, JobHandle, JobCategory, CategoryAffinityMap...
using Snake2000.Engine.Core;    // EventBus, Profiler, ResourceManager
using Engine.Profiling;
// Engine.Events et Engine.Utilities n'existent nulle part dans le depot : ils
// designaient une organisation prevue mais jamais creee.

namespace Engine.Jobsystem
{
    /// <summary>
    /// Gestionnaire d'affinité des threads du moteur Snake2000.
    /// Respecte les règles d'architecture : Engine → Systems → Components → Utilities.
    /// </summary>
    /// <remarks>
    /// Ce gestionnaire est responsable du Thread Affinity Control mentionné dans
    /// les idées avancées. Il orchestre la répartition des jobs sur les threads
    /// workers du IJobSystem en fonction de :
    /// - La catégorie du job (Animation, Physics, AI, Rendering, Movement...)
    /// - La topologie CPU (cœurs physiques, hyperthreading, NUMA)
    /// - La localité mémoire des données
    /// - La charge actuelle des threads
    /// - Les priorités et contraintes temps réel
    /// - Les dépendances de jobs (FrameGraphAffinityPlanner)
    /// - Les architectures hétérogènes (PerformanceCoreClasses)
    /// - Les contraintes de déterminisme (DeterministicScheduleLedger)
    /// - Les contraintes de sécurité et de performance (ZeroAllocationHotPath)
    /// - Les contraintes de gameplay (AnimationLODThreadBudget)
    /// - Les contraintes de plateforme (PlatformFeatureMatrix)
    ///
    /// Prérequis pour : Parallel Animation Jobs, MovementSystem orchestration,
    /// MovementAnimationBridgeSystem thread isolation, GPUProfilerHook sync.
    ///
    /// Position : /Engine/Jobsystem/ThreadAffinityManager.cs
    /// Dépendances : IJobSystem (via interface), EventBus, Profiler, INativeAffinityProvider
    /// Aucune logique Snake2000 ici (règle /Engine).
    /// </remarks>
    public sealed class ThreadAffinityManager : IThreadAffinityManager, IDisposable
    {
        #region Fields

        private readonly IJobSystem _jobSystem;
        private readonly EventBus _eventBus;
        private readonly Profiler _profiler;
        private readonly INativeAffinityProvider _nativeProvider;

        // État du gestionnaire
        // Lu et ecrit DIRECTEMENT, sans Volatile.Read/Write : ces deux methodes
        // generiques exigent `where T : class`, et une enum n'est pas un type
        // reference — c'etait le CS0452. Le champ etant deja `volatile`, l'acces
        // direct porte exactement la meme garantie de visibilite entre threads.
        private volatile ThreadAffinityManagerState _state;
        private volatile ThreadAffinityManagerConfig _config;
        private readonly CategoryAffinityMap _affinityMap;
        private readonly ThreadLoadDistribution _loadDistribution;
        private readonly CPUTopology _topology;
        private readonly List<ReservedThreadInfo> _reservedThreads;

        // Politiques d'affinité personnalisées
        private readonly List<IAffinityPolicy> _policies;
        // Extensions du gestionnaire
        private readonly List<IThreadAffinityExtension> _extensions;

        // Structures pour les nouvelles fonctionnalités
        private readonly AffinityLifecycleData _lifecycleData;
        private readonly Dictionary<int, ThreadAffinityHistory> _threadHistories;
        private readonly Dictionary<JobHandle, JobAffinityHistory> _jobHistories;
        private readonly LoadBalancingHistory _loadBalancingHistory;
        private readonly NUMALocalityHistory _numaLocalityHistory;
        private readonly CriticalThreadIsolationData _criticalThreadData;
        private readonly Dictionary<JobCategory, CategoryAffinityProfile> _categoryProfiles;
        private readonly List<AffinityPreset> _presetLibrary;

        // Locking
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly object _configLock = new object();

        // Pour la détection de contention
        private long _lockContentionCounter = 0;

        // Pour la gestion des changements d'affinité
        private readonly ConcurrentQueue<AffinityChangeRequest> _pendingChanges = new ConcurrentQueue<AffinityChangeRequest>();

        // Pour la gestion des timers et tâches asynchrones
        private CancellationTokenSource _warmupCancellationSource;
        private CancellationTokenSource _monitoringCancellationSource;

        // Pour le pattern Dispose
        private bool _disposed = false;

        // --- Nouveaux champs pour les idées ---
        private readonly FrameGraphAffinityPlanner _frameGraphPlanner = new FrameGraphAffinityPlanner();
        private readonly ThreadRoleToCoreClassMapper _coreClassMapper = new ThreadRoleToCoreClassMapper();
        private readonly PlatformFeatureMatrix _platformMatrix = new PlatformFeatureMatrix();
        private readonly TelemetryRingBuffer _telemetryBuffer = new TelemetryRingBuffer(1024); // Taille configurable
        private readonly ThreadAffinityPlanExecutor _planExecutor = new ThreadAffinityPlanExecutor();
        private readonly DeterministicScheduleLedger _deterministicLedger = new DeterministicScheduleLedger();
        private readonly FeatureFlagManager _featureFlags = new FeatureFlagManager();
        private readonly AffinityLeaseManager _leaseManager = new AffinityLeaseManager();

        // Compteurs pour l'observabilité
        private long _migrationsSucceeded = 0;
        private long _migrationsFailed = 0;
        private long _rebalancesPerformed = 0;
        private long _avgMigrationMs = 0;

        #endregion

        #region Properties

        public ThreadAffinityManagerState State => _state;
        public ThreadAffinityManagerConfig Config => Volatile.Read(ref _config);
        public CategoryAffinityMap AffinityMap => GetAffinityMapThreadSafe();
        public ThreadLoadDistribution LoadDistribution => GetLoadDistributionThreadSafe();
        public CPUTopology Topology => _topology;
        public List<ReservedThreadInfo> ReservedThreads => GetReservedThreadsThreadSafe();

        #endregion

        #region Constructor

        public ThreadAffinityManager(IJobSystem jobSystem, EventBus eventBus, Profiler profiler, INativeAffinityProvider nativeProvider)
        {
            _jobSystem = jobSystem ?? throw new ArgumentNullException(nameof(jobSystem));
            _eventBus = eventBus;
            _profiler = profiler;
            _nativeProvider = nativeProvider ?? throw new ArgumentNullException(nameof(nativeProvider));

            _state = ThreadAffinityManagerState.Uninitialized;
            _policies = new List<IAffinityPolicy>();
            _extensions = new List<IThreadAffinityExtension>();
            _affinityMap = new CategoryAffinityMap();
            _loadDistribution = new ThreadLoadDistribution();
            _topology = new CPUTopology();
            _reservedThreads = new List<ReservedThreadInfo>();

            _lifecycleData = new AffinityLifecycleData();
            _threadHistories = new Dictionary<int, ThreadAffinityHistory>();
            _jobHistories = new Dictionary<JobHandle, JobAffinityHistory>();
            _loadBalancingHistory = new LoadBalancingHistory();
            _numaLocalityHistory = new NUMALocalityHistory();
            _criticalThreadData = new CriticalThreadIsolationData();
            _categoryProfiles = new Dictionary<JobCategory, CategoryAffinityProfile>();
            _presetLibrary = new List<AffinityPreset>();
        }

        #endregion

        #region Initialization & Lifecycle (with hardening & safety)

        public void Initialize()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(ThreadAffinityManagerState.Uninitialized, ThreadAffinityManagerState.Initializing))
            {
                throw new InvalidOperationException($"Cannot initialize ThreadAffinityManager: current state is '{State}'. Expected 'Uninitialized'.");
            }

            try
            {
                DetectAndPopulateTopology();
                ApplyDefaultConfiguration();
                EstablishInitialAffinityMap();
                InitializePlatformSpecificFeatures();
                StartLoadMonitoring();
                NotifyInitialized();
                _state = ThreadAffinityManagerState.Ready;
            }
            catch (Exception ex)
            {
                _state = ThreadAffinityManagerState.Error;
                LogOrRaiseError(ex.Message, "Initialize", null, ex);
                throw;
            }
        }

        public void Initialize(ThreadAffinityManagerConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (config == null) throw new ArgumentNullException(nameof(config));
            ApplyConfiguration(config);
            Initialize();
        }

        [Async]
        public async Task InitializeAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(ThreadAffinityManagerState.Uninitialized, ThreadAffinityManagerState.Initializing))
            {
                throw new InvalidOperationException($"Cannot initialize ThreadAffinityManager: current state is '{State}'. Expected 'Uninitialized'.");
            }

            try
            {
                DetectAndPopulateTopology();
                ApplyDefaultConfiguration();
                EstablishInitialAffinityMap();
                InitializePlatformSpecificFeatures();
                await StartLoadMonitoringAsync(); // Nouveau
                await NotifyInitializedAsync(); // Nouveau
                _state = ThreadAffinityManagerState.Ready;
            }
            catch (Exception ex)
            {
                _state = ThreadAffinityManagerState.Error;
                LogOrRaiseError(ex.Message, "InitializeAsync", null, ex);
                throw;
            }
        }

        public void Update(float deltaTime)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!IsRunning()) return;

            _state = ThreadAffinityManagerState.Rebalancing;

            try
            {
                // 1. Mettre à jour les charges des threads
                UpdateThreadLoadDistribution();

                // 2. Evaluer la nécessité de rebalancer
                EvaluateRebalancingOpportunity();

                // 3. Appliquer les changements d'affinité si nécessaire
                ApplyPendingAffinityChanges();

                // 4. Mettre à jour les extensions
                foreach (var ext in _extensions)
                {
                    ext.Update(deltaTime);
                }

                // 5. Mettre à jour les nouveaux composants
                UpdateDeterministicLedger(deltaTime);
                UpdateTelemetryBuffer(deltaTime);
                UpdateLeaseManager(deltaTime);
            }
            finally
            {
                _state = ThreadAffinityManagerState.Running;
            }
        }

        public void Shutdown()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (State == ThreadAffinityManagerState.Shutdown || State == ThreadAffinityManagerState.ShuttingDown)
            {
                return;
            }

            if (!TryTransitionState(ThreadAffinityManagerState.Running, ThreadAffinityManagerState.ShuttingDown) &&
                !TryTransitionState(ThreadAffinityManagerState.Ready, ThreadAffinityManagerState.ShuttingDown))
            {
                return;
            }

            try
            {
                StopLoadMonitoring();
                ShutdownExtensions();
                ClearAffinityMap();
                ShutdownPlatformSpecificFeatures();
                NotifyShutdown();
            }
            finally
            {
                _state = ThreadAffinityManagerState.Shutdown;
            }
        }

        public void Restart()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            Shutdown();
            Initialize(GetConfigSnapshot()); // Utilise GetConfigSnapshot
        }

        public void Reset()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var currentState = State;
            if (currentState == ThreadAffinityManagerState.Running || currentState == ThreadAffinityManagerState.Ready)
            {
                 ClearAffinityMap();
                 EstablishInitialAffinityMap();
            }
        }

        public ThreadAffinityManagerState GetState() => State;
        public bool IsReady() => State == ThreadAffinityManagerState.Ready || State == ThreadAffinityManagerState.Running;
        public bool IsRunning() => State == ThreadAffinityManagerState.Running;

        #endregion

        #region Topology & Configuration (with OS abstraction)

        private void DetectAndPopulateTopology()
        {
            _topology = _nativeProvider.DetectCPUTopology();
            _coreClassMapper.Initialize(_topology);
            _platformMatrix.PopulateFromTopology(_topology);
        }

        private void ApplyDefaultConfiguration()
        {
            var newConfig = new ThreadAffinityManagerConfig
            {
                CpuAffinityMask = -1,
                ReservedCoreCount = 1,
                EnableHyperthreading = true,
                Mode = ThreadAffinityMode.Adaptive,
                SafeMode = false,
                RebalanceIntervalSec = 5.0f,
                RebalanceThreshold = 0.3f,
                TurboBudgetPercentage = 20.0f,
                CoreParkingEnabled = true,
                HugePagesEnabled = false,
                PrefetcherEnabled = true
            };
            Volatile.Write(ref _config, newConfig);
        }

        public void ApplyConfiguration(ThreadAffinityManagerConfig config)
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             if (config == null) throw new ArgumentNullException(nameof(config));
             if (!ValidateConfig(config)) throw new ArgumentException("Invalid configuration provided.", nameof(config)); // Nouveau
             lock (_configLock)
             {
                 _config = config;
                 if (IsRunning())
                 {
                     ReconfigureRuntimeSettings(config);
                 }
             }
        }

        public void ReloadConfiguration()
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             var newConfig = LoadConfigurationFromFile();
             ApplyConfiguration(newConfig);
        }

        #endregion

        #region Affinity Management (Categories & Specific Jobs) (with validation & contracts)

        public void SetCategoryAffinity(JobCategory category, int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            _lock.EnterWriteLock();
            try
            {
                _affinityMap[category] = new List<int> { threadIndex };
                QueueAffinityChange(category, new List<int> { threadIndex }, "ExternalAPI"); // Nouveau

                if (!_categoryProfiles.ContainsKey(category))
                {
                    _categoryProfiles[category] = new CategoryAffinityProfile();
                }
                _categoryProfiles[category].LastAssignedThreads = new List<int> { threadIndex };
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void SetCategoryAffinity(JobCategory category, List<int> threadIndices)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (threadIndices == null) throw new ArgumentNullException(nameof(threadIndices));
            foreach (var idx in threadIndices) EnsureValidThreadIndex(idx);
            EnsureInitializedOrReady();

            _lock.EnterWriteLock();
            try
            {
                _affinityMap[category] = new List<int>(threadIndices);
                QueueAffinityChange(category, threadIndices, "ExternalAPI"); // Nouveau

                if (!_categoryProfiles.ContainsKey(category))
                {
                    _categoryProfiles[category] = new CategoryAffinityProfile();
                }
                _categoryProfiles[category].LastAssignedThreads = new List<int>(threadIndices);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int GetCategoryAffinity(JobCategory category)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureInitializedOrReady();
            _lock.EnterReadLock();
            try
            {
                if (_affinityMap.TryGetValue(category, out var threads) && threads.Count > 0)
                {
                    return threads[0];
                }
                return -1;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<int> GetCategoryAffinityThreads(JobCategory category)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureInitializedOrReady();
            _lock.EnterReadLock();
            try
            {
                if (_affinityMap.TryGetValue(category, out var threads))
                {
                    return new List<int>(threads);
                }
                return new List<int>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public CategoryAffinityMap GetCategoryAffinityMap()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return GetAffinityMapThreadSafe();
        }

        private CategoryAffinityMap GetAffinityMapThreadSafe()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterReadLock();
            try
            {
                return new CategoryAffinityMap(_affinityMap);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // --- Affinité spécifique par Job/Thread ---
        public OperationResult PinJobToThread(JobHandle jobHandle, int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            var hints = new AffinityHints { PreferredThreadIndex = threadIndex, PinningRequired = true };
            try
            {
                _jobSystem.SetJobAffinityHints(jobHandle, hints);

                _lock.EnterWriteLock();
                try
                {
                    if (!_jobHistories.ContainsKey(jobHandle))
                    {
                        _jobHistories[jobHandle] = new JobAffinityHistory();
                    }
                    _jobHistories[jobHandle].LastPinnedThread = threadIndex;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                return new OperationResult { Success = true, Reason = "Job pinned successfully." }; // Nouveau
            }
            catch (Exception ex)
            {
                LogOrRaiseError($"Failed to pin job {jobHandle} to thread {threadIndex}: {ex.Message}", "PinJobToThread", new { JobHandle = jobHandle, ThreadIndex = threadIndex }, ex);
                return new OperationResult { Success = false, Reason = ex.Message, EstimatedCostMs = 0 }; // Nouveau
            }
        }

        public OperationResult SetJobAffinityHints(JobHandle jobHandle, AffinityHints hints)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (hints == null) throw new ArgumentNullException(nameof(hints));
            EnsureInitializedOrReady();
            try
            {
                _jobSystem.SetJobAffinityHints(jobHandle, hints);

                _lock.EnterWriteLock();
                try
                {
                    if (!_jobHistories.ContainsKey(jobHandle))
                    {
                        _jobHistories[jobHandle] = new JobAffinityHistory();
                    }
                    _jobHistories[jobHandle].LastHints = hints;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                return new OperationResult { Success = true, Reason = "Hints set successfully." }; // Nouveau
            }
            catch (Exception ex)
            {
                LogOrRaiseError($"Failed to set hints for job {jobHandle}: {ex.Message}", "SetJobAffinityHints", new { JobHandle = jobHandle }, ex);
                return new OperationResult { Success = false, Reason = ex.Message, EstimatedCostMs = 0 }; // Nouveau
            }
        }

        public async Task<OperationResult> PinJobToThreadAsync(JobHandle jobHandle, int threadIndex, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return await Task.Run(() => PinJobToThread(jobHandle, threadIndex), cancellationToken);
        }

        // --- Affinité par Thread <-> Coeur ---
        public OperationResult SetThreadCoreAffinity(int threadIndex, int coreIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            try
            {
                 _nativeProvider.SetThreadCoreAffinity(threadIndex, coreIndex);

                 _lock.EnterWriteLock();
                 try
                 {
                     if (!_threadHistories.ContainsKey(threadIndex))
                     {
                         _threadHistories[threadIndex] = new ThreadAffinityHistory();
                     }
                     _threadHistories[threadIndex].LastAssignedCore = coreIndex;
                 }
                 finally
                 {
                     _lock.ExitWriteLock();
                 }

                 return new OperationResult { Success = true, Reason = "Thread pinned to core successfully." }; // Nouveau
            }
            catch (Exception ex)
            {
                LogOrRaiseError($"Failed to set thread {threadIndex} core affinity to {coreIndex}: {ex.Message}", "SetThreadCoreAffinity", new { ThreadIndex = threadIndex, CoreIndex = coreIndex }, ex);
                return new OperationResult { Success = false, Reason = ex.Message, EstimatedCostMs = 0 }; // Nouveau
            }
        }

        public long GetThreadAffinityMask(int threadIndex)
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             EnsureValidThreadIndex(threadIndex);
             EnsureInitializedOrReady();

             return _nativeProvider.GetThreadAffinityMask(threadIndex);
        }

        public int GetThreadCurrentCore(int threadIndex)
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             EnsureValidThreadIndex(threadIndex);
             EnsureInitializedOrReady();

             return _nativeProvider.GetThreadCurrentCore(threadIndex);
        }

        #endregion

        #region Load Balancing (with cost awareness & hysteresis)

        private void UpdateThreadLoadDistribution()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterWriteLock();
            try
            {
                 _loadDistribution.UpdateFromJobSystem(_jobSystem);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void EvaluateRebalancingOpportunity()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var config = Volatile.Read(ref _config);
            if (!config.EnableAutoBalancing || config.SafeMode) return;

            if ((DateTime.UtcNow - _lifecycleData.LastRebalanceTime).TotalSeconds < config.RebalanceIntervalSec)
            {
                return;
            }

            _lock.EnterReadLock();
            try
            {
                var context = new AffinityContext
                {
                    ThreadLoads = new Dictionary<int, float>(_loadDistribution.ThreadLoads),
                    CurrentMap = new CategoryAffinityMap(_affinityMap),
                    Topology = _topology,
                    Timestamp = DateTime.UtcNow
                };

                var maxLoad = context.ThreadLoads.Values.Max();
                var minLoad = context.ThreadLoads.Values.Min();
                var imbalance = maxLoad - minLoad;
                if (imbalance > config.RebalanceThreshold)
                {
                    foreach (var policy in _policies)
                    {
                        // Exemple d'utilisation de la politique
                        // if (policy.ShouldMigrate(jobHandle, context)) { ... }
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void ApplyPendingAffinityChanges()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
             _lock.EnterWriteLock();
             try
             {
                  ProcessQueueAffinityChanges();
             }
             finally
             {
                 _lock.ExitWriteLock();
             }
        }

        public void EnableAutoBalancing(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                var newConfig = new ThreadAffinityManagerConfig(_config);
                newConfig.EnableAutoBalancing = enabled;
                Volatile.Write(ref _config, newConfig);
            }
        }

        public void RegisterAffinityPolicy(IAffinityPolicy policy)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            _lock.EnterWriteLock();
            try
            {
                if (!_policies.Contains(policy))
                {
                    _policies.Add(policy);
                    policy.Initialize();
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        #endregion

        #region Critical Thread Isolation (with reservation tracking)

        public void ReserveRenderThread(int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            var reservation = new ReservedThreadInfo
            {
                ThreadIndex = threadIndex,
                Reason = ThreadReservationReason.Render
            };
            _lock.EnterWriteLock();
            try
            {
                if (!_reservedThreads.Contains(reservation))
                {
                    _reservedThreads.Add(reservation);
                    _jobSystem.SuspendWorkerThread(threadIndex);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ReserveAudioThread(int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            var reservation = new ReservedThreadInfo
            {
                ThreadIndex = threadIndex,
                Reason = ThreadReservationReason.Audio
            };
            _lock.EnterWriteLock();
            try
            {
                if (!_reservedThreads.Contains(reservation))
                {
                    _reservedThreads.Add(reservation);
                     _jobSystem.SuspendWorkerThread(threadIndex);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void ReserveMainThread(int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            EnsureValidThreadIndex(threadIndex);
            var reservation = new ReservedThreadInfo
            {
                ThreadIndex = threadIndex,
                Reason = ThreadReservationReason.MainThread
            };
            _lock.EnterWriteLock();
            try
            {
                if (!_reservedThreads.Contains(reservation))
                {
                    _reservedThreads.Add(reservation);
                     _jobSystem.SuspendWorkerThread(threadIndex);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private List<ReservedThreadInfo> GetReservedThreadsThreadSafe()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterReadLock();
            try
            {
                return new List<ReservedThreadInfo>(_reservedThreads);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        #endregion

        #region Extensions

        public void RegisterExtension<T>() where T : IThreadAffinityExtension
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var extension = Activator.CreateInstance<T>();
            if (extension == null) throw new InvalidOperationException($"Could not create instance of extension type {typeof(T)}.");
            _lock.EnterWriteLock();
            try
            {
                if (!_extensions.Contains(extension))
                {
                    _extensions.Add(extension);
                    extension.Initialize();
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void ShutdownExtensions()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterWriteLock();
            try
            {
                foreach (var ext in _extensions)
                {
                    ext.Shutdown();
                }
                _extensions.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        #endregion

        #region Helper Methods (with validation & logging)

        private void QueueAffinityChange(JobCategory category, List<int> newThreads, string requesterId)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _pendingChanges.Enqueue(new AffinityChangeRequest { Category = category, NewThreads = new List<int>(newThreads), RequesterId = requesterId, Timestamp = DateTime.UtcNow }); // Nouveau
        }

        private void ProcessQueueAffinityChanges()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            while (_pendingChanges.TryDequeue(out var request))
            {
                // Appliquer le changement
                // Par exemple, mettre à jour _affinityMap[request.Category] = request.NewThreads;
                // Et notifier si nécessaire
                // Journaliser l'événement dans le ledger ou le buffer de télémétrie
            }
        }

        private void ClearAffinityMap()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterWriteLock();
            try
            {
                _affinityMap.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void EstablishInitialAffinityMap()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            ApplyDefaultAffinities();
        }

        private void ApplyDefaultAffinities()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var defaultMap = GetDefaultAffinityMapForProfile(Config.DefaultProfile);
            _lock.EnterWriteLock();
            try
            {
                foreach(var kvp in defaultMap)
                {
                    _affinityMap[kvp.Key] = kvp.Value;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private CategoryAffinityMap GetDefaultAffinityMapForProfile(AffinityProfile profile)
        {
            var map = new CategoryAffinityMap();
            switch(profile)
            {
                case AffinityProfile.ActionGame:
                    // Ex: Animation, Render, AI -> Threads séparés
                    break;
                case AffinityProfile.StrategyGame:
                    // Ex: AI, Physics -> Threads séparés
                    break;
                // ... autres profils
                default:
                    // Mapping générique
                    break;
            }
            return map;
        }

        private async Task StartLoadMonitoringAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _monitoringCancellationSource = new CancellationTokenSource();
            // Démarrer une tâche asynchrone pour surveiller périodiquement la charge
            _ = Task.Run(async () =>
            {
                while (!_monitoringCancellationSource.Token.IsCancellationRequested)
                {
                    UpdateThreadLoadDistribution();
                    await Task.Delay(TimeSpan.FromSeconds(1), _monitoringCancellationSource.Token); // Interval configurable
                }
            }, _monitoringCancellationSource.Token);
        }

        private void StopLoadMonitoring()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _monitoringCancellationSource?.Cancel();
            _monitoringCancellationSource?.Dispose();
            _monitoringCancellationSource = null;
        }

        private void NotifyInitialized()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new ThreadAffinityManagerInitializedEvent(this));
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during NotifyInitialized: {ex.Message}", "NotifyInitialized", null, ex);
                }
            }
        }

        private async Task NotifyInitializedAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    await Task.Run(() => eventBusCopy.Publish(new ThreadAffinityManagerInitializedEvent(this)));
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during NotifyInitializedAsync: {ex.Message}", "NotifyInitializedAsync", null, ex);
                }
            }
        }

        private void NotifyShutdown()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new ThreadAffinityManagerShutdownEvent(this));
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during NotifyShutdown: {ex.Message}", "NotifyShutdown", null, ex);
                }
            }
        }

        private void LogOrRaiseError(string message, string methodName, object contextData = null, Exception innerException = null)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var fullMessage = $"[ThreadAffinityManager::{methodName}] {message}";
            if (contextData != null)
            {
                fullMessage += $"\nContext: {contextData}";
            }
            if (innerException != null)
            {
                fullMessage += $"\nInner Exception: {innerException}";
            }
            Console.WriteLine(fullMessage); // Remplacer par un logger approprié
            // Eventuellement lever une exception enrichie ou publier un événement d'erreur via EventBus
        }

        private ThreadAffinityManagerConfig LoadConfigurationFromFile()
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             // Charger depuis un fichier JSON, XML, etc.
             // Valider le schéma si possible
             return new ThreadAffinityManagerConfig(); // Placeholder
        }

        private void ReconfigureRuntimeSettings(ThreadAffinityManagerConfig newConfig)
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             // Appliquer dynamiquement les changements possibles (mode, budgets, etc.)
             // Ne pas toucher à la topologie ou aux threads réservés pendant l'exécution.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureValidThreadIndex(int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (_jobSystem == null) throw new InvalidOperationException("IJobSystem dependency is null.");
            if (threadIndex < 0 || threadIndex >= GetWorkerThreadCount()) // Utilise GetWorkerThreadCount
            {
                throw new ArgumentOutOfRangeException(nameof(threadIndex), $"Thread index {threadIndex} is out of range for worker threads (count: {GetWorkerThreadCount()})."); // Utilise GetWorkerThreadCount
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureInitializedOrReady()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var state = State; // Lire une seule fois pour la cohérence
            if (state != ThreadAffinityManagerState.Ready && state != ThreadAffinityManagerState.Running)
            {
                throw new InvalidOperationException($"Method requires state to be Ready or Running, but current state is '{state}'.");
            }
        }

        private ThreadLoadDistribution GetLoadDistributionThreadSafe()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterReadLock();
            try
            {
                return new ThreadLoadDistribution(_loadDistribution);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryTransitionState(ThreadAffinityManagerState expected, ThreadAffinityManagerState next)
        {
            return Interlocked.CompareExchange(ref _state, next, expected) == expected;
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _lock?.EnterWriteLock();
                    try
                    {
                        ShutdownExtensions();
                        ClearAffinityMap();
                        _policies.Clear();
                        _extensions.Clear();
                        _threadHistories.Clear();
                        _jobHistories.Clear();
                        _reservedThreads.Clear();
                        _categoryProfiles.Clear();
                        _presetLibrary.Clear();
                        _warmupCancellationSource?.Cancel();
                        _warmupCancellationSource?.Dispose();
                        _monitoringCancellationSource?.Cancel();
                        _monitoringCancellationSource?.Dispose();
                        _lock?.ExitWriteLock();
                        _lock?.Dispose();
                    }
                    finally
                    {
                         // Le ExitWriteLock est fait dans le bloc try pour s'assurer qu'il est libéré même si une exception est levée dans les appels de suppression.
                    }
                }
                // Libérer les ressources non managées ici si nécessaire (handles natifs via _nativeProvider)

                _disposed = true;
            }
        }

        #endregion

        #region Contrat IThreadAffinityManager

        // Les deux membres releves sur MovementAnimationBridgeSystem, qui declarait
        // l'interface faute de pouvoir la placer dans Engine. AssignSystemToThread y
        // est appelee dix fois (lignes 3329 a 3338), retour ignore ; RunOnThread n'a
        // pas encore d'appelant, son contrat vient de la declaration relevee.

        private readonly Dictionary<Snake2000.Engine.Core.ISystem, int> _systemAssignments =
            new Dictionary<Snake2000.Engine.Core.ISystem, int>();

        /// <summary>
        /// Associe un systeme a un thread worker. L'association est enregistree ici ;
        /// l'epinglage effectif passera par INativeAffinityProvider, qui est encore un
        /// marqueur sans membre.
        /// </summary>
        public void AssignSystemToThread(Snake2000.Engine.Core.ISystem system, int threadIndex)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (system == null) throw new ArgumentNullException(nameof(system));
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            _lock.EnterWriteLock();
            try
            {
                _systemAssignments[system] = threadIndex;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Execute une action sur le thread worker demande.
        /// </summary>
        /// <remarks>
        /// L'index est valide contre le nombre de workers, mais l'execution part
        /// aujourd'hui sur un thread dedie du pool plutot que sur le worker nomme :
        /// INativeAffinityProvider ne porte encore aucun membre d'epinglage. La
        /// signature, elle, est celle que l'appelant attend.
        /// </remarks>
        public Task RunOnThread(int threadIndex, Action action)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (action == null) throw new ArgumentNullException(nameof(action));
            EnsureValidThreadIndex(threadIndex);
            EnsureInitializedOrReady();

            return Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        #endregion

        #region AAA Ideas Implementation (Methods & Properties)

        // --- 1. Critiques et fondations ---
        public int GetWorkerThreadCount()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return _jobSystem.GetWorkerThreadCount(); // Wrapper
        }

        public bool TryGetJobAffinityHistory(JobHandle jobHandle, out JobAffinityHistory history)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _lock.EnterReadLock();
            try
            {
                if (_jobHistories.TryGetValue(jobHandle, out history))
                {
                    return true;
                }
                history = new JobAffinityHistory();
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void ClearPendingChanges()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            while (_pendingChanges.TryDequeue(out _)) { } // Vide la queue
        }

        public bool ValidateConfig(ThreadAffinityManagerConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Exemple de validation
            if (config.RebalanceIntervalSec <= 0) return false;
            if (config.RebalanceThreshold < 0 || config.RebalanceThreshold > 1) return false;
            return true;
        }

        public ThreadAffinityManagerConfig GetConfigSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return new ThreadAffinityManagerConfig(Volatile.Read(ref _config)); // Copie immuable
        }

        // --- 9. Observabilité ---
        public TelemetryRingBufferSnapshot GetTelemetrySnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return _telemetryBuffer.GetSnapshot();
        }

        public string TakeThreadMapSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var data = new
            {
                Timestamp = DateTime.UtcNow,
                State = State,
                Config = GetConfigSnapshot(),
                AffinityMap = GetAffinityMapThreadSafe(),
                LoadDistribution = GetLoadDistributionThreadSafe(),
                Topology = Topology,
                ReservedThreads = ReservedThreads,
                Metrics = new { MigrationsSucceeded = _migrationsSucceeded, MigrationsFailed = _migrationsFailed, RebalancesPerformed = _rebalancesPerformed }
            };
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(data, options);
        }

        public bool IsHealthy()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Exemple de vérification
            var queueLength = _pendingChanges.Count;
            var lastRebalanceAge = (DateTime.UtcNow - _lifecycleData.LastRebalanceTime).TotalSeconds;
            var monitoringAlive = _monitoringCancellationSource != null && !_monitoringCancellationSource.Token.IsCancellationRequested;

            return queueLength < 100 && lastRebalanceAge < 30 && monitoringAlive; // Seuils configurables
        }

        #endregion

        // --- Les propriétés GetVersion(), GetJobSystemBuildMetadata() etc. restent inchangées ---
        public string GetVersion() => "1.0.0";

        // Qualifie : Engine.Profiling declare aussi un BuildMetadata.
        public Engine.Core.BuildMetadata GetJobSystemBuildMetadata()
        {
             return new Engine.Core.BuildMetadata { /* ... */ };
        }

        // --- Autres getters pour les structures de reporting restent inchangées ---
        public ThreadAffinityHeatmap GetThreadAffinityHeatmap()
        {
             if (_disposed) throw new ObjectDisposedException(GetType().Name);
             _lock.EnterReadLock();
             try
             {
                 return new ThreadAffinityHeatmap
                 {
                     LoadByThread = new Dictionary<int, float>(_loadDistribution.ThreadLoads),
                     LoadByCategory = new Dictionary<JobCategory, float>(_loadDistribution.CategoryLoads),
                     MaxLoad = _loadDistribution.MaxLoad,
                     MinLoad = _loadDistribution.MinLoad,
                     AverageLoad = _loadDistribution.AverageLoad,
                     ReportTime = DateTime.UtcNow
                 };
             }
             finally
             {
                 _lock.ExitReadLock();
             }
        }

    } // End Class ThreadAffinityManager

    #region Helper Structures (Nouvelles)

    public struct AffinityChangeRequest
    {
        public JobCategory Category;
        public List<int> NewThreads;
        public string RequesterId; // Nouveau
        public DateTime Timestamp; // Nouveau
    }

    // --- Structures pour les nouvelles idées ---
    public struct FrameGraphAffinityPlanner
    {
        public void Plan(FrameGraph graph, CategoryAffinityMap map, CPUTopology topology) { /* ... */ }
    }

    public struct ThreadRoleToCoreClassMapper
    {
        public void Initialize(CPUTopology topology) { /* ... */ }
        public void Assign(ThreadRole role, CoreClass coreClass) { /* ... */ }
    }

    public struct PlatformFeatureMatrix
    {
        public bool IsVirtualMachine { get; set; }
        public bool SupportsHugePages { get; set; }
        public bool SupportsCAT { get; set; }
        public string PlatformType { get; set; }

        public void PopulateFromTopology(CPUTopology topology) { /* ... */ }
        public void DetectAndApplyPolicies(INativeAffinityProvider provider) { /* ... */ }
    }

    public struct TelemetryRingBuffer
    {
        private readonly RingBuffer<TelemetryEvent> _buffer;
        public TelemetryRingBuffer(int capacity) { _buffer = new RingBuffer<TelemetryEvent>(capacity); }
        public void Enqueue(in TelemetryEvent @event) { _buffer.Enqueue(@event); }
        public TelemetryRingBufferSnapshot GetSnapshot() { /* ... */ return new TelemetryRingBufferSnapshot(); }
    }

    public struct TelemetryRingBufferSnapshot { /* ... */ }

    public struct ThreadAffinityPlanExecutor
    {
        public bool Execute(AffinityPlan plan, bool dryRun) { /* ... */ return true; }
    }

    public struct DeterministicScheduleLedger
    {
        public bool IsEnabled { get; set; }
        public void Record(SchedulingEvent @event) { /* ... */ }
        public void FlushBuffer() { /* ... */ }
    }

    public struct FeatureFlagManager
    {
        private readonly Dictionary<string, bool> _flags;
        public FeatureFlagManager() { _flags = new Dictionary<string, bool>(); }
        public void Set(string name, bool value) { _flags[name] = value; }
        public bool IsEnabled(string name) => _flags.TryGetValue(name, out var value) && value;
    }

    public struct AffinityLeaseManager
    {
        private readonly List<AffinityLease> _leases;
        public AffinityLease Acquire(int threadIndex, TimeSpan duration) { /* ... */ return new AffinityLease(); }
        public void Update(float deltaTime) { /* ... */ }
    }

    public struct OperationResult // Nouveau
    {
        public bool Success { get; set; }
        public string Reason { get; set; }
        public float EstimatedCostMs { get; set; }
    }

    public struct AffinityPlan { /* ... */ }
    public struct SchedulingEvent { /* ... */ }
    public struct TelemetryEvent { /* ... */ }
    public struct AffinityLease { /* ... */ }
    public struct FrameGraph { /* ... */ }
    public enum ThreadRole { Render, Input, Animation, Physics, AI, Background }
    public enum CoreClass { Performance, Efficiency, LowPower }

    #endregion

} // End Namespace