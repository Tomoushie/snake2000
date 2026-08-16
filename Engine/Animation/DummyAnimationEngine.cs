// /Engine/Animation/DummyAnimationEngine.cs
//
// Implémentation dummy / stub AAA du moteur d'animation.
// Ce fichier fournit une base robuste pour :
// - le développement précoce
// - les tests automatisés
// - l'éditeur
// - le debugging
// - le profiling
// - l'intégration progressive avec le rendu, le mouvement et l'audio
//
// Règles :
// - Aucune logique spécifique à Snake2000 ici.
// - Ce fichier appartient uniquement à /Engine/Animation.
// - Les interactions runtime passent par événements, services et messages.
// - Le moteur doit rester déterministe, thread-safe et testable.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Core;              // IJobSystem, JobHandle
using Snake2000.Engine.Core;    // EventBus, Profiler, ResourceManager
using Engine.Rendering;         // IRenderDevice, IRenderBackend, IMesh, MaterialHandle,
                                // LODLevel, LODStrategy, SkeletonPoseHandle
// IJob est declare des deux cotes — Engine.Core et Snake2000.Engine.Core — il est
// donc qualifie sur ses usages plutot qu'importe.

namespace Engine.Animation
{
    #region Enums

    public enum DummyAnimationEngineState
    {
        Uninitialized,
        Initializing,
        Ready,
        Running,
        Paused,
        Degraded,
        Recovering,
        Error,
        ShuttingDown,
        Shutdown
    }

    public enum DummyPlaybackState
    {
        Stopped,
        Playing,
        Paused,
        FadingIn,
        FadingOut,
        Finished
    }

    public enum DummyAnimationEventType
    {
        Generic,
        Marker,
        Notify,
        Footstep,
        Audio,
        VFX,
        Gameplay
    }

    // [CORRECTION] Enum pour les sous-systèmes
    public enum DummyAnimationSubsystemType
    {
        Device,
        Resource,
        Playback,
        Culling,
        Rendering,
        Physics,
        Audio
    }

    // [CORRECTION] Enum pour les erreurs dummy
    public enum DummyAnimationErrorCode
    {
        None,
        InvalidHandle,
        OutOfMemory,
        SubsystemNotFound,
        NotImplemented
    }

    // [AJOUT 1️⃣0️⃣] Feature Flags
    [Flags]
    public enum DummyAnimationEngineFeatures
    {
        None = 0,
        Diagnostics = 1 << 0,
        CallTracking = 1 << 1,
        GhostSkeleton = 1 << 2,
        StrictValidation = 1 << 3,
        FastPath = 1 << 4,
        ReplayMode = 1 << 5
    }

    // [AJOUT 9️⃣] Modes
    public enum DummyAnimationEngineMode
    {
        Headless,
        Editor,
        CI,
        Benchmark,
        Replay,
        Deterministic
    }

    #endregion

    #region Handles

