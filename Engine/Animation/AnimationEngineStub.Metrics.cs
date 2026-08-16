// /Engine/Animation/AnimationEngineStub.Metrics.cs
//
// Responsabilités : Collecte des métriques, télémétrie, intégration avec le dashboard Orchestrator.
// Dépendances : AnimationEngineStub.Core (pour les types et _metricsLock).
// Intègre : MetricCollector, MetricsExporter, MetricsHealthMonitor, MetricsSnapshotHistory.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Engine.Animation
{
    #region Supporting Types (Metrics)

    // Types de métriques pour le dashboard
    public enum OrchestratorMetricType
    {
        ActivePlaybacks,
        ActivePoses,
        CpuUpdateMs,
        MemoryUsedBytes,
        TotalBonesAnimated,
        RenderedPoses,
        RootMotionApplied,
        BlendsCalculated,
        IKIterations,
        ProceduralUpdates,
        CompressedClipsLoaded,
        StreamingRequests,
        ThreadingTasksQueued,
        DeterministicFrames,
        ErrorCount,
        WarningCount,
        HealthPercentage,
        // Ajouts pour les idées 398-597
        LoadedPlugins,
        TelemetryEvents,
        ConfigChanges,
        SnapshotsTaken,
        AssetsLoaded,
        AssetsUnloaded,
        Prefetches,
        IntegrityChecks,
        ChaosEvents,
        ABRTests,
        FeatureToggles,
        RuntimeAudits,
        CircuitBreakerTrips
    }

    public readonly struct AnimationEngineMetrics
    {
        public readonly Dictionary<OrchestratorMetricType, float> Values;
        public AnimationEngineMetrics(Dictionary<OrchestratorMetricType, float> values) => Values = values;
    }

    public readonly struct PerformanceSnapshot
    {
        public readonly float CpuTimeMs;
        public readonly long MemoryUsedBytes;
        public readonly int ActiveThreads;
        public readonly float FrameTimeMs;
        public PerformanceSnapshot(float cpuTime, long mem, int threads, float frameTime) => (CpuTimeMs, MemoryUsedBytes, ActiveThreads, FrameTimeMs) = (cpuTime, mem, threads, frameTime);
    }

    // [AJOUT] Structures pour les idées 398-597
    public readonly struct TelemetryEvent
    {
        public readonly string Name;
        public readonly Dictionary<string, object> Properties;
        public readonly DateTime Timestamp;
        public TelemetryEvent(string name, Dictionary<string, object> props, DateTime time) => (Name, Properties, Timestamp) = (name, props, time);
    }

    #endregion

    public partial class AnimationEngineStub
    {
        #region Fields (Metrics)

        // --- Métriques internes (centralisées dans MetricCollector) ---
        private readonly MetricCollector _metricCollector = new MetricCollector();
        // Remplace la vieille structure :
        // private readonly Dictionary<OrchestratorMetricType, float> _metrics = new Dictionary<OrchestratorMetricType, float>();
        // private readonly object _metricsLock = new object(); // Maintenu dans Core.cs pour d'autres usages potentiels

        // --- Gestionnaires Metrics ---
        private readonly MetricsExporter _metricsExporter = new MetricsExporter();
        private readonly MetricsHealthMonitor _metricsHealthMonitor = new MetricsHealthMonitor();
        private readonly MetricsSnapshotHistory _snapshotHistory = new MetricsSnapshotHistory();

        #endregion

        #region Metrics & Dashboard Integration

        // GetMetrics : version fusionnee dans AnimationEngineStub.cs

        // [AJOUT] Méthodes pour les idées 398-597
        public AnimationEngineStub RecordTelemetryEvent(TelemetryEvent evt)
        {
            _telemetryCollector.RecordEvent(evt);
            _metricCollector.Increment(OrchestratorMetricType.TelemetryEvents, 1); // Mise à jour métrique
            LogCall($"RecordTelemetryEvent({evt.Name})");
            return this;
        }

        public AnimationEngineStub FlushTelemetry()
        {
            _telemetryCollector.Flush();
            LogCall("FlushTelemetry");
            return this;
        }

        #endregion

        #region Metrics Helpers (Internal)

        // Helper pour incrémenter une métrique
        private void UpdateMetric(OrchestratorMetricType type, float value)
        {
            _metricCollector.SetValue(type, value);
        }

        // Helper pour incrémenter une métrique
        private void IncrementMetric(OrchestratorMetricType type, float amount = 1.0f)
        {
            _metricCollector.Increment(type, amount);
        }

        // Helper pour réinitialiser une métrique
        private void ResetMetric(OrchestratorMetricType type)
        {
            _metricCollector.SetValue(type, 0.0f);
        }

        #endregion
    }

    #region MetricCollector Implementation

    public class MetricCollector
    {
        private readonly Dictionary<OrchestratorMetricType, float> _metrics = new Dictionary<OrchestratorMetricType, float>();
        private readonly object _lock = new object();

        public MetricCollector()
        {
            // Initialiser toutes les métriques à 0
            foreach (OrchestratorMetricType type in Enum.GetValues(typeof(OrchestratorMetricType)))
            {
                _metrics[type] = 0.0f;
            }
        }

        public void SetValue(OrchestratorMetricType type, float value)
        {
            lock (_lock)
            {
                _metrics[type] = value;
            }
        }

        public void Increment(OrchestratorMetricType type, float amount)
        {
            lock (_lock)
            {
                _metrics[type] += amount;
            }
        }

        public AnimationEngineMetrics GetSnapshot()
        {
            lock (_lock)
            {
                return new AnimationEngineMetrics(new Dictionary<OrchestratorMetricType, float>(_metrics));
            }
        }

        public float GetValue(OrchestratorMetricType type)
        {
            lock (_lock)
            {
                return _metrics.TryGetValue(type, out var value) ? value : 0.0f;
            }
        }
    }

    #endregion

    #region MetricsExporter Implementation

    public class MetricsExporter
    {
        public void ExportToDashboard(AnimationEngineMetrics metrics)
        {
            // Logique pour envoyer les métriques au dashboard
            // Cela pourrait impliquer un appel à une API, une écriture dans un fichier, ou une mise à jour d'une UI.
            System.Console.WriteLine($"[MetricsExporter] Exporting metrics to dashboard. ActivePlaybacks: {metrics.Values[OrchestratorMetricType.ActivePlaybacks]}, CpuUpdateMs: {metrics.Values[OrchestratorMetricType.CpuUpdateMs]}");
        }
    }

    #endregion

    #region MetricsHealthMonitor Implementation

    public class MetricsHealthMonitor
    {
        private readonly Dictionary<OrchestratorMetricType, float> _thresholds = new Dictionary<OrchestratorMetricType, float>();
        private readonly object _lock = new object();

        public MetricsHealthMonitor()
        {
            // Définir des seuils par défaut
            _thresholds[OrchestratorMetricType.CpuUpdateMs] = 16.0f; // 60 FPS
            _thresholds[OrchestratorMetricType.MemoryUsedBytes] = 100 * 1024 * 1024; // 100 MB
        }

        public void SetThreshold(OrchestratorMetricType type, float threshold)
        {
            lock (_lock)
            {
                _thresholds[type] = threshold;
            }
        }

        public List<OrchestratorMetricType> CheckForAnomalies(AnimationEngineMetrics metrics)
        {
            var anomalies = new List<OrchestratorMetricType>();
            lock (_lock)
            {
                foreach (var threshold in _thresholds)
                {
                    if (metrics.Values.TryGetValue(threshold.Key, out var value) && value > threshold.Value)
                    {
                        anomalies.Add(threshold.Key);
                    }
                }
            }
            return anomalies;
        }
    }

    #endregion

    #region MetricsSnapshotHistory Implementation

    public class MetricsSnapshotHistory
    {
        private readonly Queue<AnimationEngineMetrics> _history = new Queue<AnimationEngineMetrics>(100); // Taille arbitraire
        private readonly object _lock = new object();

        public void RecordSnapshot(AnimationEngineMetrics metrics)
        {
            lock (_lock)
            {
                _history.Enqueue(metrics);
                if (_history.Count > 100) _history.Dequeue(); // Limiter la taille
            }
        }

        public List<AnimationEngineMetrics> GetHistory(int count = 10)
        {
            lock (_lock)
            {
                var list = new List<AnimationEngineMetrics>(_history);
                // Retourner les 'count' dernières
                int start = Math.Max(0, list.Count - count);
                return list.GetRange(start, list.Count - start);
            }
        }

        public AnimationEngineMetrics GetSnapshotAt(int indexFromEnd = 0)
        {
            lock (_lock)
            {
                if (indexFromEnd < 0 || indexFromEnd >= _history.Count) return new AnimationEngineMetrics(new Dictionary<OrchestratorMetricType, float>());
                var arr = _history.ToArray();
                return arr[arr.Length - 1 - indexFromEnd];
            }
        }
    }

    #endregion
}