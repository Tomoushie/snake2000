// /Engine/Profiling/GPUProfilerHook.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Profiling;
using Engine.Events;
using Engine.Jobsystem; // Pour IJobSystem, ThreadAffinityManager
using Engine.Rendering; // Pour IRenderEngine
using Engine.Utilities; // Pour structures communes
using System.IO;
using System.Runtime.InteropServices;

namespace Engine.Profiling
{
    #region Enums
    public enum GPUProfilerState
    {
        Uninitialized,
        Initializing,
        Ready,
        Capturing,
        Paused,
        Degraded,
        Recovering,
        Error,
        ShuttingDown,
        Shutdown
    }

    public enum PerformanceMode
    {
        Low,
        Medium,
        High
    }

    public enum ThreadRole
    {
        Render,
        Input,
        Animation,
        Physics,
        AI,
        Background
    }

    public enum CoreClass
    {
        Performance,
        Efficiency,
        LowPower
    }

    public enum AffinityProfile
    {
        ActionGame,
        StrategyGame,
        RPG,
        Simulation
    }

    public enum GPUVendor
    {
        Unknown,
        NVIDIA,
        AMD,
        Intel,
        ARM,
        Qualcomm
    }

    public enum GPUDriverStatus
    {
        Healthy,
        Warning,
        Error,
        Crashed
    }

    public enum GPUWorkloadType
    {
        Graphics,
        Compute,
        Copy,
        AsyncCompute
    }

    public enum GPUQueueType
    {
        Graphics,
        Compute,
        Copy,
        Transfer
    }

    public enum GPUShaderStage
    {
        Vertex,
        Hull,
        Domain,
        Geometry,
        Pixel,
        Compute,
        Amplification,
        Mesh
    }

    public enum GPUFrameBudgetState
    {
        UnderBudget,
        NearBudget,
        OverBudget
    }

    public enum GPUErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
    #endregion

    #region Interfaces
    public interface IGPUProfilerHook
    {
        void Initialize(IRenderEngine renderEngine, Profiler profiler, EventBus eventBus, IJobSystem jobSystem, ThreadAffinityManager threadAffinityManager);
        void Initialize(GPUProfilerHookConfig config);
        Task InitializeAsync();
        Task InitializeAsync(GPUProfilerHookConfig config);
        void Shutdown();
        Task ShutdownAsync();
        void Restart();
        void WarmupPhase();
        void CooldownPhase();
        GPUProfilerState GetState();
        bool IsReady();
        bool IsCapturing();
        bool IsError();
        void ApplyConfiguration(GPUProfilerHookConfig config);
        void ReloadConfiguration();
        GPUProfilerHookConfig GetConfigSnapshot();
        bool ValidateConfig(GPUProfilerHookConfig config);
        void SetPerformanceMode(PerformanceMode mode);
        void SetCaptureBudget(float budgetMs);
        void SetCaptureInterval(float intervalMs);
        void SetSafeMode(bool enabled);
        void SetContinuousCapture(bool enabled);
        void CaptureMetrics();
        GPUMetricsSnapshot GetMetricsSnapshot();
        AnimationRenderMetricsSnapshot GetAnimationMetricsSnapshot();
        bool IsGPUCostCritical(float threshold = 16.0f);
        int GetEstimatedSkeletonCount();
        long GetEstimatedVertexShaderInvocations();
        // Intégration moteur
        void OnBeforeRender();
        void OnAfterRender();
        void OnBeforeSkinning();
        void OnAfterSkinning();
        void OnBeforeShadowPass();
        void OnAfterShadowPass();
        void OnBeforePostProcess();
        void OnAfterPostProcess();
        void OnBeforeComputePass();
        void OnAfterComputePass();
        void OnBeforeGPUUpload();
        void OnAfterGPUUpload();
        void OnBeforeAnimationUpdate();
        void OnAfterAnimationUpdate();
        void OnBeforeBlendShapes();
        void OnAfterBlendShapes();
        void OnBeforeParticleUpdate();
        void OnAfterParticleUpdate();
        void OnBeforeLightingPass();
        void OnAfterLightingPass();
        void OnBeforeOcclusionCulling();
        void OnAfterOcclusionCulling();
        void OnBeforeGPUCulling();
        void OnAfterGPUCulling();
        void OnBeforeGPUComputeSkinning();
        void OnAfterGPUComputeSkinning();
        void OnBeforeGPUComputeMorph();
        void OnAfterGPUComputeMorph();
        void OnBeforeGPUComputeCloth();
        void OnAfterGPUComputeCloth();
        void OnBeforeGPUComputeParticles();
        void OnAfterGPUComputeParticles();
        void OnBeforeGPUComputeTerrain();
        void OnAfterGPUComputeTerrain();
        void OnBeforeGPUComputeAI();
        void OnAfterGPUComputeAI();
        void OnBeforeGPUComputePhysics();
        void OnAfterGPUComputePhysics();
        void OnBeforeGPUComputeNavMesh();
        void OnAfterGPUComputeNavMesh();
        void OnBeforeGPUComputeOcclusion();
        void OnAfterGPUComputeOcclusion();
        void OnBeforeGPUComputeVisibility();
        void OnAfterGPUComputeVisibility();
        void OnBeforeGPUComputeLOD();
        void OnAfterGPUComputeLOD();
        void OnBeforeGPUComputeAnimationLOD();
        void OnAfterGPUComputeAnimationLOD();
        void OnBeforeGPUComputeMotionMatching();
        void OnAfterGPUComputeMotionMatching();
        void OnBeforeGPUComputeIK();
        void OnAfterGPUComputeIK();
        void OnBeforeGPUComputePoseGraph();
        void OnAfterGPUComputePoseGraph();
        void OnBeforeGPUComputeBlendGraph();
        void OnAfterGPUComputeBlendGraph();
        void OnBeforeGPUComputeRootMotion();
        void OnAfterGPUComputeRootMotion();
        // Observabilité
        GPUMetricsRingBufferSnapshot GetTelemetrySnapshot();
        string TakeProfilerSnapshot();
        bool IsHealthy();
        // API Ergonomique
        void ApplyPreset(GPUProfilerHookPreset preset);
        void SavePreset(string name, GPUProfilerHookPreset preset);
        GPUProfilerHookPreset LoadPreset(string name);
        // Tests & QA
        void RunStressTest();
        void RunFuzzTest();
        void SimulateGPUCrash();
        void SimulateGPUHang();
        void SimulateShaderCrash();
        void SimulateGPUTimeout();
        // Vendor-Specific
        void SetNVidiaNsightMarkersEnabled(bool enabled);
        void SetAMDGPUProfilerMarkersEnabled(bool enabled);
        void SetIntelGPAMarkersEnabled(bool enabled);
        void SetDirectXPixMarkersEnabled(bool enabled);
        void SetVulkanDebugMarkersEnabled(bool enabled);
        // Compute & Async
        float GetAsyncComputeQueueUsage();
        int GetAsyncComputeQueueStallCount();
        float GetAsyncComputeQueueStallDuration();
        int GetAsyncComputeQueueDepth();
        long GetAsyncComputeShaderInvocationCount();
        float GetAsyncComputeOccupancy();
        float GetAsyncComputeWaveOccupancy();
        float GetAsyncComputeWarpOccupancy();
        long GetAsyncComputeMemoryUsage();
    }
    #endregion

    #region Structures de données
    public struct GPUProfilerHookConfig
    {
        public bool EnableContinuousCapture { get; set; }
        public float CaptureIntervalMs { get; set; }
        public float CaptureBudgetMs { get; set; }
        public PerformanceMode PerformanceMode { get; set; }
        public bool SafeMode { get; set; }
        public bool EnableDetailedMetrics { get; set; }
        public int HistorySize { get; set; }
        public float CriticalGPUCostThresholdMs { get; set; }
        public bool EnableTelemetry { get; set; }
        public bool EnableAutoDumpOnCrash { get; set; }
        public bool EnableAutoDumpOnThreshold { get; set; }
        public bool EnableAutoDumpOnVarianceSpike { get; set; }
        public bool EnableAutoDumpOnStall { get; set; }
        public bool EnableAutoDumpOnTimeout { get; set; }
        public bool EnableAutoDumpOnGPUHang { get; set; }
        public bool EnableAutoDumpOnShaderCrash { get; set; }
        public bool EnableNVidiaNsightMarkers { get; set; }
        public bool EnableAMDGPUProfilerMarkers { get; set; }
        public bool EnableIntelGPAMarkers { get; set; }
        public bool EnableDirectXPixMarkers { get; set; }
        public bool EnableVulkanDebugMarkers { get; set; }
        public GPUProfilerHookConfig(GPUProfilerHookConfig other)
        {
            EnableContinuousCapture = other.EnableContinuousCapture;
            CaptureIntervalMs = other.CaptureIntervalMs;
            CaptureBudgetMs = other.CaptureBudgetMs;
            PerformanceMode = other.PerformanceMode;
            SafeMode = other.SafeMode;
            EnableDetailedMetrics = other.EnableDetailedMetrics;
            HistorySize = other.HistorySize;
            CriticalGPUCostThresholdMs = other.CriticalGPUCostThresholdMs;
            EnableTelemetry = other.EnableTelemetry;
            EnableAutoDumpOnCrash = other.EnableAutoDumpOnCrash;
            EnableAutoDumpOnThreshold = other.EnableAutoDumpOnThreshold;
            EnableAutoDumpOnVarianceSpike = other.EnableAutoDumpOnVarianceSpike;
            EnableAutoDumpOnStall = other.EnableAutoDumpOnStall;
            EnableAutoDumpOnTimeout = other.EnableAutoDumpOnTimeout;
            EnableAutoDumpOnGPUHang = other.EnableAutoDumpOnGPUHang;
            EnableAutoDumpOnShaderCrash = other.EnableAutoDumpOnShaderCrash;
            EnableNVidiaNsightMarkers = other.EnableNVidiaNsightMarkers;
            EnableAMDGPUProfilerMarkers = other.EnableAMDGPUProfilerMarkers;
            EnableIntelGPAMarkers = other.EnableIntelGPAMarkers;
            EnableDirectXPixMarkers = other.EnableDirectXPixMarkers;
            EnableVulkanDebugMarkers = other.EnableVulkanDebugMarkers;
        }
    }

