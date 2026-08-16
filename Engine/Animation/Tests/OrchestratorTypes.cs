using System;
using System.Collections.Generic;

namespace Engine.Animation.Test
{
    public enum ReportFormat
    {
        Json,
        Xml,
        Html
    }

    public enum ErrorRecoveryStrategy
    {
        Ignore,
        Retry,
        Abort
    }

    public enum StubConfigurationPreset
    {
        Minimal,
        Standard,
        Full,
        Custom
    }

    public interface IStubPlugin
    {
        string Name { get; }
        void Initialize(object orchestrator);
    }

    public class StubScenario
    {
        public string Name { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioName { get; set; }
        public bool Success { get; set; }
    }

    public class RecordedSession
    {
        public DateTime RecordingEndTime { get; set; }   // ligne 1128 de l'orchestrateur
        public List<string> Calls { get; set; } = new List<string>();
        public List<string> Events { get; set; } = new List<string>();
        public List<string> StateTransitions { get; set; } = new List<string>();
    }

    public class DetailedMetrics
    {
        public double FrameTimeAverage { get; set; }
        public double FrameTimeMedian { get; set; }
        public double FrameTimeP95 { get; set; }
        public double FrameTimeP99 { get; set; }
        public long TotalMemoryUsedMB { get; set; }
        public long PeakMemoryUsedMB { get; set; }
        public double AllocationRatePerFrame { get; set; }
        public int GCGen0Count { get; set; }
        public int GCGen1Count { get; set; }
        public int GCGen2Count { get; set; }
        public Dictionary<string, double> CPUBreakdown { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, long> MemoryBreakdown { get; set; } = new Dictionary<string, long>();
    }

    public class DiagnosticReport
    {
        public string Status { get; set; }
        public string OverallStatus { get; set; }
        public DateTime Timestamp { get; set; }
        public DetailedMetrics Metrics { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Findings { get; set; } = new List<string>();
        public List<string> RecentCalls { get; set; } = new List<string>();
        public List<string> RecentStateTransitions { get; set; } = new List<string>();
        public DataValidationReport DataValidation { get; set; } = new DataValidationReport();

        public override string ToString()
        {
            return Status;
        }
    }

    // Trois types que l'analyse des corps de methode a reclames, avec leurs
    // membres exacts : HealthStatus est compare a trois valeurs, et
    // DataValidationReport est construit ligne 1114 avec deux champs.
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public class DataValidationReport
    {
        public bool AllValid { get; set; }
        public int ValidatedItems { get; set; }
    }
}
