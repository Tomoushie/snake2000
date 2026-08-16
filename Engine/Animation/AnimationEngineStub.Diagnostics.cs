// /Engine/Animation/AnimationEngineStub.Diagnostics.cs
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Engine.Animation
{
    // B. Diagnostic et instrumentation
    public readonly struct TraceEvent
    {
        public readonly string Message;
        public readonly DateTime Timestamp;
        public readonly AnimationEngineState State;
        public readonly string CorrelationId; // [AJOUT]
        public TraceEvent(string msg, DateTime time, AnimationEngineState state, string corrId = null) => (Message, Timestamp, State, CorrelationId) = (msg, time, state, corrId);
    }

    // D. Diagnostic et instrumentation
    public enum DiagnosticsLevel
    {
        None,
        Basic,
        Verbose,
        Full
    }

    // N. Diagnostics avancés (implémentations conceptuelles)
    public class AnimationMemoryLeakDetector
    {
        private readonly Dictionary<string, long> _allocations = new Dictionary<string, long>(); // Tag -> Size
        private readonly object _lock = new object();

        public void ReportAllocation(string tag, long size)
        {
            lock (_lock)
            {
                if (_allocations.ContainsKey(tag))
                {
                    _allocations[tag] += size;
                }
                else
                {
                    _allocations[tag] = size;
                }
            }
        }

        public void ReportDeallocation(string tag, long size)
        {
            lock (_lock)
            {
                if (_allocations.ContainsKey(tag))
                {
                    _allocations[tag] -= size;
                    if (_allocations[tag] <= 0)
                    {
                        _allocations.Remove(tag); // Nettoyer si tout est libéré
                    }
                }
            }
        }

        public List<string> GetPotentialLeaks(long threshold = 1024 * 1024) // 1 MB
        {
            lock (_lock)
            {
                return _allocations.Where(kvp => kvp.Value > threshold).Select(kvp => kvp.Key).ToList();
            }
        }
    }

    public class AnimationMemorySnapshot
    {
        public long TotalAllocatedBytes { get; set; }
        public Dictionary<string, long> AllocationBreakdown { get; set; } = new Dictionary<string, long>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AnimationMemoryProfiler
    {
        private readonly List<AnimationMemorySnapshot> _snapshots = new List<AnimationMemorySnapshot>();
        private readonly object _lock = new object();

        public void TakeSnapshot(AnimationMemorySnapshot snapshot)
        {
            lock (_lock)
            {
                _snapshots.Add(snapshot);
                if (_snapshots.Count > 100) _snapshots.RemoveAt(0); // Garder les 100 dernières
            }
        }

        public List<AnimationMemorySnapshot> GetSnapshots() => new List<AnimationMemorySnapshot>(_snapshots);
    }

    public partial class AnimationEngineStub
    {
        // B. Diagnostic et instrumentation
        private readonly DiagnosticsManager _diagnosticsManager = new DiagnosticsManager();
        private DiagnosticsLevel _diagLevel = DiagnosticsLevel.Basic;

        // N. Diagnostics avancés
        private readonly AnimationMemoryLeakDetector _memoryLeakDetector = new AnimationMemoryLeakDetector();
        private readonly AnimationMemoryProfiler _memoryProfiler = new AnimationMemoryProfiler();

        #region Diagnostics & Instrumentation

        // Expose un DumpState() pour exporter l’état complet du stub (utile pour tests unitaires).
        // DumpState : version fusionnee dans AnimationEngineStub.cs

        // Ajoute un ValidatePose() pour vérifier la cohérence des données avant rendu.
        // ValidatePose : version fusionnee dans AnimationEngineStub.cs

        #endregion
    }

    #region Diagnostics Manager Implementation

    public class DiagnosticsManager
    {
        private readonly Queue<TraceEvent> _traceBuffer = new Queue<TraceEvent>(100); // Taille fixe
        private readonly object _lock = new object();
        private DiagnosticsLevel _level = DiagnosticsLevel.Basic;

        public void Log(TraceEvent ev)
        {
            if (_level == DiagnosticsLevel.None) return;
            lock (_lock)
            {
                _traceBuffer.Enqueue(ev);
                if (_traceBuffer.Count > 100) _traceBuffer.Dequeue(); // Limiter la taille
            }
        }

        public List<TraceEvent> GetRecentEvents() => new List<TraceEvent>(_traceBuffer);
    }

    #endregion
}