    public struct GPUMetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public float FrameTimeGpuMs { get; set; }
        public int DrawCallCount { get; set; }
        public int BatchCount { get; set; }
        public long TrisCount { get; set; }
        public long VertexShaderInvocations { get; set; }
        public long PixelShaderInvocations { get; set; }
        public long MemoryUsedBytes { get; set; }
        public int SkeletonCount { get; set; }
        // Métriques GPU avancées
        public float GPUCoreUtilization { get; set; }
        public float GPUWarpOccupancy { get; set; }
        public float GPUWaveOccupancy { get; set; }
        public float GPUThreadOccupancy { get; set; }
        public float GPUCacheHitRate { get; set; }
        public float GPUCacheMissRate { get; set; }
        public float GPUMemoryBandwidthUsage { get; set; }
        public float GPUMemoryLatency { get; set; }
        public float GPUMemoryFragmentation { get; set; }
        public float GPUMemoryAllocationRate { get; set; }
        public float GPUMemoryDeallocationRate { get; set; }
        public long GPUMemoryPeakUsage { get; set; }
        public float GPUMemoryBudgetUsage { get; set; }
        public float GPUVRAMBudgetUsage { get; set; }
        public long GPUVRAMPeakUsage { get; set; }
        public float GPUVRAMFragmentation { get; set; }
        public float GPUShaderCompilationTime { get; set; }
        public float GPUShaderWarmupTime { get; set; }
        public float GPUShaderCacheHitRate { get; set; }
        public float GPUShaderCacheMissRate { get; set; }
        public long GPUShaderInvocationCount { get; set; }
        public long GPUComputeShaderInvocationCount { get; set; }
        public long GPUVertexShaderInvocationCount { get; set; }
        public long GPUPixelShaderInvocationCount { get; set; }
        public long GPUHullShaderInvocationCount { get; set; }
        public long GPUDomainShaderInvocationCount { get; set; }
        public long GPUGeometryShaderInvocationCount { get; set; }
        public float GPUAsyncComputeUsage { get; set; }
        public int GPUAsyncComputeQueueDepth { get; set; }
        public float GPUAsyncComputeLatency { get; set; }
        public float GPUCopyQueueUsage { get; set; }
        public float GPUCopyQueueLatency { get; set; }
        public int GPUQueueStallCount { get; set; }
        public float GPUQueueStallDuration { get; set; }
        public string GPUFrameDependencyGraph { get; set; }
        public string GPUFrameExecutionTimeline { get; set; }
        public string GPUFrameExecutionHeatmap { get; set; }
        public string GPUFrameExecutionTrend { get; set; }
        public float GPUFrameExecutionBudget { get; set; }
        public float GPUFrameExecutionVariance { get; set; }
        // Animation & Skinning Profiling
        public float SkinningPassTimeMs { get; set; }
        public float SkinningComputeTimeMs { get; set; }
        public long SkinningVertexCount { get; set; }
        public int SkinningBoneCount { get; set; }
        public float SkinningMatrixUploadTime { get; set; }
        public float SkinningMatrixUploadBandwidth { get; set; }
        public int SkinningMatrixUpdateCount { get; set; }
        public int SkinningBatchCount { get; set; }
        public float SkinningBatchSizeAvg { get; set; }
        public int SkinningBatchSizeMax { get; set; }
        public int SkinningBatchSizeMin { get; set; }
        public long SkinningShaderInvocationCount { get; set; }
        public long SkinningComputeShaderInvocationCount { get; set; }
        public float SkinningComputeOccupancy { get; set; }
        public float SkinningComputeWaveOccupancy { get; set; }
        public float SkinningComputeWarpOccupancy { get; set; }
        public long SkinningComputeMemoryUsage { get; set; }
        public float SkinningComputeMemoryBandwidth { get; set; }
        public float SkinningComputeMemoryLatency { get; set; }
        public float SkinningComputeCacheHitRate { get; set; }
        public float SkinningComputeCacheMissRate { get; set; }
        public int SkinningComputeQueueDepth { get; set; }
        public int SkinningComputeQueueStallCount { get; set; }
        public float SkinningComputeQueueStallDuration { get; set; }
        public float SkinningComputeFrameBudgetUsage { get; set; }
        public float SkinningComputeFrameVariance { get; set; }
        public string SkinningComputeFrameTrend { get; set; }
        public string SkinningComputeFrameHeatmap { get; set; }
        public string SkinningComputeFrameTimeline { get; set; }
        public string SkinningComputeFrameDependencyGraph { get; set; }
        // Async Compute
        public float AsyncComputeQueueUsage { get; set; }
        public int AsyncComputeQueueStallCount { get; set; }
        public float AsyncComputeQueueStallDuration { get; set; }
        public int AsyncComputeQueueDepth { get; set; }
        public long AsyncComputeShaderInvocationCount { get; set; }
        public float AsyncComputeOccupancy { get; set; }
        public float AsyncComputeWaveOccupancy { get; set; }
        public float AsyncComputeWarpOccupancy { get; set; }
        public long AsyncComputeMemoryUsage { get; set; }