    public readonly struct SkeletonHandle : IEquatable<SkeletonHandle>
    {
        public uint Id { get; }

        public SkeletonHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(SkeletonHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is SkeletonHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(SkeletonHandle left, SkeletonHandle right) => left.Equals(right);
        public static bool operator !=(SkeletonHandle left, SkeletonHandle right) => !left.Equals(right);

        public override string ToString() => $"SkeletonHandle({Id})";
    }

    public readonly struct PoseHandle : IEquatable<PoseHandle>
    {
        public uint Id { get; }

        public PoseHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(PoseHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PoseHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(PoseHandle left, PoseHandle right) => left.Equals(right);
        public static bool operator !=(PoseHandle left, PoseHandle right) => !left.Equals(right);

        public override string ToString() => $"PoseHandle({Id})";
    }

    public readonly struct ClipHandle : IEquatable<ClipHandle>
    {
        public uint Id { get; }

        public ClipHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(ClipHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ClipHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(ClipHandle left, ClipHandle right) => left.Equals(right);
        public static bool operator !=(ClipHandle left, ClipHandle right) => !left.Equals(right);

        public override string ToString() => $"ClipHandle({Id})";
    }

    public readonly struct PlaybackHandle : IEquatable<PlaybackHandle>
    {
        public uint Id { get; }

        public PlaybackHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(PlaybackHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PlaybackHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(PlaybackHandle left, PlaybackHandle right) => left.Equals(right);
        public static bool operator !=(PlaybackHandle left, PlaybackHandle right) => !left.Equals(right);

        public override string ToString() => $"PlaybackHandle({Id})";
    }

    public readonly struct AnimatedEntityHandle : IEquatable<AnimatedEntityHandle>
    {
        public uint Id { get; }

        public AnimatedEntityHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(AnimatedEntityHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is AnimatedEntityHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(AnimatedEntityHandle left, AnimatedEntityHandle right) => left.Equals(right);
        public static bool operator !=(AnimatedEntityHandle left, AnimatedEntityHandle right) => !left.Equals(right);

        public override string ToString() => $"AnimatedEntityHandle({Id})";
    }

    public readonly struct IKChainHandle : IEquatable<IKChainHandle>
    {
        public uint Id { get; }

        public IKChainHandle(uint id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        public bool Equals(IKChainHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is IKChainHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(IKChainHandle left, IKChainHandle right) => left.Equals(right);
        public static bool operator !=(IKChainHandle left, IKChainHandle right) => !left.Equals(right);

        public override string ToString() => $"IKChainHandle({Id})";
    }

    // [CORRECTION] Handles dummy
    public readonly struct DummyAnimationInstanceHandle : IEquatable<DummyAnimationInstanceHandle>
    {
        public uint Id { get; }
        public DummyAnimationInstanceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(DummyAnimationInstanceHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is DummyAnimationInstanceHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(DummyAnimationInstanceHandle left, DummyAnimationInstanceHandle right) => left.Equals(right);
        public static bool operator !=(DummyAnimationInstanceHandle left, DummyAnimationInstanceHandle right) => !left.Equals(right);
        public override string ToString() => $"DummyAnimationInstanceHandle({Id})";
    }

    #endregion

    #region Core Structures

    public struct BoneTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public BoneTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public static BoneTransform Identity => new BoneTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);

        public static BoneTransform Lerp(BoneTransform a, BoneTransform b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);

            return new BoneTransform
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Rotation = Quaternion.Lerp(a.Rotation, b.Rotation, t),
                Scale = Vector3.Lerp(a.Scale, b.Scale, t)
            };
        }

        public static BoneTransform Add(BoneTransform basePose, BoneTransform additive, float weight)
        {
            weight = Math.Clamp(weight, 0f, 1f);

            return new BoneTransform
            {
                Position = basePose.Position + additive.Position * weight,
                Rotation = Quaternion.Lerp(Quaternion.Identity, additive.Rotation, weight),
                Scale = basePose.Scale + (additive.Scale - Vector3.One) * weight
            };
        }
    }

    public struct BoneDefinition
    {
        public int Index;
        public int ParentIndex;
        public string Name;
        public BoneTransform BindPose;
        public float Length;
        public bool IsCritical;
    }

    public struct DummyEventMarker
    {
        public float Time;
        public string Name;
        public DummyAnimationEventType EventType;
        public object Payload;
    }

    public struct DummyClipDefinition
    {
        public string Name;
        public float Duration;
        public float SampleRate;
        public bool IsLooping;
        public bool HasRootMotion;
        public Vector3 RootMotionVelocity;
        public DummyEventMarker[] Events;

        public static DummyClipDefinition CreateDefault(string name, float duration = 1.0f)
        {
            return new DummyClipDefinition
            {
                Name = name,
                Duration = duration,
                SampleRate = 30f,
                IsLooping = false,
                HasRootMotion = false,
                RootMotionVelocity = Vector3.Zero,
                Events = Array.Empty<DummyEventMarker>()
            };
        }
    }

    public struct PlaybackOptions
    {
        public int Layer;
        public float Weight;
        public float Speed;
        public float StartTime;
        public bool IsLooping;
        public bool IsAdditive;
        public float BlendInDuration;
        public float BlendOutDuration;
        public int Priority;

        public static PlaybackOptions Default => new PlaybackOptions
        {
            Layer = 0,
            Weight = 1.0f,
            Speed = 1.0f,
            StartTime = 0.0f,
            IsLooping = false,
            IsAdditive = false,
            BlendInDuration = 0.1f,
            BlendOutDuration = 0.1f,
            Priority = 64
        };
    }

    // [CORRECTION] Config dummy
    public struct DummyAnimationEngineConfig
    {
        public int MaxPlaybacks;
        public int MaxPoses;
        public int MaxAnimatedEntities;
        public int MaxIKChains;
        public bool EnableEvents;
        public bool EnableMetrics;
        public bool EnableDeterministicMode;
        public bool EnableParallelEvaluation;
        public bool EnableRootMotion;
        public bool EnableIK;
        public float FixedDeltaTime;
        public int MetricsHistorySize;
        public bool EnableDiagnostics;
        public bool EnableCallTracking;
        public bool EnableGhostSkeletonMode; // [AJOUT 7️⃣]
        public bool EnableStrictValidation; // [AJOUT 3️⃣]
        public bool EnableFastPath; // [AJOUT 1️⃣1️⃣]
        public string Version;

        public static DummyAnimationEngineConfig Default => new DummyAnimationEngineConfig
        {
            MaxPlaybacks = 128,
            MaxPoses = 512,
            MaxAnimatedEntities = 256,
            MaxIKChains = 64,
            EnableEvents = true,
            EnableMetrics = true,
            EnableDeterministicMode = true, // [CORRECTION] Activé par défaut
            EnableParallelEvaluation = false,
            EnableRootMotion = true,
            EnableIK = true,
            FixedDeltaTime = 1f / 60f,
            MetricsHistorySize = 120,
            EnableDiagnostics = false,
            EnableCallTracking = false,
            EnableGhostSkeletonMode = false, // [AJOUT 7️⃣]
            EnableStrictValidation = false, // [AJOUT 3️⃣]
            EnableFastPath = false, // [AJOUT 1️⃣1️⃣]
            Version = "1.0.0-dummy"
        };
    }

    // [CORRECTION] Capabilities dummy
    public struct DummyCapabilities
    {
        public bool SupportsPlayback;
        public bool SupportsPoses;
        public bool SupportsBlending;
        public bool SupportsAdditiveBlending;
        public bool SupportsIK;
        public bool SupportsRootMotion;
        public bool SupportsEvents;
        public bool SupportsMetrics;
        public bool SupportsDeterministicMode;
        public bool SupportsParallelEvaluation;
        public bool SupportsStreaming;
        public bool SupportsTelemetry;
        public bool SupportsRealSkinning; // [CORRECTION] Explicitement faux
        public bool SupportsHeadlessMode; // [CORRECTION] Explicitement vrai

        public int MaxPlaybacks;
        public int MaxPoses;
        public int MaxAnimatedEntities;
        public int MaxIKChains;

        public string BackendName;
        public string BackendVersion;

        public static DummyCapabilities Static => new DummyCapabilities
        {
            SupportsPlayback = true,
            SupportsPoses = true,
            SupportsBlending = true,
            SupportsAdditiveBlending = true,
            SupportsIK = true,
            SupportsRootMotion = true,
            SupportsEvents = true,
            SupportsMetrics = true,
            SupportsDeterministicMode = true,
            SupportsParallelEvaluation = false,
            SupportsStreaming = false,
            SupportsTelemetry = false,
            SupportsRealSkinning = false, // [CORRECTION] Jamais vrai
            SupportsHeadlessMode = true, // [CORRECTION] Toujours vrai
            MaxPlaybacks = 128,
            MaxPoses = 512,
            MaxAnimatedEntities = 256,
            MaxIKChains = 64,
            BackendName = "DummyAnimationBackend",
            BackendVersion = "1.0.0-dummy"
        };
    }

    // [CORRECTION] Metrics dummy
    public struct DummyMetrics
    {
        public int FrameIndex;
        public int ActivePlaybacks;
        public int ActivePoses;
        public int ActiveEntities;
        public int ActiveIKChains;
        public int EvaluatedBones;
        public int EventsFired;
        public int CulledEntities;
        public int RootMotionSamples;
        public float UpdateMs;
        public float MemoryUsedMB;
        public DateTime Timestamp;
    }

    // [CORRECTION] Structures dummy
    public class DummyAnimationEventArgs : EventArgs
    {
        public string EventName { get; }
        public object Payload { get; }
        public DateTime Timestamp { get; }
        public int SequenceNumber { get; }

        public DummyAnimationEventArgs(string eventName, object payload, int sequenceNumber)
        {
            EventName = eventName;
            Payload = payload;
            Timestamp = DateTime.UtcNow;
            SequenceNumber = sequenceNumber;
        }
    }

    // [CORRECTION] Contexte dummy
    public sealed class DummyAnimationEngineContext : AnimationEngineContext
    {
        public EventBus EventBus { get; set; }
        public Profiler Profiler { get; set; }
        public IJobSystem JobSystem { get; set; }
        public ResourceManager ResourceManager { get; set; }
        public IRenderDevice Device { get; set; }
        public IRenderBackend Backend { get; set; }

        public DummyAnimationEngineContext()
        {
            // [CORRECTION] Injection de services dummy
            EventBus = new DummyEventBus();
            Profiler = new DummyProfiler();
            JobSystem = new DummyJobSystem();
            ResourceManager = new DummyResourceManager();
            Device = new DummyRenderDevice();
            Backend = new DummyRenderBackend();
        }
    }

    // [CORRECTION] Services dummy
    public class DummyEventBus : EventBus
    {
        public override void Subscribe<T>(Action<T> handler) { /* no-op */ }
        public override void Unsubscribe<T>(Action<T> handler) { /* no-op */ }
        public override void Publish<T>(T @event) { /* no-op */ }
    }

    // Aucun membre a neutraliser, et c'est le resultat d'une mesure : les deux
    // seuls appels du depot sont `Profiler.BeginSample("Frame")` et
    // `EndSample("Frame")` dans Engine.cs, tous deux STATIQUES. Aucune
    // substitution d'instance ne peut les detourner. `MarkEvent` n'existait ni
    // sur la base ni chez un appelant. Ce dummy sert de type substituable pour
    // le parametre `Profiler` des Initialize, rien de plus — et le dire
    // vaut mieux que trois `override` qui ne remplacaient rien.
    public class DummyProfiler : Profiler
    {
    }

    public class DummyJobSystem : IJobSystem
    {
        // Le contrat mesure de IJobSystem : trois membres, ceux que
        // ThreadAffinityManager appelle. Voir Docs/Intention/IJobSystem.cs.txt.
        public int GetWorkerThreadCount() => 0;
        public void SuspendWorkerThread(int threadIndex) { }
        public void SetJobAffinityHints(JobHandle jobHandle, AffinityHints hints) { }

        // Les trois membres que ce dummy portait deja et que personne n'appelle.
        // Ils ne font plus partie du contrat ; ils restent parce qu'ils ne genent
        // pas et qu'une implementation reelle en aura besoin.
        public JobHandle Schedule<T>(T jobData, JobHandle dependsOn = default) where T : struct, Engine.Core.IJob => default;
        public void Complete(JobHandle handle) { }
        public void CompleteAll() { }
    }

    // Meme constat, plus net encore : `Load`, `LoadAsync`, `Unload` et
    // `UnloadAll` n'existent pas sur ResourceManager, qui porte
    // `LoadResourceAsync` et `RegisterLoader`. Et AUCUN membre de
    // ResourceManager, ni les uns ni les autres, n'est appele nulle part dans
    // le depot. Il n'y a donc rien a neutraliser : ce dummy est un type
    // substituable, pas une implementation.
    public class DummyResourceManager : ResourceManager
    {
    }

    public class DummyRenderDevice : IRenderDevice
    {
        public string DeviceName => "DummyDevice";
        public string DriverVersion => "N/A";
        public bool IsLost => false;
        public bool TryRecover() => true;
        public void WaitForIdle() { }
    }

    public class DummyRenderBackend : IRenderBackend
    {
        public string BackendName => "DummyBackend";
        public string APIVersion => "N/A";
        public bool SupportsFeature(string feature) => false;
        public void Present() { }
    }

    // [CORRECTION] Subsystem dummy
    public sealed class DummyAnimationSubsystem : IAnimationSubsystem
    {
        public AnimationSubsystemType Type { get; set; }
        public void Initialize(AnimationEngineContext context) { /* no-op */ }
        public void Shutdown() { /* no-op */ }
        public void Update(float deltaTime) { /* no-op */ }
        public AnimationSubsystemStatus GetStatus() => AnimationSubsystemStatus.Ready;
        public void ValidateIntegrity() { /* no-op */ }
        public AnimationResult CheckHealth() => AnimationResult.Ok();
    }

    #endregion

    #region DummyAnimationEngine

    // [CORRECTION] Classe sealed implémentant IAnimationEngine
    public sealed class DummyAnimationEngine : IAnimationEngine, IDisposable
    {
        #region Internal Runtime Types

        private sealed class SkeletonRuntime
        {
            public SkeletonHandle Handle;
            public string Name;
            public BoneDefinition[] Bones;
            public int RootBoneIndex;
        }

        private sealed class ClipRuntime
        {
            public ClipHandle Handle;
            public DummyClipDefinition Definition;
        }

        private sealed class PoseRuntime
        {
            public PoseHandle Handle;
            public SkeletonHandle Skeleton;
            public BoneTransform[] Transforms;
            public int Version;
            public bool IsDirty;
            public bool IsValid; // [CORRECTION] Marqueur pour ReleasePose
        }

        private sealed class PlaybackRuntime
        {
            public PlaybackHandle Handle;
            public ClipHandle Clip;
            public PoseHandle TargetPose;
            public DummyPlaybackState State;
            public float Time;
            public float LastEventTime;
            public float Speed;
            public float Weight;
            public float TargetWeight;
            public int Layer;
            public bool IsLooping;
            public bool IsAdditive;
            public float BlendInDuration;
            public float BlendOutDuration;
            public int Priority;
            public Vector3 RootMotionAccumulator;
        }

        private sealed class EntityRuntime
        {
            public AnimatedEntityHandle Handle;
            public SkeletonHandle Skeleton;
            public PoseHandle Pose;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public bool Visible;
            public float Distance;
            public float LODBias;
        }

        private sealed class IKChainRuntime
        {
            public IKChainHandle Handle;
            public SkeletonHandle Skeleton;
            public int BoneIndex;
            public string Name;
            public bool Enabled;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public float PositionWeight;
            public float RotationWeight;
        }

        #endregion

        #region Fields

        private volatile int _stateAsInt;
        private DummyAnimationEngineConfig _config;
        private DummyCapabilities _capabilities;
        private DummyMetrics _metrics;

        private readonly object _sync = new object();
        private readonly Stopwatch _updateStopwatch = new Stopwatch();

        // [CORRECTION] Collections pour les sous-systèmes
        private readonly Dictionary<AnimationSubsystemType, IAnimationSubsystem> _subsystems = new Dictionary<AnimationSubsystemType, IAnimationSubsystem>();
        // [CORRECTION] Collection pour les appels de méthode
        private readonly ConcurrentQueue<(string method, object[] args, DateTime timestamp)> _callLog = new ConcurrentQueue<(string, object[], DateTime)>();
        // [CORRECTION] Compteur d'événements
        private int _eventSequenceCounter;

        private readonly Dictionary<SkeletonHandle, SkeletonRuntime> _skeletons = new Dictionary<SkeletonHandle, SkeletonRuntime>();
        private readonly Dictionary<ClipHandle, ClipRuntime> _clips = new Dictionary<ClipHandle, ClipRuntime>();
        private readonly Dictionary<PoseHandle, PoseRuntime> _poses = new Dictionary<PoseHandle, PoseRuntime>();
        private readonly Dictionary<PlaybackHandle, PlaybackRuntime> _playbacks = new Dictionary<PlaybackHandle, PlaybackRuntime>();
        private readonly Dictionary<AnimatedEntityHandle, EntityRuntime> _entities = new Dictionary<AnimatedEntityHandle, EntityRuntime>();
        private readonly Dictionary<IKChainHandle, IKChainRuntime> _ikChains = new Dictionary<IKChainHandle, IKChainRuntime>();

        private readonly Dictionary<string, float> _floatParameters = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _intParameters = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> _boolParameters = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _triggerParameters = new Dictionary<string, bool>();

        private readonly Queue<PlaybackRuntime> _playbackPool = new Queue<PlaybackRuntime>();
        private readonly Queue<DummyMetrics> _metricsHistory = new Queue<DummyMetrics>();

        private uint _nextId = 1;
        private int _frameIndex;
        private float _timeDilation = 1.0f;
        private float _globalSpeed = 1.0f;
        private uint _frameHash = 0; // [AJOUT 1️⃣2️⃣]

        // [AJOUT 2️⃣] Compteurs pour les hooks rendu
        private int _applyPoseCallCount;
        private int _calculateLODCallCount;

        // [AJOUT 1.1] Null Pose
        private static readonly BoneTransform[] NullPose = Array.Empty<BoneTransform>();

        // [AJOUT 1.2] Null Skeleton
        private static readonly BoneDefinition[] NullSkeleton = Array.Empty<BoneDefinition>();

        // [AJOUT 1.3] Null Playback
        private static readonly PlaybackRuntime NullPlayback = new PlaybackRuntime
        {
            Handle = new PlaybackHandle(0),
            State = DummyPlaybackState.Stopped,
            Speed = 0,
            Weight = 0
        };

        // [AJOUT 3.2] RNG déterministe
        private readonly Random _rng = new Random(123456);

        // [AJOUT 4.2] Replay Mode
        private bool _replayMode;

        // [AJOUT 4️⃣] DiagnosticsReport
        private int _validationErrors;
        private string _lastEventName;

        // [AJOUT 6️⃣] DebugOverlayData
        private bool _ghostSkeletonMode;

        // [AJOUT 7️⃣] ProfilerZones
        private readonly List<string> _profilerZones = new();

        // [AJOUT 8️⃣] EventHistory
        private readonly Queue<DummyAnimationEventArgs> _eventHistory = new();

        // [AJOUT 1️⃣0️⃣] ContractValidator
        private bool _contractValid;

        // [AJOUT 1️⃣1️⃣] FastPath
        private bool _fastPath;

        // [AJOUT 1️⃣2️⃣] MemoryTracker
        private int _allocCount;
        private int _releaseCount;

        // [AJOUT 3️⃣] HealthReport
        private string _lastError;

        // [AJOUT 4️⃣] Recorder
        private bool _recording;
        private readonly List<(string method, object[] args, DateTime timestamp)> _recordedCalls = new();

        // [AJOUT 1️⃣4️⃣] GoldenOutput
        private readonly Dictionary<string, object> _goldenOutputs = new();

        // [AJOUT 1️⃣6️⃣] IntegrityValidator
        private string _integrityReport;

        // [AJOUT 7️⃣] TimeSource
        private float _currentTime = 0f;
        private float _deltaTime = 1f / 60f;

        // [AJOUT 8️⃣] ThreadAffinity
        private int _threadId = -1;

        // [AJOUT 9️⃣] ModeSwitcher
        private DummyAnimationEngineMode _mode;

        // [AJOUT 1️⃣0️⃣] FeatureMask
        private DummyAnimationEngineFeatures _features;

        private bool _disposed;
        private bool _telemetryEnabled;

        #endregion

        #region Events

        public event EventHandler<DummyAnimationEventArgs> EventRaised;

        #endregion

        #region Properties

        public DummyAnimationEngineState State => (DummyAnimationEngineState)Volatile.Read(ref _stateAsInt);

        public bool IsReady =>
            State == DummyAnimationEngineState.Ready ||
            State == DummyAnimationEngineState.Running ||
            State == DummyAnimationEngineState.Paused ||
            State == DummyAnimationEngineState.Degraded;

        public bool IsRunning => State == DummyAnimationEngineState.Running;
        public bool IsShutdown => State == DummyAnimationEngineState.Shutdown;

        #endregion

        #region Lifecycle

        public void Initialize(
            AnimationEngineConfig config,
            EventBus eventBus,
            Profiler profiler,
            IJobSystem jobSystem,
            ResourceManager resourceManager)
        {
            var dummyConfig = new DummyAnimationEngineConfig
            {
                MaxPlaybacks = config.MaxActivePlaybacks,
                MaxPoses = config.MaxPoses,
                MaxAnimatedEntities = config.MaxAnimatedEntities,
                MaxIKChains = config.MaxIKChains,
                EnableEvents = config.EnableEvents,
                EnableMetrics = config.EnableMetrics,
                EnableDeterministicMode = config.EnableDeterministicMode,
                EnableParallelEvaluation = config.EnableParallelEvaluation,
                EnableRootMotion = config.EnableRootMotion,
                EnableIK = config.EnableIK,
                FixedDeltaTime = config.FixedDeltaTime,
                MetricsHistorySize = config.MetricsHistorySize,
                EnableDiagnostics = false,
                EnableCallTracking = false,
                EnableGhostSkeletonMode = false, // [AJOUT 7️⃣]
                EnableStrictValidation = false, // [AJOUT 3️⃣]
                EnableFastPath = false, // [AJOUT 1️⃣1️⃣]
                Version = config.Version
            };
            Initialize(dummyConfig);
        }

        public async Task InitializeAsync(
            AnimationEngineConfig config,
            EventBus eventBus,
            Profiler profiler,
            IJobSystem jobSystem,
            ResourceManager resourceManager)
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
            Initialize(config, eventBus, profiler, jobSystem, resourceManager);
        }

        public void Initialize(DummyAnimationEngineConfig config = default)
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Uninitialized, DummyAnimationEngineState.Initializing) &&
                !TryTransitionState(DummyAnimationEngineState.Shutdown, DummyAnimationEngineState.Initializing))
            {
                throw new InvalidOperationException($"Cannot initialize DummyAnimationEngine from state '{State}'.");
            }

            lock (_sync)
            {
                _config = config.Equals(default(DummyAnimationEngineConfig)) ? DummyAnimationEngineConfig.Default : config;

                if (_config.MaxPlaybacks <= 0 || _config.MaxPoses <= 0) // [CORRECTION] Validation
                {
                    throw new ArgumentException("Configuration values must be positive.");
                }

                ClearRuntimeLocked();

                _capabilities = DummyCapabilities.Static; // [CORRECTION] Toujours statique

                _metrics = new DummyMetrics
                {
                    FrameIndex = 0,
                    Timestamp = DateTime.UtcNow
                };

                _timeDilation = 1.0f;
                _globalSpeed = 1.0f;
                _frameIndex = 0;

                // [AJOUT 3️⃣] Initialisation du mode strict
                _validationErrors = 0;
                _lastEventName = string.Empty;
                _lastError = string.Empty;

                // [AJOUT 6️⃣] Initialisation du mode ghost
                _ghostSkeletonMode = _config.EnableGhostSkeletonMode;

                // [AJOUT 1️⃣1️⃣] Initialisation du fast path
                _fastPath = _config.EnableFastPath;

                // [AJOUT 4️⃣] Initialisation du recorder
                _recording = false;
                _recordedCalls.Clear();

                // [AJOUT 8️⃣] ThreadAffinity
                _threadId = Environment.CurrentManagedThreadId;

                // [AJOUT 9️⃣] ModeSwitcher
                _mode = DummyAnimationEngineMode.Headless;

                // [AJOUT 1️⃣0️⃣] FeatureMask
                _features = DummyAnimationEngineFeatures.None;
                if (_config.EnableDiagnostics) _features |= DummyAnimationEngineFeatures.Diagnostics;
                if (_config.EnableCallTracking) _features |= DummyAnimationEngineFeatures.CallTracking;
                if (_config.EnableGhostSkeletonMode) _features |= DummyAnimationEngineFeatures.GhostSkeleton;
                if (_config.EnableStrictValidation) _features |= DummyAnimationEngineFeatures.StrictValidation;
                if (_config.EnableFastPath) _features |= DummyAnimationEngineFeatures.FastPath;
                if (_replayMode) _features |= DummyAnimationEngineFeatures.ReplayMode;
            }

            Volatile.Write(ref _stateAsInt, (int)DummyAnimationEngineState.Ready);

            if (_config.EnableCallTracking) LogCall(nameof(Initialize)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineInitialized", new
            {
                State = State,
                Config = _config,
                Capabilities = _capabilities
            });
        }

        public async Task InitializeAsync(DummyAnimationEngineConfig config = default)
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
            Initialize(config);
        }

        public void Shutdown()
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Ready, DummyAnimationEngineState.ShuttingDown) &&
                !TryTransitionState(DummyAnimationEngineState.Running, DummyAnimationEngineState.ShuttingDown) &&
                !TryTransitionState(DummyAnimationEngineState.Paused, DummyAnimationEngineState.ShuttingDown) &&
                !TryTransitionState(DummyAnimationEngineState.Degraded, DummyAnimationEngineState.ShuttingDown) &&
                !TryTransitionState(DummyAnimationEngineState.Error, DummyAnimationEngineState.ShuttingDown))
            {
                return;
            }

            lock (_sync)
            {
                ClearRuntimeLocked();
            }

            Volatile.Write(ref _stateAsInt, (int)DummyAnimationEngineState.Shutdown);

            if (_config.EnableCallTracking) LogCall(nameof(Shutdown)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineShutdown", new
            {
                State = State
            });
        }

        public async Task ShutdownAsync()
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
            Shutdown();
        }

        public void Restart(DummyAnimationEngineConfig config = default)
        {
            ThrowIfDisposed();

            if (_config.EnableCallTracking) LogCall(nameof(Restart), config); // [CORRECTION] Tracking
            Shutdown();
            Initialize(config);
        }

        public void Reset()
        {
            ThrowIfDisposed();

            if (!IsReady)
                return;

            lock (_sync)
            {
                ClearRuntimeLocked();
                _metrics = new DummyMetrics
                {
                    FrameIndex = 0,
                    Timestamp = DateTime.UtcNow
                };
                _frameIndex = 0;
            }

            if (_config.EnableCallTracking) LogCall(nameof(Reset)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineReset", null);
        }

        public void WarmupPhase()
        {
            ThrowIfDisposed();

            if (!IsReady)
                return;

            if (_config.EnableCallTracking) LogCall(nameof(WarmupPhase)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineWarmup", null);
        }

        public void CooldownPhase()
        {
            ThrowIfDisposed();

            if (!IsReady)
                return;

            if (_config.EnableCallTracking) LogCall(nameof(CooldownPhase)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineCooldown", null);
        }

        public void Suspend()
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Running, DummyAnimationEngineState.Paused))
                return;

            if (_config.EnableCallTracking) LogCall(nameof(Suspend)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineSuspended", null);
        }

        public void Resume()
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Paused, DummyAnimationEngineState.Running))
                return;

            if (_config.EnableCallTracking) LogCall(nameof(Resume)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineResumed", null);
        }

        public void EnterDegradedMode(string reason)
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Running, DummyAnimationEngineState.Degraded) &&
                !TryTransitionState(DummyAnimationEngineState.Ready, DummyAnimationEngineState.Degraded))
            {
                return;
            }

            if (_config.EnableCallTracking) LogCall(nameof(EnterDegradedMode), reason); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineDegraded", new
            {
                Reason = reason
            });
        }

        public void TryRecover()
        {
            ThrowIfDisposed();

            if (!TryTransitionState(DummyAnimationEngineState.Degraded, DummyAnimationEngineState.Recovering))
                return;

            if (_config.EnableCallTracking) LogCall(nameof(TryRecover)); // [CORRECTION] Tracking
            SafeRaiseEvent("AnimationEngineRecovering", null);

            Volatile.Write(ref _stateAsInt, (int)DummyAnimationEngineState.Running);

            SafeRaiseEvent("AnimationEngineRecovered", null);
        }

        public DummyAnimationEngineState GetState() => State;

        // `public bool IsReady() => IsReady;` retiree : elle portait le nom de la
        // propriete declaree ligne 810, d'ou le CS0102 — et personne ne l'appelait,
        // les quatre sites du fichier lisent la propriete. Ecrite telle quelle elle
        // n'aurait de toute facon pas pu se compiler.

        #endregion

        #region [CORRECTION] Subsystems

        public void RegisterSubsystem(IAnimationSubsystem subsystem)
        {
            ThrowIfDisposed();

            if (subsystem == null)
                return;

            lock (_sync)
            {
                if (_subsystems.ContainsKey(subsystem.Type))
                {
                    return; // [CORRECTION] Empêche doublon
                }

                _subsystems[subsystem.Type] = subsystem;

                if (_config.EnableCallTracking) LogCall(nameof(RegisterSubsystem), subsystem.Type); // [CORRECTION] Tracking
                SafeRaiseEvent("AnimationSubsystemRegistered", new { Type = subsystem.Type });
            }
        }

        public void RemoveSubsystem(AnimationSubsystemType type)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_subsystems.Remove(type))
                {
                    if (_config.EnableCallTracking) LogCall(nameof(RemoveSubsystem), type); // [CORRECTION] Tracking
                    SafeRaiseEvent("AnimationSubsystemRemoved", new { Type = type });
                }
            }
        }

        public IAnimationSubsystem GetSubsystem(AnimationSubsystemType type)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_subsystems.TryGetValue(type, out var sub))
                {
                    return sub;
                }

                // [CORRECTION] Retourne un dummy si introuvable
                var dummy = new DummyAnimationSubsystem { Type = type };
                _subsystems[type] = dummy;
                return dummy;
            }
        }

        public bool TryGetSubsystem(AnimationSubsystemType type, out IAnimationSubsystem subsystem)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_subsystems.TryGetValue(type, out var sub))
                {
                    subsystem = sub;
                    return true;
                }
            }
            subsystem = null;
            return false;
        }

        public void ValidateSubsystemIntegrity()
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne toujours vrai
        }

        #endregion

        #region [CORRECTION] Context

        public AnimationEngineContext GetContext()
        {
            ThrowIfDisposed();

            return new DummyAnimationEngineContext();
        }

        public void SetContext(AnimationEngineContext context)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void ReloadContext()
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        #endregion

        #region [CORRECTION] Subsystem Initialization

        public void InitializeSubsystems()
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public async Task InitializeSubsystemsAsync()
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
        }

        public void ReloadSubsystems()
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public async Task ReloadSubsystemsAsync()
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
        }

        public AnimationSubsystemStatus GetSubsystemStatus(AnimationSubsystemType type)
        {
            ThrowIfDisposed();

            return new AnimationSubsystemStatus
            {
                Type = type,
                IsInitialized = true,
                IsRunning = true,
                HasErrors = false,
                LastUpdate = DateTime.UtcNow
            };
        }

        public AnimationSubsystemDependency[] GetSubsystemDependencies(AnimationSubsystemType type)
        {
            ThrowIfDisposed();

            return Array.Empty<AnimationSubsystemDependency>();
        }

        public AnimationSubsystemType[] GetSubsystemLoadOrder()
        {
            ThrowIfDisposed();

            return Array.Empty<AnimationSubsystemType>();
        }

        public AnimationSubsystemType[] GetSubsystemInitializationOrder()
        {
            ThrowIfDisposed();

            return Array.Empty<AnimationSubsystemType>();
        }

        #endregion

        #region [CORRECTION] Frame Update

        public void Update(float deltaTime)
        {
            ThrowIfDisposed();

            if (!IsReady)
                return;

            if (State == DummyAnimationEngineState.Ready)
            {
                Volatile.Write(ref _stateAsInt, (int)DummyAnimationEngineState.Running);
            }

            if (State != DummyAnimationEngineState.Running)
                return;

            // [AJOUT 8️⃣] ThreadAffinity
            if (_config.EnableStrictValidation && Environment.CurrentManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("DummyAnimationEngine used from wrong thread.");
            }

            // [CORRECTION] No-op complet
            if (_config.EnableCallTracking) LogCall(nameof(Update), deltaTime); // [CORRECTION] Tracking

            // [AJOUT 1️⃣2️⃣] Mise à jour du hash de frame
            UpdateFrameHash();
            _frameIndex++;

            // [AJOUT 1️⃣2️⃣] Mise à jour du memory tracker
            Interlocked.Increment(ref _allocCount);

            // [AJOUT 7️⃣] TimeSource
            _currentTime += deltaTime;
            _deltaTime = deltaTime;
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            ThrowIfDisposed();

            if (_config.EnableCallTracking) LogCall(nameof(FixedUpdate), fixedDeltaTime); // [CORRECTION] Tracking
        }

        public void LateUpdate(float deltaTime)
        {
            ThrowIfDisposed();

            if (_config.EnableCallTracking) LogCall(nameof(LateUpdate), deltaTime); // [CORRECTION] Tracking
        }

        #endregion

        #region [CORRECTION] Time Control

        public void SetTimeDilation(float dilation)
        {
            ThrowIfDisposed();

            dilation = Math.Clamp(dilation, 0f, 10f);
            Interlocked.Exchange(ref _timeDilation, dilation);

            if (_config.EnableCallTracking) LogCall(nameof(SetTimeDilation), dilation); // [CORRECTION] Tracking
        }

        public float GetTimeDilation()
        {
            ThrowIfDisposed();

            return Volatile.Read(ref _timeDilation);
        }

        public void SetGlobalSpeed(float speed)
        {
            ThrowIfDisposed();

            speed = Math.Clamp(speed, 0f, 10f);
            Interlocked.Exchange(ref _globalSpeed, speed);

            if (_config.EnableCallTracking) LogCall(nameof(SetGlobalSpeed), speed); // [CORRECTION] Tracking
        }

        public float GetGlobalSpeed()
        {
            ThrowIfDisposed();

            return Volatile.Read(ref _globalSpeed);
        }

        public void SetUpdateMode(AnimationUpdateMode mode)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
            if (_config.EnableCallTracking) LogCall(nameof(SetUpdateMode), mode); // [CORRECTION] Tracking
        }

        public AnimationUpdateMode GetUpdateMode()
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne une valeur constante
            return AnimationUpdateMode.VariableUpdate;
        }

        #endregion

        #region [CORRECTION] Skeleton, Pose, Clip (no-op)

        public SkeletonHandle RegisterSkeleton(SkeletonInfo skeleton)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                var handle = new SkeletonHandle(NextIdLocked());

                var runtime = new SkeletonRuntime
                {
                    Handle = handle,
                    Name = skeleton.Name,
                    Bones = new BoneInfo[skeleton.BoneCount].Select(b => new BoneDefinition()).ToArray(), // [CORRECTION] Conversion
                    RootBoneIndex = skeleton.RootBoneIndex
                };

                _skeletons.Add(handle, runtime);

                if (_config.EnableCallTracking) LogCall(nameof(RegisterSkeleton), skeleton.Name); // [CORRECTION] Tracking
                return handle;
            }
        }

        public void UnregisterSkeleton(SkeletonHandle skeleton)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                _skeletons.Remove(skeleton);

                if (_config.EnableCallTracking) LogCall(nameof(UnregisterSkeleton), skeleton); // [CORRECTION] Tracking
            }
        }

        public SkeletonInfo GetSkeletonInfo(SkeletonHandle skeleton)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_skeletons.TryGetValue(skeleton, out var runtime))
                {
                    return new SkeletonInfo
                    {
                        Name = runtime.Name,
                        Bones = runtime.Bones.Select(b => new BoneInfo()).ToArray(), // [CORRECTION] Conversion
                        RootBoneIndex = runtime.RootBoneIndex
                    };
                }
            }
            return default;
        }

        public int GetBoneIndex(SkeletonHandle skeleton, string boneName)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_skeletons.TryGetValue(skeleton, out var runtime))
                {
                    for (int i = 0; i < runtime.Bones.Length; i++)
                    {
                        if (runtime.Bones[i].Name == boneName)
                            return i;
                    }
                }
            }
            return -1;
        }

        public string GetBoneName(SkeletonHandle skeleton, int boneIndex)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_skeletons.TryGetValue(skeleton, out var runtime))
                {
                    if (boneIndex >= 0 && boneIndex < runtime.Bones.Length)
                        return runtime.Bones[boneIndex].Name;
                }
            }
            return null;
        }

        public PoseHandle CreatePose(SkeletonHandle skeleton)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                var handle = new PoseHandle(NextIdLocked());

                var runtime = new PoseRuntime
                {
                    Handle = handle,
                    Skeleton = skeleton,
                    Transforms = Array.Empty<BoneTransform>(), // [CORRECTION] Retourne vide
                    Version = 0,
                    IsDirty = true,
                    IsValid = true
                };

                _poses.Add(handle, runtime);

                if (_config.EnableCallTracking) LogCall(nameof(CreatePose), skeleton); // [CORRECTION] Tracking
                return handle;
            }
        }

        public void ReleasePose(PoseHandle pose)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_poses.TryGetValue(pose, out var runtime))
                {
                    runtime.IsValid = false; // [CORRECTION] Marque comme invalide
                }
                _poses.Remove(pose);

                if (_config.EnableCallTracking) LogCall(nameof(ReleasePose), pose); // [CORRECTION] Tracking
            }
        }

        public void ResetPose(PoseHandle pose)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_poses.TryGetValue(pose, out var runtime))
                {
                    // [CORRECTION] Ignore silencieusement
                }
            }
        }

        public void CopyPose(PoseHandle source, PoseHandle destination)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Ignore silencieusement
            }
        }

        public void BlendPoses(PoseHandle sourceA, PoseHandle sourceB, float weightB, PoseHandle output, PoseBlendMode mode = PoseBlendMode.Lerp)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Ignore silencieusement
            }
        }

        public void AdditiveBlend(PoseHandle basePose, PoseHandle additivePose, float weight, PoseHandle output)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Ignore silencieusement
            }
        }

        public void ApplyBoneOverrides(PoseHandle pose, IReadOnlyList<BoneOverride> overrides)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Ignore silencieusement
            }
        }

        public BoneTransform GetBoneTransform(PoseHandle pose, int boneIndex, BoneTransformSpace space = BoneTransformSpace.Local)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Retourne identity
                return BoneTransform.Identity;
            }
        }

        public void SetBoneTransform(PoseHandle pose, int boneIndex, BoneTransform transform, BoneTransformSpace space = BoneTransformSpace.Local)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Ignore silencieusement
            }
        }

        public bool TryGetPoseTransforms(PoseHandle pose, BoneTransform[] output)
        {
            ThrowIfDisposed();

            if (!pose.IsValid)
            {
                // [AJOUT 1.1] Utilisation de NullPose
                output = NullPose;
                return false;
            }

            lock (_sync)
            {
                if (!_poses.TryGetValue(pose, out var runtime))
                {
                    output = NullPose;
                    return false;
                }

                if (output == null || output.Length != runtime.Transforms.Length)
                {
                    output = NullPose;
                    return false;
                }

                Array.Copy(runtime.Transforms, output, runtime.Transforms.Length);
                return true;
            }
        }

        public IPoseBuffer GetPoseBuffer(PoseHandle pose)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un buffer vide
            return null;
        }

        public bool ValidatePose(PoseHandle pose, SkeletonHandle skeleton)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                // [CORRECTION] Retourne vrai
                return true;
            }
        }

        public IAnimationClip LoadClip(string path, AnimationStreamingPriority priority = AnimationStreamingPriority.Normal)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un clip dummy
            return new DummyAnimationClip(path);
        }

        public async Task<IAnimationClip> LoadClipAsync(string path, AnimationStreamingPriority priority = AnimationStreamingPriority.Normal)
        {
            // [CORRECTION] No-op asynchrone
            await Task.CompletedTask;
            return LoadClip(path, priority);
        }

        public void UnloadClip(IAnimationClip clip)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void RegisterClip(IAnimationClip clip)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public IAnimationClip GetLoadedClip(string name)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un clip dummy
            return new DummyAnimationClip(name);
        }

        public void SetClipStreamingPriority(IAnimationClip clip, AnimationStreamingPriority priority)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        #endregion

        #region [CORRECTION] Playback (no-op)

        public IAnimationPlayback PlayClip(IAnimationClip clip, PlaybackOptions options = default)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un playback dummy
            return new DummyAnimationPlayback();
        }

        public IAnimationPlayback CrossfadeToClip(PlaybackHandle current, IAnimationClip nextClip, float crossfadeDuration, PlaybackOptions options = default)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un nouveau playback dummy
            return new DummyAnimationPlayback();
        }

        public void StopPlayback(PlaybackHandle handle, float fadeOutDuration = 0f)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void StopAllPlaybacks(float fadeOutDuration = 0f)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void PauseAllPlaybacks()
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void ResumeAllPlaybacks()
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void SetPlaybackSpeed(PlaybackHandle handle, float speed)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void SetPlaybackWeight(PlaybackHandle handle, float weight)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void SetPlaybackTime(PlaybackHandle handle, float time, bool fireEvents = true)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public AnimationPlaybackState GetPlaybackState(PlaybackHandle handle)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne Playing
            return AnimationPlaybackState.Playing;
        }

        public IAnimationPlayback GetPlayback(PlaybackHandle handle)
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne un playback dummy
            return new DummyAnimationPlayback();
        }

        public IReadOnlyList<IAnimationPlayback> GetActivePlaybacks()
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne une liste vide
            return ArraySegment<IAnimationPlayback>.Empty;
        }

        #endregion

        #region [CORRECTION] Parameters (no-op)

        public void SetFloatParameter(string name, float value)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_config.EnableCallTracking) LogCall(nameof(SetFloatParameter), name, value); // [CORRECTION] Tracking
                _floatParameters[name] = value;
            }
        }

        public void SetIntParameter(string name, int value)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_config.EnableCallTracking) LogCall(nameof(SetIntParameter), name, value); // [CORRECTION] Tracking
                _intParameters[name] = value;
            }
        }

        public void SetBoolParameter(string name, bool value)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_config.EnableCallTracking) LogCall(nameof(SetBoolParameter), name, value); // [CORRECTION] Tracking
                _boolParameters[name] = value;
            }
        }

        public void SetTriggerParameter(string name)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_config.EnableCallTracking) LogCall(nameof(SetTriggerParameter), name); // [CORRECTION] Tracking
                _triggerParameters[name] = true;
            }
        }

        public void ResetTriggerParameter(string name)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_config.EnableCallTracking) LogCall(nameof(ResetTriggerParameter), name); // [CORRECTION] Tracking
                _triggerParameters[name] = false;
            }
        }

        public bool TryGetFloatParameter(string name, out float value)
        {
            ThrowIfDisposed();

            value = 0f;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_sync)
            {
                return _floatParameters.TryGetValue(name, out value);
            }
        }

        public bool TryGetIntParameter(string name, out int value)
        {
            ThrowIfDisposed();

            value = 0;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_sync)
            {
                return _intParameters.TryGetValue(name, out value);
            }
        }

        public bool TryGetBoolParameter(string name, out bool value)
        {
            ThrowIfDisposed();

            value = false;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_sync)
            {
                return _boolParameters.TryGetValue(name, out value);
            }
        }

        #endregion

        #region [CORRECTION] LOD, Culling, Rendering (no-op)

        public LODLevel CalculateLOD(Vector3 worldPosition, Vector3 cameraPosition, LODStrategy strategy = LODStrategy.DistanceBased)
        {
            ThrowIfDisposed();

            if (_config.EnableCallTracking) LogCall(nameof(CalculateLOD), worldPosition, cameraPosition, strategy); // [CORRECTION] Tracking
            Interlocked.Increment(ref _calculateLODCallCount); // [AJOUT 2️⃣] Compteur
            return LODLevel.Level0; // [CORRECTION] Toujours Level0
        }

        public void SetEntityPose(AnimatedEntityHandle entity, PoseHandle pose)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void SetEntityVisible(AnimatedEntityHandle entity, bool visible)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void SetEntityDistance(AnimatedEntityHandle entity, float distance)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void SetEntityVelocity(AnimatedEntityHandle entity, Vector3 velocity)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void SetEntityWorldTransform(AnimatedEntityHandle entity, Vector3 position, Quaternion rotation)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void GetEntityWorldTransform(AnimatedEntityHandle entity, out Vector3 position, out Quaternion rotation)
        {
            ThrowIfDisposed();

            position = Vector3.Zero;
            rotation = Quaternion.Identity;
        }

        public AnimatedEntityInfo GetEntityInfo(AnimatedEntityHandle entity)
        {
            ThrowIfDisposed();

            return new AnimatedEntityInfo
            {
                Handle = entity,
                Visible = true,
                Distance = 0f
            };
        }

        public IReadOnlyList<AnimatedEntityHandle> GetActiveEntities()
        {
            ThrowIfDisposed();

            return ArraySegment<AnimatedEntityHandle>.Empty;
        }

        public void EvaluateEntity(AnimatedEntityHandle entity, float deltaTime)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void EvaluateEntityWithOptions(AnimatedEntityHandle entity, AnimationEvaluationOptions options)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        public void EvaluateAllEntities(float deltaTime)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        #endregion

        #region [AJOUT 2️⃣] Hooks pour le rendu (no-op)

        public void ApplyPoseToSkinnedMesh(IMesh mesh, MaterialHandle material, Matrix4x4[] boneMatrices)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
            Interlocked.Increment(ref _applyPoseCallCount); // [AJOUT 2️⃣] Compteur
        }

        public void SetGPUSkinningEnabled(bool enable)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        // [AJOUT 7] Hooks pour le rendu (no-op)
        public void NotifyRenderEnginePoseApplied(SkeletonPoseHandle pose)
        {
            ThrowIfDisposed();

            Interlocked.Increment(ref _applyPoseCallCount);
        }

        public void NotifyRenderEngineLODCalculated(int lod)
        {
            ThrowIfDisposed();

            Interlocked.Increment(ref _calculateLODCallCount);
        }

        #endregion

        #region [CORRECTION] Metrics

        public AnimationEngineCapabilities GetCapabilities()
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne des capacités statiques adaptées au dummy
            return new AnimationEngineCapabilities
            {
                SupportsBlendTrees = true,
                SupportsStateMachines = true,
                SupportsIK = true,
                SupportsRootMotion = true,
                SupportsAdditiveBlending = true,
                SupportsBoneMasks = true,
                SupportsParallelEvaluation = false,
                SupportsGPUSkinning = false, // [CORRECTION] Jamais vrai
                SupportsStreaming = false,
                SupportsEvents = true,
                SupportsInterpolation = true,
                SupportsLOD = true,
                SupportsMetrics = true,
                MaxBones = 256,
                MaxPlaybacks = _config.MaxPlaybacks,
                MaxPoses = _config.MaxPoses,
                MaxAnimatedEntities = _config.MaxAnimatedEntities,
                MaxIKChains = _config.MaxIKChains,
                BackendName = "DummyAnimationBackend",
                BackendVersion = _config.Version
            };
        }

        public AnimationEngineMetrics GetMetrics()
        {
            ThrowIfDisposed();

            // [CORRECTION] Retourne des métriques factices
            return new AnimationEngineMetrics
            {
                ActivePlaybacks = 0,
                ActivePoses = 0,
                ActiveEntities = 0,
                ActiveIKChains = 0,
                EvaluatedBones = 0,
                EventsFired = 0,
                CpuUpdateMs = 0f,
                MemoryUsedMB = 0f,
                Timestamp = DateTime.UtcNow
            };
        }

        public AnimationEngineMetricsHistory GetMetricsHistory(TimeSpan duration)
        {
            ThrowIfDisposed();

            return new AnimationEngineMetricsHistory
            {
                Metrics = new List<AnimationEngineMetrics>(),
                WindowDuration = duration
            };
        }

        public void ExportMetricsToFile(string filePath, AnimationMetricsExportFormat format)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void EnableMetricsVisualization(bool enabled)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void EnableTelemetry(bool enabled)
        {
            ThrowIfDisposed();

            _telemetryEnabled = enabled;
        }

        public void SendAnalyticsEvent(string eventName, Dictionary<string, object> properties)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        #endregion

        #region [CORRECTION] Debug / Editor

        public void SetDebugOverlayEnabled(bool enabled)
        {
            ThrowIfDisposed();

            // [CORRECTION] Ignore silencieusement
        }

        public void DrawDebugPose(PoseHandle pose, bool showBones = true, bool showIK = true)
        {
            ThrowIfDisposed();

            // [CORRECTION] No-op
        }

        #endregion

        #region Pont d'animation (IAnimationEngine)

        private readonly List<IAnimationBridge> _bridges = new List<IAnimationBridge>();

        public void InitializeBridge(IAnimationBridge bridge)
        {
            ThrowIfDisposed();

            if (bridge == null)
                return;

            lock (_sync)
            {
                if (!_bridges.Contains(bridge))
                    _bridges.Add(bridge);
            }
        }

        public void ShutdownBridge(IAnimationBridge bridge)
        {
            ThrowIfDisposed();

            if (bridge == null)
                return;

            lock (_sync)
            {
                _bridges.Remove(bridge);
            }
        }

        // Vector2 est qualifie : Snake2000.Engine.Core declare le sien, et le
        // contrat de IAnimationEngine porte celui de System.Numerics.
        public System.Numerics.Vector2 ExtractRootMotionDelta(Snake2000.Engine.Core.Entity entity)
        {
            ThrowIfDisposed();

            // Le moteur muet ne produit aucun deplacement racine.
            return System.Numerics.Vector2.Zero;
        }

        #endregion

        #region [CORRECTION] IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            Shutdown();

            lock (_sync)
            {
                _skeletons.Clear();
                _clips.Clear();
                _poses.Clear();
                _playbacks.Clear();
                _entities.Clear();
                _ikChains.Clear();
                _playbackPool.Clear();
                _metricsHistory.Clear();
                _subsystems.Clear();
                _bridges.Clear();
                _callLog.Clear();
            }

            _disposed = true;
        }

        #endregion

        #region [AJOUTS 1️⃣-5️⃣]

        // 1️⃣ DummyAnimationEngineReport
        public struct DummyAnimationEngineReport
        {
            public DummyAnimationEngineState State;
            public DummyAnimationEngineMode Mode;
            public DummyAnimationEngineFeatures Features;
            public DummyMetrics Metrics;
            public bool IntegrityValid;
            public string LastError;
            public string LastEvent;
            public (int alloc, int release) MemoryStats;
            public int CallCount;
        }

        public DummyAnimationEngineReport GetReport()
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                return new DummyAnimationEngineReport
                {
                    State = State,
                    Mode = _mode,
                    Features = _features,
                    Metrics = _metrics,
                    IntegrityValid = ValidateDummyIntegrity(),
                    LastError = _lastError,
                    LastEvent = _lastEventName,
                    MemoryStats = (Volatile.Read(ref _allocCount), Volatile.Read(ref _releaseCount)),
                    CallCount = _callLog.Count
                };
            }
        }

        // 2️⃣ ValidateDummyIntegrity()
        public bool ValidateDummyIntegrity()
        {
            ThrowIfDisposed();

            if (_disposed) return false;
            if (_stateAsInt < 0 || _stateAsInt > (int)DummyAnimationEngineState.Shutdown) return false;
            if (_config.MaxPlaybacks < 1) return false;
            if (_config.MaxPoses < 1) return false;
            if (_skeletons.Count < 0) return false;
            if (_poses.Count < 0) return false;
            if (_playbacks.Count < 0) return false;
            if (_entities.Count < 0) return false;
            if (_ikChains.Count < 0) return false;
            if (_allocCount < _releaseCount) return false;
            if (_frameIndex < 0) return false;

            return true;
        }

        // 3️⃣ DumpStateAsJson()
        public string DumpStateAsJson()
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                var report = new
                {
                    State = State.ToString(),
                    Mode = _mode.ToString(),
                    Features = _features.ToString(),
                    Metrics = _metrics,
                    IntegrityValid = ValidateDummyIntegrity(),
                    LastError = _lastError,
                    LastEvent = _lastEventName,
                    MemoryStats = new { Alloc = Volatile.Read(ref _allocCount), Release = Volatile.Read(ref _releaseCount) },
                    CallCount = _callLog.Count,
                    FrameIndex = _frameIndex,
                    FrameHash = _frameHash,
                    ActiveHandles = new
                    {
                        Skeletons = _skeletons.Count,
                        Poses = _poses.Count,
                        Playbacks = _playbacks.Count,
                        Entities = _entities.Count,
                        IKChains = _ikChains.Count
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                return JsonSerializer.Serialize(report, options);
            }
        }

        // 4️⃣ GoldenOutputComparer
        public bool CompareGoldenOutput(string key, object data)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (!_goldenOutputs.TryGetValue(key, out var golden)) return false;
                return golden.Equals(data);
            }
        }

        public void ExportGoldenOutput(string key, object data)
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                _goldenOutputs[key] = data;
            }
        }

        // 5️⃣ DummyAnimationEngineFactory
        public static class Factory
        {
            public static DummyAnimationEngine CreateHeadless()
            {
                return new DummyAnimationEngine
                {
                    _config = new DummyAnimationEngineConfig
                    {
                        EnableDiagnostics = false,
                        EnableCallTracking = false,
                        EnableStrictValidation = false,
                        EnableFastPath = true,
                        Version = "headless"
                    }
                };
            }

            public static DummyAnimationEngine CreateCI()
            {
                return new DummyAnimationEngine
                {
                    _config = new DummyAnimationEngineConfig
                    {
                        EnableDiagnostics = true,
                        EnableCallTracking = true,
                        EnableStrictValidation = true,
                        EnableFastPath = false,
                        Version = "ci"
                    }
                };
            }

            public static DummyAnimationEngine CreateBenchmark()
            {
                return new DummyAnimationEngine
                {
                    _config = new DummyAnimationEngineConfig
                    {
                        EnableDiagnostics = false,
                        EnableCallTracking = false,
                        EnableStrictValidation = false,
                        EnableFastPath = true,
                        Version = "benchmark"
                    }
                };
            }

            public static DummyAnimationEngine CreateEditor()
            {
                return new DummyAnimationEngine
                {
                    _config = new DummyAnimationEngineConfig
                    {
                        EnableDiagnostics = true,
                        EnableCallTracking = true,
                        EnableStrictValidation = false,
                        EnableFastPath = false,
                        Version = "editor"
                    }
                };
            }

            public static DummyAnimationEngine CreateReplay()
            {
                return new DummyAnimationEngine
                {
                    _config = new DummyAnimationEngineConfig
                    {
                        EnableDiagnostics = false,
                        EnableCallTracking = true,
                        EnableStrictValidation = false,
                        EnableFastPath = false,
                        Version = "replay"
                    }
                };
            }
        }

        #endregion

        #region [CORRECTION] Internal Helpers

        private bool TryTransitionState(DummyAnimationEngineState expected, DummyAnimationEngineState next)
        {
            return Interlocked.CompareExchange(
                ref _stateAsInt,
                (int)next,
                (int)expected) == (int)expected;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DummyAnimationEngine));
        }

        private uint NextIdLocked()
        {
            if (_nextId == uint.MaxValue)
                _nextId = 1;

            return _nextId++;
        }

        private void ClearRuntimeLocked()
        {
            _skeletons.Clear();
            _clips.Clear();
            _poses.Clear();
            _entities.Clear();
            _ikChains.Clear();

            foreach (var playback in _playbacks.Values)
            {
                ReleasePlaybackRuntimeLocked(playback);
            }

            _playbacks.Clear();

            _floatParameters.Clear();
            _intParameters.Clear();
            _boolParameters.Clear();
            _triggerParameters.Clear();

            Interlocked.Increment(ref _releaseCount); // [AJOUT 1️⃣2️⃣] MemoryTracker
        }

        private void ReleasePlaybackRuntimeLocked(PlaybackRuntime playback)
        {
            playback.Handle = default;
            playback.Clip = default;
            playback.TargetPose = default;
            playback.State = DummyPlaybackState.Stopped;
            playback.Time = 0f;
            playback.LastEventTime = 0f;
            playback.Speed = 1f;
            playback.Weight = 0f;
            playback.TargetWeight = 0f;
            playback.Layer = 0;
            playback.IsLooping = false;
            playback.IsAdditive = false;
            playback.BlendInDuration = 0f;
            playback.BlendOutDuration = 0f;
            playback.Priority = 0;
            playback.RootMotionAccumulator = Vector3.Zero;

            _playbackPool.Enqueue(playback);
        }

        private void LogCall(string methodName, params object[] args)
        {
            if (_config.EnableCallTracking)
            {
                _callLog.Enqueue((methodName, args, DateTime.UtcNow));
            }

            if (_recording)
            {
                _recordedCalls.Add((methodName, args, DateTime.UtcNow));
            }
        }

        private void SafeRaiseEvent(string name, object payload)
        {
            if (_fastPath) return; // [AJOUT 1️⃣1️⃣] FastPath

            try
            {
                var args = new DummyAnimationEventArgs(
                    name,
                    payload,
                    Interlocked.Increment(ref _eventSequenceCounter)
                );

                _eventHistory.Enqueue(args); // [AJOUT 8️⃣] EventHistory
                if (_eventHistory.Count > 100) _eventHistory.Dequeue(); // Limite la taille

                _lastEventName = name; // [AJOUT 4️⃣] Dernier événement

                EventRaised?.Invoke(this, args);
            }
            catch
            {
                // no-op : jamais d’exception dans un moteur dummy
            }
        }

        private void UpdateFrameHash()
        {
            unchecked
            {
                _frameHash = (uint)(
                    (_frameHash * 397) ^
                    _frameIndex ^
                    _metrics.ActivePlaybacks ^
                    _metrics.ActiveEntities
                );
            }
        }

        #endregion
    }

    #endregion

    #region [CORRECTION] Classes Dummy

    public class DummyAnimationClip : IAnimationClip
    {
        public string Name { get; }
        public string Path { get; }
        public float Duration => 1.0f;
        public float SampleRate => 30f;
        public int FrameCount => (int)(Duration * SampleRate);
        public bool IsLooping { get; set; } = false;
        public bool IsLoaded => true;
        public bool IsStreamed => false;
        public bool HasRootMotion => false;
        public RootMotionMode RootMotionMode => RootMotionMode.None;
        public float MemoryMB => 0f;
        public IReadOnlyList<AnimationEventMarker> Events => Array.Empty<AnimationEventMarker>();

        public DummyAnimationClip(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public Task LoadAsync() => Task.CompletedTask;
        public void Unload() { }
    }

    public class DummyAnimationPlayback : IAnimationPlayback
    {
        public PlaybackHandle Handle { get; } = new PlaybackHandle(1);
        public IAnimationClip Clip { get; } = new DummyAnimationClip("dummy");
        public AnimationPlaybackState State => AnimationPlaybackState.Playing;
        public float Time => 0f;
        public float NormalizedTime => 0f;
        public float Speed { get; set; } = 1.0f;
        public float Weight { get; set; } = 1.0f;
        public int Layer { get; } = 0;
        public bool IsLooping { get; set; } = false;
        public bool IsAdditive { get; } = false;
        public bool IsFading => false;
        public float BlendInDuration { get; set; } = 0f;
        public float BlendOutDuration { get; set; } = 0f;
        public int Priority { get; set; } = 64;
        public bool IsFinished => false;

        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Restart() { }
        public void SetTime(float time, bool fireEvents = true) { }
        public void AddTime(float deltaTime) { }
        public void SetWeight(float weight) { }
        public void SetSpeed(float speed) { }
        public void FadeIn(float duration) { }
        public void FadeOut(float duration) { }
        public RootMotionSample ConsumeRootMotion() => RootMotionSample.Identity;
        public IReadOnlyList<AnimationEventMarker> GetPendingEvents() => Array.Empty<AnimationEventMarker>();
        public bool BelongsToLayer(int layerIndex) => Layer == layerIndex;
        public void Dispose() { }
    }

    #endregion
}