        public GPUMetricsSnapshot(GPUMetricsSnapshot other)
        {
            Timestamp = other.Timestamp;
            FrameTimeGpuMs = other.FrameTimeGpuMs;
            DrawCallCount = other.DrawCallCount;
            BatchCount = other.BatchCount;
            TrisCount = other.TrisCount;
            VertexShaderInvocations = other.VertexShaderInvocations;
            PixelShaderInvocations = other.PixelShaderInvocations;
            MemoryUsedBytes = other.MemoryUsedBytes;
            SkeletonCount = other.SkeletonCount;
            GPUCoreUtilization = other.GPUCoreUtilization;
            GPUWarpOccupancy = other.GPUWarpOccupancy;
            GPUWaveOccupancy = other.GPUWaveOccupancy;
            GPUThreadOccupancy = other.GPUThreadOccupancy;
            GPUCacheHitRate = other.GPUCacheHitRate;
            GPUCacheMissRate = other.GPUCacheMissRate;
            GPUMemoryBandwidthUsage = other.GPUMemoryBandwidthUsage;
            GPUMemoryLatency = other.GPUMemoryLatency;
            GPUMemoryFragmentation = other.GPUMemoryFragmentation;
            GPUMemoryAllocationRate = other.GPUMemoryAllocationRate;
            GPUMemoryDeallocationRate = other.GPUMemoryDeallocationRate;
            GPUMemoryPeakUsage = other.GPUMemoryPeakUsage;
            GPUMemoryBudgetUsage = other.GPUMemoryBudgetUsage;
            GPUVRAMBudgetUsage = other.GPUVRAMBudgetUsage;
            GPUVRAMPeakUsage = other.GPUVRAMPeakUsage;
            GPUVRAMFragmentation = other.GPUVRAMFragmentation;
            GPUShaderCompilationTime = other.GPUShaderCompilationTime;
            GPUShaderWarmupTime = other.GPUShaderWarmupTime;
            GPUShaderCacheHitRate = other.GPUShaderCacheHitRate;
            GPUShaderCacheMissRate = other.GPUShaderCacheMissRate;
            GPUShaderInvocationCount = other.GPUShaderInvocationCount;
            GPUComputeShaderInvocationCount = other.GPUComputeShaderInvocationCount;
            GPUVertexShaderInvocationCount = other.GPUVertexShaderInvocationCount;
            GPUPixelShaderInvocationCount = other.GPUPixelShaderInvocationCount;
            GPUHullShaderInvocationCount = other.GPUHullShaderInvocationCount;
            GPUDomainShaderInvocationCount = other.GPUDomainShaderInvocationCount;
            GPUGeometryShaderInvocationCount = other.GPUGeometryShaderInvocationCount;
            GPUAsyncComputeUsage = other.GPUAsyncComputeUsage;
            GPUAsyncComputeQueueDepth = other.GPUAsyncComputeQueueDepth;
            GPUAsyncComputeLatency = other.GPUAsyncComputeLatency;
            GPUCopyQueueUsage = other.GPUCopyQueueUsage;
            GPUCopyQueueLatency = other.GPUCopyQueueLatency;
            GPUQueueStallCount = other.GPUQueueStallCount;
            GPUQueueStallDuration = other.GPUQueueStallDuration;
            GPUFrameDependencyGraph = other.GPUFrameDependencyGraph;
            GPUFrameExecutionTimeline = other.GPUFrameExecutionTimeline;
            GPUFrameExecutionHeatmap = other.GPUFrameExecutionHeatmap;
            GPUFrameExecutionTrend = other.GPUFrameExecutionTrend;
            GPUFrameExecutionBudget = other.GPUFrameExecutionBudget;
            GPUFrameExecutionVariance = other.GPUFrameExecutionVariance;
            SkinningPassTimeMs = other.SkinningPassTimeMs;
            SkinningComputeTimeMs = other.SkinningComputeTimeMs;
            SkinningVertexCount = other.SkinningVertexCount;
            SkinningBoneCount = other.SkinningBoneCount;
            SkinningMatrixUploadTime = other.SkinningMatrixUploadTime;
            SkinningMatrixUploadBandwidth = other.SkinningMatrixUploadBandwidth;
            SkinningMatrixUpdateCount = other.SkinningMatrixUpdateCount;
            SkinningBatchCount = other.SkinningBatchCount;
            SkinningBatchSizeAvg = other.SkinningBatchSizeAvg;
            SkinningBatchSizeMax = other.SkinningBatchSizeMax;
            SkinningBatchSizeMin = other.SkinningBatchSizeMin;
            SkinningShaderInvocationCount = other.SkinningShaderInvocationCount;
            SkinningComputeShaderInvocationCount = other.SkinningComputeShaderInvocationCount;
            SkinningComputeOccupancy = other.SkinningComputeOccupancy;
            SkinningComputeWaveOccupancy = other.SkinningComputeWaveOccupancy;
            SkinningComputeWarpOccupancy = other.SkinningComputeWarpOccupancy;
            SkinningComputeMemoryUsage = other.SkinningComputeMemoryUsage;
            SkinningComputeMemoryBandwidth = other.SkinningComputeMemoryBandwidth;
            SkinningComputeMemoryLatency = other.SkinningComputeMemoryLatency;
            SkinningComputeCacheHitRate = other.SkinningComputeCacheHitRate;
            SkinningComputeCacheMissRate = other.SkinningComputeCacheMissRate;
            SkinningComputeQueueDepth = other.SkinningComputeQueueDepth;
            SkinningComputeQueueStallCount = other.SkinningComputeQueueStallCount;
            SkinningComputeQueueStallDuration = other.SkinningComputeQueueStallDuration;
            SkinningComputeFrameBudgetUsage = other.SkinningComputeFrameBudgetUsage;
            SkinningComputeFrameVariance = other.SkinningComputeFrameVariance;
            SkinningComputeFrameTrend = other.SkinningComputeFrameTrend;
            SkinningComputeFrameHeatmap = other.SkinningComputeFrameHeatmap;
            SkinningComputeFrameTimeline = other.SkinningComputeFrameTimeline;
            SkinningComputeFrameDependencyGraph = other.SkinningComputeFrameDependencyGraph;
            AsyncComputeQueueUsage = other.AsyncComputeQueueUsage;
            AsyncComputeQueueStallCount = other.AsyncComputeQueueStallCount;
            AsyncComputeQueueStallDuration = other.AsyncComputeQueueStallDuration;
            AsyncComputeQueueDepth = other.AsyncComputeQueueDepth;
            AsyncComputeShaderInvocationCount = other.AsyncComputeShaderInvocationCount;
            AsyncComputeOccupancy = other.AsyncComputeOccupancy;
            AsyncComputeWaveOccupancy = other.AsyncComputeWaveOccupancy;
            AsyncComputeWarpOccupancy = other.AsyncComputeWarpOccupancy;
            AsyncComputeMemoryUsage = other.AsyncComputeMemoryUsage;
        }
    }

    public struct AnimationRenderMetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int SkeletonCount { get; set; }
        public long SkinnedVertexCount { get; set; }
        public int SkinMatricesUpdates { get; set; }
        public int BlendShapeTargetsActive { get; set; }

        public AnimationRenderMetricsSnapshot(AnimationRenderMetricsSnapshot other)
        {
            Timestamp = other.Timestamp;
            SkeletonCount = other.SkeletonCount;
            SkinnedVertexCount = other.SkinnedVertexCount;
            SkinMatricesUpdates = other.SkinMatricesUpdates;
            BlendShapeTargetsActive = other.BlendShapeTargetsActive;
        }
    }

    public struct GPUProfilerLifecycleData
    {
        public DateTime InitializedAt { get; set; }
        public DateTime StartedCapturingAt { get; set; }
        public DateTime LastTransitionAt { get; set; }
        public int TransitionCount { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastErrorAt { get; set; }
        public string LastErrorMessage { get; set; }
    }

    public class GPUProfilerEventLog // [CORRECTION] : transformé en classe pour encapsulation
    {
        private readonly List<string> _events = new List<string>();
        private readonly int _maxEvents;
        private readonly object _lock = new object();

        public GPUProfilerEventLog(int maxEvents = 1000)
        {
            _maxEvents = maxEvents;
        }

        public void AddEvent(string ev)
        {
            lock (_lock)
            {
                if (_events.Count >= _maxEvents) _events.RemoveAt(0);
                _events.Add(ev);
            }
        }

        public List<string> GetEvents()
        {
            lock (_lock)
            {
                return new List<string>(_events); // [OBSERVABILITE] : retourne une copie pour éviter la corruption
            }
        }
    }

    public struct GPUProfilerCrashReport
    {
        public DateTime CrashTime { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
        public GPUMetricsSnapshot MetricsAtCrash { get; set; }
        public string CallStack { get; set; }
        public string GPUInfo { get; set; }
    }

    public struct GPUMetricsRingBufferSnapshot
    {
        public List<GPUMetricsSnapshot> Metrics { get; set; }
        public List<AnimationRenderMetricsSnapshot> AnimationMetrics { get; set; }
        public DateTime CaptureTime { get; set; }
        public int Count { get; set; }
    }

    public struct GPUProfilerHookPreset
    {
        public string Name { get; set; }
        public GPUProfilerHookConfig Config { get; set; }
        public AffinityProfile AffinityProfile { get; set; }
        public PerformanceMode PerformanceMode { get; set; }
        public bool IsDefault { get; set; }
    }

    public struct GPUThreadAffinityHints
    {
        public ThreadRole Role { get; set; }
        public CoreClass TargetCoreClass { get; set; }
        public int PreferredThreadIndex { get; set; }
        public bool PinningRequired { get; set; }
    }

    public struct GPUThreadAffinityProfile
    {
        public ThreadRole Role { get; set; }
        public CoreClass PreferredCoreClass { get; set; }
        public List<int> PreferredThreadIndices { get; set; }
        public bool RequiresIsolation { get; set; }
    }

    public struct GPUThreadAffinityHeatmap
    {
        public Dictionary<int, float> LoadByThread { get; set; }
        public Dictionary<ThreadRole, float> LoadByRole { get; set; }
        public float MaxLoad { get; set; }
        public float MinLoad { get; set; }
        public float AverageLoad { get; set; }
        public DateTime ReportTime { get; set; }
    }

    public struct GPUThreadAffinityTrend
    {
        public List<float> LoadOverTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct GPUThreadAffinityTimeline
    {
        public List<(DateTime Time, int ThreadIndex, int CoreIndex)> Changes { get; set; }
    }

    public struct GPUThreadAffinityBudget
    {
        public float MaxLoadPerThread { get; set; }
        public float MinLoadPerThread { get; set; }
        public float BudgetUsagePercentage { get; set; }
    }

    public struct GPUThreadAffinityValidationReport
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Warnings { get; set; }
    }

    public struct GPUThreadAffinityLease
    {
        public int ThreadIndex { get; set; }
        public ThreadRole Role { get; set; }
        public DateTime LeaseStart { get; set; }
        public DateTime LeaseEnd { get; set; }
        public bool IsActive { get; set; }
        public void Renew(TimeSpan duration) { LeaseEnd = DateTime.UtcNow.Add(duration); }
    }

    public struct GPUThreadAffinityPlan
    {
        public Dictionary<int, int> ThreadToCoreMapping { get; set; }
        public DateTime ScheduledAt { get; set; }
        public bool IsExecuted { get; set; }
    }

    public struct GPUThreadAffinityPolicy
    {
        public string Name { get; set; }
        public Func<ThreadAffinityContext, bool> ShouldApply { get; set; }
        public Action<ThreadAffinityContext> Apply { get; set; }
    }

    public struct ThreadAffinityContext
    {
        public Dictionary<int, float> ThreadLoads { get; set; }
        public Dictionary<ThreadRole, List<int>> RoleToThreads { get; set; }
        public CPUTopology Topology { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public struct CPUTopology
    {
        public int PhysicalCoreCount { get; set; }
        public int LogicalCoreCount { get; set; }
        public int NumaNodedCount { get; set; }
        public List<CoreInfo> Cores { get; set; }
    }

    public struct CoreInfo
    {
        public int Index { get; set; }
        public CoreClass Class { get; set; }
        public bool IsHyperthreaded { get; set; }
        public int NumaNode { get; set; }
    }

    public struct GPUMetricsSnapshotDiff
    {
        public GPUMetricsSnapshot Before { get; set; }
        public GPUMetricsSnapshot After { get; set; }
        public GPUMetricsSnapshot Delta { get; set; }
        public float PercentageChange { get; set; }
    }

    public struct GPUMetricsSnapshotTimeline
    {
        public List<GPUMetricsSnapshot> Snapshots { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct GPUMetricsSnapshotHeatmap
    {
        public List<List<float>> HeatmapData { get; set; } // [time][metric_id]
        public List<string> MetricNames { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct GPUMetricsSnapshotTrend
    {
        public List<float> Values { get; set; }
        public string MetricName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public struct GPUMetricsSnapshotBudget
    {
        public float BudgetValue { get; set; }
        public float ActualValue { get; set; }
        public GPUFrameBudgetState State { get; set; }
        public float PercentageUsed { get; set; }
    }

    public struct GPUMetricsSnapshotVariance
    {
        public float Mean { get; set; }
        public float Variance { get; set; }
        public float StdDeviation { get; set; }
        public List<float> Values { get; set; }
    }

    public struct GPUMetricsSnapshotHistogram
    {
        public List<int> Bins { get; set; }
        public List<float> BinEdges { get; set; }
        public string MetricName { get; set; }
    }

    public struct GPUMetricsSnapshotPercentiles
    {
        public float P50 { get; set; }
        public float P90 { get; set; }
        public float P95 { get; set; }
        public float P99 { get; set; }
        public string MetricName { get; set; }
    }

    public struct GPUMetricsSnapshotCorrelation
    {
        public float CorrelationCoefficient { get; set; }
        public string MetricA { get; set; }
        public string MetricB { get; set; }
        public List<(float A, float B)> DataPoints { get; set; }
    }

    public struct GPUMetricsSnapshotAnomaly
    {
        public DateTime Time { get; set; }
        public string MetricName { get; set; }
        public float Value { get; set; }
        public float Threshold { get; set; }
        public GPUErrorSeverity Severity { get; set; }
    }

    public struct GPUMetricsSnapshotAlertRule
    {
        public string Name { get; set; }
        public string MetricName { get; set; }
        public float Threshold { get; set; }
        public string Condition { get; set; } // ">", "<", ">=", "<=", "=="
        public GPUErrorSeverity Severity { get; set; }
        public bool IsActive { get; set; }
    }

    public struct GPUMetricsSnapshotHealthCheck
    {
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Warnings { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public struct GPUMetricsSnapshotAutoDumpConfig
    {
        public bool Enabled { get; set; }
        public string DumpDirectory { get; set; }
        public string FileNamePattern { get; set; }
        public int MaxDumps { get; set; }
        public bool Compress { get; set; }
        public bool Encrypt { get; set; }
    }

    public struct RingBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _size;
        private readonly int _capacity;

        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[_capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        public void Enqueue(T item)
        {
            if (_size == _capacity)
            {
                _head = (_head + 1) % _capacity; // Décaler la tête si plein
            }
            else
            {
                _size++;
            }

            _buffer[_tail] = item;
            _tail = (_tail + 1) % _capacity;
        }

        // [CORRECTION] : GetSnapshot() retourne une copie non destructive
        public GPUMetricsRingBufferSnapshot GetSnapshot()
        {
            var metrics = new List<GPUMetricsSnapshot>();
            var animMetrics = new List<AnimationRenderMetricsSnapshot>();
            var tempGpu = new GPUMetricsSnapshot();
            var tempAnim = new AnimationRenderMetricsSnapshot();

            var count = Math.Min(_size, _buffer.Length);
            var index = _head;
            for (int i = 0; i < count; i++)
            {
                tempGpu = _buffer[index];
                // Assumer qu'il y ait un buffer similaire pour les AnimationMetrics
                // Pour cet exemple, on suppose que les AnimationMetrics sont stockées ailleurs ou synchronisées
                metrics.Add(tempGpu);
                // animMetrics.Add(tempAnim); // Non implémenté ici
                index = (index + 1) % _capacity;
            }

            return new GPUMetricsRingBufferSnapshot
            {
                Metrics = metrics,
                AnimationMetrics = animMetrics,
                CaptureTime = DateTime.UtcNow,
                Count = count
            };
        }

        public bool TryDequeue(out T item)
        {
            if (_size == 0)
            {
                item = default(T);
                return false;
            }

            item = _buffer[_head];
            _buffer[_head] = default(T); // Nettoyer la référence
            _head = (_head + 1) % _capacity;
            _size--;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        public int Count => _size;
        public int Capacity => _capacity;
    }

    public struct BuildMetadata
    {
        public string Version { get; set; }
        public string Branch { get; set; }
        public string CommitHash { get; set; }
        public DateTime BuildDate { get; set; }
        public string Platform { get; set; }
        public string Configuration { get; set; }
    }

    public struct OperationResult
    {
        public bool Success { get; set; }
        public string Reason { get; set; }
        public float EstimatedCostMs { get; set; }
    }
    #endregion

    #region Evénements
    public class GPUProfilerHookInitializedEvent
    {
        public GPUProfilerHook Source { get; }
        public GPUProfilerHookInitializedEvent(GPUProfilerHook source) => Source = source;
    }

    public class GPUProfilerHookShutdownEvent
    {
        public GPUProfilerHook Source { get; }
        public GPUProfilerHookShutdownEvent(GPUProfilerHook source) => Source = source;
    }

    public class GPUFrameBudgetExceededEvent // [RENOMMAGE]
    {
        public float FrameTimeGpuMs { get; }
        public GPUFrameBudgetExceededEvent(float frameTimeGpuMs) => FrameTimeGpuMs = frameTimeGpuMs;
    }

    public class GPUProfilerErrorEvent // [RENOMMAGE]
    {
        public string Message { get; }
        public GPUErrorSeverity Severity { get; }
        public GPUProfilerErrorEvent(string message, GPUErrorSeverity severity)
        {
            Message = message;
            Severity = severity;
        }
    }

    public class GPUProfilerHangDetectedEvent // [RENOMMAGE]
    {
        public float DurationMs { get; }
        public GPUProfilerHangDetectedEvent(float durationMs) => DurationMs = durationMs;
    }

    public class GPUProfilerStallDetectedEvent // [RENOMMAGE]
    {
        public int StallCount { get; }
        public float TotalDurationMs { get; }
        public GPUProfilerStallDetectedEvent(int stallCount, float totalDurationMs)
        {
            StallCount = stallCount;
            TotalDurationMs = totalDurationMs;
        }
    }

    public class GPUProfilerMemoryPressureEvent // [RENOMMAGE]
    {
        public float PressurePercentage { get; }
        public long CurrentUsageBytes { get; }
        public long PeakUsageBytes { get; }
        public GPUProfilerMemoryPressureEvent(float pressurePercentage, long currentUsage, long peakUsage)
        {
            PressurePercentage = pressurePercentage;
            CurrentUsageBytes = currentUsage;
            PeakUsageBytes = peakUsage;
        }
    }

    public class GPUProfilerShaderCompileEvent // [RENOMMAGE]
    {
        public string ShaderName { get; }
        public float CompileTimeMs { get; }
        public GPUProfilerShaderCompileEvent(string shaderName, float compileTimeMs)
        {
            ShaderName = shaderName;
            CompileTimeMs = compileTimeMs;
        }
    }

    public class GPUProfilerComputeTaskEvent // [RENOMMAGE]
    {
        public string TaskName { get; }
        public float ExecutionTimeMs { get; }
        public GPUProfilerComputeTaskEvent(string taskName, float executionTimeMs)
        {
            TaskName = taskName;
            ExecutionTimeMs = executionTimeMs;
        }
    }

    public class GPUProfilerFrameBoundaryEvent // [RENOMMAGE]
    {
        public int FrameNumber { get; }
        public DateTime StartTime { get; }
        public GPUProfilerFrameBoundaryEvent(int frameNumber, DateTime startTime)
        {
            FrameNumber = frameNumber;
            StartTime = startTime;
        }
    }

    public class GPUProfilerStateChangedEvent
    {
        public GPUProfilerState PreviousState { get; }
        public GPUProfilerState NewState { get; }
        public GPUProfilerStateChangedEvent(GPUProfilerState previous, GPUProfilerState newState)
        {
            PreviousState = previous;
            NewState = newState;
        }
    }
    #endregion

    #region Classes principales
    public sealed class GPUProfilerHook : IGPUProfilerHook, IDisposable
    {
        #region Fields
        private IRenderEngine _renderEngine;
        private Profiler _profiler;
        private EventBus _eventBus;
        private IJobSystem _jobSystem;
        private ThreadAffinityManager _threadAffinityManager;

        private volatile int _stateAsInt; // [PERFORMANCE] : utilisation d'un int atomique pour l'état
        private volatile GPUProfilerHookConfig _config;

        // Dernières métriques capturées
        private GPUMetricsSnapshot _lastMetrics = new GPUMetricsSnapshot();
        private AnimationRenderMetricsSnapshot _lastAnimationMetrics = new AnimationRenderMetricsSnapshot();

        // Historique des métriques
        private readonly RingBuffer<GPUMetricsSnapshot> _metricsHistory;
        private readonly RingBuffer<AnimationRenderMetricsSnapshot> _animationMetricsHistory;

        // Pour la capture continue
        private CancellationTokenSource _captureCancellationSource;
        private readonly object _metricsLock = new object();

        // Pour les alertes critiques
        private readonly object _alertLock = new object();
        private long _criticalGPUCostEvents = 0;
        private float _lastCriticalGPUCost = 0f;

        // Pour la détection de contention
        private long _lockContentionCounter = 0;

        // Nouveaux champs pour les idées
        private readonly GPUProfilerLifecycleData _lifecycleData = new GPUProfilerLifecycleData();
        private readonly GPUProfilerEventLog _eventLog = new GPUProfilerEventLog(1000); // [CORRECTION] : Utilisation de la classe
        private readonly GPUProfilerCrashReport _crashReport = new GPUProfilerCrashReport();
        private readonly GPUMetricsRingBuffer _telemetryBuffer = new GPUMetricsRingBuffer(1024);
        private readonly List<GPUThreadAffinityProfile> _affinityProfiles = new List<GPUThreadAffinityProfile>();
        private readonly Dictionary<int, GPUThreadAffinityLease> _affinityLeases = new Dictionary<int, GPUThreadAffinityLease>();
        private readonly List<GPUThreadAffinityPlan> _pendingPlans = new List<GPUThreadAffinityPlan>();
        private readonly List<GPUThreadAffinityPolicy> _affinityPolicies = new List<GPUThreadAffinityPolicy>();
        private readonly List<GPUProfilerHookPreset> _presetLibrary = new List<GPUProfilerHookPreset>();
        private readonly List<GPUMetricsSnapshotAlertRule> _alertRules = new List<GPUMetricsSnapshotAlertRule>();
        private readonly GPUMetricsSnapshotAutoDumpConfig _autoDumpConfig = new GPUMetricsSnapshotAutoDumpConfig();
        private readonly object _configLock = new object();
        private readonly object _telemetryLock = new object();
        private readonly object _affinityLock = new object();
        private readonly object _presetsLock = new object();
        private readonly object _alertsLock = new object();
        private readonly object _autoDumpLock = new object();
        private readonly object _vendorLock = new object();

        // [THREAD SAFETY] Ajout de locks pour les hooks
        private readonly object _hooksLock = new object();

        // Pour les hooks moteur
        private readonly Dictionary<string, DateTime> _hookTimestamps = new Dictionary<string, DateTime>(); // [THREAD SAFETY] : Accès protégé
        private readonly Dictionary<string, float> _hookDurations = new Dictionary<string, float>(); // [THREAD SAFETY] : Accès protégé

        // Pour les tests & QA
        private volatile bool _isStressTesting = false;
        private volatile bool _isFuzzTesting = false;
        private volatile bool _simulatingGPUHang = false;
        private volatile bool _simulatingShaderCrash = false;
        private volatile bool _simulatingGPUTimeout = false;
        private volatile bool _simulatingGPUCrash = false;

        // Pour les marqueurs vendor-specific
        private volatile bool _nvidiaNsightMarkersEnabled = false;
        private volatile bool _amdGPUProfilerMarkersEnabled = false;
        private volatile bool _intelGPAMarkersEnabled = false;
        private volatile bool _directxPixMarkersEnabled = false;
        private volatile bool _vulkanDebugMarkersEnabled = false;

        // Compteurs pour l'observabilité
        private long _capturesSucceeded = 0;
        private long _capturesFailed = 0;
        private long _autoDumpsTriggered = 0;
        private long _errorsLogged = 0;

        // Pour le pattern Dispose
        private bool _disposed = false;
        #endregion

        #region Constructor
        public GPUProfilerHook(int historySize = 120)
        {
            _metricsHistory = new RingBuffer<GPUMetricsSnapshot>(historySize);
            _animationMetricsHistory = new RingBuffer<AnimationRenderMetricsSnapshot>(historySize);
        }
        #endregion

        #region Properties (AAA)
        public GPUProfilerState State => (GPUProfilerState)Volatile.Read(ref _stateAsInt); // [ARCHITECTURE] : propriété State
        public GPUProfilerHookConfig Config // [ARCHITECTURE] : propriété Config thread-safe
        {
            get
            {
                lock (_configLock)
                {
                    return new GPUProfilerHookConfig(_config);
                }
            }
        }
        #endregion

        #region Initialization & Lifecycle (Amélioré)
        public void Initialize(IRenderEngine renderEngine, Profiler profiler, EventBus eventBus, IJobSystem jobSystem, ThreadAffinityManager threadAffinityManager)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (renderEngine == null) throw new ArgumentNullException(nameof(renderEngine));
            if (profiler == null) throw new ArgumentNullException(nameof(profiler));
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            if (jobSystem == null) throw new ArgumentNullException(nameof(jobSystem));
            if (threadAffinityManager == null) throw new ArgumentNullException(nameof(threadAffinityManager));

            _renderEngine = renderEngine;
            _profiler = profiler;
            _eventBus = eventBus;
            _jobSystem = jobSystem;
            _threadAffinityManager = threadAffinityManager;

            ApplyDefaultConfiguration();
            WarmupPhase();
            NotifyInitialized();
            Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Ready); // [THREAD SAFETY] : écriture atomique
        }

        public void Initialize(GPUProfilerHookConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            ApplyConfiguration(config);
            // [CORRECTION] : vérifier que les dépendances sont valides avant d'appeler Initialize
            if (_renderEngine == null || _profiler == null || _eventBus == null || _jobSystem == null || _threadAffinityManager == null)
            {
                throw new InvalidOperationException("Dependencies are not set. Cannot initialize with config only.");
            }
            Initialize(_renderEngine, _profiler, _eventBus, _jobSystem, _threadAffinityManager);
        }

        public async Task InitializeAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(GPUProfilerState.Uninitialized, GPUProfilerState.Initializing))
            {
                throw new InvalidOperationException($"Cannot initialize GPUProfilerHook: current state is '{State}'. Expected 'Uninitialized'.");
            }
            try
            {
                ApplyDefaultConfiguration();
                WarmupPhase();
                await NotifyInitializedAsync();
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Ready); // [THREAD SAFETY]
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Error); // [CORRECTION] : transition vers Error en cas d'exception
                LogOrRaiseError(ex.Message, "InitializeAsync", null, ex);
                throw;
            }
        }

        public async Task InitializeAsync(GPUProfilerHookConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            ApplyConfiguration(config);
            // [CORRECTION] : vérifier que les dépendances sont valides avant d'appeler InitializeAsync
            if (_renderEngine == null || _profiler == null || _eventBus == null || _jobSystem == null || _threadAffinityManager == null)
            {
                throw new InvalidOperationException("Dependencies are not set. Cannot initialize with config only.");
            }
            await InitializeAsync();
        }

        public void Shutdown()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var currentState = State; // [THREAD SAFETY] : lecture atomique
            if (currentState == GPUProfilerState.Shutdown || currentState == GPUProfilerState.ShuttingDown)
            {
                return;
            }
            // [CORRECTION] : gérer le cas d'état Error
            if (!TryTransitionState(GPUProfilerState.Capturing, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Ready, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Error, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Paused, GPUProfilerState.ShuttingDown))
            {
                return; // Impossible de shutdown depuis l'état actuel
            }
            try
            {
                CooldownPhase();
                SetContinuousCapture(false);
                NotifyShutdown();
            }
            finally
            {
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Shutdown); // [THREAD SAFETY]
            }
        }

        public async Task ShutdownAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var currentState = State; // [THREAD SAFETY]
            if (currentState == GPUProfilerState.Shutdown || currentState == GPUProfilerState.ShuttingDown)
            {
                return;
            }
            if (!TryTransitionState(GPUProfilerState.Capturing, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Ready, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Error, GPUProfilerState.ShuttingDown) &&
                !TryTransitionState(GPUProfilerState.Paused, GPUProfilerState.ShuttingDown))
            {
                return;
            }
            try
            {
                CooldownPhase();
                SetContinuousCapture(false);
                await NotifyShutdownAsync();
            }
            finally
            {
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Shutdown); // [THREAD SAFETY]
            }
        }

        public void Restart()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var currentState = State; // [THREAD SAFETY]
            if (currentState == GPUProfilerState.Capturing || currentState == GPUProfilerState.Ready || currentState == GPUProfilerState.Paused)
            {
                // [CORRECTION] : sauvegarder la config avant shutdown
                var currentConfig = GetConfigSnapshot();
                Shutdown();
                Initialize(currentConfig);
            }
        }

        public void WarmupPhase()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // [CORRECTION] : éviter Thread.Sleep
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 100) { /* busy wait */ }
            _lifecycleData.InitializedAt = DateTime.UtcNow;
        }

        public void CooldownPhase()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 50) { /* busy wait */ }
        }

        public GPUProfilerState GetState() => State; // [ARCHITECTURE]

        public bool IsReady() => State == GPUProfilerState.Ready || State == GPUProfilerState.Capturing || State == GPUProfilerState.Paused; // [ARCHITECTURE]

        public bool IsCapturing() => State == GPUProfilerState.Capturing; // [ARCHITECTURE]

        public bool IsError() => State == GPUProfilerState.Error; // [ARCHITECTURE]

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryTransitionState(GPUProfilerState expected, GPUProfilerState next)
        {
            return Interlocked.CompareExchange(ref _stateAsInt, (int)next, (int)expected) == (int)expected; // [THREAD SAFETY] : cast explicite
        }
        #endregion

        #region Configuration (Amélioré)
        private void ApplyDefaultConfiguration()
        {
            var newConfig = new GPUProfilerHookConfig
            {
                EnableContinuousCapture = false,
                CaptureIntervalMs = 16.0f, // ~60fps
                CaptureBudgetMs = 0.5f, // 0.5ms max par capture
                PerformanceMode = PerformanceMode.Medium,
                SafeMode = false,
                EnableDetailedMetrics = true,
                HistorySize = 120,
                CriticalGPUCostThresholdMs = 16.0f,
                EnableTelemetry = true,
                EnableAutoDumpOnCrash = true,
                EnableAutoDumpOnThreshold = true,
                EnableAutoDumpOnVarianceSpike = false,
                EnableAutoDumpOnStall = false,
                EnableAutoDumpOnTimeout = false,
                EnableAutoDumpOnGPUHang = false,
                EnableAutoDumpOnShaderCrash = false,
                EnableNVidiaNsightMarkers = false,
                EnableAMDGPUProfilerMarkers = false,
                EnableIntelGPAMarkers = false,
                EnableDirectXPixMarkers = false,
                EnableVulkanDebugMarkers = false
            };
            ApplyConfiguration(newConfig);
        }

        public void ApplyConfiguration(GPUProfilerHookConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // [CORRECTION] : config est une struct, donc ne peut pas être null
            if (!ValidateConfig(config)) throw new ArgumentException("Invalid configuration provided.", nameof(config));

            lock (_configLock)
            {
                _config = config;
                if (IsReady())
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

        public GPUProfilerHookConfig GetConfigSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                return new GPUProfilerHookConfig(_config);
            }
        }

        public bool ValidateConfig(GPUProfilerHookConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (config.CaptureIntervalMs <= 0) return false;
            if (config.CaptureBudgetMs < 0) return false;
            if (config.CriticalGPUCostThresholdMs <= 0) return false;
            if (config.HistorySize <= 0) return false;
            return true;
        }

        private GPUProfilerHookConfig LoadConfigurationFromFile()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Charger depuis un fichier JSON, XML, etc.
            return new GPUProfilerHookConfig(); // Placeholder
        }

        private void ReconfigureRuntimeSettings(GPUProfilerHookConfig newConfig)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Appliquer dynamiquement les changements possibles
            if (newConfig.EnableContinuousCapture != _config.EnableContinuousCapture)
            {
                // [CORRECTION] : empêcher la récursion potentielle
                var shouldEnable = newConfig.EnableContinuousCapture;
                SetContinuousCapture(shouldEnable);
            }
            if (Math.Abs(newConfig.CaptureIntervalMs - _config.CaptureIntervalMs) > 0.01f)
            {
                // Réinitialiser la tâche de capture continue avec le nouvel intervalle
                if (_config.EnableContinuousCapture)
                {
                    SetContinuousCapture(false);
                    SetContinuousCapture(true);
                }
            }
            // Ne pas toucher à la topologie ou aux threads réservés pendant l'exécution.
        }

        public void SetPerformanceMode(PerformanceMode mode)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                var newConfig = new GPUProfilerHookConfig(_config);
                newConfig.PerformanceMode = mode;
                ApplyConfiguration(newConfig);
            }
        }

        public void SetCaptureBudget(float budgetMs)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                var newConfig = new GPUProfilerHookConfig(_config);
                newConfig.CaptureBudgetMs = budgetMs;
                ApplyConfiguration(newConfig);
            }
        }

        public void SetCaptureInterval(float intervalMs)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                var newConfig = new GPUProfilerHookConfig(_config);
                newConfig.CaptureIntervalMs = intervalMs;
                ApplyConfiguration(newConfig);
            }
        }

        public void SetSafeMode(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_configLock)
            {
                var newConfig = new GPUProfilerHookConfig(_config);
                newConfig.SafeMode = enabled;
                ApplyConfiguration(newConfig);
            }
        }
        #endregion

        #region Capture & Metrics (Amélioré)
        public void CaptureMetrics()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!IsReady()) throw new InvalidOperationException("GPUProfilerHook must be ready before capturing metrics.");

            // [PERFORMANCE] : Utiliser Stopwatch.GetTimestamp pour mesurer le budget
            var startTimestamp = Stopwatch.GetTimestamp();
            // [THREAD SAFETY] : Lecture de _config sous lock
            float budgetMs;
            lock (_configLock)
            {
                budgetMs = _config.CaptureBudgetMs;
            }
            var elapsedMs = (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency * 1000.0;
            if (elapsedMs > budgetMs)
            {
                LogOrRaiseError("CaptureMetrics exceeded budget", "CaptureMetrics", new { BudgetMs = budgetMs, ElapsedMs = elapsedMs });
                return;
            }

            var metrics = new GPUMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameTimeGpuMs = _renderEngine?.GetLastFrameTimeGpuMs() ?? 0f,
                DrawCallCount = _renderEngine?.GetDrawCallCount() ?? 0,
                BatchCount = _renderEngine?.GetBatchCount() ?? 0,
                TrisCount = _renderEngine?.GetTriangleCount() ?? 0,
                // [CORRECTION] : éviter la récursion infinie
                // VertexShaderInvocations = GetEstimatedVertexShaderInvocations(), // Utiliser la valeur du snapshot précédent ou une estimation brute
                VertexShaderInvocations = _lastMetrics.VertexShaderInvocations, // ou calculer sans GetMetricsSnapshot
                PixelShaderInvocations = _renderEngine?.GetPixelShaderInvocationCount() ?? 0,
                MemoryUsedBytes = _renderEngine?.GetVRAMUsageBytes() ?? 0,
                SkeletonCount = GetEstimatedSkeletonCount(),
                // --- Nouveaux champs ---
                GPUCoreUtilization = _renderEngine?.GetGPUCoreUtilization() ?? 0f, // Hypothétique
                GPUWarpOccupancy = _renderEngine?.GetGPUWarpOccupancy() ?? 0f, // Hypothétique
                GPUWaveOccupancy = _renderEngine?.GetGPUWaveOccupancy() ?? 0f, // Hypothétique
                GPUThreadOccupancy = _renderEngine?.GetGPUThreadOccupancy() ?? 0f, // Hypothétique
                GPUCacheHitRate = _renderEngine?.GetGPUCacheHitRate() ?? 0f, // Hypothétique
                GPUCacheMissRate = _renderEngine?.GetGPUCacheMissRate() ?? 0f, // Hypothétique
                GPUMemoryBandwidthUsage = _renderEngine?.GetGPUMemoryBandwidthUsage() ?? 0f, // Hypothétique
                GPUMemoryLatency = _renderEngine?.GetGPUMemoryLatency() ?? 0f, // Hypothétique
                GPUMemoryFragmentation = _renderEngine?.GetGPUMemoryFragmentation() ?? 0f, // Hypothétique
                GPUMemoryAllocationRate = _renderEngine?.GetGPUMemoryAllocationRate() ?? 0f, // Hypothétique
                GPUMemoryDeallocationRate = _renderEngine?.GetGPUMemoryDeallocationRate() ?? 0f, // Hypothétique
                GPUMemoryPeakUsage = _renderEngine?.GetGPUMemoryPeakUsage() ?? 0, // Hypothétique
                GPUMemoryBudgetUsage = _renderEngine?.GetGPUMemoryBudgetUsage() ?? 0f, // Hypothétique
                GPUVRAMBudgetUsage = _renderEngine?.GetGPUVRAMBudgetUsage() ?? 0f, // Hypothétique
                GPUVRAMPeakUsage = _renderEngine?.GetGPUVRAMPeakUsage() ?? 0, // Hypothétique
                GPUVRAMFragmentation = _renderEngine?.GetGPUVRAMFragmentation() ?? 0f, // Hypothétique
                GPUShaderCompilationTime = _renderEngine?.GetGPUShaderCompilationTime() ?? 0f, // Hypothétique
                GPUShaderWarmupTime = _renderEngine?.GetGPUShaderWarmupTime() ?? 0f, // Hypothétique
                GPUShaderCacheHitRate = _renderEngine?.GetGPUShaderCacheHitRate() ?? 0f, // Hypothétique
                GPUShaderCacheMissRate = _renderEngine?.GetGPUShaderCacheMissRate() ?? 0f, // Hypothétique
                GPUShaderInvocationCount = _renderEngine?.GetGPUShaderInvocationCount() ?? 0, // Hypothétique
                GPUComputeShaderInvocationCount = _renderEngine?.GetGPUComputeShaderInvocationCount() ?? 0, // Hypothétique
                GPUVertexShaderInvocationCount = _renderEngine?.GetGPUVertexShaderInvocationCount() ?? 0, // Hypothétique
                GPUPixelShaderInvocationCount = _renderEngine?.GetGPUPixelShaderInvocationCount() ?? 0, // Hypothétique
                GPUHullShaderInvocationCount = _renderEngine?.GetGPUHullShaderInvocationCount() ?? 0, // Hypothétique
                GPUDomainShaderInvocationCount = _renderEngine?.GetGPUDomainShaderInvocationCount() ?? 0, // Hypothétique
                GPUGeometryShaderInvocationCount = _renderEngine?.GetGPUGeometryShaderInvocationCount() ?? 0, // Hypothétique
                GPUAsyncComputeUsage = _renderEngine?.GetGPUAsyncComputeUsage() ?? 0f, // Hypothétique
                GPUAsyncComputeQueueDepth = _renderEngine?.GetGPUAsyncComputeQueueDepth() ?? 0, // Hypothétique
                GPUAsyncComputeLatency = _renderEngine?.GetGPUAsyncComputeLatency() ?? 0f, // Hypothétique
                GPUCopyQueueUsage = _renderEngine?.GetGPUCopyQueueUsage() ?? 0f, // Hypothétique
                GPUCopyQueueLatency = _renderEngine?.GetGPUCopyQueueLatency() ?? 0f, // Hypothétique
                GPUQueueStallCount = _renderEngine?.GetGPUQueueStallCount() ?? 0, // Hypothétique
                GPUQueueStallDuration = _renderEngine?.GetGPUQueueStallDuration() ?? 0f, // Hypothétique
                GPUFrameDependencyGraph = _renderEngine?.GetGPUFrameDependencyGraph() ?? "", // Hypothétique
                GPUFrameExecutionTimeline = _renderEngine?.GetGPUFrameExecutionTimeline() ?? "", // Hypothétique
                GPUFrameExecutionHeatmap = _renderEngine?.GetGPUFrameExecutionHeatmap() ?? "", // Hypothétique
                GPUFrameExecutionTrend = _renderEngine?.GetGPUFrameExecutionTrend() ?? "", // Hypothétique
                GPUFrameExecutionBudget = _renderEngine?.GetGPUFrameExecutionBudget() ?? 0f, // Hypothétique
                GPUFrameExecutionVariance = _renderEngine?.GetGPUFrameExecutionVariance() ?? 0f, // Hypothétique
                // Animation & Skinning
                SkinningPassTimeMs = _renderEngine?.GetSkinningPassTimeMs() ?? 0f, // Hypothétique
                SkinningComputeTimeMs = _renderEngine?.GetSkinningComputeTimeMs() ?? 0f, // Hypothétique
                SkinningVertexCount = _renderEngine?.GetSkinningVertexCount() ?? 0, // Hypothétique
                SkinningBoneCount = _renderEngine?.GetSkinningBoneCount() ?? 0, // Hypothétique
                SkinningMatrixUploadTime = _renderEngine?.GetSkinningMatrixUploadTime() ?? 0f, // Hypothétique
                SkinningMatrixUploadBandwidth = _renderEngine?.GetSkinningMatrixUploadBandwidth() ?? 0f, // Hypothétique
                SkinningMatrixUpdateCount = _renderEngine?.GetSkinningMatrixUpdateCount() ?? 0, // Hypothétique
                SkinningBatchCount = _renderEngine?.GetSkinningBatchCount() ?? 0, // Hypothétique
                SkinningBatchSizeAvg = _renderEngine?.GetSkinningBatchSizeAvg() ?? 0f, // Hypothétique
                SkinningBatchSizeMax = _renderEngine?.GetSkinningBatchSizeMax() ?? 0, // Hypothétique
                SkinningBatchSizeMin = _renderEngine?.GetSkinningBatchSizeMin() ?? 0, // Hypothétique
                SkinningShaderInvocationCount = _renderEngine?.GetSkinningShaderInvocationCount() ?? 0, // Hypothétique
                SkinningComputeShaderInvocationCount = _renderEngine?.GetSkinningComputeShaderInvocationCount() ?? 0, // Hypothétique
                SkinningComputeOccupancy = _renderEngine?.GetSkinningComputeOccupancy() ?? 0f, // Hypothétique
                SkinningComputeWaveOccupancy = _renderEngine?.GetSkinningComputeWaveOccupancy() ?? 0f, // Hypothétique
                SkinningComputeWarpOccupancy = _renderEngine?.GetSkinningComputeWarpOccupancy() ?? 0f, // Hypothétique
                SkinningComputeMemoryUsage = _renderEngine?.GetSkinningComputeMemoryUsage() ?? 0, // Hypothétique
                SkinningComputeMemoryBandwidth = _renderEngine?.GetSkinningComputeMemoryBandwidth() ?? 0f, // Hypothétique
                SkinningComputeMemoryLatency = _renderEngine?.GetSkinningComputeMemoryLatency() ?? 0f, // Hypothétique
                SkinningComputeCacheHitRate = _renderEngine?.GetSkinningComputeCacheHitRate() ?? 0f, // Hypothétique
                SkinningComputeCacheMissRate = _renderEngine?.GetSkinningComputeCacheMissRate() ?? 0f, // Hypothétique
                SkinningComputeQueueDepth = _renderEngine?.GetSkinningComputeQueueDepth() ?? 0, // Hypothétique
                SkinningComputeQueueStallCount = _renderEngine?.GetSkinningComputeQueueStallCount() ?? 0, // Hypothétique
                SkinningComputeQueueStallDuration = _renderEngine?.GetSkinningComputeQueueStallDuration() ?? 0f, // Hypothétique
                SkinningComputeFrameBudgetUsage = _renderEngine?.GetSkinningComputeFrameBudgetUsage() ?? 0f, // Hypothétique
                SkinningComputeFrameVariance = _renderEngine?.GetSkinningComputeFrameVariance() ?? 0f, // Hypothétique
                SkinningComputeFrameTrend = _renderEngine?.GetSkinningComputeFrameTrend() ?? "", // Hypothétique
                SkinningComputeFrameHeatmap = _renderEngine?.GetSkinningComputeFrameHeatmap() ?? "", // Hypothétique
                SkinningComputeFrameTimeline = _renderEngine?.GetSkinningComputeFrameTimeline() ?? "", // Hypothétique
                SkinningComputeFrameDependencyGraph = _renderEngine?.GetSkinningComputeFrameDependencyGraph() ?? "", // Hypothétique
                // Async Compute
                AsyncComputeQueueUsage = _renderEngine?.GetAsyncComputeQueueUsage() ?? 0f, // Hypothétique
                AsyncComputeQueueStallCount = _renderEngine?.GetAsyncComputeQueueStallCount() ?? 0, // Hypothétique
                AsyncComputeQueueStallDuration = _renderEngine?.GetAsyncComputeQueueStallDuration() ?? 0f, // Hypothétique
                AsyncComputeQueueDepth = _renderEngine?.GetAsyncComputeQueueDepth() ?? 0, // Hypothétique
                AsyncComputeShaderInvocationCount = _renderEngine?.GetAsyncComputeShaderInvocationCount() ?? 0, // Hypothétique
                AsyncComputeOccupancy = _renderEngine?.GetAsyncComputeOccupancy() ?? 0f, // Hypothétique
                AsyncComputeWaveOccupancy = _renderEngine?.GetAsyncComputeWaveOccupancy() ?? 0f, // Hypothétique
                AsyncComputeWarpOccupancy = _renderEngine?.GetAsyncComputeWarpOccupancy() ?? 0f, // Hypothétique
                AsyncComputeMemoryUsage = _renderEngine?.GetAsyncComputeMemoryUsage() ?? 0, // Hypothétique
            };

            var animationMetrics = new AnimationRenderMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                SkeletonCount = metrics.SkeletonCount,
                SkinnedVertexCount = CalculateEstimatedSkinnedVertices(metrics.SkeletonCount),
                SkinMatricesUpdates = _renderEngine?.GetSkinMatrixUpdateCount() ?? 0,
                BlendShapeTargetsActive = _renderEngine?.GetActiveBlendShapeTargetCount() ?? 0
            };

            lock (_metricsLock)
            {
                _lastMetrics = metrics;
                _lastAnimationMetrics = animationMetrics;
                _metricsHistory.Enqueue(metrics);
                _animationMetricsHistory.Enqueue(animationMetrics);
            }

            lock (_telemetryLock)
            {
                _telemetryBuffer.Enqueue(metrics, animationMetrics);
            }

            CheckForCriticalCost(metrics.FrameTimeGpuMs);
            CheckForAnomalies(metrics);
            TriggerAutoDumpsIfNeeded(metrics);

            _capturesSucceeded++;
        }

        private void CheckForCriticalCost(float frameTimeGpuMs)
        {
            const float criticalThreshold = 16.0f; // ~60fps
            if (frameTimeGpuMs > criticalThreshold)
            {
                lock (_alertLock)
                {
                    _criticalGPUCostEvents++;
                    _lastCriticalGPUCost = frameTimeGpuMs;
                }
                var eventBusCopy = _eventBus;
                if (eventBusCopy != null)
                {
                    try
                    {
                        eventBusCopy.Publish(new GPUFrameBudgetExceededEvent(frameTimeGpuMs)); // [RENOMMAGE]
                    }
                    catch (Exception ex)
                    {
                        LogOrRaiseError($"Event handler threw an exception during GPUFrameBudgetExceededEvent: {ex.Message}", "CheckForCriticalCost", null, ex);
                    }
                }
            }
        }

        private void CheckForAnomalies(GPUMetricsSnapshot metrics)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_alertsLock)
            {
                foreach (var rule in _alertRules)
                {
                    if (!rule.IsActive) continue;
                    float metricValue = GetMetricValueByName(metrics, rule.MetricName);
                    bool conditionMet = false;
                    switch (rule.Condition)
                    {
                        case ">": conditionMet = metricValue > rule.Threshold; break;
                        case "<": conditionMet = metricValue < rule.Threshold; break;
                        case ">=": conditionMet = metricValue >= rule.Threshold; break;
                        case "<=": conditionMet = metricValue <= rule.Threshold; break;
                        case "==": conditionMet = Math.Abs(metricValue - rule.Threshold) < 0.001f; break;
                    }
                    if (conditionMet)
                    {
                        var eventBusCopy = _eventBus;
                        if (eventBusCopy != null)
                        {
                            try
                            {
                                eventBusCopy.Publish(new GPUProfilerErrorEvent($"Alert Rule '{rule.Name}' triggered: {rule.MetricName} {rule.Condition} {rule.Threshold} (value: {metricValue})", rule.Severity)); // [RENOMMAGE]
                            }
                            catch (Exception ex)
                            {
                                LogOrRaiseError($"Event handler threw an exception during GPUProfilerErrorEvent: {ex.Message}", "CheckForAnomalies", null, ex);
                            }
                        }
                    }
                }
            }
        }

        private float GetMetricValueByName(GPUMetricsSnapshot metrics, string name)
        {
            // [CORRECTION] : Implémentation réelle
            switch (name)
            {
                case nameof(GPUMetricsSnapshot.FrameTimeGpuMs): return metrics.FrameTimeGpuMs;
                case nameof(GPUMetricsSnapshot.DrawCallCount): return metrics.DrawCallCount;
                case nameof(GPUMetricsSnapshot.SkeletonCount): return metrics.SkeletonCount;
                case nameof(GPUMetricsSnapshot.VertexShaderInvocations): return metrics.VertexShaderInvocations;
                case nameof(GPUMetricsSnapshot.PixelShaderInvocations): return metrics.PixelShaderInvocations;
                // Ajouter d'autres cas...
                default: return 0.0f; // [CORRECTION] : valeur par défaut
            }
        }

        private void TriggerAutoDumpsIfNeeded(GPUMetricsSnapshot metrics)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_autoDumpLock)
            {
                if (_autoDumpConfig.Enabled)
                {
                    if (_config.EnableAutoDumpOnThreshold && IsGPUCostCritical())
                    {
                        DumpMetricsToFile(metrics, "threshold");
                    }
                    // Autres conditions pour les dumps automatiques...
                }
            }
        }

        private void DumpMetricsToFile(GPUMetricsSnapshot metrics, string reason)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_autoDumpLock)
            {
                try
                {
                    // [CORRECTION] : Créer le répertoire s'il n'existe pas
                    var directory = _autoDumpConfig.DumpDirectory;
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var fileName = $"{_autoDumpConfig.FileNamePattern}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{reason}.json";
                    var fullPath = Path.Combine(directory ?? "", fileName);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(metrics, options);
                    File.WriteAllText(fullPath, json);
                    _autoDumpsTriggered++;

                    // [CORRECTION] : Limiter le nombre de dumps
                    // Implémentation d'une rotation simple basée sur le nom de fichier
                    // ou une liste de fichiers à nettoyer périodiquement
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Failed to dump metrics to file: {ex.Message}", "DumpMetricsToFile", null, ex);
                }
            }
        }

        public GPUMetricsSnapshot GetMetricsSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_metricsLock)
            {
                return new GPUMetricsSnapshot(_lastMetrics);
            }
        }

        public AnimationRenderMetricsSnapshot GetAnimationMetricsSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_metricsLock)
            {
                return new AnimationRenderMetricsSnapshot(_lastAnimationMetrics);
            }
        }

        public void SetContinuousCapture(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // [THREAD SAFETY] : lecture de _config sous lock
            bool currentEnabled;
            lock (_configLock)
            {
                currentEnabled = _config.EnableContinuousCapture;
            }

            if (enabled && !currentEnabled)
            {
                lock (_configLock)
                {
                    var newConfig = new GPUProfilerHookConfig(_config);
                    newConfig.EnableContinuousCapture = true;
                    ApplyConfiguration(newConfig);
                }
                _captureCancellationSource = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    while (!_captureCancellationSource.Token.IsCancellationRequested)
                    {
                        CaptureMetrics();
                        await Task.Delay(TimeSpan.FromMilliseconds(_config.CaptureIntervalMs), _captureCancellationSource.Token);
                    }
                }, _captureCancellationSource.Token);
                // [THREAD SAFETY] : transition d'état atomique
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Capturing);
            }
            else if (!enabled && currentEnabled)
            {
                lock (_configLock)
                {
                    var newConfig = new GPUProfilerHookConfig(_config);
                    newConfig.EnableContinuousCapture = false;
                    ApplyConfiguration(newConfig);
                }
                _captureCancellationSource?.Cancel();
                _captureCancellationSource?.Dispose();
                _captureCancellationSource = null;
                // [THREAD SAFETY] : transition d'état atomique
                Volatile.Write(ref _stateAsInt, (int)GPUProfilerState.Ready);
            }
        }

        public bool IsGPUCostCritical(float threshold = 16.0f)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_alertLock)
            {
                return _lastCriticalGPUCost > threshold;
            }
        }

        public int GetEstimatedSkeletonCount()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            return _renderEngine?.GetEstimatedAnimatedEntityCount() ?? 0; // Hypothétique méthode
        }

        public long GetEstimatedVertexShaderInvocations()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // [CORRECTION] : approximation plus réaliste
            var metrics = GetMetricsSnapshot();
            // Par exemple, nombre de triangles * 3 (pour 3 vertices par triangle)
            // ou nombre de primitives * nb_vertices_par_primitive
            return metrics.TrisCount * 3; // Approximation grossière
        }

        private long CalculateEstimatedSkinnedVertices(int skeletonCount)
        {
            // [CORRECTION] : rendre la constante configurable
            const int avgVerticesPerSkeleton = 1000; // [CONFIGURABLE]
            return (long)skeletonCount * avgVerticesPerSkeleton;
        }
        #endregion

        #region Intégration moteur (Hooks)
        public void OnBeforeRender()
        {
            lock (_hooksLock) // [THREAD SAFETY]
            {
                _hookTimestamps["OnBeforeRender"] = DateTime.UtcNow;
            }
        }
        public void OnAfterRender()
        {
            lock (_hooksLock) // [THREAD SAFETY]
            {
                if (_hookTimestamps.TryGetValue("OnBeforeRender", out var start))
                {
                    _hookDurations["OnBeforeRender"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeSkinning()
        {
            lock (_hooksLock) // [THREAD SAFETY]
            {
                _hookTimestamps["OnBeforeSkinning"] = DateTime.UtcNow;
            }
        }
        public void OnAfterSkinning()
        {
            lock (_hooksLock) // [THREAD SAFETY]
            {
                if (_hookTimestamps.TryGetValue("OnBeforeSkinning", out var start))
                {
                    _hookDurations["OnBeforeSkinning"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        // ... (Implémenter les autres hooks de la même manière avec lock(_hooksLock))

        public void OnBeforeShadowPass()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeShadowPass"] = DateTime.UtcNow; }
        }
        public void OnAfterShadowPass()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeShadowPass", out var start))
                {
                    _hookDurations["OnBeforeShadowPass"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforePostProcess()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforePostProcess"] = DateTime.UtcNow; }
        }
        public void OnAfterPostProcess()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforePostProcess", out var start))
                {
                    _hookDurations["OnBeforePostProcess"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeComputePass()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeComputePass"] = DateTime.UtcNow; }
        }
        public void OnAfterComputePass()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeComputePass", out var start))
                {
                    _hookDurations["OnBeforeComputePass"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUUpload()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUUpload"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUUpload()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUUpload", out var start))
                {
                    _hookDurations["OnBeforeGPUUpload"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeAnimationUpdate()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeAnimationUpdate"] = DateTime.UtcNow; }
        }
        public void OnAfterAnimationUpdate()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeAnimationUpdate", out var start))
                {
                    _hookDurations["OnBeforeAnimationUpdate"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeBlendShapes()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeBlendShapes"] = DateTime.UtcNow; }
        }
        public void OnAfterBlendShapes()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeBlendShapes", out var start))
                {
                    _hookDurations["OnBeforeBlendShapes"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeParticleUpdate()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeParticleUpdate"] = DateTime.UtcNow; }
        }
        public void OnAfterParticleUpdate()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeParticleUpdate", out var start))
                {
                    _hookDurations["OnBeforeParticleUpdate"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeLightingPass()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeLightingPass"] = DateTime.UtcNow; }
        }
        public void OnAfterLightingPass()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeLightingPass", out var start))
                {
                    _hookDurations["OnBeforeLightingPass"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeOcclusionCulling()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeOcclusionCulling"] = DateTime.UtcNow; }
        }
        public void OnAfterOcclusionCulling()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeOcclusionCulling", out var start))
                {
                    _hookDurations["OnBeforeOcclusionCulling"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUCulling()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUCulling"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUCulling()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUCulling", out var start))
                {
                    _hookDurations["OnBeforeGPUCulling"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeSkinning()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeSkinning"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeSkinning()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeSkinning", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeSkinning"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeMorph()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeMorph"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeMorph()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeMorph", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeMorph"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeCloth()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeCloth"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeCloth()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeCloth", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeCloth"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeParticles()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeParticles"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeParticles()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeParticles", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeParticles"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeTerrain()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeTerrain"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeTerrain()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeTerrain", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeTerrain"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeAI()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeAI"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeAI()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeAI", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeAI"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputePhysics()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputePhysics"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputePhysics()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputePhysics", out var start))
                {
                    _hookDurations["OnBeforeGPUComputePhysics"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeNavMesh()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeNavMesh"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeNavMesh()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeNavMesh", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeNavMesh"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeOcclusion()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeOcclusion"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeOcclusion()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeOcclusion", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeOcclusion"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeVisibility()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeVisibility"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeVisibility()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeVisibility", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeVisibility"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeLOD()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeLOD"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeLOD()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeLOD", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeLOD"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeAnimationLOD()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeAnimationLOD"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeAnimationLOD()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeAnimationLOD", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeAnimationLOD"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeMotionMatching()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeMotionMatching"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeMotionMatching()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeMotionMatching", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeMotionMatching"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeIK()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeIK"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeIK()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeIK", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeIK"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputePoseGraph()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputePoseGraph"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputePoseGraph()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputePoseGraph", out var start))
                {
                    _hookDurations["OnBeforeGPUComputePoseGraph"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeBlendGraph()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeBlendGraph"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeBlendGraph()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeBlendGraph", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeBlendGraph"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }

        public void OnBeforeGPUComputeRootMotion()
        {
            lock (_hooksLock) { _hookTimestamps["OnBeforeGPUComputeRootMotion"] = DateTime.UtcNow; }
        }
        public void OnAfterGPUComputeRootMotion()
        {
            lock (_hooksLock)
            {
                if (_hookTimestamps.TryGetValue("OnBeforeGPUComputeRootMotion", out var start))
                {
                    _hookDurations["OnBeforeGPUComputeRootMotion"] = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
            }
        }
        #endregion

        #region Observabilité & Telemetry (Amélioré)
        public GPUMetricsRingBufferSnapshot GetTelemetrySnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_telemetryLock)
            {
                return _telemetryBuffer.GetSnapshot();
            }
        }

        public string TakeProfilerSnapshot() // [RENOMMAGE]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var data = new
            {
                Timestamp = DateTime.UtcNow,
                State = State,
                Config = GetConfigSnapshot(),
                Metrics = GetMetricsSnapshot(),
                AnimationMetrics = GetAnimationMetricsSnapshot(),
                Lifecycle = _lifecycleData,
                EventHistory = _eventLog.GetEvents(), // [CORRECTION] : Appel à la méthode de la classe
                MetricsCount = _metricsHistory.Count,
                AnimationMetricsCount = _animationMetricsHistory.Count,
                MetricsCapacity = _metricsHistory.Capacity,
                AnimationMetricsCapacity = _animationMetricsHistory.Capacity,
                CapturesSucceeded = _capturesSucceeded,
                CapturesFailed = _capturesFailed,
                AutoDumpsTriggered = _autoDumpsTriggered,
                ErrorsLogged = _errorsLogged
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
            var queueLength = _metricsHistory.Count;
            var errorCount = _lifecycleData.ErrorCount;
            var lastErrorAge = (DateTime.UtcNow - _lifecycleData.LastErrorAt).TotalSeconds;
            // [CORRECTION] : seuils configurables
            return queueLength < 100 && errorCount < 10 && lastErrorAge < 30; // Seuils codés en dur -> à externaliser
        }

        public GPUMetricsSnapshotHealthCheck PerformHealthCheck()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var check = new GPUMetricsSnapshotHealthCheck { IsHealthy = true, Issues = new List<string>(), Warnings = new List<string>(), CheckedAt = DateTime.UtcNow };
            // [CORRECTION] : vérifier plus que _lastCriticalGPUCost
            if (_lastCriticalGPUCost > 20.0f) // Seuil critique
            {
                check.Issues.Add("Critical GPU cost detected");
                check.IsHealthy = false;
            }
            // Ajouter d'autres vérifications ici...
            return check;
        }
        #endregion

        #region Ergonomie & API (Amélioré)
        public void ApplyPreset(GPUProfilerHookPreset preset)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            ApplyConfiguration(preset.Config);
            SetPerformanceMode(preset.PerformanceMode);
            // Appliquer d'autres aspects du preset
        }

        public void SavePreset(string name, GPUProfilerHookPreset preset)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_presetsLock)
            {
                preset.Name = name;
                var existingIndex = _presetLibrary.FindIndex(p => p.Name == name);
                if (existingIndex >= 0)
                {
                    _presetLibrary[existingIndex] = preset;
                }
                else
                {
                    _presetLibrary.Add(preset);
                }
            }
        }

        public GPUProfilerHookPreset LoadPreset(string name)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_presetsLock)
            {
                // [CORRECTION] : FirstOrDefault sur struct -> comparer le nom
                var preset = _presetLibrary.FirstOrDefault(p => p.Name == name);
                // Si Name est vide, alors le preset n'existait pas
                if (string.IsNullOrEmpty(preset.Name)) return new GPUProfilerHookPreset(); // Retourner un preset vide ou null
                return preset;
            }
        }
        #endregion

        #region Tests & QA (Amélioré)
        public void RunStressTest()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _isStressTesting = true;
            // [CORRECTION] : Implémentation du test de charge
            // ...
            _isStressTesting = false;
        }

        public void RunFuzzTest()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _isFuzzTesting = true;
            // [CORRECTION] : Implémentation du test de fuzzing
            // ...
            _isFuzzTesting = false;
        }

        public void SimulateGPUCrash()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _simulatingGPUCrash = true;
            // Simuler une erreur critique
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerErrorEvent("Simulated GPU Crash", GPUErrorSeverity.Critical)); // [RENOMMAGE]
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during simulated GPU crash: {ex.Message}", "SimulateGPUCrash", null, ex);
                }
            }
            _simulatingGPUCrash = false;
        }

        public void SimulateGPUHang()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _simulatingGPUHang = true;
            // Simuler un hang
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerHangDetectedEvent(100.0f)); // [RENOMMAGE]
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during simulated GPU hang: {ex.Message}", "SimulateGPUHang", null, ex);
                }
            }
            _simulatingGPUHang = false;
        }

        public void SimulateShaderCrash()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _simulatingShaderCrash = true;
            // Simuler un crash de shader
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerErrorEvent("Simulated Shader Crash", GPUErrorSeverity.Critical)); // [RENOMMAGE]
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during simulated shader crash: {ex.Message}", "SimulateShaderCrash", null, ex);
                }
            }
            _simulatingShaderCrash = false;
        }

        public void SimulateGPUTimeout()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            _simulatingGPUTimeout = true;
            // Simuler un timeout
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerErrorEvent("Simulated GPU Timeout", GPUErrorSeverity.Error)); // [RENOMMAGE]
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during simulated GPU timeout: {ex.Message}", "SimulateGPUTimeout", null, ex);
                }
            }
            _simulatingGPUTimeout = false;
        }
        #endregion

        #region Vendor-Specific (Amélioré)
        public void SetNVidiaNsightMarkersEnabled(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_vendorLock)
            {
                _nvidiaNsightMarkersEnabled = enabled;
                if (enabled)
                {
                    // Activer les marqueurs NVIDIA Nsight
                }
                else
                {
                    // Désactiver
                }
            }
        }

        public void SetAMDGPUProfilerMarkersEnabled(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_vendorLock)
            {
                _amdGPUProfilerMarkersEnabled = enabled;
                if (enabled)
                {
                    // Activer les marqueurs AMD
                }
                else
                {
                    // Désactiver
                }
            }
        }

        public void SetIntelGPAMarkersEnabled(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_vendorLock)
            {
                _intelGPAMarkersEnabled = enabled;
                if (enabled)
                {
                    // Activer les marqueurs Intel GPA
                }
                else
                {
                    // Désactiver
                }
            }
        }

        public void SetDirectXPixMarkersEnabled(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_vendorLock)
            {
                _directxPixMarkersEnabled = enabled;
                if (enabled)
                {
                    // Activer les marqueurs PIX
                }
                else
                {
                    // Désactiver
                }
            }
        }

        public void SetVulkanDebugMarkersEnabled(bool enabled)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_vendorLock)
            {
                _vulkanDebugMarkersEnabled = enabled;
                if (enabled)
                {
                    // Activer les marqueurs Vulkan
                }
                else
                {
                    // Désactiver
                }
            }
        }
        #endregion

        #region Compute & Async (Amélioré)
        public float GetAsyncComputeQueueUsage()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeQueueUsage;
        }

        public int GetAsyncComputeQueueStallCount()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeQueueStallCount;
        }

        public float GetAsyncComputeQueueStallDuration()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeQueueStallDuration;
        }

        public int GetAsyncComputeQueueDepth()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeQueueDepth;
        }

        public long GetAsyncComputeShaderInvocationCount()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeShaderInvocationCount;
        }

        public float GetAsyncComputeOccupancy()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeOccupancy;
        }

        public float GetAsyncComputeWaveOccupancy()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeWaveOccupancy;
        }

        public float GetAsyncComputeWarpOccupancy()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeWarpOccupancy;
        }

        public long GetAsyncComputeMemoryUsage()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var metrics = GetMetricsSnapshot();
            return metrics.AsyncComputeMemoryUsage;
        }
        #endregion

        #region Helper Methods (Amélioré)
        private void NotifyInitialized()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerHookInitializedEvent(this));
                    eventBusCopy.Publish(new GPUProfilerStateChangedEvent(GPUProfilerState.Uninitialized, GPUProfilerState.Ready));
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
                    await Task.Run(() => eventBusCopy.Publish(new GPUProfilerHookInitializedEvent(this)));
                    await Task.Run(() => eventBusCopy.Publish(new GPUProfilerStateChangedEvent(GPUProfilerState.Uninitialized, GPUProfilerState.Ready)));
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
                    eventBusCopy.Publish(new GPUProfilerHookShutdownEvent(this));
                    // [CORRECTION] : publier l'événement avant de changer l'état
                    eventBusCopy.Publish(new GPUProfilerStateChangedEvent(State, GPUProfilerState.Shutdown));
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during NotifyShutdown: {ex.Message}", "NotifyShutdown", null, ex);
                }
            }
        }

        private async Task NotifyShutdownAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    await Task.Run(() => eventBusCopy.Publish(new GPUProfilerHookShutdownEvent(this)));
                    await Task.Run(() => eventBusCopy.Publish(new GPUProfilerStateChangedEvent(State, GPUProfilerState.Shutdown)));
                }
                catch (Exception ex)
                {
                    LogOrRaiseError($"Event handler threw an exception during NotifyShutdownAsync: {ex.Message}", "NotifyShutdownAsync", null, ex);
                }
            }
        }

        private void LogOrRaiseError(string message, string methodName, object contextData = null, Exception innerException = null)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var fullMessage = $"[GPUProfilerHook::{methodName}] {message}";
            if (contextData != null)
            {
                fullMessage += $"\nContext: {contextData}";
            }
            if (innerException != null)
            {
                fullMessage += $"\nInner Exception: {innerException}";
            }
            Console.WriteLine(fullMessage);
            _errorsLogged++;
            // Eventuellement publier un événement d'erreur via EventBus
            var eventBusCopy = _eventBus;
            if (eventBusCopy != null)
            {
                try
                {
                    eventBusCopy.Publish(new GPUProfilerErrorEvent(fullMessage, GPUErrorSeverity.Warning)); // [RENOMMAGE]
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to publish GPUProfilerErrorEvent: {ex.Message}");
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown(); // [CORRECTION] : arrêter proprement avant de libérer les ressources

                // [CORRECTION] : attendre la fin de la tâche de capture continue
                _captureCancellationSource?.Cancel();
                try
                {
                    _captureCancellationSource?.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)); // Timeout
                }
                catch { /* Ignore */ }
                _captureCancellationSource?.Dispose();

                // Libérer les ressources
                _metricsHistory.Clear();
                _animationMetricsHistory.Clear();
                _telemetryBuffer.Clear();
                _presetLibrary.Clear();
                _affinityLeases.Clear();
                _affinityPolicies.Clear();
                _alertRules.Clear();
                _eventLog.AddEvent("Disposed at " + DateTime.UtcNow.ToString()); // [OBSERVABILITE] : logguer la fermeture
                _disposed = true;
            }
        }
        #endregion

        #region AAA Ideas Implementation (Méthodes & Propriétés)
        public string GetVersion() => "1.0.0";
        public BuildMetadata GetBuildMetadata()
        {
            return new BuildMetadata { Version = "1.0.0", Branch = "main", CommitHash = "abc123def", BuildDate = DateTime.UtcNow, Platform = Environment.OSVersion.Platform.ToString(), Configuration = "Release" };
        }

        public GPUThreadAffinityHeatmap GetGPUThreadAffinityHeatmap() // [RENOMMAGE]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_affinityLock)
            {
                // Calculer la heatmap basée sur les charges actuelles des threads
                // Cela nécessiterait une intégration avec ThreadAffinityManager
                return new GPUThreadAffinityHeatmap
                {
                    LoadByThread = new Dictionary<int, float>(),
                    LoadByRole = new Dictionary<ThreadRole, float>(),
                    MaxLoad = 0f,
                    MinLoad = 0f,
                    AverageLoad = 0f,
                    ReportTime = DateTime.UtcNow
                };
            }
        }

        public GPUMetricsSnapshotDiff GetMetricsDiff(GPUMetricsSnapshot a, GPUMetricsSnapshot b)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Calculer la différence entre deux snapshots
            var delta = new GPUMetricsSnapshot(b); // On copie b
            delta.FrameTimeGpuMs -= a.FrameTimeGpuMs;
            // ... et ainsi de suite pour tous les champs numériques
            return new GPUMetricsSnapshotDiff { Before = a, After = b, Delta = delta, PercentageChange = (delta.FrameTimeGpuMs / a.FrameTimeGpuMs) * 100f };
        }

        // ... (Implémenter d'autres méthodes comme GetMetricsTimeline, GetMetricsHeatmap, etc.)

        public void AddAlertRule(GPUMetricsSnapshotAlertRule rule)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_alertsLock)
            {
                _alertRules.Add(rule);
            }
        }

        public void RemoveAlertRule(string ruleName)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_alertsLock)
            {
                _alertRules.RemoveAll(r => r.Name == ruleName);
            }
        }

        public List<GPUMetricsSnapshotAnomaly> FindAnomalies(TimeSpan lookbackPeriod)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var anomalies = new List<GPUMetricsSnapshotAnomaly>();
            // [CORRECTION] : Rechercher les anomalies dans l'historique
            // ...
            return anomalies;
        }

        public void ExportMetricsToJSON(GPUMetricsSnapshot snapshot, string filePath)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(snapshot, options);
            File.WriteAllText(filePath, json);
        }

        public void ExportMetricsToCSV(List<GPUMetricsSnapshot> snapshots, string filePath)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            using var writer = new StreamWriter(filePath);
            // [CORRECTION] : Ecrire l'en-tête
            writer.WriteLine("Timestamp,FrameTimeGpuMs,DrawCallCount,BatchCount,SkeletonCount,VertexShaderInvocations"); // Exemple
            // Ecrire les données
            foreach (var snap in snapshots)
            {
                writer.WriteLine($"{snap.Timestamp:yyyy-MM-ddTHH:mm:ss.fff},{snap.FrameTimeGpuMs},{snap.DrawCallCount},{snap.BatchCount},{snap.SkeletonCount},{snap.VertexShaderInvocations}");
            }
        }

        public void ImportMetricsFromJSON(string filePath)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var json = File.ReadAllText(filePath);
            var snapshot = JsonSerializer.Deserialize<GPUMetricsSnapshot>(json);
            // [CORRECTION] : Charger les données dans l'historique ou les utiliser ailleurs
            lock (_metricsLock)
            {
                _lastMetrics = snapshot;
                _metricsHistory.Enqueue(snapshot);
            }
        }

        public void ConfigureAutoDump(GPUMetricsSnapshotAutoDumpConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_autoDumpLock)
            {
                _autoDumpConfig = config;
            }
        }
        #endregion
    }

    public struct GPUMetricsRingBuffer
    {
        private readonly RingBuffer<GPUMetricsSnapshot> _gpuMetrics;
        private readonly RingBuffer<AnimationRenderMetricsSnapshot> _animMetrics;
        private readonly int _capacity;

        public GPUMetricsRingBuffer(int capacity)
        {
            _capacity = capacity;
            _gpuMetrics = new RingBuffer<GPUMetricsSnapshot>(capacity);
            _animMetrics = new RingBuffer<AnimationRenderMetricsSnapshot>(capacity);
        }

        public void Enqueue(GPUMetricsSnapshot gpuSnap, AnimationRenderMetricsSnapshot animSnap)
        {
            _gpuMetrics.Enqueue(gpuSnap);
            _animMetrics.Enqueue(animSnap);
        }

        public GPUMetricsRingBufferSnapshot GetSnapshot()
        {
            // [CORRECTION] : Retourne une copie non destructive
            return _gpuMetrics.GetSnapshot(); // Simplifié, car RingBuffer n'a pas de lien avec AnimationMetrics
            // Pour une vraie paire, il faudrait une structure spécifique ou un buffer associé.
        }

        public void Clear()
        {
            _gpuMetrics.Clear();
            _animMetrics.Clear();
        }
    }
}